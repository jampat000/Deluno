using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace Deluno.Tray;

[SupportedOSPlatform("windows")]
public static class TrayIconRenderer
{
    // Renders a 16×16 icon matching the Deluno brand mark:
    // film-frame screen (outline + side tabs) with a play triangle.
    public static Icon Render(TrayState state)
    {
        const int size = 16;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bgBrush = new SolidBrush(StateColor(state));
        g.FillEllipse(bgBrush, 0, 0, size - 1, size - 1);

        using var whitePen   = new Pen(Color.White, 1.2f);
        using var whiteBrush = new SolidBrush(Color.White);

        // Screen outline
        DrawRoundedRect(g, whitePen, 2.5f, 5f, 11f, 6f, 1.2f);

        // Film strip tabs — left edge (two small filled rects straddling left stroke)
        g.FillRectangle(whiteBrush, 1f, 6f,   2f, 1.5f);
        g.FillRectangle(whiteBrush, 1f, 8.5f, 2f, 1.5f);

        // Film strip tabs — right edge
        g.FillRectangle(whiteBrush, 13f, 6f,   2f, 1.5f);
        g.FillRectangle(whiteBrush, 13f, 8.5f, 2f, 1.5f);

        // Play triangle, centred in screen
        g.FillPolygon(whiteBrush, new PointF[]
        {
            new(6.5f, 6.5f),
            new(6.5f, 10.5f),
            new(11f,  8.5f),
        });

        return Icon.FromHandle(bmp.GetHicon());
    }

    private static void DrawRoundedRect(Graphics g, Pen pen, float x, float y, float w, float h, float r)
    {
        using var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }

    private static Color StateColor(TrayState state) => state switch
    {
        TrayState.Running  => Color.FromArgb(0xF5, 0xA6, 0x23),  // brand orange
        TrayState.Degraded => Color.FromArgb(0xF5, 0x9E, 0x0B),  // amber
        TrayState.Error    => Color.FromArgb(0xEF, 0x44, 0x44),  // red
        TrayState.Starting => Color.FromArgb(0xF5, 0xA6, 0x23),  // brand orange
        TrayState.Updating => Color.FromArgb(0x63, 0x66, 0xF1),  // indigo
        _                  => Color.FromArgb(0x6B, 0x72, 0x80),  // gray
    };
}
