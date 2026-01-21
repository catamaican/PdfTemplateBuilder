using System;
using System.Collections.Generic;
using System.IO;
using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Properties;
using PdfTemplateBuilder.Models;
using PdfTemplateBuilder.Utilities;
using PdfTemplateBuilder.Layout;
using PdfTemplateBuilder.Renderers;
using static PdfTemplateBuilder.Layout.LayoutHelpers; 

namespace PdfTemplateBuilder
{
	// Program.Main moved to CLI project. The library exposes `PdfTemplateRenderer.Build(outputPath, spec)`.
	// Keep the namespace and public types for library consumption.


	public static class PdfTemplateRenderer
	{
		/// <summary>
		/// Build PDF into a file on disk. Kept for backward compatibility; delegates to <see cref="Build(System.IO.Stream, TemplateSpec)"/>.
		/// </summary>
		public static void Build(string outputPath, TemplateSpec spec)
		{
			using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
			Build(fs, spec);
		}

		/// <summary>
		/// Build PDF into the provided stream (positioned at end after the write). The stream is NOT disposed by this method.
		/// </summary>
		public static void Build(System.IO.Stream outputStream, TemplateSpec spec)
		{
			// Do not dispose the provided stream here - PdfWriter will not dispose it but close operations are safe.
			var writer = new PdfWriter(outputStream);
			using var pdf = new PdfDocument(writer);
			var pageSpec = spec.Page ?? new PageSpec();
			var pageSize = UnitConverter.ResolvePageSize(pageSpec.Size ?? "A4");
			pdf.SetDefaultPageSize(pageSize);

			var fontSpec = spec.Fonts ?? new FontSpec();
			var fontBundle = FontResolver.Resolve(fontSpec);
			var font = fontBundle.Regular;
			var boldFont = fontBundle.Bold ?? font;
			var page = pdf.AddNewPage();
			var canvas = new PdfCanvas(page);
			var form = PdfAcroForm.GetAcroForm(pdf, true);
			form.SetNeedAppearances(true);

			var unit = spec.Unit ?? "mm";
			var originTopLeft = string.Equals(spec.Origin ?? "top-left", "top-left", StringComparison.OrdinalIgnoreCase);
			var pageHeight = page.GetPageSize().GetHeight();
			var pageWidth = page.GetPageSize().GetWidth();
			var margins = pageSpec.Margins ?? new MarginSpec();
			var marginLeft = UnitConverter.ToPoints(margins.Left, unit);
			var marginRight = UnitConverter.ToPoints(margins.Right, unit);
			var marginTop = UnitConverter.ToPoints(margins.Top, unit);
			var marginBottom = UnitConverter.ToPoints(margins.Bottom, unit);
			var offsetX = marginLeft;
			var offsetY = 0f; // Margins are handled via layoutMetrics, not as Y offset
			var layoutMetrics = new LayoutMetrics(pageWidth, pageHeight, marginLeft, marginRight, marginTop, marginBottom);
			var layoutContext = new LayoutContext();

			StaticTextRenderer.DrawStaticTexts(spec, pdf, page, canvas, font, boldFont, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);
			FieldRenderer.DrawFields(spec, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);
			CheckboxRenderer.DrawCheckboxes(spec, pdf, page, form, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutContext);
			TableRenderer.DrawTables(spec, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);
			SignatureRenderer.DrawSignatures(spec, pdf, ref page, ref canvas, form, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);

			SubformRenderer.DrawSubforms(spec, pdf, ref page, ref canvas, form, font, boldFont, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);
		}

		/// <summary>
		/// Build PDF and return the result as a byte array.
		/// </summary>
		public static byte[] BuildToBytes(TemplateSpec spec)
		{
			using var ms = new MemoryStream();
			Build(ms, spec);
			return ms.ToArray();
		}

		/// <summary>
		/// Build PDF and return a seekable stream positioned at 0.
		/// Caller is responsible for disposing the returned stream.
		/// </summary>
		public static System.IO.Stream BuildToStream(TemplateSpec spec)
		{
			var ms = new MemoryStream();
			Build(ms, spec);
			ms.Position = 0;
			return ms;
		}

		// Rendering delegated to `PdfTemplateBuilder.Renderers` and shared helpers in `PdfTemplateBuilder.Layout` and `PdfTemplateBuilder.Utilities`.
	}

}
