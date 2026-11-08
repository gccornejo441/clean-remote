using System.Drawing;

namespace CleanTVRemote;

internal static class ThemePalette
{
    public static bool Light { get; set; }

    public static Color Background => Light ? Color.FromArgb(247, 249, 252) : Color.FromArgb(19, 18, 24);
    public static Color Surface => Light ? Color.FromArgb(226, 230, 239) : Color.FromArgb(44, 42, 55);
    public static Color Lifted => Light ? Color.FromArgb(212, 217, 230) : Color.FromArgb(58, 55, 72);
    public static Color Foreground => Light ? Color.FromArgb(26, 31, 44) : Color.FromArgb(242, 240, 249);
    public static Color Muted => Light ? Color.FromArgb(80, 87, 104) : Color.FromArgb(153, 148, 171);
    public static Color Purple => Light ? Color.FromArgb(102, 57, 215) : Color.FromArgb(125, 72, 255);
    public static Color Red => Light ? Color.FromArgb(196, 39, 51) : Color.FromArgb(231, 53, 66);
    public static Color Green => Light ? Color.FromArgb(17, 137, 64) : Color.FromArgb(35, 207, 109);
}
