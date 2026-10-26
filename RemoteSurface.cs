using System.Drawing;
using System.Drawing.Drawing2D;

namespace CleanTVRemote;

internal sealed class RemoteSurface : Control
{
    private const float DesignWidth = 320f;
    private const float DesignHeight = 704f;

    private static readonly Color Background = Color.FromArgb(19, 18, 24);
    private static readonly Color Surface = Color.FromArgb(44, 42, 55);
    private static readonly Color Lifted = Color.FromArgb(58, 55, 72);
    private static readonly Color TextPrimary = Color.FromArgb(242, 240, 249);
    private static readonly Color TextSecondary = Color.FromArgb(153, 148, 171);
    private static readonly Color Purple = Color.FromArgb(125, 72, 255);
    private static readonly Color Red = Color.FromArgb(231, 53, 66);
    private static readonly Color Green = Color.FromArgb(35, 207, 109);

    private readonly (string Id, RectangleF Area)[] _targets =
    [
        ("device", new RectangleF(12, 16, 245, 42)),
        ("help", new RectangleF(272, 17, 37, 40)),
        ("power", new RectangleF(19, 105, 56, 69)),
        ("input", new RectangleF(93, 105, 56, 69)),
        ("apps", new RectangleF(172, 105, 56, 69)),
        ("keyboard", new RectangleF(246, 105, 56, 69)),
        ("up", new RectangleF(125, 187, 70, 59)),
        ("left", new RectangleF(57, 252, 65, 75)),
        ("ok", new RectangleF(122, 250, 76, 79)),
        ("right", new RectangleF(199, 252, 65, 75)),
        ("down", new RectangleF(125, 334, 70, 61)),
        ("back", new RectangleF(14, 410, 90, 39)),
        ("home", new RectangleF(113, 410, 94, 39)),
        ("options", new RectangleF(216, 410, 90, 39)),
        ("volume", new RectangleF(17, 469, 60, 136)),
        ("mute", new RectangleF(136, 471, 48, 48)),
        ("info", new RectangleF(136, 542, 48, 48)),
        ("ch_up", new RectangleF(243, 469, 60, 50)),
        ("ch_down", new RectangleF(243, 552, 60, 53)),
        ("rewind", new RectangleF(65, 629, 44, 44)),
        ("play", new RectangleF(138, 629, 44, 44)),
        ("forward", new RectangleF(211, 629, 44, 44))
    ];

    private string? _hover;
    private string? _pressed;
    private string _status = "Tap Living Room TV to connect";
    private string _tvName = "Living Room TV";
    private bool _connected;
    private bool _draggingVolume;
    private float _volumeThumbY = 537f;
    private float _volumeLastY;
    private float _volumeStepRemainder;

    private const float VolumeTrackTop = 497f;
    private const float VolumeTrackBottom = 578f;
    private const float VolumeStepsPerControlUnit = 12f;

    public event Action<string>? ActionRequested;

