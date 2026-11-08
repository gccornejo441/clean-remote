using System.Drawing;

namespace CleanTVRemote;

internal sealed class TextInputDialog : Form
{
    private static Color Background => ThemePalette.Background;
    private static Color Surface => ThemePalette.Surface;
    private static Color Foreground => ThemePalette.Foreground;
    private static Color Muted => ThemePalette.Muted;
    private static Color Purple => ThemePalette.Purple;

    private readonly TextBox _text = new();

    public string EnteredText => _text.Text;

    public TextInputDialog()
    {
        Text = "Type on TV";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(390, 226);
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10);

        Controls.Add(new Label
        {
            Text = "TYPE ON YOUR TV",
            Bounds = new Rectangle(20, 16, 350, 28),
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Foreground
        });

        Controls.Add(new Label
        {
            Text = "First open a search or text field on your TV. Only native Samsung text fields may accept typing.",
            Bounds = new Rectangle(20, 51, 350, 47),
            ForeColor = Muted
        });

        _text.SetBounds(20, 106, 350, 32);
        _text.BackColor = Surface;
        _text.ForeColor = Foreground;
        _text.BorderStyle = BorderStyle.FixedSingle;
        _text.PlaceholderText = "Enter text to send";
        _text.MaxLength = 256;
        Controls.Add(_text);

        Controls.Add(new Label
        {
            Text = "Some apps, including Netflix and YouTube, may ignore this.",
            Bounds = new Rectangle(20, 143, 350, 28),
            ForeColor = Muted
        });

        var cancel = new Button
        {
            Text = "Cancel",
            Bounds = new Rectangle(166, 180, 94, 34),
            BackColor = Surface,
            ForeColor = Foreground,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };
        cancel.FlatAppearance.BorderSize = 0;
        Controls.Add(cancel);
        CancelButton = cancel;

        var send = new Button
        {
            Text = "Send text",
            Bounds = new Rectangle(270, 180, 100, 34),
            BackColor = Purple,
            ForeColor = Foreground,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK,
            Enabled = false
        };
        send.FlatAppearance.BorderSize = 0;
        Controls.Add(send);
        AcceptButton = send;

        _text.TextChanged += (_, _) => send.Enabled = !string.IsNullOrWhiteSpace(_text.Text);
        Shown += (_, _) => _text.Focus();
    }
}
