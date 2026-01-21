using System;
using System.Collections.Generic;
using iText.Kernel.Geom;

namespace PdfTemplateBuilder.Layout
{
	internal readonly record struct LayoutMetrics(float PageWidth, float PageHeight, float MarginLeft, float MarginRight, float MarginTop, float MarginBottom);
	internal readonly record struct ColumnWidthResult(IReadOnlyList<float> Widths, float Scale);

	internal static class LayoutHelpers
	{
		public static float ResolveY(string? below, float gap, float ySpec, float heightPoints, string unit, bool originTopLeft, float pageHeight, float offsetY, LayoutContext layoutContext)
		{
			var gapPoints = Utilities.UnitConverter.ToPoints(gap, unit);
			if (layoutContext.TryGetAnchor(below, out var anchor))
			{
				return anchor.GetY() - gapPoints - heightPoints;
			}

			var yPoints = Utilities.UnitConverter.ToPoints(ySpec, unit);
			var yPdf = originTopLeft ? pageHeight - yPoints - heightPoints : yPoints;
			return yPdf + offsetY;
		}

		public static float ResolveTopY(string? below, float gap, float ySpec, string unit, bool originTopLeft, float pageHeight, float offsetY, LayoutContext layoutContext)
		{
			var gapPoints = Utilities.UnitConverter.ToPoints(gap, unit);
			if (layoutContext.TryGetAnchor(below, out var anchor))
			{
				return anchor.GetY() - gapPoints;
			}

			var yPoints = Utilities.UnitConverter.ToPoints(ySpec, unit);
			var yPdf = originTopLeft ? pageHeight - yPoints : yPoints;
			return yPdf + offsetY;
		}

		public static float ResolveLengthToPoints(Models.LengthSpec length, float left, LayoutMetrics metrics, string unit)
		{
			if (length.IsAuto)
			{
				var rightLimit = metrics.PageWidth - metrics.MarginRight;
				return Math.Max(0, rightLimit - left);
			}

			return length.Value > 0 ? Utilities.UnitConverter.ToPoints(length.Value, unit) : 0f;
		}

		public static float GetFlowTop(float pageHeight, float marginTop) => pageHeight - marginTop;

		public static void EnsurePageHasSpace(iText.Kernel.Pdf.PdfDocument pdf, ref iText.Kernel.Pdf.PdfPage page, ref iText.Kernel.Pdf.Canvas.PdfCanvas canvas, LayoutContext layoutContext, ref float y, float height, float bottomLimit, float flowTop)
		{
			if (y < bottomLimit)
			{
				page = pdf.AddNewPage();
				canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
				layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
				y = flowTop - height;
			}
		}

		public static void EnsureNewPageIfNeeded(iText.Kernel.Pdf.PdfDocument pdf, ref iText.Kernel.Pdf.PdfPage page, ref iText.Kernel.Pdf.Canvas.PdfCanvas canvas, LayoutContext layoutContext, float y, float bottomLimit)
		{
			if (y < bottomLimit)
			{
				page = pdf.AddNewPage();
				canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
				layoutContext.SetCurrentPage(pdf.GetPageNumber(page));
			}
		}
	}

	internal sealed class LayoutContext
	{
		private readonly Dictionary<string, Rectangle> _named = new(StringComparer.OrdinalIgnoreCase);
		private Rectangle? _last;
		private readonly LayoutContext? _parent;
		private readonly Dictionary<int, Rectangle> _pageBounds = new();
		private int _currentPageNumber = 1;

		public LayoutContext(LayoutContext? parent = null)
		{
			_parent = parent;
		}

		public LayoutContext CreateChild() => new(this);

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
}
