using CircuitDiagram.Drawing;
using CircuitDiagram.Drawing.Text;
using CircuitDiagram.Primitives;
using CircuitDiagram.Render.Path;
using SkiaSharp;
using System.Text;
using CDPoint = CircuitDiagram.Primitives.Point;
using CDSize = CircuitDiagram.Primitives.Size;
using CDTextAlignment = CircuitDiagram.Drawing.Text.TextAlignment;
using CDSweepDirection = CircuitDiagram.Render.Path.SweepDirection;

namespace CircuitDiagram.UI
{
    public class MauiDrawingContext : IDrawingContext
    {
        private readonly SKCanvas _canvas;
        private readonly float _scale;

        public MauiDrawingContext(SKCanvas canvas, float scale = 1.0f)
        {
            _canvas = canvas;
            _scale = scale;
        }

        public SKColor Color { get; set; } = SKColors.Black;

        public void DrawLine(CDPoint start, CDPoint end, double thickness)
        {
            var paint = new SKPaint
            {
                Color = Color,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)thickness * _scale,
                StrokeCap = SKStrokeCap.Square,
            };

            _canvas.DrawLine(start.ToSkPoint(_scale), end.ToSkPoint(_scale), paint);
        }

        public void DrawRectangle(CDPoint start, CDSize size, double thickness, bool fill = false)
        {
            var paint = new SKPaint
            {
                Color = Color,
                IsAntialias = true,
                Style = fill ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Stroke,
                StrokeWidth = (float)thickness * _scale,
                StrokeCap = SKStrokeCap.Square,
            };

            _canvas.DrawRect((float)start.X * _scale, (float)start.Y * _scale, (float)size.Width * _scale, (float)size.Height * _scale, paint);
        }

        public void DrawEllipse(CDPoint centre, double radiusX, double radiusY, double thickness, bool fill = false)
        {
            var paint = new SKPaint
            {
                Color = Color,
                IsAntialias = true,
                Style = fill ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Stroke,
                StrokeWidth = (float)thickness * _scale,
                StrokeCap = SKStrokeCap.Square,
            };

            _canvas.DrawOval(centre.ToSkPoint(_scale), new SKSize((float)radiusX * _scale, (float)radiusY * _scale), paint);
        }

        public void DrawPath(CDPoint start, IList<IPathCommand> commands, double thickness, bool fill = false)
        {
            var s = start.ToSkPoint(_scale);

            var path = new SKPath();
            path.MoveTo(s);
            foreach (var c in commands)
            {
                switch (c)
                {
                    case LineTo line:
                    {
                        path.LineTo(s + line.End.ToSkPoint(_scale));
                        break;
                    }
                    case CurveTo curve:
                    {
                        path.CubicTo(s + curve.ControlStart.ToSkPoint(_scale), s + curve.ControlEnd.ToSkPoint(_scale), s + curve.End.ToSkPoint(_scale));
                        break;
                    }
                    case MoveTo move:
                    {
                        path.MoveTo(s + move.End.ToSkPoint(_scale));
                        break;
                    }
                    case QuadraticBeizerCurveTo curve:
                    {
                        path.QuadTo(s + curve.Control.ToSkPoint(_scale), s + curve.End.ToSkPoint(_scale));
                        break;
                    }
                    case EllipticalArcTo arc:
                    {
                        path.ArcTo((float)arc.Size.Width * _scale,
                                   (float)arc.Size.Height * _scale,
                                   (float)arc.RotationAngle,
                                   arc.IsLargeArc ? SKPathArcSize.Large : SKPathArcSize.Small,
                                   arc.SweepDirection == CDSweepDirection.Clockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise,
                                   s.X + (float)arc.End.X * _scale,
                                   s.Y + (float)arc.End.Y * _scale);
                        break;
                    }
                    case ClosePath close:
                    {
                        path.Close();
                        break;
                    }
                    default:
                    {
                        path.MoveTo(s + c.End.ToSkPoint(_scale));
                        break;
                    }
                }
            }

            var paint = new SKPaint
            {
                Color = Color,
                IsAntialias = true,
                Style = fill ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Stroke,
                StrokeWidth = (float)thickness * _scale,
                StrokeCap = SKStrokeCap.Square,
            };

            _canvas.DrawPath(path, paint);
        }

