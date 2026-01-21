using System;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Geom;
using iText.Kernel.Font;
using PdfTemplateBuilder.Models;
using PdfTemplateBuilder.Layout;
using static PdfTemplateBuilder.Layout.LayoutHelpers;

namespace PdfTemplateBuilder.Renderers
{
	internal static class SubformRenderer
	{
		internal static void DrawSubforms(TemplateSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, PdfFont boldFont, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
		{
			if (spec.Subforms == null)
			{
				return;
			}

			foreach (var subform in spec.Subforms)
			{
				layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
				var subOffsetX = offsetX + Utilities.UnitConverter.ToPoints(subform.X, unit);
				var heightPoints = Utilities.UnitConverter.ToPoints(subform.Height, unit);
				var widthPoints = Utilities.UnitConverter.ToPoints(subform.Width, unit);
				var gapPoints = Utilities.UnitConverter.ToPoints(subform.Gap, unit);
				var anchor = new Rectangle(0, 0, 0, 0);
				var hasAnchor = !string.IsNullOrWhiteSpace(subform.Below) && layoutContext.TryGetAnchor(subform.Below, out anchor);
				var resolvedTop = hasAnchor
					? anchor.GetY() - gapPoints
					: ResolveTopY(null, 0, subform.Y, unit, originTopLeft, pageHeight, offsetY, layoutContext);

				// For subforms at y=0 without anchor, start at the top margin
				var useFlowTop = !hasAnchor && subform.Y <= 0;
				if (useFlowTop)
				{
					resolvedTop = GetFlowTop(pageHeight, metrics.MarginTop);
				}

				var minBottom = Math.Max(metrics.MarginBottom, 0);
				var maxTop = pageHeight - Math.Max(metrics.MarginTop, 0);
				var availableWidth = Math.Max(0, metrics.PageWidth - metrics.MarginRight - subOffsetX);
				var resolvedWidth = widthPoints > 0 ? Math.Min(widthPoints, availableWidth) : availableWidth;
				var availableHeight = originTopLeft
					? Math.Max(0, resolvedTop - minBottom)
					: Math.Max(0, maxTop - resolvedTop);

				// Only use explicit height for page break check, not the full available height
				// This prevents immediate page breaks for subforms without explicit height
				var desiredHeight = heightPoints > 0 ? heightPoints : 0;

				// subOffsetY: offset for child elements relative to subform position
				// When subform is at y=0 (flow top), child Y coordinates are from page top
				// When subform has explicit Y or anchor, child Y is relative to subform top
				var subOffsetY = useFlowTop
					? 0f
					: (originTopLeft ? resolvedTop - pageHeight : resolvedTop);

				// Page break check: only if subform has explicit height that doesn't fit
				if (originTopLeft)
				{
					if (desiredHeight > 0 && resolvedTop - desiredHeight < minBottom)
					{
						page = pdf.AddNewPage();
						canvas = new PdfCanvas(page);
						resolvedTop = GetFlowTop(pageHeight, metrics.MarginTop);
						availableHeight = Math.Max(0, resolvedTop - minBottom);
						subOffsetY = 0f;
					}
				}
				else
				{
					if (desiredHeight > 0 && resolvedTop + desiredHeight > maxTop)
					{
						page = pdf.AddNewPage();
						canvas = new PdfCanvas(page);
						resolvedTop = minBottom;
						availableHeight = Math.Max(0, maxTop - resolvedTop);
						subOffsetY = 0f;
					}
				}

				var resolvedHeight = heightPoints > 0 ? Math.Min(heightPoints, availableHeight) : availableHeight;

				var subLayoutContext = layoutContext.CreateChild();
				subLayoutContext.SetCurrentPage(pdf.GetPageNumber(page));

				StaticTextRenderer.DrawStaticTexts(subform, pdf, page, canvas, font, boldFont, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);
				FieldRenderer.DrawFields(subform, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);
				CheckboxRenderer.DrawCheckboxes(subform, pdf, page, form, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, subLayoutContext);
				TableRenderer.DrawTables(subform, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);
				SignatureRenderer.DrawSignatures(subform, pdf, ref page, ref canvas, form, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);

				DrawSubforms(subform, pdf, ref page, ref canvas, form, font, boldFont, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);

				if (subform.BorderWidth > 0 && resolvedWidth > 0)
				{
					DrawSubformBorders(subform, pdf, subLayoutContext, unit, originTopLeft, metrics, subOffsetX, resolvedWidth);
				}

				if (subLayoutContext.TryGetLast(out var lastRect))
				{
					layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
					layoutContext.Register(subform.Name, lastRect);
				}
				else
				{
					var subformBottom = originTopLeft ? resolvedTop - resolvedHeight : resolvedTop;
					var anchorRect = new Rectangle(subOffsetX, subformBottom, resolvedWidth, resolvedHeight);
					layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
					layoutContext.Register(subform.Name, anchorRect);
				}
			}
		}

		internal static void DrawSubformBorders(SubformSpec spec, PdfDocument pdf, LayoutContext layoutContext, string unit, bool originTopLeft, LayoutMetrics metrics, float offsetX, float resolvedWidth)
		{
			var borderWidth = Utilities.UnitConverter.ToPoints(spec.BorderWidth, unit);
			if (borderWidth <= 0)
			{
				return;
			}

			var boundsByPage = layoutContext.GetPageBounds();
			if (boundsByPage.Count == 0)
			{
				return;
			}

			var left = Math.Max(offsetX, metrics.MarginLeft);
			var rightLimit = metrics.PageWidth - metrics.MarginRight;
			var width = resolvedWidth > 0 ? Math.Min(resolvedWidth, rightLimit - left) : Math.Max(0, rightLimit - left);
			if (width <= 0)
			{
				return;
			}

			foreach (var entry in boundsByPage)
			{
				var bounds = entry.Value;
				var minY = Math.Max(bounds.GetY(), metrics.MarginBottom);
				var maxY = Math.Min(bounds.GetY() + bounds.GetHeight(), metrics.PageHeight - metrics.MarginTop);
				var height = maxY - minY;
				if (height <= 0)
				{
					continue;
				}

				var page = pdf.GetPage(entry.Key);
				var canvas = new PdfCanvas(page);
				var resolvedTop = originTopLeft ? maxY : minY;
				DrawingHelpers.DrawSubformBorder(spec, canvas, unit, left, resolvedTop, width, height, originTopLeft);
			}
		}
	}
}
