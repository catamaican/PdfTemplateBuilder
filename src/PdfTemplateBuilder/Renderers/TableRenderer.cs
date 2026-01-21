using System;
using System.Collections.Generic;
using System.Linq;
using iText.Forms;
using iText.Forms.Fields;
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
	internal static class TableRenderer
	{
		internal static void DrawTables(TemplateSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
		{
			if (spec.Tables == null)
			{
				return;
			}

			layoutContext.SetCurrentPage(pdf.GetPageNumber(page));

			foreach (var table in spec.Tables)
			{
				// If the table's Visible Render is explicitly false, skip the table (backwards-compatible behavior)
				if (table.Visible.Render.HasValue && !table.Visible.Render.Value)
				{
					continue;
				}

				DrawTable(table, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, offsetX, offsetY, metrics, layoutContext);
			}
		}

		internal static void DrawTable(TableSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext, int? rowCountOverride = null)
		{
			layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
			var x = Utilities.UnitConverter.ToPoints(spec.X, unit) + offsetX;
			var y = ResolveTopY(spec.Below, spec.Gap, spec.YStart, unit, originTopLeft, pageHeight, offsetY, layoutContext);
			var headerHeight = Utilities.UnitConverter.ToPoints(spec.HeaderHeight <= 0 ? spec.RowHeight : spec.HeaderHeight, unit);
			var rowHeight = Utilities.UnitConverter.ToPoints(spec.RowHeight, unit);
			var bottomLimit = Math.Max(Utilities.UnitConverter.ToPoints(spec.BottomLimit, unit), metrics.MarginBottom);
			var rowCount = rowCountOverride ?? spec.SampleRowCount;
			var flowTop = GetFlowTop(pageHeight, metrics.MarginTop);
			var availableWidth = metrics.PageWidth - metrics.MarginRight - x;
			if (availableWidth < 0)
			{
				availableWidth = 0;
			}
			var visibleColumns = spec.Columns.Where(c => !c.Visible.ShouldOmitLayout()).ToList();
			if (visibleColumns.Count == 0)
			{
				// Nothing to draw
				return;
			}

			var widthResult = ResolveColumnWidths(spec, unit, availableWidth, visibleColumns);
			var columnWidths = widthResult.Widths;
			var headerScale = widthResult.Scale;

			if (spec.FitToSpace)
			{
				var maxRows = (int)Math.Floor(Math.Max(0, (y - bottomLimit) / rowHeight));
				if (maxRows > 0 && rowCount > maxRows)
				{
					rowCount = maxRows;
				}
			}

			var totalWidth = 0f;
			foreach (var width in columnWidths)
			{
				totalWidth += width;
			}

			var pageSize = page.GetPageSize();
			DrawTableHeader(spec, page, canvas, font, x, y, headerHeight, columnWidths, headerScale, visibleColumns);
			var headerRect = new Rectangle(x, y - headerHeight, totalWidth, headerHeight);
			layoutContext.Register(null, headerRect);
			y -= headerHeight;

			// Only draw rows if the table has RowsVisible == true
			if (spec.RowsVisible)
			{
				for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
				{
					if (y - rowHeight < bottomLimit)
					{
						page = pdf.AddNewPage();
						canvas = new PdfCanvas(page);
						layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
						DrawTableHeader(spec, page, canvas, font, x, flowTop, headerHeight, columnWidths, headerScale, visibleColumns);
						layoutContext.Register(null, new Rectangle(x, flowTop - headerHeight, totalWidth, headerHeight));
						y = flowTop - headerHeight;
					}

					var rowName = string.IsNullOrWhiteSpace(spec.RowNamePrefix)
						? (rowIndex + 1).ToString()
						: $"{spec.RowNamePrefix}{rowIndex + 1}";

					var currentX = x;
					for (var colIndex = 0; colIndex < visibleColumns.Count; colIndex++)
					{
						var column = visibleColumns[colIndex];
						var width = columnWidths[colIndex];
						var rect = new Rectangle(currentX, y - rowHeight, width, rowHeight);
						layoutContext.Register(null, rect);

						canvas.Rectangle(rect);
						canvas.Stroke();

						var tableName = string.IsNullOrWhiteSpace(spec.Name) ? "table" : spec.Name;
						var fieldName = $"{tableName}_{rowName}_{column.Name}";

						var field = new TextFormFieldBuilder(pdf, fieldName)
							.SetWidgetRectangle(rect)
							.SetPage(page)
							.CreateText();
						field.SetFont(font);
						field.SetFontSize(spec.BodyFontSize <= 0 ? 9 : spec.BodyFontSize);
						field.SetJustification(column.Align switch
						{
							"center" => TextAlignment.CENTER,
							"right" => TextAlignment.RIGHT,
							_ => TextAlignment.LEFT
						});

						// Determine effective annotation flag: column overrides table
						var colFlag = column.Visible.Flag;
						var tableFlag = spec.Visible.Flag;
						var effectiveFlag = colFlag != AnnotationVisibility.Visible ? colFlag : tableFlag;

						form.AddField(field, page);

						// Apply effective visibility for this cell (column may override table-level flags)
						var effectiveVis = new VisibilitySpec { Render = column.Visible.Render, Flag = effectiveFlag };
						DrawingHelpers.SetWidgetVisibility(field.GetWidgets()[0].GetPdfObject(), effectiveVis);

						currentX += width;
					}

					y -= rowHeight;
				}
			}

			var tableAnchorRect = new Rectangle(x, y, totalWidth, 0);
			layoutContext.Register(spec.Name, tableAnchorRect);
		}

		internal static void DrawTableHeader(TableSpec spec, PdfPage page, PdfCanvas canvas, PdfFont font, float x, float y, float headerHeight, IReadOnlyList<float> columnWidths, float headerScale, IReadOnlyList<TableColumnSpec> columns)
		{
			var pageSize = page.GetPageSize();
			using var layoutCanvas = new iText.Layout.Canvas(canvas, pageSize);
			var currentX = x;
			var baseFontSize = spec.HeaderFontSize <= 0 ? 9 : spec.HeaderFontSize;

			for (var colIndex = 0; colIndex < columns.Count; colIndex++)
			{
				var column = columns[colIndex];
				var width = columnWidths[colIndex];
				var headerAlign = (column.HeaderAlign ?? spec.HeaderAlign ?? column.Align ?? "left").ToLowerInvariant();
				var textAlignment = headerAlign switch
				{
					"center" => TextAlignment.CENTER,
					"right" => TextAlignment.RIGHT,
					_ => TextAlignment.LEFT
				};
				var wrapHeader = column.HeaderWrap ?? spec.HeaderWrap ?? false;
				var autoFit = column.HeaderAutoFit ?? spec.HeaderAutoFit ?? true;
				var fontSize = autoFit && headerScale < 1f ? baseFontSize * headerScale : baseFontSize;
				var headerText = column.Header ?? string.Empty;
				var availableTextWidth = Math.Max(0, width - 4);
				if (autoFit && availableTextWidth > 0 && headerText.Length > 0)
				{
					var textWidth = font.GetWidth(headerText, fontSize);
					if (textWidth > availableTextWidth)
					{
						var scale = availableTextWidth / textWidth;
						fontSize = Math.Max(6f, fontSize * scale);
					}
				}
				var rect = new Rectangle(currentX, y - headerHeight, width, headerHeight);
				canvas.Rectangle(rect);
				canvas.Stroke();

				layoutCanvas.SetFont(font).SetFontSize(fontSize);
				var cellRect = new Rectangle(currentX + 2, y - headerHeight + 2, Math.Max(0, width - 4), Math.Max(0, headerHeight - 4));
				DrawingHelpers.ClipAndDrawHeaderText(layoutCanvas, cellRect, font, headerText, fontSize, textAlignment, wrapHeader);

				currentX += width;
			}
		}

		internal static ColumnWidthResult ResolveColumnWidths(TableSpec spec, string unit, float availableWidth, IReadOnlyList<TableColumnSpec> columns)
		{
			var widths = new List<float>(columns.Count);
			var total = 0f;
			foreach (var column in columns)
			{
				var width = Utilities.UnitConverter.ToPoints(column.Width, unit);
				widths.Add(width);
				total += width;
			}

			if (!spec.FitWidth || total <= 0 || total <= availableWidth || availableWidth <= 0)
			{
				return new ColumnWidthResult(widths, 1f);
			}

			var scale = availableWidth / total;
			for (var i = 0; i < widths.Count; i++)
			{
				widths[i] *= scale;
			}

			return new ColumnWidthResult(widths, scale);
		}
	}
}
