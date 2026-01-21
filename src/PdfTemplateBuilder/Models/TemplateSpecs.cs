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
        // When false the field is still added to the AcroForm but its widget annotation is hidden
        public bool Visible { get; set; } = true;
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
        // If false the checkbox widget is added but hidden in the PDF viewer
        public bool Visible { get; set; } = true;
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
        // When false the entire table is not rendered (but anchors remain absent)
        public bool Visible { get; set; } = true;
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
        // Column-level visibility (default: visible)
        public bool Visible { get; set; } = true;
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
}
