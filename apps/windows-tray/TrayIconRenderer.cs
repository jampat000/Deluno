using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace Deluno.Tray;

[SupportedOSPlatform("windows")]
public static class TrayIconRenderer
{
    // Renders a 16×16 icon using the Deluno crescent-moon mark.
    // Background colour changes per state; the crescent shape is always white.
    public static Icon Render(TrayState state)
    {
        const int size = 16;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bgBrush = new SolidBrush(StateColor(state));
        g.FillEllipse(bgBrush, 0, 0, size - 1, size - 1);

        DrawCrescent(g, Color.White);

        return Icon.FromHandle(bmp.GetHicon());
    }

    // Crescent geometry mirrors the favicon.svg (scaled to 16×16):
    //   full moon  → ellipse at (2, 3, 11, 11)
    //   bite circle → ellipse at (6, 0, 10, 10)  [offset upper-right]
    private static void DrawCrescent(Graphics g, Color color)
    {
        using var fullPath = new GraphicsPath();
        fullPath.AddEllipse(2f, 3f, 11f, 11f);

        using var region  = new Region(fullPath);
        using var cutPath = new GraphicsPath();
        cutPath.AddEllipse(6f, 0f, 10f, 10f);
        region.Exclude(cutPath);

        using var brush = new SolidBrush(color);
        g.FillRegion(brush, region);
    }

    private static Color StateColor(TrayState state) => state switch
    {
        TrayState.Running  => Color.FromArgb(0x22, 0xC5, 0x5E),  // green-500
        TrayState.Degraded => Color.FromArgb(0xF5, 0x9E, 0x0B),  // amber-500
        TrayState.Error    => Color.FromArgb(0xEF, 0x44, 0x44),  // red-500
        TrayState.Starting => Color.FromArgb(0x63, 0x66, 0xF1),  // indigo-500 (brand)
        TrayState.Updating => Color.FromArgb(0x63, 0x66, 0xF1),  // indigo-500 (brand)
        _                  => Color.FromArgb(0x6B, 0x72, 0x80),  // gray-500
    };
}
