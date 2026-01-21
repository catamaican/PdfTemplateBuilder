using System.Text.Json;
using Xunit;

namespace PdfTemplateBuilder.Tests
{
	public class FlexibleVisibilityConverterTests
	{
		[Theory]
		[InlineData("true", true, "Visible")]
		[InlineData("false", false, "Hidden")]
		[InlineData("\"Hidden\"", null, "Hidden")]
		[InlineData("\"Hidden|NoView\"", null, "Hidden, NoView")]
		[InlineData("[\"Hidden\",\"NoView\"]", null, "Hidden, NoView")]
		public void ReadVisibilityVariants(string jsonValue, bool? expectedRender, string expectedFlagToString)
		{
			// Deserialize the visibility value directly to exercise the converter in isolation
			var vis = JsonSerializer.Deserialize<PdfTemplateBuilder.Models.VisibilitySpec>(jsonValue);
			Assert.NotNull(vis);
			Assert.Equal(expectedRender, vis.Render);
			Assert.Equal(expectedFlagToString, vis.Flag.ToString());
		}

		[Fact]
		public void DefaultVisibleWhenNull()
		{
			// When the top-level token is 'null' the deserializer returns null; properties default to DefaultVisible when omitted
			var vis = JsonSerializer.Deserialize<PdfTemplateBuilder.Models.VisibilitySpec>("null");
			Assert.Null(vis);
		}
	}
}
