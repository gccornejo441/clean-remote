using System.Drawing;

namespace CleanTVRemote;

internal sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(17, 24, 34);
    private static readonly Color Surface = Color.FromArgb(35, 46, 61);
    private static readonly Color Accent = Color.FromArgb(0, 155, 171);
    private static readonly Color Foreground = Color.FromArgb(244, 247, 250);
    private static readonly Color Muted = Color.FromArgb(169, 185, 199);

    private readonly SamsungRemoteClient _remote = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly TextBox _tvIp = new();
    private readonly TextBox _mac = new();
    private readonly ComboBox _mode = new();
    private readonly Label _status = new();
    private readonly Button _connect = new();

    public MainForm()
    {
        Text = "Clean TV Remote";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(492, 766);
        MinimumSize = new Size(492, 760);
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Font;

        _remote.StatusChanged += message => SetStatus(message);
        _remote.PairingTokenChanged += token => UpdateToken(token);

        BuildInterface();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _remote.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildInterface()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 12, 18, 12),
            ColumnCount = 1,
            RowCount = 0,
            AutoScroll = true,
            BackColor = Background
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(content);

        var title = new Label
        {
            Text = "CLEAN TV REMOTE",
            Font = new Font("Segoe UI Semibold", 17, FontStyle.Bold),
            ForeColor = Foreground,
            TextAlign = ContentAlignment.MiddleLeft
        };
        AddRow(content, title, 52);

        _tvIp.Text = _settings.TvIp;
        _tvIp.PlaceholderText = "192.168.1.50";
        _mac.Text = _settings.MacAddress;
        _mac.PlaceholderText = "Optional for Wake-on-LAN";

        var addresses = MakeColumns(2);
        addresses.Controls.Add(MakeField("TV IP ADDRESS", _tvIp), 0, 0);
        addresses.Controls.Add(MakeField("TV MAC ADDRESS", _mac), 1, 0);
        AddRow(content, addresses, 76);

        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(["Secure (8002)", "Legacy (8001)"]);
        _mode.SelectedIndex = _settings.Secure ? 0 : 1;
        _mode.BackColor = Surface;
        _mode.ForeColor = Foreground;
        _mode.FlatStyle = FlatStyle.Flat;
        _mode.Dock = DockStyle.Fill;
        _mode.Margin = new Padding(5, 6, 5, 6);

        _connect.Text = "CONNECT";
        StyleButton(_connect, true);
        _connect.Click += async (_, _) =>
        {
            if (_remote.IsConnected)
            {
                _remote.Disconnect();
                SetStatus("Disconnected");
            }
            else
            {
                await RunAsync(ConnectAsync);
            }
        };

        var connectionRow = MakeColumns(2);
        connectionRow.Controls.Add(_mode, 0, 0);
        connectionRow.Controls.Add(_connect, 1, 0);
        AddRow(content, connectionRow, 53);

        _status.ForeColor = Muted;
        _status.Text = "Enter your TV's IP address, then connect.";
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.AutoEllipsis = true;
        AddRow(content, _status, 46);

        var directionalPad = MakeColumns(3, 3);
        directionalPad.Controls.Add(MakeButton("▲", "KEY_UP"), 1, 0);
        directionalPad.Controls.Add(MakeButton("◀", "KEY_LEFT"), 0, 1);
        directionalPad.Controls.Add(MakeButton("OK", "KEY_ENTER", true), 1, 1);
        directionalPad.Controls.Add(MakeButton("▶", "KEY_RIGHT"), 2, 1);
        directionalPad.Controls.Add(MakeButton("▼", "KEY_DOWN"), 1, 2);
        AddRow(content, directionalPad, 205);

        AddRow(content, ButtonRow(
            ("BACK", "KEY_RETURN"),
            ("HOME", "KEY_HOME"),
            ("SOURCE", "KEY_SOURCE")), 62);

        AddRow(content, ButtonRow(
            ("VOL −", "KEY_VOLDOWN"),
            ("MUTE", "KEY_MUTE"),
            ("VOL +", "KEY_VOLUP")), 62);

        AddRow(content, ButtonRow(
            ("CH −", "KEY_CHDOWN"),
            ("PLAY", "KEY_PLAY"),
            ("CH +", "KEY_CHUP")), 62);

        var powerRow = MakeColumns(2);
        var powerOff = MakeButton("POWER OFF", "KEY_POWEROFF");
        powerRow.Controls.Add(powerOff, 0, 0);
        var wake = new Button { Text = "WAKE TV" };
        StyleButton(wake, true);
        wake.Click += async (_, _) => await RunAsync(async () =>
        {
            await SamsungRemoteClient.WakeAsync(_mac.Text.Trim());
            SetStatus("Wake packet sent. Actual power-on depends on TV settings and network.");
        });
        powerRow.Controls.Add(wake, 1, 0);
        AddRow(content, powerRow, 62);

        var footer = new LinkLabel
        {
            Text = "Forget pairing and ask the TV again",
            LinkColor = Muted,
            ActiveLinkColor = Foreground,
            LinkBehavior = LinkBehavior.HoverUnderline,
            TextAlign = ContentAlignment.MiddleCenter
        };
        footer.LinkClicked += (_, _) =>
        {
            _remote.Disconnect();
            _settings.Token = "";
            try
            {
                _settings.Save();
                SetStatus("Pairing cleared. Click Connect and approve the TV prompt.");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
        };
        AddRow(content, footer, 35);
    }

    private async Task ConnectAsync()
    {
        string ip = _tvIp.Text.Trim();
        string existingToken = string.Equals(_settings.TvIp, ip, StringComparison.OrdinalIgnoreCase)
            ? _settings.Token
            : "";

        _settings.TvIp = ip;
        _settings.MacAddress = _mac.Text.Trim();
        _settings.Secure = _mode.SelectedIndex == 0;
        _settings.Token = existingToken;
        _settings.Save();

        _connect.Enabled = false;
        try
        {
            string? newToken = await _remote.ConnectAsync(ip, _settings.Secure, existingToken);
            if (!string.IsNullOrWhiteSpace(newToken))
            {
                UpdateToken(newToken);
            }
        }
        finally
        {
            _connect.Enabled = true;
            _connect.Text = _remote.IsConnected ? "DISCONNECT" : "CONNECT";
        }
    }

    private void UpdateToken(string token)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateToken(token)));
            return;
        }

        _settings.Token = token;
        try
        {
            _settings.Save();
        }
        catch (IOException ex)
        {
            SetStatus($"Connected, but pairing could not be saved: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            SetStatus($"Connected, but pairing could not be saved: {ex.Message}");
        }
    }

    private void SetStatus(string message)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetStatus(message)));
            return;
        }

        _status.Text = message;
        if (_connect.Enabled)
        {
            _connect.Text = _remote.IsConnected ? "DISCONNECT" : "CONNECT";
        }
    }

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private Button MakeButton(string label, string key, bool primary = false)
    {
        var button = new Button { Text = label };
        StyleButton(button, primary);
        button.Click += async (_, _) => await RunAsync(() => _remote.SendKeyAsync(key));
        return button;
    }

    private static Control MakeField(string label, TextBox field)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = label,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(5, 0, 5, 0)
        }, 0, 0);
        field.Dock = DockStyle.Fill;
        field.Margin = new Padding(5);
        field.BackColor = Surface;
        field.ForeColor = Foreground;
        field.BorderStyle = BorderStyle.FixedSingle;
        panel.Controls.Add(field, 0, 1);
        return panel;
    }

    private TableLayoutPanel ButtonRow(params (string Title, string Key)[] actions)
    {
        var row = MakeColumns(actions.Length);
        for (int i = 0; i < actions.Length; i++)
        {
            row.Controls.Add(MakeButton(actions[i].Title, actions[i].Key), i, 0);
        }
        return row;
    }

    private static TableLayoutPanel MakeColumns(int columns, int rows = 1)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            RowCount = rows,
            Margin = Padding.Empty,
            BackColor = Background
        };
        for (int i = 0; i < columns; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        }
        for (int i = 0; i < rows; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        }
        return grid;
    }

    private static void AddRow(TableLayoutPanel parent, Control child, int height)
    {
        int index = parent.RowCount;
        parent.RowCount++;
        parent.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        child.Dock = DockStyle.Fill;
        parent.Controls.Add(child, 0, index);
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(5);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = primary ? Accent : Surface;
        button.ForeColor = Foreground;
        button.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }
}
