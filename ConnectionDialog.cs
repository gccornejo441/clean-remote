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
    private readonly ComboBox _profile = new();
    private readonly AppSettings _settings;
    private TvProfile _editingProfile;
    private bool _loadingProfile;

    public string TvIp => _ip.Text.Trim();
    public string Mac => _mac.Text.Trim();
    public bool Secure => _mode.SelectedIndex == 0;
    public TvProfile SelectedProfile => _editingProfile;

    public ConnectionDialog(AppSettings settings, bool connected)
    {
        _settings = settings;
        _editingProfile = settings.ActiveProfile;
        Text = "TV connection";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(380, 438);
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10);

        AddText("CONNECT TO SAMSUNG", 23, 16, 334, 32, 17, Foreground, true);
        AddText("Your computer and TV must be on the same network.", 24, 53, 339, 28, 9, Muted);
        AddText("TV PROFILE", 24, 87, 220, 19, 9, Muted, true);
        _profile.SetBounds(24, 110, 236, 31);
        _profile.DropDownStyle = ComboBoxStyle.DropDownList;
        _profile.DisplayMember = nameof(TvProfile.Name);
        _profile.Items.AddRange(settings.Profiles.Cast<object>().ToArray());
        _profile.SelectedItem = _editingProfile;
        _profile.BackColor = Surface;
        _profile.ForeColor = Foreground;
        _profile.FlatStyle = FlatStyle.Flat;
        _profile.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingProfile || _profile.SelectedItem is not TvProfile selected) return;
            SaveFieldsToProfile(_editingProfile);
            _editingProfile = selected;
            LoadProfileFields(selected);
        };
        Controls.Add(_profile);

        var addProfile = CreateButton("Add profile", 270, 110, 84, 31, Surface, DialogResult.None);
        addProfile.Click += (_, _) => AddProfile();

        AddText("TV IP ADDRESS", 24, 153, 330, 19, 9, Muted, true);
        StyleField(_ip, 24, 177, 330);
        _ip.PlaceholderText = "192.168.1.50";

        AddText("TV MAC ADDRESS · OPTIONAL FOR WAKE", 24, 220, 330, 19, 9, Muted, true);
        StyleField(_mac, 24, 244, 330);
        _mac.PlaceholderText = "AA:BB:CC:DD:EE:FF";

        AddText("CONNECTION MODE", 24, 287, 330, 19, 9, Muted, true);
        _mode.SetBounds(24, 311, 330, 32);
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(["Secure (port 8002)", "Legacy (port 8001)"]);
        _mode.FlatStyle = FlatStyle.Flat;
        _mode.BackColor = Surface;
        _mode.ForeColor = Foreground;
        Controls.Add(_mode);

        LoadProfileFields(_editingProfile);

        var connect = CreateButton("Save & connect", 24, 356, 330, 38, Purple, DialogResult.None);
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

        var forget = CreateButton("Forget pairing", 24, 403, 112, 28, Surface, DialogResult.Retry);
        var disconnect = CreateButton("Disconnect", 145, 403, 104, 28, Surface, DialogResult.Abort);
        disconnect.Enabled = connected;
        var cancel = CreateButton("Cancel", 258, 403, 96, 28, Surface, DialogResult.Cancel);
        CancelButton = cancel;
    }

    private void AddProfile()
    {
        using var prompt = new Form
        {
            Text = "Add TV profile",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(300, 126),
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Background,
            ForeColor = Foreground,
            Font = Font
        };
        prompt.Controls.Add(new Label { Text = "Profile name", ForeColor = Foreground, Bounds = new Rectangle(16, 12, 260, 22) });
        var name = new TextBox { Bounds = new Rectangle(16, 38, 268, 28), Text = "TV" };
        prompt.Controls.Add(name);
        var save = new Button { Text = "Add", Bounds = new Rectangle(184, 82, 100, 30), DialogResult = DialogResult.OK };
        prompt.Controls.Add(save);
        prompt.AcceptButton = save;
        if (prompt.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(name.Text)) return;

        SaveFieldsToProfile(_editingProfile);
        var profile = new TvProfile { Name = name.Text.Trim() };
        _settings.Profiles.Add(profile);
        _profile.Items.Add(profile);
        _profile.SelectedItem = profile;
    }

    private void LoadProfileFields(TvProfile profile)
    {
        _loadingProfile = true;
        _ip.Text = profile.TvIp;
        _mac.Text = profile.MacAddress;
        _mode.SelectedIndex = profile.Secure ? 0 : 1;
        _loadingProfile = false;
    }

    private void SaveFieldsToProfile(TvProfile profile)
    {
        profile.TvIp = TvIp;
        profile.MacAddress = Mac;
        profile.Secure = Secure;
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
