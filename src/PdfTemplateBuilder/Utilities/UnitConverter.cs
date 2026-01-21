using System;
using iText.Kernel.Geom;

namespace PdfTemplateBuilder
{
    public static class UnitConverter
    {
        public static float ToPoints(float value, string unit)
        {
            switch (unit.ToLowerInvariant())
            {
                case "mm":
                    return (float)(value * 72.0 / 25.4);
                case "cm":
                    return (float)(value * 10.0 * 72.0 / 25.4);
                case "pt":
                    return value;
                default:
                    return value;
            }
        }

        public static float ToPointsY(float value, string unit, float pageHeight, float elementHeight, bool originTopLeft)
        {
            var y = ToPoints(value, unit);
            var h = ToPoints(elementHeight, unit);
            return originTopLeft ? pageHeight - y - h : y;
        }

        public static PageSize ResolvePageSize(string size)
        {
            switch (size.Trim().ToUpperInvariant())
            {
                case "A4":
                    return PageSize.A4;
                case "LETTER":
                    return PageSize.LETTER;
                case "A3":
                    return PageSize.A3;
                case "A5":
                    return PageSize.A5;
                default:
                    return PageSize.A4;
            }
        }
    }
}
