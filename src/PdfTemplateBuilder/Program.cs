using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Forms.Fields.Properties;

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

            DrawStaticTexts(spec, pdf, page, canvas, font, boldFont, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);
            DrawFields(spec, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);
            DrawCheckboxes(spec, pdf, page, form, font, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutContext);
            DrawTables(spec, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);
            DrawSignatures(spec, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);

            DrawSubforms(spec, pdf, ref page, ref canvas, form, font, boldFont, unit, originTopLeft, pageHeight, offsetX, offsetY, layoutMetrics, layoutContext);
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

        /// <summary>
        /// Draw static text elements and handle flexible widths and wrapping.
        /// When a static text has <c>width: "auto"</c>, the method computes the available horizontal space and wraps text into the available vertical space.
        /// The registered layout rectangle reflects the actual space consumed so anchors using <c>below</c> work correctly with wrapped content.
        /// </summary>
        private static void DrawStaticTexts(TemplateSpec spec, PdfDocument pdf, PdfPage page, PdfCanvas canvas, PdfFont font, PdfFont boldFont, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
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
                var x = UnitConverter.ToPoints(text.X, unit) + offsetX;
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

                    ClipAndDrawHeaderText(layoutCanvas, cellRect, usedFont, text.Value ?? string.Empty, fontSize, TextAlignment.LEFT, true);
                }
                else
                {
                    // Single-line, register baseline-sized rect
                    layoutContext.Register(text.Name, rect);
                    layoutCanvas.ShowTextAligned(text.Value ?? string.Empty, x, y + fontSize, TextAlignment.LEFT);
                }
            }
        }

        /// <summary>
        /// Draw text fields defined in <paramref name="spec"/>.
        /// Supports flexible widths using <c>LengthSpec</c>. When width is <c>auto</c>, the field expands to the available horizontal space inside the container.
        /// </summary>
        private static void DrawFields(TemplateSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
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
                var heightPoints = UnitConverter.ToPoints(fieldSpec.Height, unit);
                var resolvedY = ResolveY(fieldSpec.Below, fieldSpec.Gap, fieldSpec.Y, heightPoints, unit, originTopLeft, pageHeight, offsetY, layoutContext);
                
                // Page break if field goes below bottom margin
                if (resolvedY < minY)
                {
                    page = pdf.AddNewPage();
                    canvas = new PdfCanvas(page);
                    layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
                    resolvedY = flowTop - heightPoints;
                }
                
                var left = UnitConverter.ToPoints(fieldSpec.X, unit) + offsetX;
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
                    ApplyWidgetBorder(field, pdf, fieldSpec.BorderWidth);
                }

                // Respect visibility: keep field in AcroForm but set widget annotation flags based on spec
                SetWidgetVisibility(field.GetWidgets()[0].GetPdfObject(), fieldSpec.Visible);

                form.AddField(field, page);
                layoutContext.Register(fieldSpec.Name, rect);
            }
        }

            private static void DrawCheckboxes(TemplateSpec spec, PdfDocument pdf, PdfPage page, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutContext layoutContext)
        {
            if (spec.Checkboxes == null)
            {
                return;
            }

            layoutContext.SetCurrentPage(pdf.GetPageNumber(page));

            foreach (var checkbox in spec.Checkboxes)
            {
                var size = checkbox.Size <= 0 ? 5 : checkbox.Size;
                var sizePoints = UnitConverter.ToPoints(size, unit);
                var rect = new Rectangle(
                    UnitConverter.ToPoints(checkbox.X, unit) + offsetX,
                    ResolveY(checkbox.Below, checkbox.Gap, checkbox.Y, sizePoints, unit, originTopLeft, pageHeight, offsetY, layoutContext),
                    sizePoints,
                    sizePoints);

                var field = CreateCheckBoxField(pdf, checkbox.Name ?? string.Empty, rect, page, checkbox.CheckType);

                if (checkbox.BorderWidth > 0)
                {
                    ApplyWidgetBorder(field, pdf, checkbox.BorderWidth);
                }

                if (checkbox.Checked)
                {
                    field.SetValue("Yes");
                }

                // Respect Checkbox visibility: keep field but set widget annotation flags based on spec
                SetWidgetVisibility(field.GetWidgets()[0].GetPdfObject(), checkbox.Visible);

                form.AddField(field, page);
                layoutContext.Register(checkbox.Name, rect);
            }
        }

        private static void DrawSignatures(TemplateSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
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
                DrawSingleSignature(signature, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, offsetX, offsetY, metrics, layoutContext, flowTop, minY);
            }
            
            // Draw grouped signatures - check page break once per group
            foreach (var group in groups.Values)
            {
                if (group.Count == 0) continue;
                
                // Calculate Y for the first signature in group to check if page break needed
                var firstSig = group[0];
                var heightPoints = UnitConverter.ToPoints(firstSig.Height, unit);
                var resolvedY = ResolveY(firstSig.Below, firstSig.Gap, firstSig.Y, heightPoints, unit, originTopLeft, pageHeight, offsetY, layoutContext);
                
                // If any signature in the group needs a page break, do it once for all
                EnsureNewPageIfNeeded(pdf, ref page, ref canvas, layoutContext, resolvedY, minY);
                
                // Draw all signatures in the group on the current page
                foreach (var signature in group)
                {
                    heightPoints = UnitConverter.ToPoints(signature.Height, unit);
                    resolvedY = ResolveY(signature.Below, signature.Gap, signature.Y, heightPoints, unit, originTopLeft, pageHeight, offsetY, layoutContext);
                    
                    // After page break, recalculate from flow top
                    EnsurePageHasSpace(pdf, ref page, ref canvas, layoutContext, ref resolvedY, heightPoints, minY, flowTop);
                    
                    var rect = new Rectangle(
                        UnitConverter.ToPoints(signature.X, unit) + offsetX,
                        resolvedY,
                        UnitConverter.ToPoints(signature.Width, unit),
                        heightPoints);

                    var field = new SignatureFormFieldBuilder(pdf, signature.Name ?? string.Empty)
                        .SetWidgetRectangle(rect)
                        .SetPage(page)
                        .CreateSignature();

                    if (signature.BorderWidth > 0)
                    {
                        ApplyWidgetBorder(field, pdf, signature.BorderWidth);
                    }

                    form.AddField(field, page);
                    layoutContext.Register(signature.Name, rect);
                }
            }
        }
        
        private static void DrawSingleSignature(SignatureSpec signature, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext, float flowTop, float minY)
        {
            var heightPoints = UnitConverter.ToPoints(signature.Height, unit);
            var resolvedY = ResolveY(signature.Below, signature.Gap, signature.Y, heightPoints, unit, originTopLeft, pageHeight, offsetY, layoutContext);
            
            EnsurePageHasSpace(pdf, ref page, ref canvas, layoutContext, ref resolvedY, heightPoints, minY, flowTop);

            var rect = new Rectangle(
                UnitConverter.ToPoints(signature.X, unit) + offsetX,
                resolvedY,
                UnitConverter.ToPoints(signature.Width, unit),
                heightPoints);

            var field = new SignatureFormFieldBuilder(pdf, signature.Name ?? string.Empty)
                .SetWidgetRectangle(rect)
                .SetPage(page)
                .CreateSignature();

            if (signature.BorderWidth > 0)
            {
                var widget = field.GetWidgets()[0];
                var formAnnotation = PdfFormAnnotation.MakeFormAnnotation(widget.GetPdfObject(), pdf);
                formAnnotation.SetBorderWidth(signature.BorderWidth);
                formAnnotation.SetBorderColor(ColorConstants.BLACK);
            }

            form.AddField(field, page);
            layoutContext.Register(signature.Name, rect);
        }

        private static void DrawTables(TemplateSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
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

        private static void DrawTable(TableSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext, int? rowCountOverride = null)
        {
            layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
            var x = UnitConverter.ToPoints(spec.X, unit) + offsetX;
            var y = ResolveTopY(spec.Below, spec.Gap, spec.YStart, unit, originTopLeft, pageHeight, offsetY, layoutContext);
            var headerHeight = UnitConverter.ToPoints(spec.HeaderHeight <= 0 ? spec.RowHeight : spec.HeaderHeight, unit);
            var rowHeight = UnitConverter.ToPoints(spec.RowHeight, unit);
            var bottomLimit = Math.Max(UnitConverter.ToPoints(spec.BottomLimit, unit), metrics.MarginBottom);
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

            DrawTableHeader(spec, pdf, page, canvas, font, unit, x, y, headerHeight, columnWidths, headerScale, visibleColumns);
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
                        DrawTableHeader(spec, pdf, page, canvas, font, unit, x, flowTop, headerHeight, columnWidths, headerScale, visibleColumns);
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
                        SetWidgetVisibility(field.GetWidgets()[0].GetPdfObject(), effectiveVis);

                        currentX += width;
                    }

                    y -= rowHeight;
                }
            }

            var tableAnchorRect = new Rectangle(x, y, totalWidth, 0);
            layoutContext.Register(spec.Name, tableAnchorRect);
        }

        private static void DrawTableHeader(TableSpec spec, PdfDocument pdf, PdfPage page, PdfCanvas canvas, PdfFont font, string unit, float x, float y, float headerHeight, IReadOnlyList<float> columnWidths, float headerScale, IReadOnlyList<TableColumnSpec> columns)
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
                ClipAndDrawHeaderText(layoutCanvas, cellRect, font, headerText, fontSize, textAlignment, wrapHeader);

                currentX += width;
            }
        }

        private static void DrawSubforms(TemplateSpec spec, PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, PdfAcroForm form, PdfFont font, PdfFont boldFont, string unit, bool originTopLeft, float pageHeight, float offsetX, float offsetY, LayoutMetrics metrics, LayoutContext layoutContext)
        {
            if (spec.Subforms == null)
            {
                return;
            }

            foreach (var subform in spec.Subforms)
            {
                layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
                var subOffsetX = offsetX + UnitConverter.ToPoints(subform.X, unit);
                var heightPoints = UnitConverter.ToPoints(subform.Height, unit);
                var widthPoints = UnitConverter.ToPoints(subform.Width, unit);
                var gapPoints = UnitConverter.ToPoints(subform.Gap, unit);
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

                DrawStaticTexts(subform, pdf, page, canvas, font, boldFont, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);
                DrawFields(subform, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);
                DrawCheckboxes(subform, pdf, page, form, font, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, subLayoutContext);
                DrawTables(subform, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);
                DrawSignatures(subform, pdf, ref page, ref canvas, form, font, unit, originTopLeft, pageHeight, subOffsetX, subOffsetY, metrics, subLayoutContext);

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

        private static void DrawSubformBorders(SubformSpec spec, PdfDocument pdf, LayoutContext layoutContext, string unit, bool originTopLeft, LayoutMetrics metrics, float offsetX, float resolvedWidth)
        {
            var borderWidth = UnitConverter.ToPoints(spec.BorderWidth, unit);
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
                DrawSubformBorder(spec, canvas, unit, left, resolvedTop, width, height, originTopLeft);
            }
        }

        private static void DrawSubformBorder(SubformSpec spec, PdfCanvas canvas, string unit, float offsetX, float resolvedTop, float widthPoints, float heightPoints, bool originTopLeft)
        {
            var borderWidth = UnitConverter.ToPoints(spec.BorderWidth, unit);
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

        private static ColumnWidthResult ResolveColumnWidths(TableSpec spec, string unit, float availableWidth, IReadOnlyList<TableColumnSpec> columns)
        {
            var widths = new List<float>(columns.Count);
            var total = 0f;
            foreach (var column in columns)
            {
                var width = UnitConverter.ToPoints(column.Width, unit);
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

        private static void ApplyWidgetBorder(iText.Forms.Fields.PdfFormField field, PdfDocument pdf, float borderWidth)
        {
            if (borderWidth <= 0) return;
            var widget = field.GetWidgets()[0];
            var formAnnotation = PdfFormAnnotation.MakeFormAnnotation(widget.GetPdfObject(), pdf);
            formAnnotation.SetBorderWidth(borderWidth);
            formAnnotation.SetBorderColor(ColorConstants.BLACK);
        }

        private static void SetWidgetVisibility(iText.Kernel.Pdf.PdfDictionary widgetObj, VisibilitySpec vis)
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

        private const float DefaultFieldFontSize = 10f;
        private const float DefaultBodyFontSize = 9f;

        private static int MapVisibilityFlag(AnnotationVisibility v)
        {
            // AnnotationVisibility now matches PDF bits; cast directly to int to support combined flags
            return (int)v;
        }

        /// <summary>
        /// Ensure there is vertical space on the current page for an element at <paramref name="y"/> with the given <paramref name="height"/>.
        /// If not, add a new page and update <paramref name="y"/> to the flowTop-based position.
        /// </summary>
        private static void EnsurePageHasSpace(PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, LayoutContext layoutContext, ref float y, float height, float bottomLimit, float flowTop)
        {
            if (y < bottomLimit)
            {
                page = pdf.AddNewPage();
                canvas = new PdfCanvas(page);
                layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
                y = flowTop - height;
            }
        }

        /// <summary>
        /// Add a new page if the provided y is below the bottom limit. Unlike EnsurePageHasSpace this does not modify the provided Y value.
        /// Useful when callers will recompute the element Y after a page break.
        /// </summary>
        private static void EnsureNewPageIfNeeded(PdfDocument pdf, ref PdfPage page, ref PdfCanvas canvas, LayoutContext layoutContext, float y, float bottomLimit)
        {
            if (y < bottomLimit)
            {
                page = pdf.AddNewPage();
                canvas = new PdfCanvas(page);
                layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
            }
        }

        private static PdfFormField CreateTextField(PdfDocument pdf, string name, Rectangle rect, PdfPage page, PdfFont font, float fontSize, string? align, bool multiline, string? value)
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

        private static PdfFormField CreateCheckBoxField(PdfDocument pdf, string name, Rectangle rect, PdfPage page, string? checkType)
        {
            var field = new CheckBoxFormFieldBuilder(pdf, name ?? string.Empty)
                .SetWidgetRectangle(rect)
                .SetPage(page)
                .CreateCheckBox();

            field.SetCheckType(ParseCheckType(checkType));
            return field;
        }

        private static void ClipAndDrawHeaderText(iText.Layout.Canvas canvas, Rectangle cellRect, PdfFont font, string text, float fontSize, TextAlignment alignment, bool wrap)
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

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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

        private static IEnumerable<string> SplitWord(string word, PdfFont font, float fontSize, float maxWidth)
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

        private static Color ResolveBorderColor(string? colorSpec)
        {
            if (string.IsNullOrWhiteSpace(colorSpec))
            {
                return ColorConstants.BLACK;
            }

            var trimmed = colorSpec.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                var hex = trimmed.Substring(1);
                if (hex.Length == 6
                    && int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
                    && int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
                    && int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
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

        private static void ApplyBorderStyle(PdfCanvas canvas, string? borderStyle)
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

        private static float GetFlowTop(float pageHeight, float marginTop)
        {
            return pageHeight - marginTop;
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

        private readonly record struct LayoutMetrics(float PageWidth, float PageHeight, float MarginLeft, float MarginRight, float MarginTop, float MarginBottom);
        private readonly record struct ColumnWidthResult(IReadOnlyList<float> Widths, float Scale);

        private static float ResolveY(string? below, float gap, float ySpec, float heightPoints, string unit, bool originTopLeft, float pageHeight, float offsetY, LayoutContext layoutContext)
        {
            var gapPoints = UnitConverter.ToPoints(gap, unit);
            if (layoutContext.TryGetAnchor(below, out var anchor))
            {
                return anchor.GetY() - gapPoints - heightPoints;
            }

            var yPoints = UnitConverter.ToPoints(ySpec, unit);
            var yPdf = originTopLeft ? pageHeight - yPoints - heightPoints : yPoints;
            return yPdf + offsetY;
        }

        private static float ResolveTopY(string? below, float gap, float ySpec, string unit, bool originTopLeft, float pageHeight, float offsetY, LayoutContext layoutContext)
        {
            var gapPoints = UnitConverter.ToPoints(gap, unit);
            if (layoutContext.TryGetAnchor(below, out var anchor))
            {
                return anchor.GetY() - gapPoints;
            }

            var yPoints = UnitConverter.ToPoints(ySpec, unit);
            var yPdf = originTopLeft ? pageHeight - yPoints : yPoints;
            return yPdf + offsetY;
        }

        /// <summary>
        /// Resolve a <see cref="LengthSpec"/> (number or 'auto') into points.
        /// If <c>length</c> is <c>auto</c> this returns the available horizontal space from <c>left</c> to the container right limit (page/subform) using <paramref name="metrics"/>.
        /// Returns 0 when the resolved length is non-positive.
        /// </summary>
        private static float ResolveLengthToPoints(LengthSpec length, float left, LayoutMetrics metrics, string unit)
        {
            if (length.IsAuto)
            {
                var rightLimit = metrics.PageWidth - metrics.MarginRight;
                return Math.Max(0, rightLimit - left);
            }

            return length.Value > 0 ? UnitConverter.ToPoints(length.Value, unit) : 0f;
        }

        private sealed class LayoutContext
        {
            private readonly Dictionary<string, Rectangle> _named = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
            private Rectangle? _last;
            private readonly LayoutContext? _parent;
            private readonly Dictionary<int, Rectangle> _pageBounds = new Dictionary<int, Rectangle>();
            private int _currentPageNumber = 1;

            public LayoutContext(LayoutContext? parent = null)
            {
                _parent = parent;
            }

            public LayoutContext CreateChild() => new LayoutContext(this);

            public void SetCurrentPage(int pageNumber)
            {
                _currentPageNumber = Math.Max(1, pageNumber);
            }

            public void Register(string? name, Rectangle rect)
            {
                _last = rect;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _named[name] = rect;
                }

                if (_pageBounds.TryGetValue(_currentPageNumber, out var bounds))
                {
                    _pageBounds[_currentPageNumber] = Union(bounds, rect);
                }
                else
                {
                    _pageBounds[_currentPageNumber] = rect;
                }
            }

            public IReadOnlyDictionary<int, Rectangle> GetPageBounds() => _pageBounds;

            public bool TryGetLast(out Rectangle rect)
            {
                if (_last != null)
                {
                    rect = _last;
                    return true;
                }

                rect = new Rectangle(0, 0, 0, 0);
                return false;
            }

            public bool TryGetAnchor(string? name, out Rectangle rect)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    rect = new Rectangle(0, 0, 0, 0);
                    return false;
                }

                if (string.Equals(name, "$prev", StringComparison.OrdinalIgnoreCase))
                {
                    if (_last != null)
                    {
                        rect = _last;
                        return true;
                    }
                }

                if (_named.TryGetValue(name, out var found) && found != null)
                {
                    rect = found;
                    return true;
                }

                if (_parent != null)
                {
                    return _parent.TryGetAnchor(name, out rect);
                }

                rect = new Rectangle(0, 0, 0, 0);
                return false;
            }

            private static Rectangle Union(Rectangle a, Rectangle b)
            {
                var minX = Math.Min(a.GetX(), b.GetX());
                var minY = Math.Min(a.GetY(), b.GetY());
                var maxX = Math.Max(a.GetX() + a.GetWidth(), b.GetX() + b.GetWidth());
                var maxY = Math.Max(a.GetY() + a.GetHeight(), b.GetY() + b.GetHeight());
                return new Rectangle(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
            }
        }

        private static CheckBoxType ParseCheckType(string? value)
        {
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "check":
                case "checkmark":
                    return CheckBoxType.CHECK;
                case "circle":
                    return CheckBoxType.CIRCLE;
                case "diamond":
                    return CheckBoxType.DIAMOND;
                case "square":
                    return CheckBoxType.SQUARE;
                case "star":
                    return CheckBoxType.STAR;
                default:
                    return CheckBoxType.CROSS;
            }
        }
    }

}
