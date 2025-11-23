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
            var font = new SKFont
            {
                Size = 12f * _scale,
                Subpixel = true,
                LinearMetrics = rotation != 0.0
            };

            var paint = new SKPaint
            {
                Color = Color,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            var startLocation = anchor.ToSkPoint(_scale);
            
            foreach (TextRun run in textRuns)
            {
                 _canvas.DrawText(run.Text, startLocation, font, paint);
            }
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
