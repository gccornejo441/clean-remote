using System.Drawing;

namespace CleanTVRemote;

internal sealed class FavoriteAppsDialog : Form
{
    private static Color Background => ThemePalette.Background;
    private static Color Foreground => ThemePalette.Foreground;
    private static Color Surface => ThemePalette.Surface;

    private readonly ListBox _apps = new();
    private readonly TextBox _name = new();
    private readonly TextBox _appId = new();

    public List<AppShortcut> Result =>
        _apps.Items.Cast<AppShortcut>().Select(app => new AppShortcut
        {
            Name = app.Name,
            AppId = app.AppId
        }).ToList();

    public FavoriteAppsDialog(IEnumerable<AppShortcut> current)
    {
        Text = "Favorite Samsung apps";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(435, 345);
        BackColor = Background;
        ForeColor = Foreground;
        MaximizeBox = false;
        MinimizeBox = false;

        _apps.Bounds = new Rectangle(16, 15, 400, 145);
        _apps.BackColor = Surface;
        _apps.ForeColor = Foreground;
        foreach (AppShortcut app in current)
            _apps.Items.Add(new AppShortcut { Name = app.Name, AppId = app.AppId });
        Controls.Add(_apps);

        Controls.Add(new Label { Text = "Shortcut name", Bounds = new Rectangle(16, 169, 140, 22) });
        Controls.Add(new Label { Text = "Numeric Tizen app ID", Bounds = new Rectangle(176, 169, 238, 22) });
        _name.Bounds = new Rectangle(16, 194, 150, 30);
        _name.BackColor = Surface;
        _name.ForeColor = Foreground;
        _name.MaxLength = 28;
        _name.PlaceholderText = "YouTube";
        _appId.Bounds = new Rectangle(176, 194, 238, 30);
        _appId.BackColor = Surface;
        _appId.ForeColor = Foreground;
        _appId.MaxLength = 32;
        _appId.PlaceholderText = "Enter your TV's app ID";
        Controls.Add(_name);
        Controls.Add(_appId);

        var add = new Button { Text = "Add", Bounds = new Rectangle(16, 238, 92, 34) };
        add.Click += (_, _) =>
        {
            string name = _name.Text.Trim();
            string appId = _appId.Text.Trim();
            if (name.Length == 0 || appId.Length < 3 || !appId.All(char.IsAsciiDigit) ||
                _apps.Items.Count >= 8 ||
                _apps.Items.Cast<AppShortcut>().Any(app => app.AppId == appId))
            {
                MessageBox.Show(this, "Provide a unique numeric app ID (3–32 digits), a name, and at most 8 favorites.",
                    "Invalid favorite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _apps.Items.Add(new AppShortcut { Name = name, AppId = appId });
            _name.Clear();
            _appId.Clear();
        };
        Controls.Add(add);

        var remove = new Button { Text = "Remove selected", Bounds = new Rectangle(116, 238, 145, 34) };
        remove.Click += (_, _) =>
        {
            if (_apps.SelectedIndex >= 0)
                _apps.Items.RemoveAt(_apps.SelectedIndex);
        };
        Controls.Add(remove);

        Controls.Add(new Label
        {
            Text = "App IDs vary by Samsung model; launching cannot be guaranteed.",
            Bounds = new Rectangle(16, 281, 400, 24)
        });

        var save = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(322, 306, 92, 32)
        };
        Controls.Add(save);
        AcceptButton = save;
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Bounds = new Rectangle(220, 306, 92, 32)
        };
        Controls.Add(cancel);
        CancelButton = cancel;

        foreach (Button button in Controls.OfType<Button>())
        {
            button.BackColor = Surface;
            button.ForeColor = Foreground;
            button.FlatStyle = FlatStyle.Flat;
        }
    }
}
