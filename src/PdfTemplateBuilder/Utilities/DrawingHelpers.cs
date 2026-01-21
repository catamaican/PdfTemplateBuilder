using System;
using System.Collections.Generic;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Canvas;
using iText.Forms.Fields;
using iText.Forms.Fields.Properties;
using iText.Layout;
using iText.Layout.Properties;
using PdfTemplateBuilder.Models;

namespace PdfTemplateBuilder
{
	internal static class DrawingHelpers
	{
		internal const float DefaultFieldFontSize = 10f;
		internal const float DefaultBodyFontSize = 9f;

		internal static void ApplyWidgetBorder(PdfFormField field, PdfDocument pdf, float borderWidth)
		{
			if (borderWidth <= 0) return;
			var widget = field.GetWidgets()[0];
			var formAnnotation = PdfFormAnnotation.MakeFormAnnotation(widget.GetPdfObject(), pdf);
			formAnnotation.SetBorderWidth(borderWidth);
			formAnnotation.SetBorderColor(ColorConstants.BLACK);
		}

		internal static void SetWidgetVisibility(iText.Kernel.Pdf.PdfDictionary widgetObj, VisibilitySpec vis)
		{
			if (widgetObj == null) return;
			var existingNum = widgetObj.GetAsNumber(PdfName.F);
			var existingFlags = existingNum == null ? 0 : existingNum.IntValue();

			var flagToAdd = 0;
			if (vis.Flag != AnnotationVisibility.Visible)
			{
				flagToAdd = MapVisibilityFlag(vis.Flag);
			}
			else if (vis.Render.HasValue && !vis.Render.Value)
			{
				// boolean false historically hides widget
				flagToAdd = MapVisibilityFlag(AnnotationVisibility.Hidden);
			}

			if (flagToAdd != 0)
			{
				widgetObj.Put(PdfName.F, new PdfNumber(existingFlags | flagToAdd));
			}
		}

		internal static int MapVisibilityFlag(AnnotationVisibility v)
		{
			// AnnotationVisibility now matches PDF bits; cast directly to int to support combined flags
			return (int)v;
		}

		internal static IEnumerable<string> SplitWord(string word, PdfFont font, float fontSize, float maxWidth)
		{
			if (font.GetWidth(word, fontSize) <= maxWidth)
			{
				yield return word;
				yield break;
			}

			var current = string.Empty;
			foreach (var ch in word)
			{
				var candidate = current + ch;
				if (font.GetWidth(candidate, fontSize) > maxWidth && current.Length > 0)
				{
					yield return current;
					current = ch.ToString();
				}
				else
				{
					current = candidate;
				}
			}

			if (!string.IsNullOrEmpty(current))
			{
				yield return current;
			}
		}

		internal static void ClipAndDrawHeaderText(iText.Layout.Canvas canvas, Rectangle cellRect, PdfFont font, string text, float fontSize, TextAlignment alignment, bool wrap)
		{
			if (string.IsNullOrWhiteSpace(text) || cellRect.GetWidth() <= 0 || cellRect.GetHeight() <= 0)
			{
				return;
			}

			var pdfCanvas = canvas.GetPdfCanvas();
			pdfCanvas.SaveState();
			pdfCanvas.Rectangle(cellRect);
			pdfCanvas.Clip();
			pdfCanvas.EndPath();

			canvas.SetFont(font).SetFontSize(fontSize);

			if (!wrap)
			{
				var anchorX = alignment switch
				{
					TextAlignment.CENTER => cellRect.GetX() + cellRect.GetWidth() / 2,
					TextAlignment.RIGHT => cellRect.GetX() + cellRect.GetWidth(),
					_ => cellRect.GetX()
				};
				var anchorY = cellRect.GetY() + cellRect.GetHeight() - fontSize;
				canvas.ShowTextAligned(text, anchorX, anchorY, alignment);
				pdfCanvas.RestoreState();
				return;
			}

			var maxWidth = cellRect.GetWidth();
			var lineHeight = fontSize * 1.1f;
			var availableLines = (int)Math.Floor(cellRect.GetHeight() / lineHeight);
			if (availableLines <= 0)
			{
				pdfCanvas.RestoreState();
				return;
			}

			var words = text.Split([' '], StringSplitOptions.RemoveEmptyEntries);
			var lines = new List<string>();
			var currentLine = string.Empty;

			foreach (var rawWord in words)
			{
				foreach (var word in SplitWord(rawWord, font, fontSize, maxWidth))
				{
					var candidate = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
					var candidateWidth = font.GetWidth(candidate, fontSize);
					if (candidateWidth <= maxWidth || string.IsNullOrEmpty(currentLine))
					{
						currentLine = candidate;
					}
					else
					{
						lines.Add(currentLine);
						currentLine = word;
					}
				}
			}

			if (!string.IsNullOrEmpty(currentLine))
			{
				lines.Add(currentLine);
			}

			var startY = cellRect.GetY() + cellRect.GetHeight() - fontSize;
			var xAnchor = alignment switch
			{
				TextAlignment.CENTER => cellRect.GetX() + cellRect.GetWidth() / 2,
				TextAlignment.RIGHT => cellRect.GetX() + cellRect.GetWidth(),
				_ => cellRect.GetX()
			};

			for (var i = 0; i < lines.Count && i < availableLines; i++)
			{
				var lineY = startY - i * lineHeight;
				canvas.ShowTextAligned(lines[i], xAnchor, lineY, alignment);
			}

			pdfCanvas.RestoreState();
		}

