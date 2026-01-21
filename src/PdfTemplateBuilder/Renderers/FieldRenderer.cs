using System;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Layout.Properties;
using PdfTemplateBuilder.Models;
using PdfTemplateBuilder.Layout;
using static PdfTemplateBuilder.Layout.LayoutHelpers;

namespace PdfTemplateBuilder.Renderers
{
	internal static class FieldRenderer
	{
		internal const float DefaultFieldFontSize = 10f;

		internal static void DrawFields(TemplateSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
		{
			if (spec.Fields == null)
			{
				return;
			}

			layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
			var flowTop = GetFlowTop(pageHeight, metrics.MarginTop);
			var minY = Math.Max(metrics.MarginBottom, 0);

			foreach (var fieldSpec in spec.Fields)
			{
				var heightPoints = Utilities.UnitConverter.ToPoints(fieldSpec.Height, unit);
				var resolvedY = ResolveY(fieldSpec.Below, fieldSpec.Gap, fieldSpec.Y, heightPoints, unit, originTopLeft, pageHeight, offsetY, layoutContext);

				// Page break if field goes below bottom margin
				if (resolvedY < minY)
				{
					page = pdf.AddNewPage();
					canvas = new PdfCanvas(page);
					layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
					resolvedY = flowTop - heightPoints;
				}

				var left = Utilities.UnitConverter.ToPoints(fieldSpec.X, unit) + offsetX;
				var widthPoints = ResolveLengthToPoints(fieldSpec.Width, left, metrics, unit);

				var rect = new Rectangle(
					left,
					resolvedY,
					widthPoints,
					heightPoints);

				var fontSize = fieldSpec.FontSize <= 0 ? DefaultFieldFontSize : fieldSpec.FontSize;
				var resolvedAlign = fieldSpec.Align;
				if (string.IsNullOrWhiteSpace(resolvedAlign) && string.Equals(fieldSpec.DataType, "decimal", StringComparison.OrdinalIgnoreCase))
				{
					resolvedAlign = "right";
				}

				var value = !string.IsNullOrWhiteSpace(fieldSpec.Value) ? fieldSpec.Value : (fieldSpec.SampleValue ? GetSampleValue(fieldSpec) : null);
				var field = CreateTextField(pdf, fieldSpec.Name ?? string.Empty, rect, page, font, fontSize, resolvedAlign, fieldSpec.Multiline, value);

				if (fieldSpec.BorderWidth > 0)
				{
					DrawingHelpers.ApplyWidgetBorder(field, pdf, fieldSpec.BorderWidth);
				}

				// Respect visibility: keep field in AcroForm but set widget annotation flags based on spec
				DrawingHelpers.SetWidgetVisibility(field.GetWidgets()[0].GetPdfObject(), fieldSpec.Visible);

				form.AddField(field, page);
				layoutContext.Register(fieldSpec.Name, rect);
			}
		}

		internal static iText.Forms.Fields.PdfTextFormField CreateTextField(PdfDocument pdf, string name, Rectangle rect, PdfPage page, PdfFont font, float fontSize, string? align, bool multiline, string? value)
		{
			var field = new TextFormFieldBuilder(pdf, name ?? string.Empty)
				.SetWidgetRectangle(rect)
				.SetPage(page)
				.CreateText();

			field.SetFont(font);
			field.SetFontSize(fontSize <= 0 ? DefaultFieldFontSize : fontSize);
			field.SetJustification(align switch
			{
				"center" => TextAlignment.CENTER,
				"right" => TextAlignment.RIGHT,
				_ => TextAlignment.LEFT
			});

			if (multiline)
			{
				field.SetMultiline(true);
			}

			if (!string.IsNullOrWhiteSpace(value))
			{
				field.SetValue(value);
			}

			return field;
		}

		private static string GetSampleValue(FieldSpec fieldSpec)
		{
			var dataType = fieldSpec.DataType ?? string.Empty;
			if (dataType.Equals("date", StringComparison.OrdinalIgnoreCase))
			{
				var format = string.IsNullOrWhiteSpace(fieldSpec.Format) ? "yyyy-MM-dd" : fieldSpec.Format;
				return DateTime.Now.ToString(format);
			}

			if (dataType.Equals("decimal", StringComparison.OrdinalIgnoreCase))
			{
				var format = string.IsNullOrWhiteSpace(fieldSpec.Format) ? "0.00" : fieldSpec.Format;
				return 0m.ToString(format);
			}

			return string.Empty;
		}
	}
}
