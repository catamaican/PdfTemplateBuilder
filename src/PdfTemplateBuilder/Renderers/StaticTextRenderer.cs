using System;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Layout;
using iText.Layout.Properties;
using PdfTemplateBuilder.Models;
using PdfTemplateBuilder.Layout;
using static PdfTemplateBuilder.Layout.LayoutHelpers;

namespace PdfTemplateBuilder.Renderers
{
	internal static class StaticTextRenderer
	{
		internal static void DrawStaticTexts(TemplateSpec spec, PdfDocument pdf, PdfPage page, PdfCanvas canvas, PdfFont font, PdfFont boldFont, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
		{
			if (spec.StaticTexts == null)
			{
				return;
			}

			layoutContext.SetCurrentPage(pdf.GetPageNumber(page));

			var pageSize = page.GetPageSize();
			using var layoutCanvas = new iText.Layout.Canvas(canvas, pageSize);

			foreach (var text in spec.StaticTexts)
			{
				var x = Utilities.UnitConverter.ToPoints(text.X, unit) + offsetX;
				var fontSize = text.FontSize <= 0 ? 10 : text.FontSize;
				var y = ResolveY(text.Below, text.Gap, text.Y, fontSize, unit, originTopLeft, pageHeight, offsetY, layoutContext);

				// Resolve width (number or 'auto') using shared helper
				var widthPoints = ResolveLengthToPoints(text.Width, x, metrics, unit);

				// Default single-line rect
				var rect = new Rectangle(x, y, widthPoints, fontSize);

				var usedFont = text.Bold ? boldFont : font;
				layoutCanvas.SetFont(usedFont).SetFontSize(fontSize);

				if (widthPoints > 0)
				{
					// Compute available vertical space down to page bottom (respecting margins)
					var minBottom = Math.Max(metrics.MarginBottom, 0);
					var maxAvailableHeight = originTopLeft ? Math.Max(0, y - minBottom) : Math.Max(0, Math.Min(pageHeight - metrics.MarginTop, y));
					var lineHeight = fontSize * 1.1f;
					var maxLines = Math.Max(1, (int)Math.Floor(maxAvailableHeight / lineHeight));

					// Use a cell bounded by available height; ClipAndDrawHeaderText will wrap and render up to available lines
					var cellHeight = Math.Max(fontSize, maxLines * lineHeight);
					var cellRect = new Rectangle(x, y - cellHeight + fontSize, widthPoints, cellHeight);

					// Register the actual occupied rect so anchors can reference it
					layoutContext.Register(text.Name, cellRect);

					DrawingHelpers.ClipAndDrawHeaderText(layoutCanvas, cellRect, usedFont, text.Value ?? string.Empty, fontSize, TextAlignment.LEFT, true);
				}
				else
				{
					// Single-line, register baseline-sized rect
					layoutContext.Register(text.Name, rect);
					layoutCanvas.ShowTextAligned(text.Value ?? string.Empty, x, y + fontSize, TextAlignment.LEFT);
				}
			}
		}
	}
}
