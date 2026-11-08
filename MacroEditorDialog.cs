using System.Drawing;

namespace CleanTVRemote;

internal sealed class MacroEditorDialog : Form
{
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HOME"] = "KEY_HOME",
        ["UP"] = "KEY_UP",
        ["DOWN"] = "KEY_DOWN",
        ["LEFT"] = "KEY_LEFT",
        ["RIGHT"] = "KEY_RIGHT",
        ["OK"] = "KEY_ENTER",
        ["ENTER"] = "KEY_ENTER",
        ["BACK"] = "KEY_RETURN",
        ["INPUT"] = "KEY_SOURCE",
        ["VOLUP"] = "KEY_VOLUP",
        ["VOLDOWN"] = "KEY_VOLDOWN",
        ["CHUP"] = "KEY_CHUP",
        ["CHDOWN"] = "KEY_CHDOWN",
        ["MUTE"] = "KEY_MUTE",
        ["PLAY"] = "KEY_PLAY",
        ["PAUSE"] = "KEY_PAUSE",
        ["REWIND"] = "KEY_REWIND",
        ["FORWARD"] = "KEY_FF",
        ["INFO"] = "KEY_INFO"
    };

    private readonly ListBox _saved = new();
    private readonly TextBox _name = new();
    private readonly TextBox _steps = new();

    public List<RemoteMacro> Result => _saved.Items.Cast<RemoteMacro>()
        .Select(macro => new RemoteMacro
        {
            Name = macro.Name,
            Steps = macro.Steps.Select(step => new RemoteMacroStep
            {
                Key = step.Key,
                DelayMs = step.DelayMs
            }).ToList()
        }).ToList();

    public static bool IsAllowedKey(string key) =>
        Allowed.Values.Contains(key, StringComparer.Ordinal);

    public MacroEditorDialog(IEnumerable<RemoteMacro> current)
    {
        Text = "Manage command macros";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(540, 408);
        BackColor = ThemePalette.Background;
        ForeColor = ThemePalette.Foreground;
        MaximizeBox = false;
        MinimizeBox = false;

        _saved.Bounds = new Rectangle(14, 14, 160, 285);
        _saved.BackColor = ThemePalette.Surface;
        _saved.ForeColor = ThemePalette.Foreground;
        foreach (RemoteMacro macro in current)
            _saved.Items.Add(new RemoteMacro
            {
                Name = macro.Name,
                Steps = macro.Steps.Select(step => new RemoteMacroStep
                {
                    Key = step.Key,
                    DelayMs = step.DelayMs
                }).ToList()
            });
        Controls.Add(_saved);

        Controls.Add(new Label
        {
            Text = "Macro name",
            ForeColor = ThemePalette.Muted,
            Bounds = new Rectangle(193, 14, 326, 23)
        });
        _name.Bounds = new Rectangle(193, 39, 323, 28);
        _name.MaxLength = 32;
        _name.BackColor = ThemePalette.Surface;
        _name.ForeColor = ThemePalette.Foreground;
        Controls.Add(_name);

        Controls.Add(new Label
        {
            Text = "Commands, comma or newline-separated",
            ForeColor = ThemePalette.Muted,
            Bounds = new Rectangle(193, 77, 323, 22)
        });
        _steps.Bounds = new Rectangle(193, 103, 323, 130);
        _steps.Multiline = true;
        _steps.ScrollBars = ScrollBars.Vertical;
        _steps.BackColor = ThemePalette.Surface;
        _steps.ForeColor = ThemePalette.Foreground;
        Controls.Add(_steps);

        Controls.Add(new Label
        {
            Text = "Example: HOME:300, RIGHT:150, RIGHT:150, OK",
            ForeColor = ThemePalette.Muted,
            Bounds = new Rectangle(193, 239, 323, 44)
        });

        _saved.SelectedIndexChanged += (_, _) =>
        {
            if (_saved.SelectedItem is not RemoteMacro chosen)
                return;

            _name.Text = chosen.Name;
            _steps.Text = string.Join(", ", chosen.Steps.Select(step =>
                $"{DisplayName(step.Key)}:{step.DelayMs}"));
        };

        var save = MakeButton("Save macro", 193, 292, 130, 32);
        save.Click += (_, _) => SaveMacro();
        var remove = MakeButton("Remove", 333, 292, 90, 32);
        remove.Click += (_, _) =>
        {
            if (_saved.SelectedIndex >= 0)
                _saved.Items.RemoveAt(_saved.SelectedIndex);
            _name.Clear();
            _steps.Clear();
        };
        var newMacro = MakeButton("New", 433, 292, 83, 32);
        newMacro.Click += (_, _) =>
        {
            _saved.ClearSelected();
            _name.Clear();
            _steps.Clear();
        };

        Controls.Add(new Label
        {
            Text = "Up to 8 macros, 12 steps each, delays 0–1500 ms. UI behavior varies by TV.",
            Bounds = new Rectangle(14, 341, 507, 27),
            ForeColor = ThemePalette.Muted
        });
        var done = MakeButton("Done", 432, 371, 86, 30);
        done.DialogResult = DialogResult.OK;
        AcceptButton = done;
        var cancel = MakeButton("Cancel", 336, 371, 86, 30);
        cancel.DialogResult = DialogResult.Cancel;
        CancelButton = cancel;
    }

    private Button MakeButton(string text, int x, int y, int width, int height)
    {
        var button = new Button
        {
            Text = text,
            Bounds = new Rectangle(x, y, width, height),
            BackColor = ThemePalette.Surface,
            ForeColor = ThemePalette.Foreground,
            FlatStyle = FlatStyle.Flat
        };
        Controls.Add(button);
        return button;
    }

    private static string DisplayName(string key) =>
        Allowed.FirstOrDefault(pair => pair.Value == key).Key ?? key;

    private void SaveMacro()
    {
        string name = _name.Text.Trim();
        string[] tokens = _steps.Text.Split([',', '\n', '\r', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (name.Length == 0 || tokens.Length is < 1 or > 12)
        {
            MessageBox.Show(this, "Use a name and between 1 and 12 commands.", "Invalid macro");
            return;
        }

        var steps = new List<RemoteMacroStep>();
        foreach (string token in tokens)
        {
            string[] parts = token.Split(':', 2, StringSplitOptions.TrimEntries);
            if (!Allowed.TryGetValue(parts[0], out string? command))
            {
                MessageBox.Show(this,
                    $"Unsupported command: {parts[0]}. Supported: {string.Join(", ", Allowed.Keys)}",
                    "Invalid macro");
                return;
            }

            int delay = 0;
            if (parts.Length == 2 && (!int.TryParse(parts[1], out delay) || delay is < 0 or > 1500))
            {
                MessageBox.Show(this, "Delay must be between 0 and 1500 milliseconds.", "Invalid macro");
                return;
            }

            steps.Add(new RemoteMacroStep { Key = command, DelayMs = delay });
        }

        int selectedIndex = _saved.SelectedIndex;
        if (selectedIndex < 0 && _saved.Items.Count >= 8 ||
            _saved.Items.Cast<RemoteMacro>().Where((_, i) => i != selectedIndex)
                .Any(macro => string.Equals(macro.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "Use a unique name and at most 8 saved macros.", "Invalid macro");
            return;
        }

        var saved = new RemoteMacro { Name = name, Steps = steps };
        if (selectedIndex >= 0)
            _saved.Items[selectedIndex] = saved;
        else
            _saved.Items.Add(saved);

        _saved.SelectedItem = saved;
    }
}
