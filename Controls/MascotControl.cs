using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lotus.Models;
using Lotus.Services;

namespace Lotus.Controls;

public class MascotControl : Control
{
    public MascotEngine? Engine { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        if (Engine == null) return;
        
        var headPos = new Point(Bounds.Width / 2, Bounds.Height / 2 - 10);
        var eyeOpen = Engine.EyeOpenness;
        var pupilX = Engine.PupilOffsetX;
        var pupilY = Engine.PupilOffsetY;
        
        // Partículas
        foreach (var p in Engine.Particles.Where(p => p.Color != null))
        {
            if (p.Color is SolidColorBrush brush)
            {
                var c = brush.Color;
                var fadedColor = Color.FromArgb((byte)(255 * p.Life), c.R, c.G, c.B);
                context.DrawEllipse(new SolidColorBrush(fadedColor), null, p.Position, 4 * p.Life, 4 * p.Life);
            }
        }
        
        // Cuerpo (Frutiger Aero)
        var bodyRect = new Rect(headPos.X - 25, headPos.Y + 10, 50, 40);
        var bodyGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = 
            {
                new GradientStop(Color.Parse("#E0F7FA"), 0),
                new GradientStop(Color.Parse("#B2EBF2"), 0.5),
                new GradientStop(Color.Parse("#80DEEA"), 1)
            }
        };
        
        var bodyStroke = new SolidColorBrush(Color.FromArgb(153, 255, 255, 255)); // ~0.6 alpha
        context.DrawRectangle(bodyGradient, new Pen(bodyStroke, 2), bodyRect, 20, 20);
        
        // Cabeza
        var headColor = Color.Parse("#86EFAC"); // Default green
        var headBrush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.3, 0.3, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(headColor, 1) }
        };
        var headStroke = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)); // ~0.8 alpha
        context.DrawEllipse(headBrush, new Pen(headStroke, 2), headPos, 22, 20);
        
        // Ojos
        double eyeY = headPos.Y - 3 + (1 - eyeOpen) * 6;
        var whiteBrush = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)); // ~0.9 alpha
        context.DrawEllipse(whiteBrush, null, new Point(headPos.X - 8, eyeY), 7, 9 * eyeOpen);
        context.DrawEllipse(whiteBrush, null, new Point(headPos.X + 8, eyeY), 7, 9 * eyeOpen);
        
        // Pupilas
        var pupilBrush = new SolidColorBrush(Colors.Black);
        context.DrawEllipse(pupilBrush, null, new Point(headPos.X - 8 + pupilX, eyeY + pupilY), 3.5, 4);
        context.DrawEllipse(pupilBrush, null, new Point(headPos.X + 8 + pupilX, eyeY + pupilY), 3.5, 4);
        
        // Reflejos
        context.DrawEllipse(Brushes.White, null, new Point(headPos.X - 9 + pupilX, eyeY + pupilY - 2), 1.5, 2);
        context.DrawEllipse(Brushes.White, null, new Point(headPos.X + 7 + pupilX, eyeY + pupilY - 2), 1.5, 2);
        
        // Boca
        var mouthY = headPos.Y + 10;
        StreamGeometry? mouthGeometry = null;
        
        switch (Engine.State)
        {
            case EmotionalState.Happy:
            case EmotionalState.Party:
                mouthGeometry = CreateCurve(headPos.X - 8, mouthY, headPos.X, mouthY + 8, headPos.X + 8, mouthY);
                break;
            case EmotionalState.Sick:
            case EmotionalState.Tired:
                mouthGeometry = CreateCurve(headPos.X - 6, mouthY + 4, headPos.X, mouthY - 2, headPos.X + 6, mouthY + 4);
                break;
            case EmotionalState.Sleep:
                context.DrawEllipse(pupilBrush, null, new Point(headPos.X, mouthY + 2), 4, 2);
                break;
            case EmotionalState.Hungry:
                mouthGeometry = CreateOval(headPos.X, mouthY + 3, 5, 6);
                break;
            default:
                context.DrawLine(new Pen(pupilBrush, 2), new Point(headPos.X - 5, mouthY + 3), new Point(headPos.X + 5, mouthY + 3));
                break;
        }
        
        if (mouthGeometry != null)
            context.DrawGeometry(null, new Pen(pupilBrush, 2), mouthGeometry);
        
        // Mejillas blush
        if (Engine.State == EmotionalState.Happy || Engine.State == EmotionalState.Party)
        {
            var blush = new SolidColorBrush(Color.FromArgb(153, 255, 182, 193)); // ~0.6 alpha pink
            context.DrawEllipse(blush, null, new Point(headPos.X - 15, headPos.Y + 5), 4, 3);
            context.DrawEllipse(blush, null, new Point(headPos.X + 15, headPos.Y + 5), 4, 3);
        }
        
        // Aura cansado
        if (Engine.State == EmotionalState.Tired)
        {
            var tiredBrush = new SolidColorBrush(Color.FromArgb(77, 173, 216, 230)); // ~0.3 alpha light blue
            context.DrawEllipse(tiredBrush, null, headPos, 30, 28);
        }
    }

    private static StreamGeometry CreateCurve(double x1, double y1, double cx, double cy, double x2, double y2)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(x1, y1), false);
            ctx.QuadraticBezierTo(new Point(cx, cy), new Point(x2, y2));
        }
        return geo;
    }

    private static StreamGeometry CreateOval(double cx, double cy, double rx, double ry)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(cx, cy - ry), false);
            ctx.ArcTo(new Point(cx, cy + ry), new Size(rx, ry), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(new Point(cx, cy - ry), new Size(rx, ry), 0, false, SweepDirection.Clockwise);
        }
        return geo;
    }
}
