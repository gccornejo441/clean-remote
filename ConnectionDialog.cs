using System.Drawing;
using System.Net;
using System.Net.Sockets;

namespace CleanTVRemote;

internal sealed class ConnectionDialog : Form
{
    private static readonly Color Background = Color.FromArgb(19, 18, 24);
    private static readonly Color Surface = Color.FromArgb(44, 42, 55);
    private static readonly Color Foreground = Color.FromArgb(242, 240, 249);
    private static readonly Color Muted = Color.FromArgb(153, 148, 171);
    private static readonly Color Purple = Color.FromArgb(125, 72, 255);

    private readonly TextBox _ip = new();
    private readonly TextBox _mac = new();
    private readonly ComboBox _mode = new();

    public string TvIp => _ip.Text.Trim();
    public string Mac => _mac.Text.Trim();
    public bool Secure => _mode.SelectedIndex == 0;

    public ConnectionDialog(AppSettings settings, bool connected)
    {
        Text = "TV connection";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(380, 389);
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10);

        AddText("CONNECT TO SAMSUNG", 23, 16, 334, 32, 17, Foreground, true);
        AddText("Your computer and TV must be on the same network.", 24, 53, 339, 28, 9, Muted);
        AddText("TV IP ADDRESS", 24, 92, 330, 19, 9, Muted, true);
        StyleField(_ip, 24, 116, 330);
        _ip.Text = settings.TvIp;
        _ip.PlaceholderText = "192.168.1.50";

        AddText("TV MAC ADDRESS · OPTIONAL FOR WAKE", 24, 159, 330, 19, 9, Muted, true);
        StyleField(_mac, 24, 183, 330);
        _mac.Text = settings.MacAddress;
        _mac.PlaceholderText = "AA:BB:CC:DD:EE:FF";

        AddText("CONNECTION MODE", 24, 226, 330, 19, 9, Muted, true);
        _mode.SetBounds(24, 250, 330, 32);
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(["Secure (port 8002)", "Legacy (port 8001)"]);
        _mode.SelectedIndex = settings.Secure ? 0 : 1;
        _mode.FlatStyle = FlatStyle.Flat;
        _mode.BackColor = Surface;
        _mode.ForeColor = Foreground;
        Controls.Add(_mode);

        var connect = CreateButton("Save & connect", 24, 300, 330, 38, Purple, DialogResult.None);
        connect.Click += (_, _) =>
        {
            if (!IPAddress.TryParse(TvIp, out IPAddress? address) || address.AddressFamily != AddressFamily.InterNetwork)
            {
                MessageBox.Show(this, "Enter a valid IPv4 address for your TV.", "Invalid TV IP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        AcceptButton = connect;

        var forget = CreateButton("Forget pairing", 24, 346, 112, 28, Surface, DialogResult.Retry);
        var disconnect = CreateButton("Disconnect", 145, 346, 104, 28, Surface, DialogResult.Abort);
        disconnect.Enabled = connected;
        var cancel = CreateButton("Cancel", 258, 346, 96, 28, Surface, DialogResult.Cancel);
        CancelButton = cancel;
    }

    private void AddText(string value, int x, int y, int width, int height, float size, Color color, bool bold = false)
    {
        Controls.Add(new Label
        {
            Text = value,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color,
            Bounds = new Rectangle(x, y, width, height)
        });
    }

    private void StyleField(TextBox field, int x, int y, int width)
    {
        field.SetBounds(x, y, width, 31);
        field.BackColor = Surface;
        field.ForeColor = Foreground;
        field.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(field);
    }

    private Button CreateButton(string caption, int x, int y, int width, int height, Color color, DialogResult result)
    {
        var button = new Button
        {
            Text = caption,
            Bounds = new Rectangle(x, y, width, height),
            BackColor = color,
            ForeColor = Foreground,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            DialogResult = result,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        Controls.Add(button);
        return button;
    }
}
