using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace Blitztext;

/// <summary>Die funktionalen Tray-/Icon-Zustände von Blitztext.</summary>
public enum TrayVisual { Idle, Recording, Processing, Success }

/// <summary>
/// Vektor-Zeichen-Routine des Blitztext-Zeichens (Audio-Welle + Textlinien), parametrisch in
/// Größe + Zustand + Animations-Phase. Eine Quelle für Tray-Icon-Frames (DrawTray) und das
/// App-Icon (DrawAppIcon). Motiv aus der 2.0-Version, neu als Vektor in ORAH-Optik/Palette.
/// </summary>
internal static class BtEmblem
{
    private static Color Accent(TrayVisual s) => s switch
    {
        TrayVisual.Recording  => BtPalette.Recording,
        TrayVisual.Processing => BtPalette.Transcribing,
        TrayVisual.Success    => BtPalette.Success,
        _                     => BtPalette.Accent,
    };

    // ── App-Icon: dunkles Squircle + Welle + Textbalken (immer Ruhe/Cyan) ──────────
    public static void DrawAppIcon(DrawingContext ctx, double size)
    {
        double f = size / 64.0;
        var rect = new Rect(5 * f, 5 * f, 54 * f, 54 * f);
        double r = 14 * f;

        var bg = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(255, 28, 34, 43), 0),
                new GradientStop(Color.FromArgb(255, 7, 10, 15), 1),
            },
        };
        ctx.DrawRectangle(bg, null, rect, r, r);

        var rimPen = new Pen(new SolidColorBrush(BtPalette.TextPrimary.WithAlpha(0.30)), Math.Max(1, 1.2 * f));
        ctx.DrawRectangle(null, rimPen, rect, r, r);

        // subtiler oberer Glanz
        var sheen = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(BtPalette.TextPrimary.WithAlpha(0.05), 0),
                new GradientStop(Colors.Transparent, 0.5),
            },
        };
        ctx.DrawRectangle(sheen, null, rect, r, r);

        DrawWave(ctx, f, Accent(TrayVisual.Idle), 1.0);
        DrawTextBars(ctx, f, 1.0);
    }

    // ── Tray (transparenter Grund: Welle + Balken + optionaler Status-Punkt) ───────
    public static void DrawTray(DrawingContext ctx, double size, TrayVisual state, double phase)
    {
        double f = size / 64.0;
        // sanftes Atmen für aktive Zustände (Opazität pulsiert)
        double pulse = 0.5 * (1 + Math.Cos(2 * Math.PI * phase));
        double bright = state switch
        {
            TrayVisual.Recording  => 0.6 + 0.4 * pulse,
            TrayVisual.Processing => 0.7 + 0.3 * pulse,
            _ => 1.0,
        };

        DrawWave(ctx, f, Accent(state), bright);
        DrawTextBars(ctx, f, 0.9);
        if (state != TrayVisual.Idle)
            DrawStatusDot(ctx, f, Accent(state), bright);
    }

    // ── Helfer ─────────────────────────────────────────────────────────────────────
    private static void DrawWave(DrawingContext ctx, double f, Color color, double alpha)
    {
        var pts = new[]
        {
            new Point(14 * f, 34 * f),
            new Point(20 * f, 26 * f),
            new Point(26 * f, 39 * f),
            new Point(32 * f, 20 * f),
            new Point(38 * f, 33 * f),
        };
        var geo = SmoothGeometry(pts);
        var pen = new Pen(new SolidColorBrush(color.WithAlpha(alpha)), Math.Max(2, 4 * f))
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        ctx.DrawGeometry(null, pen, geo);
    }

    private static void DrawTextBars(DrawingContext ctx, double f, double alpha)
    {
        var brush = new SolidColorBrush(Color.FromRgb(248, 252, 255).WithAlpha(0.91 * alpha));
        double rr = 1.6 * f;
        ctx.DrawRectangle(brush, null, new Rect(41 * f, 22 * f, 10 * f, 3.2 * f), rr, rr);
        ctx.DrawRectangle(brush, null, new Rect(41 * f, 31 * f, 14 * f, 3.2 * f), rr, rr);
        ctx.DrawRectangle(brush, null, new Rect(41 * f, 40 * f, 8 * f, 3.2 * f), rr, rr);
    }

    private static void DrawStatusDot(DrawingContext ctx, double f, Color color, double alpha)
    {
        var center = new Point(51 * f, 51 * f);
        ctx.DrawEllipse(new SolidColorBrush(color.WithAlpha(0.25 * alpha)), null, center, 8 * f, 8 * f);
        ctx.DrawEllipse(new SolidColorBrush(color.WithAlpha(alpha)), null, center, 4 * f, 4 * f);
    }

    private static StreamGeometry SmoothGeometry(Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            var sampled = CatmullRom(pts, 16);
            gc.BeginFigure(sampled[0], false);
            for (int i = 1; i < sampled.Count; i++) gc.LineTo(sampled[i]);
            gc.EndFigure(false);
        }
        return geo;
    }

    private static List<Point> CatmullRom(Point[] p, int seg)
    {
        var res = new List<Point>();
        for (int i = 0; i < p.Length - 1; i++)
        {
            var p0 = i == 0 ? p[i] : p[i - 1];
            var p1 = p[i];
            var p2 = p[i + 1];
            var p3 = i + 2 < p.Length ? p[i + 2] : p[i + 1];
            for (int j = 0; j < seg; j++)
            {
                double t = (double)j / seg;
                double t2 = t * t, t3 = t2 * t;
                double x = 0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
                double y = 0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
                res.Add(new Point(x, y));
            }
        }
        res.Add(p[^1]);
        return res;
    }
}
