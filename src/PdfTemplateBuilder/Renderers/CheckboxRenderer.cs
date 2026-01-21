using System;
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
	internal static class CheckboxRenderer
	{
		internal static void DrawCheckboxes(TemplateSpec spec, PdfDocument pdf, PdfPage page, PdfAcroForm form, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutContext layoutContext)
		{
			if (spec.Checkboxes == null)
			{
				return;
			}

			layoutContext.SetCurrentPage(pdf.GetPageNumber(page));

			foreach (var checkbox in spec.Checkboxes)
			{
				var size = checkbox.Size <= 0 ? 5 : checkbox.Size;
				var sizePoints = Utilities.UnitConverter.ToPoints(size, unit);
				var rect = new Rectangle(
					Utilities.UnitConverter.ToPoints(checkbox.X, unit) + offsetX,
					ResolveY(checkbox.Below, checkbox.Gap, checkbox.Y, sizePoints, unit, originTopLeft, pageHeight, offsetY, layoutContext),
					sizePoints,
					sizePoints);

				var field = CreateCheckBoxField(pdf, checkbox.Name ?? string.Empty, rect, page, checkbox.CheckType);

				if (checkbox.BorderWidth > 0)
				{
					DrawingHelpers.ApplyWidgetBorder(field, pdf, checkbox.BorderWidth);
				}

				if (checkbox.Checked)
				{
					field.SetValue("Yes");
				}

				// Respect Checkbox visibility: keep field but set widget annotation flags based on spec
				DrawingHelpers.SetWidgetVisibility(field.GetWidgets()[0].GetPdfObject(), checkbox.Visible);

				form.AddField(field, page);
				layoutContext.Register(checkbox.Name, rect);
			}
		}

		internal static iText.Forms.Fields.PdfButtonFormField CreateCheckBoxField(PdfDocument pdf, string name, Rectangle rect, PdfPage page, string? checkType)
		{
			var field = new CheckBoxFormFieldBuilder(pdf, name ?? string.Empty)
				.SetWidgetRectangle(rect)
				.SetPage(page)
				.CreateCheckBox();

			field.SetCheckType(DrawingHelpers.ParseCheckType(checkType));
			return field;
		}
	}
}
