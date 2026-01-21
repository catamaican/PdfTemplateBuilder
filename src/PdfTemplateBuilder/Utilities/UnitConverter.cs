using System;
using iText.Kernel.Geom;

namespace PdfTemplateBuilder.Utilities
{
	public static class UnitConverter
	{
		public static float ToPoints(float value, string unit)
		{
			return unit.ToLowerInvariant() switch
			{
				"mm" => (float)(value * 72.0 / 25.4),
				"cm" => (float)(value * 10.0 * 72.0 / 25.4),
				"pt" => value,
				_ => value,
			};
		}

		public static float ToPointsY(float value, string unit, float pageHeight, float elementHeight, bool originTopLeft)
		{
			var y = ToPoints(value, unit);
			var h = ToPoints(elementHeight, unit);
			return originTopLeft ? pageHeight - y - h : y;
		}

		public static PageSize ResolvePageSize(string size)
		{
			return size.Trim().ToUpperInvariant() switch
			{
				"A4" => PageSize.A4,
				"LETTER" => PageSize.LETTER,
				"A3" => PageSize.A3,
				"A5" => PageSize.A5,
				_ => PageSize.A4,
			};
		}
	}
}
