using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfTemplateBuilder
{
    public class TemplateSpec
    {
        public string? Origin { get; set; }
        public string? Unit { get; set; }
        public PageSpec? Page { get; set; }
        public FontSpec? Fonts { get; set; }
        public List<StaticTextSpec>? StaticTexts { get; set; }
        public List<FieldSpec>? Fields { get; set; }
        public List<CheckboxSpec>? Checkboxes { get; set; }
        public List<SignatureSpec>? Signatures { get; set; }
        public List<TableSpec>? Tables { get; set; }
        public List<SubformSpec>? Subforms { get; set; }
    }

    public sealed class FontSpec
    {
        public string? Regular { get; set; }
        public string? Bold { get; set; }
    }

    public sealed class PageSpec
    {
        public string? Size { get; set; }
        public MarginSpec? Margins { get; set; }
    }

    public sealed class MarginSpec
    {
        public float Left { get; set; }
        public float Right { get; set; }
        public float Top { get; set; }
        public float Bottom { get; set; }
    }

    [JsonConverter(typeof(FlexibleLengthConverter))]
    public readonly struct LengthSpec
    {
        public bool IsAuto { get; init; }
        public float Value { get; init; }

        public static LengthSpec Auto() => new LengthSpec { IsAuto = true, Value = 0f };
        public static LengthSpec FromValue(float value) => new LengthSpec { IsAuto = false, Value = value };
    }

    public sealed class StaticTextSpec
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        [JsonConverter(typeof(FlexibleLengthConverter))]
        public LengthSpec Width { get; set; }
        public float FontSize { get; set; }
        public bool Bold { get; set; }
        public string? Below { get; set; }
        public float Gap { get; set; }
    }

    public sealed class FieldSpec
    {
        public string? Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        [JsonConverter(typeof(FlexibleLengthConverter))]
        public LengthSpec Width { get; set; }
        public float Height { get; set; }
        public float FontSize { get; set; }
        public float BorderWidth { get; set; }
        public string? Align { get; set; }
        public bool Multiline { get; set; }
        public string? Below { get; set; }
        public float Gap { get; set; }
        public string? DataType { get; set; }
        public string? Format { get; set; }
        public string? Value { get; set; }
        public bool SampleValue { get; set; }
        // Controls rendering/annotation visibility. Accepts: true|false or "Invisible"|"Hidden"|"NoView"
        [JsonConverter(typeof(FlexibleVisibilityConverter))]
        public VisibilitySpec Visible { get; set; } = VisibilitySpec.DefaultVisible();
    }

    public sealed class CheckboxSpec
    {
        public string? Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Size { get; set; }
        public float BorderWidth { get; set; }
        public bool Checked { get; set; }
        public string? CheckType { get; set; }
        public string? Below { get; set; }
        public float Gap { get; set; }
        // Controls rendering/annotation visibility. Accepts: true|false or "Invisible"|"Hidden"|"NoView"
        [JsonConverter(typeof(FlexibleVisibilityConverter))]
        public VisibilitySpec Visible { get; set; } = VisibilitySpec.DefaultVisible();
    }

    public sealed class SignatureSpec
    {
        public string? Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float BorderWidth { get; set; }
        public string? Below { get; set; }
        public float Gap { get; set; }
    }

    public sealed class TableSpec
    {
        public string? Name { get; set; }
        public float X { get; set; }
        public float YStart { get; set; }
        public float RowHeight { get; set; }
        public float HeaderHeight { get; set; }
        public float BottomLimit { get; set; }
        public float HeaderFontSize { get; set; }
        public float BodyFontSize { get; set; }
        public int SampleRowCount { get; set; }
        public string? RowNamePrefix { get; set; }
        public string? Below { get; set; }
        public float Gap { get; set; }
        public bool FitWidth { get; set; } = true;
        public bool FitToSpace { get; set; }
        [JsonConverter(typeof(FlexibleBoolConverter))]
        public bool? HeaderWrap { get; set; }
        public string? HeaderAlign { get; set; }
        [JsonConverter(typeof(FlexibleBoolConverter))]
        public bool? HeaderAutoFit { get; set; }
        public List<TableColumnSpec> Columns { get; set; } = new List<TableColumnSpec>();
        // Controls rendering/annotation visibility on the table level.
        // - boolean false => table is not rendered (backwards compatible)
        // - string value ("Hidden"/"Invisible"/"NoView") => table is rendered but its cells' widgets will have the given annotation flag
        [JsonConverter(typeof(FlexibleVisibilityConverter))]
        public VisibilitySpec Visible { get; set; } = VisibilitySpec.DefaultVisible();
        // When false the table header is still drawn but no data rows are generated
        public bool RowsVisible { get; set; } = true;
    }

    public sealed class TableColumnSpec
    {
        public string? Name { get; set; }
        public string? Header { get; set; }
        public float Width { get; set; }
        public string? Align { get; set; }
        [JsonConverter(typeof(FlexibleBoolConverter))]
        public bool? HeaderWrap { get; set; }
        public string? HeaderAlign { get; set; }
        [JsonConverter(typeof(FlexibleBoolConverter))]
        public bool? HeaderAutoFit { get; set; }
        // Column-level visibility. Accepts: true|false or "Invisible"|"Hidden"|"NoView".
        // - boolean false => column omitted from layout (backwards compatible)
        // - string flag => column header & cell boxes are rendered but the widgets will be flagged
        [JsonConverter(typeof(FlexibleVisibilityConverter))]
        public VisibilitySpec Visible { get; set; } = VisibilitySpec.DefaultVisible();
    }

    public sealed class FlexibleBoolConverter : JsonConverter<bool?>
    {
        public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True)
            {
                return true;
            }

            if (reader.TokenType == JsonTokenType.False)
            {
                return false;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (bool.TryParse(value, out var parsed))
                {
                    return parsed;
                }
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            throw new JsonException("Invalid boolean value.");
        }

        public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteBooleanValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    public sealed class SubformSpec : TemplateSpec
    {
        public string? Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string? Below { get; set; }
        public float Gap { get; set; }
        public float BorderWidth { get; set; }
        public string? BorderColor { get; set; }
        public string? BorderStyle { get; set; }
    }

    public sealed class FlexibleLengthConverter : JsonConverter<LengthSpec>
    {
        public override LengthSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetSingle(out var v))
                {
                    return LengthSpec.FromValue(v);
                }

                return LengthSpec.FromValue((float)reader.GetDouble());
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.Equals(s, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    return LengthSpec.Auto();
                }

                if (float.TryParse(s, out var parsed))
                {
                    return LengthSpec.FromValue(parsed);
                }
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                return LengthSpec.FromValue(0f);
            }

            throw new JsonException("Invalid length value. Expected number or 'auto'.");
        }

        public override void Write(Utf8JsonWriter writer, LengthSpec value, JsonSerializerOptions options)
        {
            if (value.IsAuto)
            {
                writer.WriteStringValue("auto");
            }
            else
            {
                writer.WriteNumberValue(value.Value);
            }
        }
    }

    // Visibility controls used to define widget annotation behavior and optional rendering
    [System.Flags]
    public enum AnnotationVisibility
    {
        Visible = 0,
        Invisible = 1, // PDF annotation flag bit 1
        Hidden = 2,    // PDF annotation flag bit 2
        NoView = 32    // PDF annotation flag bit 32
    }

    public sealed class VisibilitySpec
    {
        // If set, controls whether the element is rendered (table/column level). If null, rendering is determined by caller defaults.
        public bool? Render { get; init; }

        // Which annotation flag(s) to apply to the widget. When set to Visible (0) no annotation flag is applied.
        public AnnotationVisibility Flag { get; init; } = AnnotationVisibility.Visible;

        public static VisibilitySpec DefaultVisible() => new VisibilitySpec { Render = null, Flag = AnnotationVisibility.Visible };

        public bool ShouldOmitLayout() => Render.HasValue && !Render.Value;

        public bool ShouldHideWidget() => Flag != AnnotationVisibility.Visible || (Render.HasValue && !Render.Value);
    }

    public sealed class FlexibleVisibilityConverter : JsonConverter<VisibilitySpec>
    {
        public override VisibilitySpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True)
            {
                return new VisibilitySpec { Render = true, Flag = AnnotationVisibility.Visible };
            }

            if (reader.TokenType == JsonTokenType.False)
            {
                // boolean false historically meant 'not visible' — interpret as Render=false and also default to Hidden flag
                return new VisibilitySpec { Render = false, Flag = AnnotationVisibility.Hidden };
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return VisibilitySpec.DefaultVisible();

                // support combined flags like "Hidden|NoView" or "Hidden,NoView"
                var parts = s.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
                AnnotationVisibility combined = AnnotationVisibility.Visible;
                foreach (var p in parts)
                {
                    if (Enum.TryParse<AnnotationVisibility>(p.Trim(), true, out var parsed))
                    {
                        combined |= parsed;
                    }
                    else
                    {
                        throw new JsonException($"Invalid visibility token '{p}' in '{s}'. Expected one of: Visible, Invisible, Hidden, NoView.");
                    }
                }

                return new VisibilitySpec { Render = null, Flag = combined };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var combined = AnnotationVisibility.Visible;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray) break;
                    if (reader.TokenType != JsonTokenType.String) throw new JsonException("Visibility array items must be strings.");
                    var token = reader.GetString();
                    if (!Enum.TryParse<AnnotationVisibility>(token?.Trim() ?? string.Empty, true, out var parsed))
                    {
                        throw new JsonException($"Invalid visibility token '{token}' in array.");
                    }
                    combined |= parsed;
                }

                return new VisibilitySpec { Render = null, Flag = combined };
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                return VisibilitySpec.DefaultVisible();
            }

            throw new JsonException("Invalid visibility value. Expected true/false, string or array of strings (Visible, Invisible, Hidden, NoView).");
        }

        public override void Write(Utf8JsonWriter writer, VisibilitySpec value, JsonSerializerOptions options)
        {
            if (value.Render.HasValue)
            {
                writer.WriteBooleanValue(value.Render.Value);
                return;
            }

            // Write combined flags as a '|' separated string (e.g. "Hidden|NoView") or "Visible" for none
            var nm = value.Flag == AnnotationVisibility.Visible ? "Visible" : value.Flag.ToString();
            writer.WriteStringValue(nm);
        }
    }
}
