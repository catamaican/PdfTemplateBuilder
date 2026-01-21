using System;
using System.Collections.Generic;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Geom;
using PdfTemplateBuilder.Models;
using PdfTemplateBuilder.Layout;
using static PdfTemplateBuilder.Layout.LayoutHelpers;

namespace PdfTemplateBuilder.Renderers
{
	internal static class SignatureRenderer
	{
		internal static void DrawSignatures(TemplateSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
		{
			if (spec.Signatures == null)
			{
				return;
			}

			layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
			var flowTop = GetFlowTop(pageHeight, metrics.MarginTop);
			var minY = Math.Max(metrics.MarginBottom, 0);

			// Group signatures by their anchor (below) to handle them together
			// This prevents multiple page breaks for signatures on the same row
			var groups = new Dictionary<string, List<SignatureSpec>>(StringComparer.OrdinalIgnoreCase);
			var noAnchor = new List<SignatureSpec>();

			foreach (var signature in spec.Signatures)
			{
				var anchorKey = signature.Below ?? string.Empty;
				if (string.IsNullOrWhiteSpace(anchorKey))
				{
					noAnchor.Add(signature);
				}
				else
				{
					if (!groups.TryGetValue(anchorKey, out var list))
					{
						list = new List<SignatureSpec>();
						groups[anchorKey] = list;
					}
					list.Add(signature);
				}
			}

			// Draw signatures without anchors first
			foreach (var signature in noAnchor)
			{
				DrawSingleSignature(signature, pdf, ref page, ref canvas, form, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutContext, flowTop, minY);
			}

			// Draw grouped signatures - check page break once per group
			foreach (var group in groups.Values)
			{
				if (group.Count == 0) continue;

				// Calculate Y for the first signature in group to check if page break needed
				var firstSig = group[0];
				var heightPoints = Utilities.UnitConverter.ToPoints(firstSig.Height, unit);
				var resolvedY = ResolveY(firstSig.Below, firstSig.Gap, firstSig.Y, heightPoints, unit, originTopLeft, pageHeight, offsetY, layoutContext);

				// If any signature in the group needs a page break, do it once for all
				EnsureNewPageIfNeeded(pdf, ref page, ref canvas, layoutContext, resolvedY, minY);

				// Draw all signatures in the group on the current page
				foreach (var signature in group)
				{
					heightPoints = Utilities.UnitConverter.ToPoints(signature.Height, unit);
					resolvedY = ResolveY(signature.Below, signature.Gap, signature.Y, heightPoints, unit, originTopLeft, pageHeight, offsetY, layoutContext);

					// After page break, recalculate from flow top
					EnsurePageHasSpace(pdf, ref page, ref canvas, layoutContext, ref resolvedY, heightPoints, minY, flowTop);

					var rect = new Rectangle(
						Utilities.UnitConverter.ToPoints(signature.X, unit) + offsetX,
						resolvedY,
						Utilities.UnitConverter.ToPoints(signature.Width, unit),
						heightPoints);

					var field = new SignatureFormFieldBuilder(pdf, signature.Name ?? string.Empty)
						.SetWidgetRectangle(rect)
						.SetPage(page)
						.CreateSignature();

					if (signature.BorderWidth > 0)
					{
						DrawingHelpers.ApplyWidgetBorder(field, pdf, signature.BorderWidth);
					}

					form.AddField(field, page);
					layoutContext.Register(signature.Name, rect);
				}
			}
		}

		internal static void DrawSingleSignature(SignatureSpec signature, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutContext layoutContext, float flowTop, float minY)
		{
			var heightPoints = Utilities.UnitConverter.ToPoints(signature.Height, unit);
			var resolvedY = ResolveY(signature.Below, signature.Gap, signature.Y, heightPoints, unit, originTopLeft, pageHeight, offsetY, layoutContext);

			EnsurePageHasSpace(pdf, ref page, ref canvas, layoutContext, ref resolvedY, heightPoints, minY, flowTop);

			var rect = new Rectangle(
				Utilities.UnitConverter.ToPoints(signature.X, unit) + offsetX,
				resolvedY,
				Utilities.UnitConverter.ToPoints(signature.Width, unit),
				heightPoints);

			var field = new SignatureFormFieldBuilder(pdf, signature.Name ?? string.Empty)
				.SetWidgetRectangle(rect)
				.SetPage(page)
				.CreateSignature();

			if (signature.BorderWidth > 0)
			{
				DrawingHelpers.ApplyWidgetBorder(field, pdf, signature.BorderWidth);
			}

			form.AddField(field, page);
			layoutContext.Register(signature.Name, rect);
		}
	}
}
