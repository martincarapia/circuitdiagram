using System;
using System.Collections.Generic;
using System.Linq;
using CircuitDiagram.Drawing;
using CircuitDiagram.Drawing.Text;
using CircuitDiagram.Primitives;
using CircuitDiagram.Render.Path;

namespace CircuitDiagram.Render
{
    public class BoundsDrawingContext : IDrawingContext
    {
        private Rect _bounds;
        private bool _hasBounds;

        public Rect Bounds => _hasBounds ? _bounds : new Rect(0, 0, 0, 0);

        public void DrawLine(Point start, Point end, double thickness)
        {
            ExpandBounds(start);
            ExpandBounds(end);
        }

        public void DrawRectangle(Point start, Size size, double thickness, bool fill = false)
        {
            ExpandBounds(new Rect(start, size));
        }

        public void DrawEllipse(Point centre, double radiusX, double radiusY, double thickness, bool fill = false)
        {
            ExpandBounds(new Rect(centre.X - radiusX, centre.Y - radiusY, radiusX * 2, radiusY * 2));
        }

        public void DrawPath(Point start, IList<IPathCommand> commands, double thickness, bool fill = false)
        {
            ExpandBounds(start);
            foreach (var command in commands)
            {
                ExpandBounds(Point.Add(start, command.End));
            }
        }

        public void DrawText(Point anchor, TextAlignment alignment, double rotation, IList<TextRun> textRuns)
        {
            // Rough estimation of text bounds
            double totalWidth = 0;
            double maxHeight = 0;
            
            foreach (var run in textRuns)
            {
                // Assume average char width is 0.6 * fontSize
                double charWidth = run.Formatting.Size * 0.6;
                totalWidth += run.Text.Length * charWidth;
                maxHeight = Math.Max(maxHeight, run.Formatting.Size);
            }

            double x = anchor.X;
            double y = anchor.Y;

            // Adjust x based on alignment
            if (alignment == TextAlignment.TopCentre || alignment == TextAlignment.CentreCentre || alignment == TextAlignment.BottomCentre)
            {
                x -= totalWidth / 2;
            }
            else if (alignment == TextAlignment.TopRight || alignment == TextAlignment.CentreRight || alignment == TextAlignment.BottomRight)
            {
                x -= totalWidth;
            }

            // Adjust y based on alignment
            if (alignment == TextAlignment.CentreLeft || alignment == TextAlignment.CentreCentre || alignment == TextAlignment.CentreRight)
            {
                y -= maxHeight / 2;
            }
            else if (alignment == TextAlignment.BottomLeft || alignment == TextAlignment.BottomCentre || alignment == TextAlignment.BottomRight)
            {
                y -= maxHeight;
            }

            // If rotated, this simple box is not enough, but better than nothing.
            // For now, we ignore rotation for the bounding box calculation to keep it simple.
            
            ExpandBounds(new Rect(x, y, totalWidth, maxHeight));
        }

        private void ExpandBounds(Point point)
        {
            ExpandBounds(new Rect(point, new Size(0, 0)));
        }

        private void ExpandBounds(Rect rect)
        {
            if (!_hasBounds)
            {
                _bounds = rect;
                _hasBounds = true;
            }
            else
            {
                _bounds = _bounds.Union(rect);
            }
        }

        public void Dispose()
        {
        }
    }
}
