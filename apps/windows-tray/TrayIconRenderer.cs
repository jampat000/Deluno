using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace Deluno.Tray;

[SupportedOSPlatform("windows")]
public static class TrayIconRenderer
{
    // Renders a 16×16 icon for the given state using GDI+.
    // Avoids shipping binary .ico files that would need updating with each brand change.
    public static Icon Render(TrayState state)
    {
        const int size = 16;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Background circle
        var bg = BackgroundColor(state);
        using var bgBrush = new SolidBrush(bg);
        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

        // State glyph
        DrawGlyph(g, state, size);

        return Icon.FromHandle(bmp.GetHicon());
    }

    private static Color BackgroundColor(TrayState state) => state switch
    {
        TrayState.Starting  => Color.FromArgb(100, 150, 220),   // blue-grey
        TrayState.Running   => Color.FromArgb(52,  168, 83),    // green
        TrayState.Degraded  => Color.FromArgb(251, 188, 5),     // amber
        TrayState.Error     => Color.FromArgb(234, 67,  53),    // red
        TrayState.Updating  => Color.FromArgb(66,  133, 244),   // blue
        TrayState.Stopped   => Color.FromArgb(158, 158, 158),   // grey
        _                   => Color.FromArgb(52,  168, 83)
    };

    private static void DrawGlyph(Graphics g, TrayState state, int size)
    {
        using var pen   = new Pen(Color.White, 1.5f);
        using var brush = new SolidBrush(Color.White);
        int cx = size / 2, cy = size / 2;

        switch (state)
        {
            case TrayState.Running:
                // Play-style "D" letterform (Deluno initial)
                g.DrawString("D", new Font("Arial", 7f, FontStyle.Bold),
                    brush, new PointF(4f, 3f));
                break;

            case TrayState.Starting:
            case TrayState.Updating:
                // Arc suggesting motion / loading
                g.DrawArc(pen, 4, 4, size - 8, size - 8, -90, 270);
                break;

            case TrayState.Stopped:
                // Square / stop symbol
                g.FillRectangle(brush, cx - 3, cy - 3, 6, 6);
                break;

            case TrayState.Error:
                // Exclamation mark
                g.FillRectangle(brush, cx - 1, 4, 2, 5);
                g.FillEllipse(brush, cx - 1, 11, 2, 2);
                break;

            case TrayState.Degraded:
                // Warning triangle outline
                var tri = new PointF[]
                {
                    new(cx, 4), new(cx + 4, 12), new(cx - 4, 12)
                };
                g.DrawPolygon(pen, tri);
                g.FillRectangle(brush, cx - 1, 6, 2, 3);
                g.FillEllipse(brush, cx - 1, 10, 2, 2);
                break;
        }
    }
}
