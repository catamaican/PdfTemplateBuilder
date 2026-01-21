using System.IO;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using PdfTemplateBuilder.Models;

namespace PdfTemplateBuilder.Utilities
{
	public sealed class FontBundle(PdfFont regular, PdfFont? bold)
	{
		public PdfFont Regular { get; } = regular;
		public PdfFont? Bold { get; } = bold;
	}

	public static class FontResolver
	{
		private const string DefaultRegular = "C:\\Windows\\Fonts\\segoeui.ttf";
		private const string DefaultBold = "C:\\Windows\\Fonts\\segoeuib.ttf";

		public static FontBundle Resolve(FontSpec spec)
		{
			var regular = LoadFontOrFallback(spec.Regular, DefaultRegular, StandardFonts.HELVETICA);
			var bold = LoadFontOrFallback(spec.Bold, DefaultBold, StandardFonts.HELVETICA_BOLD);
			return new FontBundle(regular, bold);
		}

		private static PdfFont LoadFontOrFallback(string? fontPath, string fallbackPath, string standardFont)
		{
			var pathToUse = string.IsNullOrWhiteSpace(fontPath) ? fallbackPath : fontPath;
			if (File.Exists(pathToUse))
			{
				return PdfFontFactory.CreateFont(pathToUse, iText.IO.Font.PdfEncodings.IDENTITY_H);
			}

			if (File.Exists(fallbackPath))
			{
				return PdfFontFactory.CreateFont(fallbackPath, iText.IO.Font.PdfEncodings.IDENTITY_H);
			}

			return PdfFontFactory.CreateFont(standardFont);
		}
	}
}