		internal static Color ResolveBorderColor(string? colorSpec)
		{
			if (string.IsNullOrWhiteSpace(colorSpec))
			{
				return ColorConstants.BLACK;
			}

			var trimmed = colorSpec.Trim();
			if (trimmed.StartsWith('#'))
			{
				var hex = trimmed[1..];
				if (hex.Length == 6
					&& int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
					&& int.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
					&& int.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
				{
					return new DeviceRgb(r, g, b);
				}
			}

			return trimmed.ToLowerInvariant() switch
			{
				"gray" => ColorConstants.GRAY,
				"lightgray" => ColorConstants.LIGHT_GRAY,
				"red" => ColorConstants.RED,
				"green" => ColorConstants.GREEN,
				"blue" => ColorConstants.BLUE,
				_ => ColorConstants.BLACK
			};
		}

		internal static void ApplyBorderStyle(PdfCanvas canvas, string? borderStyle)
		{
			if (string.IsNullOrWhiteSpace(borderStyle))
			{
				canvas.SetLineDash(0);
				return;
			}

			switch (borderStyle.Trim().ToLowerInvariant())
			{
				case "dashed":
					canvas.SetLineDash(3, 3);
					break;
				case "dotted":
					canvas.SetLineDash(1, 2);
					break;
				default:
					canvas.SetLineDash(0);
					break;
			}
		}

		internal static void DrawSubformBorder(SubformSpec spec, PdfCanvas canvas, string unit, float offsetX, float resolvedTop, float widthPoints, float heightPoints, bool originTopLeft)
		{
			var borderWidth = Utilities.UnitConverter.ToPoints(spec.BorderWidth, unit);
			if (borderWidth <= 0)
			{
				return;
			}

			var inset = borderWidth / 2f;
			var left = offsetX + inset;
			var rect = originTopLeft
				? new Rectangle(left, resolvedTop - heightPoints + inset, Math.Max(0, widthPoints - borderWidth), Math.Max(0, heightPoints - borderWidth))
				: new Rectangle(left, resolvedTop + inset, Math.Max(0, widthPoints - borderWidth), Math.Max(0, heightPoints - borderWidth));

			canvas.SaveState();
			canvas.SetLineWidth(borderWidth);
			canvas.SetStrokeColor(ResolveBorderColor(spec.BorderColor));
			ApplyBorderStyle(canvas, spec.BorderStyle);
			canvas.Rectangle(rect);
			canvas.Stroke();
			canvas.RestoreState();
		}

		internal static CheckBoxType ParseCheckType(string? value)
		{
			return (value ?? string.Empty).ToLowerInvariant() switch
			{
				"check" or "checkmark" => CheckBoxType.CHECK,
				"circle" => CheckBoxType.CIRCLE,
				"diamond" => CheckBoxType.DIAMOND,
				"square" => CheckBoxType.SQUARE,
				"star" => CheckBoxType.STAR,
				_ => CheckBoxType.CROSS,
			};
		}
	}
}
