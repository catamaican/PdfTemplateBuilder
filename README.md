# PDF Template Builder

Generates an AcroForm PDF template from a JSON spec using iText 9.5.

## Requirements
- .NET 9 SDK

## Run
From the workspace root:

- Build/run (project):
  - `dotnet run --project src/PdfTemplateBuilder/PdfTemplateBuilder.csproj`

- Build/run (solution):
  - Build solution: `dotnet build PdfTemplateBuilder.sln`
  - Run CLI: `dotnet run --project src/PdfTemplateBuilder.Cli/PdfTemplateBuilder.Cli.csproj`
  - Or use the convenience scripts below: `./build.ps1` / `./build.sh` and `./run-cli.ps1` / `./run-cli.sh`

Publishing / CI
- This repository includes a GitHub Actions workflow at `.github/workflows/ci.yml` that builds the solution on push and pull requests and produces a NuGet package artifact.
- To create a NuGet package locally: `dotnet pack src/PdfTemplateBuilder/PdfTemplateBuilder.csproj -c Release -o ./artifacts`
Library API additions:
- `PdfTemplateRenderer.Build(Stream outputStream, TemplateSpec spec)` — write PDF to a provided stream.
- `PdfTemplateRenderer.BuildToBytes(TemplateSpec spec)` — returns PDF bytes.
- `PdfTemplateRenderer.BuildToStream(TemplateSpec spec)` — returns a seekable `Stream` positioned at 0 (caller disposes).

Examples:
- Write to file (existing behavior):
  - `PdfTemplateRenderer.Build("out.pdf", spec, rows);`
- Get bytes (e.g., for HTTP response):
  - `var pdfBytes = PdfTemplateRenderer.BuildToBytes(spec, rows);`
- Get a stream (e.g., return from ASP.NET action):
  - `using var stream = PdfTemplateRenderer.BuildToStream(spec, rows);`
The output PDF is written to:
- `src/bin/Debug/net9.0/document-template-YYYYMMDD-HHMMSS.pdf`

The generator resolves the template spec in this order:
1. `./template-spec.json`
2. `./src/PdfTemplateBuilder/template-spec.json`
3. `./src/PdfTemplateBuilder/bin/Debug/net9.0/template-spec.json`

## JSON schema overview
Top-level:
- `unit`: `"mm" | "cm" | "pt"` (default `mm`)
- `origin`: `"top-left" | "bottom-left"` (default `top-left`)
- `page`: page size and margins
- `fonts`: font paths for Unicode rendering
- `staticTexts[]`, `fields[]`, `checkboxes[]`, `signatures[]`, `tables[]`, `subforms[]`

### Page
```
{
  "page": {
    "size": "A4",
    "margins": { "left": 10, "right": 10, "top": 10, "bottom": 10 }
  }
}
```

### Fonts (Unicode)
```
{
  "fonts": {
    "regular": "C:/Windows/Fonts/segoeui.ttf",
    "bold": "C:/Windows/Fonts/segoeuib.ttf"
  }
}
```

### Common positioning
- `x`, `y`: absolute coordinates in `unit`
- `below`: name of an element to place below (or `"$prev"` for previous element)
- `gap`: vertical gap in `unit` applied when `below` is used

If `below` is provided, `y` is ignored. `gap` is only considered when the referenced anchor can be resolved (i.e., the named element exists in the layout); it is converted to points (using `unit`) and used as the vertical spacing between the bottom of the anchor and the top of the current element.

