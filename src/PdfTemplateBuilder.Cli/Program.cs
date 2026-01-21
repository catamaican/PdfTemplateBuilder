using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PdfTemplateBuilder;
using PdfTemplateBuilder.Models;

namespace PdfTemplateBuilder.Cli
{
	internal static class Program
	{
		private static void Main()
		{
			var baseDir = AppContext.BaseDirectory;
			var cwd = Directory.GetCurrentDirectory();

			var specPath = Path.Combine(cwd, "template-spec.json");
			if (!File.Exists(specPath))
			{
				specPath = Path.Combine(cwd, "src", "template-spec.json");
			}
			if (!File.Exists(specPath))
			{
				specPath = Path.Combine(cwd, "src", "PdfTemplateBuilder", "template-spec.json");
			}
			if (!File.Exists(specPath))
			{
				specPath = Path.Combine(baseDir, "template-spec.json");
			}

			Console.WriteLine($"Using template spec: {specPath}");

			var outputFileName = $"document-template-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
			var outputPath = Path.Combine(baseDir, outputFileName);

			if (!File.Exists(specPath))
			{
				Console.WriteLine($"Missing template spec: {specPath}");
				return;
			}

			var specJson = File.ReadAllText(specPath);
			var spec = JsonSerializer.Deserialize<TemplateSpec>(specJson, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			if (spec == null)
			{
				Console.WriteLine("Invalid template spec.");
				return;
			}

			PdfTemplateRenderer.Build(outputPath, spec);
			Console.WriteLine($"Generated: {outputPath}");
		}
	}
}
