using Xunit;
using iText.Kernel.Geom;
using PdfTemplateBuilder.Utilities;

namespace PdfTemplateBuilder.Tests
{
	public class PageSizeTests
	{
		[Fact]
		public void ResolvePageSize_WithExplicitMmMatchesA4()
		{
			// A4 in mm is 210 x 297
			var custom = UnitConverter.ResolvePageSize(210f, 297f, "mm");
			Assert.Equal(PageSize.A4.GetWidth(), custom.GetWidth(), 0.5f);
			Assert.Equal(PageSize.A4.GetHeight(), custom.GetHeight(), 0.5f);
		}

		[Fact]
		public void ResolvePageSize_WithPointsMatchesA4()
		{
			// Use points directly to match A4 exactly
			var custom = UnitConverter.ResolvePageSize(PageSize.A4.GetWidth(), PageSize.A4.GetHeight(), "pt");
			Assert.Equal(PageSize.A4.GetWidth(), custom.GetWidth(), 0.01f);
			Assert.Equal(PageSize.A4.GetHeight(), custom.GetHeight(), 0.01f);
		}
	}
}