    public RemoteSurface()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        DoubleBuffered = true;
        TabStop = true;
        BackColor = Background;
        ForeColor = TextPrimary;
    }

    public void SetConnection(string message, bool connected)
    {
        _status = message;
        _connected = connected;
        Invalidate();
    }

    public void SetTvName(string name)
    {
        _tvName = name;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.Clear(Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        float scale = GetScale();
        var prior = g.Save();
        g.TranslateTransform((ClientSize.Width - DesignWidth * scale) / 2, (ClientSize.Height - DesignHeight * scale) / 2);
        g.ScaleTransform(scale, scale);
        PaintRemote(g);
        g.Restore(prior);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_draggingVolume)
        {
            float y = Math.Clamp(ToDesignPoint(e.Location).Y, VolumeTrackTop, VolumeTrackBottom);
            ApplyVolumeMovement(_volumeLastY - y);
            _volumeLastY = y;
            _volumeThumbY = y;
            _hover = "volume";
            Cursor = Cursors.SizeNS;
            Invalidate();
            return;
        }

        string? next = HitTest(e.Location);
        if (_hover != next)
        {
            _hover = next;
            Cursor = next == null ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_draggingVolume) return;
        _hover = null;
        _pressed = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        _pressed = HitTest(e.Location);
        if (_pressed == "volume")
        {
            float y = Math.Clamp(ToDesignPoint(e.Location).Y, VolumeTrackTop, VolumeTrackBottom);
            _volumeStepRemainder = 0;
            _volumeLastY = y;
            _volumeThumbY = y;
            _draggingVolume = true;
            Capture = true;
            ApplyVolumeMovement(537f - y);
            Cursor = Cursors.SizeNS;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _draggingVolume)
        {
            _draggingVolume = false;
            _pressed = null;
            _volumeThumbY = 537f;
            _volumeStepRemainder = 0;
            Capture = false;
            Cursor = Cursors.Default;
            Invalidate();
            return;
        }

        string? hit = HitTest(e.Location);
        string? press = _pressed;
        _pressed = null;
        Invalidate();
        if (e.Button == MouseButtons.Left && press != null && press == hit)
        {
            ActionRequested?.Invoke(press);
        }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        return key is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        string? action = e.KeyCode switch
        {
            Keys.Up => "up",
            Keys.Down => "down",
            Keys.Left => "left",
            Keys.Right => "right",
            Keys.Enter => "ok",
            Keys.Escape or Keys.Back => "back",
            Keys.Home => "home",
            Keys.Add or Keys.Oemplus => "vol_up",
            Keys.Subtract or Keys.OemMinus => "vol_down",
            Keys.M => "mute",
            Keys.PageUp => "ch_up",
            Keys.PageDown => "ch_down",
            Keys.Space => "play",
            _ => null
        };
        if (action == null)
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
        ActionRequested?.Invoke(action);
    }

    private float GetScale() => Math.Min(ClientSize.Width / DesignWidth, ClientSize.Height / DesignHeight);

    private PointF ToDesignPoint(Point point)
    {
        float scale = GetScale();
        if (scale <= 0) return PointF.Empty;
        return new PointF(
            (point.X - (ClientSize.Width - DesignWidth * scale) / 2) / scale,
            (point.Y - (ClientSize.Height - DesignHeight * scale) / 2) / scale);
    }

    private void ApplyVolumeMovement(float upwardMovement)
    {
        _volumeStepRemainder += upwardMovement;
        while (_volumeStepRemainder >= VolumeStepsPerControlUnit)
        {
            ActionRequested?.Invoke("vol_up");
            _volumeStepRemainder -= VolumeStepsPerControlUnit;
        }
        while (_volumeStepRemainder <= -VolumeStepsPerControlUnit)
        {
            ActionRequested?.Invoke("vol_down");
            _volumeStepRemainder += VolumeStepsPerControlUnit;
        }
    }

    private string? HitTest(Point point)
    {
        PointF designPoint = ToDesignPoint(point);
        if (GetScale() <= 0) return null;
        foreach (var target in _targets)
        {
            if (target.Area.Contains(designPoint)) return target.Id;
        }
        return null;
    }

    private Color ActiveColor(string id, Color normal)
    {
        if (_pressed == id) return Color.FromArgb(103, 87, 151);
        if (_hover == id) return Blend(normal, Color.White, 0.12f);
        return normal;
    }

    private static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

    private void PaintRemote(Graphics g)
    {
        FillRounded(g, new RectangleF(12, 16, 245, 42), 23, ActiveColor("device", Surface));
        FillCircle(g, 32, 37, 4.5f, _connected ? Green : Color.FromArgb(240, 171, 69));
        DrawText(g, _tvName, new RectangleF(44, 23, 142, 28), 13, true, TextPrimary);
        FillRounded(g, new RectangleF(193, 29, 50, 17), 9, Purple);
        DrawText(g, "Samsung", new RectangleF(196, 30, 45, 14), 8.1f, true, Color.White, StringAlignment.Center);
        Chevron(g, 252, 37, "down", TextSecondary, 3.1f);
        FillCircle(g, 290, 37, 19, ActiveColor("help", Surface));
        DrawText(g, "?", new RectangleF(274, 24, 32, 24), 16, true, TextPrimary, StringAlignment.Center);
        DrawText(g, _status, new RectangleF(15, 70, 291, 30), 10.4f, false, TextSecondary, StringAlignment.Near, true);

        TopAction(g, 47, "power", "Power", "power", Red);
        TopAction(g, 121, "input", "Input", "screen", Surface);
        TopAction(g, 199, "apps", "Apps", "apps", Surface);
        TopAction(g, 273, "keyboard", "Keyboard", "keyboard", Surface);

        FillCircle(g, 160, 290, 103, Surface);
        PadArrow(g, "up", 160, 208, "up");
        PadArrow(g, "left", 83, 290, "left");
        PadArrow(g, "right", 237, 290, "right");
        PadArrow(g, "down", 160, 373, "down");
        FillCircle(g, 160, 290, 38, ActiveColor("ok", Purple));
        DrawText(g, "OK", new RectangleF(133, 277, 54, 26), 14, true, Color.White, StringAlignment.Center);

        Pill(g, 14, 410, 90, "back", "back", "Back");
        Pill(g, 113, 410, 94, "home", "home", "Home");
        Pill(g, 216, 410, 90, "options", "more", "Options");

        FillRounded(g, new RectangleF(17, 469, 60, 136), 30, ActiveColor("volume", Surface));
        FillRounded(g, new RectangleF(243, 469, 60, 136), 30, Surface);
        if (_hover == "ch_up" || _pressed == "ch_up") FillRounded(g, new RectangleF(245, 471, 56, 48), 24, ActiveColor("ch_up", Surface));
        if (_hover == "ch_down" || _pressed == "ch_down") FillRounded(g, new RectangleF(245, 555, 56, 48), 24, ActiveColor("ch_down", Surface));
        Icon(g, "plus", 57, 484, TextPrimary);
        using (var trackPen = new Pen(Color.FromArgb(88, 84, 104), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(trackPen, 57, VolumeTrackTop, 57, VolumeTrackBottom);
        FillCircle(g, 57, _volumeThumbY, 7.5f, _draggingVolume ? Purple : Lifted);
        using (var thumbOutline = new Pen(Color.FromArgb(197, 177, 255), 1.3f))
            g.DrawEllipse(thumbOutline, 50, _volumeThumbY - 7, 14, 14);
        DrawText(g, "VOL", new RectangleF(19, 523, 29, 24), 9.2f, true, TextSecondary, StringAlignment.Center);
        Icon(g, "minus", 57, 591, TextPrimary);
        Chevron(g, 273, 495, "up", TextPrimary, 7);
        DrawText(g, "CH", new RectangleF(248, 526, 50, 25), 10, true, TextSecondary, StringAlignment.Center);
        Chevron(g, 273, 579, "down", TextPrimary, 7);

        FillCircle(g, 160, 495, 24, ActiveColor("mute", Surface));
        Icon(g, "mute", 160, 495, TextSecondary);
        DrawText(g, "Mute", new RectangleF(136, 518, 48, 20), 9.2f, false, TextSecondary, StringAlignment.Center);
        FillCircle(g, 160, 566, 24, ActiveColor("info", Surface));
        Icon(g, "info", 160, 566, TextSecondary);
        DrawText(g, "Info", new RectangleF(136, 590, 48, 18), 9.2f, false, TextSecondary, StringAlignment.Center);

        Media(g, 87, "rewind", "rewind");
        Media(g, 160, "play", "play");
        Media(g, 233, "forward", "forward");
        FillCircle(g, 111, 693, 3, Purple);
        DrawText(g, "LOCAL NETWORK CONTROL", new RectangleF(120, 679, 156, 27), 8.5f, true, Purple);
    }

    private void TopAction(Graphics g, float x, string id, string label, string icon, Color fill)
    {
        FillCircle(g, x, 128, 23, ActiveColor(id, fill));
        Icon(g, icon, x, 128, id == "power" ? Color.White : TextSecondary);
        DrawText(g, label, new RectangleF(x - 33, 154, 66, 24), 9.9f, false, TextSecondary, StringAlignment.Center);
    }

    private void PadArrow(Graphics g, string id, float x, float y, string direction)
    {
        if (_hover == id || _pressed == id) FillCircle(g, x, y, 24, ActiveColor(id, Lifted));
        Chevron(g, x, y, direction, TextPrimary, 6);
    }

    private void Pill(Graphics g, float x, float y, float width, string id, string icon, string label)
    {
        FillRounded(g, new RectangleF(x, y, width, 39), 20, ActiveColor(id, Surface));
        Icon(g, icon, x + 21, y + 19.5f, TextSecondary);
        DrawText(g, label, new RectangleF(x + 32, y + 7, width - 36, 26), 10.6f, false, TextPrimary);
    }

    private void Media(Graphics g, float x, string id, string icon)
    {
        FillCircle(g, x, 651, 22, ActiveColor(id, Surface));
        Icon(g, icon, x, 651, TextSecondary);
    }

    private static void FillCircle(Graphics g, float x, float y, float r, Color color)
    {
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, x - r, y - r, r * 2, r * 2);
    }

    private static void FillRounded(Graphics g, RectangleF rectangle, float r, Color color)
    {
        using var path = new GraphicsPath();
        float diameter = Math.Min(r * 2, Math.Min(rectangle.Width, rectangle.Height));
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static void DrawText(Graphics g, string value, RectangleF bounds, float size, bool bold, Color color,
        StringAlignment alignment = StringAlignment.Near, bool ellipsis = false)
    {
        using var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = alignment,
            LineAlignment = StringAlignment.Center,
            Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None,
            FormatFlags = StringFormatFlags.NoWrap
        };
        g.DrawString(value, font, brush, bounds, format);
    }

    private static void Chevron(Graphics g, float x, float y, string direction, Color color, float size)
    {
        using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        PointF[] points = direction switch
        {
            "up" => [new(x - size, y + size / 2), new(x, y - size / 2), new(x + size, y + size / 2)],
            "down" => [new(x - size, y - size / 2), new(x, y + size / 2), new(x + size, y - size / 2)],
            "left" => [new(x + size / 2, y - size), new(x - size / 2, y), new(x + size / 2, y + size)],
            _ => [new(x - size / 2, y - size), new(x + size / 2, y), new(x - size / 2, y + size)]
        };
        g.DrawLines(pen, points);
    }

    private static void Icon(Graphics g, string icon, float x, float y, Color color)
    {
        using var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        switch (icon)
        {
            case "power":
                g.DrawArc(pen, x - 8, y - 7, 16, 16, 310, 280);
                g.DrawLine(pen, x, y - 11, x, y - 1);
                break;
            case "screen":
                g.DrawRectangle(pen, x - 9, y - 7, 18, 13);
                g.DrawLine(pen, x, y + 6, x, y + 10);
                g.DrawLine(pen, x - 5, y + 10, x + 5, y + 10);
                break;
            case "apps":
                for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                    g.DrawRectangle(pen, x - 9 + a * 11, y - 9 + b * 11, 6, 6);
                break;
            case "keyboard":
                g.DrawRoundedRectangleSafe(pen, new RectangleF(x - 10, y - 7, 20, 14), 2);
                for (int col = -5; col <= 5; col += 5)
                {
                    g.FillEllipse(brush, x + col - 1, y - 3, 2, 2);
                    g.FillEllipse(brush, x + col - 1, y + 1, 2, 2);
                }
                g.DrawLine(pen, x - 5, y + 5, x + 5, y + 5);
                break;
            case "back":
                g.DrawLines(pen, [new PointF(x - 2, y - 6), new PointF(x - 8, y), new PointF(x - 2, y + 6)]);
                g.DrawLine(pen, x - 8, y, x + 8, y);
                break;
            case "home":
                g.DrawLines(pen, [new PointF(x - 9, y), new PointF(x, y - 8), new PointF(x + 9, y)]);
                g.DrawLines(pen, [new PointF(x - 7, y - 1), new PointF(x - 7, y + 8), new PointF(x + 7, y + 8), new PointF(x + 7, y - 1)]);
                g.DrawLine(pen, x - 2, y + 8, x - 2, y + 2);
                g.DrawLine(pen, x + 2, y + 8, x + 2, y + 2);
                break;
            case "more":
                for (int i = -1; i <= 1; i++) g.FillEllipse(brush, x + i * 5 - 1.2f, y - 1.2f, 2.4f, 2.4f);
                break;
            case "plus":
                g.DrawLine(pen, x - 6, y, x + 6, y);
                g.DrawLine(pen, x, y - 6, x, y + 6);
                break;
            case "minus":
                g.DrawLine(pen, x - 6, y, x + 6, y);
                break;
            case "mute":
                g.DrawLines(pen, [new PointF(x - 9, y - 4), new PointF(x - 5, y - 4), new PointF(x, y - 9), new PointF(x, y + 9), new PointF(x - 5, y + 4), new PointF(x - 9, y + 4)]);
                g.DrawLine(pen, x + 4, y - 5, x + 10, y + 5);
                g.DrawLine(pen, x + 10, y - 5, x + 4, y + 5);
                break;
            case "info":
                g.DrawEllipse(pen, x - 8, y - 8, 16, 16);
                g.DrawLine(pen, x, y - 1, x, y + 5);
                g.FillEllipse(brush, x - 1, y - 5, 2, 2);
                break;
            case "rewind":
                DrawTriangle(g, pen, x - 8, y, false);
                DrawTriangle(g, pen, x + 1, y, false);
                g.DrawLine(pen, x - 10, y - 8, x - 10, y + 8);
                break;
            case "forward":
                DrawTriangle(g, pen, x - 1, y, true);
                DrawTriangle(g, pen, x + 8, y, true);
                g.DrawLine(pen, x + 10, y - 8, x + 10, y + 8);
                break;
            case "play":
                g.DrawPolygon(pen, [new PointF(x - 9, y - 8), new PointF(x + 1, y), new PointF(x - 9, y + 8)]);
                g.DrawLine(pen, x + 5, y - 7, x + 5, y + 7);
                g.DrawLine(pen, x + 10, y - 7, x + 10, y + 7);
                break;
        }
    }

    private static void DrawTriangle(Graphics g, Pen pen, float x, float y, bool right)
    {
        float dir = right ? 1 : -1;
        g.DrawPolygon(pen, [new PointF(x - 4 * dir, y - 7), new PointF(x + 4 * dir, y), new PointF(x - 4 * dir, y + 7)]);
    }
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangleSafe(this Graphics g, Pen pen, RectangleF rectangle, float radius)
    {
        using var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rectangle.X, rectangle.Y, d, d, 180, 90);
        path.AddArc(rectangle.Right - d, rectangle.Y, d, d, 270, 90);
        path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }
}