Where `gap` is valid:
- `staticTexts[]` (e.g., put text below another element)
- `fields[]` (text fields)
- `checkboxes[]`
- `signatures[]`
- `tables[]` (table's `below` / `gap` controls top-of-table placement)
- `subforms[]` (subform positioning when using `below`)

Behavior notes:
- `gap` is measured in the configured `unit` (default `mm`).
- For top-based layouts (`origin: "top-left"`), the resolved Y is calculated as: `anchorBottomY - gap - elementHeight` for elements that need the element's top to be placed below an anchor (this ensures the specified gap is the space between the anchor and the element).
- If a referenced anchor is on a previous page, `gap` is still used, but the element may be moved to the next page according to normal flow and margin rules.

Example:
- If anchor bottom is at 50mm from the page bottom, `gap: 5` (mm), and element height is 10mm, the element's Y will be placed so there is 5mm between the anchor and the element and the element occupies 10mm below that gap.

Examples:
- Simple `below` + `gap`:
```
{ "name": "label", "x": 15, "y": 100, "height": 10 }
{ "name": "value", "below": "label", "gap": 5, "x": 15, "height": 8 }
```

- Using `"$prev"` to anchor to the previous element:
```
{ "name": "first", "x": 15, "y": 200, "height": 10 }
{ "name": "second", "below": "$prev", "gap": 4, "x": 15, "height": 10 }
```

- Anchor on previous page (note): If the anchor resolves to an element on a previous page, `gap` is still applied but the dependent element may be moved to the next page and positioned at the page `flowTop` (page top minus top margin).

### Static text
```
{
  "name": "title",
  "value": "DOCUMENT DE FUNDAMENTARE",
  "x": 70,
  "y": 35,
  "width": "auto",
  "fontSize": 11,
  "bold": true
}
```

- `width`: number (units) or `'auto'` to fill remaining horizontal space within the container.

Notes:
- When `width` is `'auto'`, the element (field or static text) expands to the available horizontal space inside its container (page or subform), respecting the element's `x`, the container width, and the right margin.
- For `staticTexts`, `'auto'` enables simple wrapping within the computed width.

### Text field
```
{
  "name": "institutie_publica",
  "x": 50,
  "y": 17,
  "width": "auto",
  "height": 7,
  "fontSize": 9,
  "borderWidth": 0.5,
  "align": "left",
  "multiline": false,
  "dataType": "date|decimal|string",
  "format": "yyyy-MM-dd|0.00",
  "value": "",
  "sampleValue": false
}
```

- `width`: number (units) or `'auto'` to fill remaining horizontal space within the container.

Notes:
- When `width` is `'auto'`, the element (field or static text) expands to the available horizontal space inside its container (page or subform), respecting the element's `x`, the container width, and the right margin.
- For text fields, `'auto'` will cause the field widget to be created using the computed width.

### Checkbox
```
{
  "name": "angajament_legal",
  "x": 150,
  "y": 50,
  "size": 4,
  "borderWidth": 0.5,
  "checked": false,
  "checkType": "check"
}
```

### Signature field
```
{
  "name": "Sig_S1",
  "x": 15,
  "y": 260,
  "width": 70,
  "height": 12,
  "borderWidth": 0.5
}
```

### Table
```
{
  "name": "valoare_angajamente",
  "x": 15,
  "yStart": 175,
  "rowHeight": 7,
  "headerHeight": 8,
  "bottomLimit": 20,
  "headerFontSize": 8,
  "bodyFontSize": 8,
  "sampleRowCount": 5,
  "rowNamePrefix": "row_",
  "fitWidth": true,
  "fitToSpace": false,
  "headerWrap": true,
  "headerAlign": "center",
  "headerAutoFit": false,
  "columns": [
    { "name": "element", "header": "Element", "width": 45, "align": "left" },
    { "name": "program", "header": "Program", "width": 25, "align": "center", "headerWrap": true, "headerAlign": "center", "headerAutoFit": false }
  ]
}
```

Notes:
- `headerWrap`/`headerAlign`/`headerAutoFit` can be set at table level or per column to override.
- `headerAutoFit: false` keeps header font size fixed even when columns are fit.
- `headerWrap` accepts boolean or string values (e.g. `true` or `"true"`).

Field naming for table cells:
- `tableName_rowName_columnName`

### Subforms
Subforms are positioned containers that can include all element types. They also support flow-style placement with `below` and optional enclosure borders.

```
{
  "name": "main_form",
  "x": 0,
  "y": 0,
  "width": 210,
  "height": 297,
  "staticTexts": [ ... ],
  "fields": [ ... ],
  "checkboxes": [ ... ],
  "signatures": [ ... ],
  "tables": [ ... ],
  "subforms": [ ... ]
}
```

Subform placement and border options:
```
{
  "name": "subform2",
  "x": 0,
  "y": 0,
  "width": 210,
  "height": 200,
  "below": "main_form",
  "gap": 6,
  "borderWidth": 0.6,
  "borderColor": "#6E6E6E",
  "borderStyle": "dashed"
}
```

Notes:
- `below` can reference another element (or `"$prev"`) and offsets the subform using `gap`.
- When `below` is used, the subform's top is computed from the last element of the referenced container.
- If there isn't enough space, the subform flows to the next page.
- If `width` or `height` is omitted or set to `0`, the subform size expands to the available page area within margins.
- `borderStyle` supports `solid` (default), `dashed`, `dotted`.
- `borderColor` accepts named colors (`red`, `green`, `blue`, `gray`, `lightgray`) or hex like `#RRGGBB`.

## Layout behavior & fixes ✅

- **Margins**: Page margins define content *flow* limits (left/right/top/bottom). Element coordinates (`x`, `y`) are absolute distances from the page edge in the configured `unit` (e.g., `mm`). Margins are used for flow and page-break calculations and are no longer applied as an extra offset to every explicit `y` coordinate on the first page.

- **`y` semantics**: A numeric `y` value is measured from the page top when using `origin: "top-left"`. If you specify `y: 0`, the element aligns with the page top; to place content inside the page safe area, use `page.margins.top` for flow and page-break control rather than offsetting every `y` value.

- **Subforms**:
  - Subforms without an explicit `y` (or with `y <= 0`) and without a `below` anchor start at the *flow top* (page height minus top margin).
  - Child elements inside a flow-style subform use their own `y` coordinates relative to the page top; a flow subform does **not** add the top margin again to its children.
  - Subforms without explicit `height` expand to the available page area within margins; if `height` is set and doesn't fit on the current page, the subform is moved to the next page.

- **Tables & page breaks**: Tables compute a `flowTop` (page height minus top margin) and will repeat headers on new pages. `bottomLimit` and `marginBottom` are respected when deciding when to break rows onto the next page.

- **Signatures**: Signatures that share the same `below` anchor are handled as a group and will be moved as a unit to the next page if needed, avoiding multiple page breaks that would separate paired signatures.

- **Fields pagination**: Fields now support page breaks: when a field would be placed below the bottom margin it is moved to the next page and positioned at the flow top.

- **Migration note**: If you adjusted `y` values to compensate for previous behavior (where the top margin was applied as an offset on the first page), please review and update those values. Recommended practice: use `y` measured from the page top and rely on `page.margins` for flow and page-break behavior.

---

## Notes
- Keep the PDF open? Use the timestamped output to avoid file locks.
- Use `origin: "top-left"` for coordinates measured from the top edge.
- `page.margins` shifts all coordinates inward (left/right/top/bottom).