        public void DrawText(CDPoint anchor, CDTextAlignment alignment, double rotation, IList<TextRun> textRuns)
        {
            if (textRuns == null || textRuns.Count == 0) return;

            var paint = new SKPaint
            {
                Color = Color,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            // 1. Measure total width and height
            float totalWidth = 0;
            float maxHeight = 0;
            float maxAscent = 0;
            float maxDescent = 0;

            var measuredRuns = new List<(TextRun Run, float Width, float Height, float Ascent, float Descent, SKFont Font)>();

            foreach (var run in textRuns)
            {
                var fontSize = (float)run.Formatting.Size * _scale;
                if (run.Formatting.FormattingType != TextRunFormattingType.Normal)
                {
                    fontSize *= 0.7f; // Smaller for sub/super
                }

                var font = new SKFont
                {
                    Size = fontSize,
                    Subpixel = true,
                    LinearMetrics = true
                };

                var width = font.MeasureText(run.Text, paint);
                font.GetFontMetrics(out var metrics);
                
                var ascent = -metrics.Ascent;
                var descent = metrics.Descent;
                var height = ascent + descent;

                measuredRuns.Add((run, width, height, ascent, descent, font));

                totalWidth += width;
                maxHeight = Math.Max(maxHeight, height);
                maxAscent = Math.Max(maxAscent, ascent);
                maxDescent = Math.Max(maxDescent, descent);
            }

            // 2. Calculate offsets based on alignment
            float xOffset = 0;
            float yOffset = 0;

            // Horizontal alignment
            switch (alignment)
            {
                case CDTextAlignment.TopCentre:
                case CDTextAlignment.CentreCentre:
                case CDTextAlignment.BottomCentre:
                    xOffset = -totalWidth / 2;
                    break;
                case CDTextAlignment.TopRight:
                case CDTextAlignment.CentreRight:
                case CDTextAlignment.BottomRight:
                    xOffset = -totalWidth;
                    break;
            }

            // Vertical alignment
            switch (alignment)
            {
                case CDTextAlignment.TopLeft:
                case CDTextAlignment.TopCentre:
                case CDTextAlignment.TopRight:
                    yOffset = maxAscent; 
                    break;
                case CDTextAlignment.CentreLeft:
                case CDTextAlignment.CentreCentre:
                case CDTextAlignment.CentreRight:
                    yOffset = maxAscent - (maxAscent + maxDescent) / 2;
                    break;
                case CDTextAlignment.BottomLeft:
                case CDTextAlignment.BottomCentre:
                case CDTextAlignment.BottomRight:
                    yOffset = -maxDescent;
                    break;
            }

            // 3. Draw
            _canvas.Save();
            var anchorSk = anchor.ToSkPoint(_scale);
            _canvas.Translate(anchorSk.X, anchorSk.Y);
            if (rotation != 0)
            {
                _canvas.RotateDegrees((float)rotation);
            }

            float currentX = xOffset;
            
            foreach (var item in measuredRuns)
            {
                var run = item.Run;
                var font = item.Font;
                
                float yPos = yOffset;
                
                if (run.Formatting.FormattingType == TextRunFormattingType.Subscript)
                {
                    yPos += item.Ascent * 0.3f; 
                }
                else if (run.Formatting.FormattingType == TextRunFormattingType.Superscript)
                {
                    yPos -= item.Ascent * 0.4f;
                }

                _canvas.DrawText(run.Text, currentX, yPos, font, paint);
                currentX += item.Width;
            }

            _canvas.Restore();
        }

        public void Dispose()
        {
            // Nothing to dispose as we don't own the canvas
        }
    }

    public static class SkiaExtensions
    {
        public static SKPoint ToSkPoint(this CDPoint point, float scale = 1.0f)
        {
            return new SKPoint((float)point.X * scale, (float)point.Y * scale);
        }
    }
}
