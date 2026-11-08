using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CleanTVRemote;

internal sealed class MainForm : Form
{
    private const int HotkeyMessage = 0x0312;
    private const uint HotkeyModifiers = 0x0001 | 0x0002 | 0x0004 | 0x4000;
    private bool _hotkeysRegistered;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
    private static readonly Color Background = Color.FromArgb(19, 18, 24);
    private static readonly Color Surface = Color.FromArgb(44, 42, 55);
    private static readonly Color Foreground = Color.FromArgb(242, 240, 249);

    private readonly SamsungRemoteClient _remote = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly RemoteSurface _surface = new();
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private bool _connecting;
    private bool _powerRequestInProgress;
    private bool _manualDisconnected;
    private bool _repeatCommandInFlight;
    private bool _closing;
    private readonly System.Windows.Forms.Timer _reconnectTimer = new() { Interval = 2000 };
    private readonly System.Windows.Forms.Timer _sleepTimer = new() { Interval = 1000 };
    private DateTimeOffset? _sleepDeadline;
    private CancellationTokenSource? _connectCancellation;
    private DateTimeOffset _nextReconnectAt = DateTimeOffset.MinValue;
    private DateTimeOffset? _lastConnectAt;
    private TimeSpan? _lastConnectDuration;
    private string _lastConnectionError = "None";
    private int _reconnectDelaySeconds = 2;
    private DateTimeOffset _lastPowerRequest = DateTimeOffset.MinValue;

    public MainForm()
    {
        Text = "Clean TV Remote";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(365, 798);
        MinimumSize = new Size(342, 746);
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        TopMost = _settings.AlwaysOnTop;

        _trayMenu = new ContextMenuStrip
        {
            BackColor = Surface,
            ForeColor = Foreground,
            ShowImageMargin = false
        };
        _trayMenu.Items.Add("Open Clean TV Remote", null, (_, _) => RestoreFromTray());
        _trayMenu.Items.Add("Exit", null, (_, _) => Close());
        _trayIcon = new NotifyIcon
        {
            Icon = Icon,
            Text = "Clean TV Remote",
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                RestoreFromTray();
        };

        _surface.Dock = DockStyle.Fill;
        _surface.SetTvName(_settings.ActiveProfile.Name);
        _surface.SetConnection(string.IsNullOrWhiteSpace(_settings.TvIp)
            ? $"Tap {_settings.ActiveProfile.Name} to set up your TV"
            : $"Ready to connect · Tap {_settings.ActiveProfile.Name}", false);
        _surface.ActionRequested += async action => await HandleActionAsync(action);
        _surface.RepeatActionRequested += async action => await HandleRepeatAsync(action);
        Controls.Add(_surface);
        _reconnectTimer.Tick += async (_, _) =>
        {
            if (_closing || _manualDisconnected || _connecting || _remote.IsConnected ||
                string.IsNullOrWhiteSpace(_settings.TvIp) || DateTimeOffset.UtcNow < _nextReconnectAt)
                return;

            await RunAsync(ConnectAsync);
        };
        _reconnectTimer.Start();

        _sleepTimer.Tick += async (_, _) =>
        {
            if (_closing || _sleepDeadline is null || DateTimeOffset.UtcNow < _sleepDeadline.Value)
                return;

            _sleepDeadline = null;
            _sleepTimer.Stop();

            if (!_remote.IsConnected)
            {
                SetStatus("Sleep timer expired · TV is disconnected; no power command sent");
                return;
            }

            await RunAsync(async () =>
            {
                await _remote.SendKeyAsync("KEY_POWER");
                SetStatus("Sleep timer power toggle sent · TV state not confirmed");
            });
        };

        _remote.StatusChanged += SetStatus;
        _remote.PairingTokenChanged += UpdateToken;
        Shown += async (_, _) =>
        {
            _surface.Focus();
            if (_settings.GlobalHotkeysEnabled && !TryRegisterGlobalHotkeys())
                SetStatus("Global hotkeys unavailable · Another app may use these shortcuts");
            if (!string.IsNullOrWhiteSpace(_settings.TvIp))
                await RunAsync(ConnectAsync);
        };
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == HotkeyMessage && _hotkeysRegistered)
        {
            string? action = m.WParam.ToInt32() switch
            {
                6101 => "vol_up",
                6102 => "vol_down",
                6103 => "mute",
                6104 => "play",
                _ => null
            };
            if (action is not null)
                _ = HandleActionAsync(action);

            return;
        }

        base.WndProc(ref m);
    }

    private bool TryRegisterGlobalHotkeys()
    {
        if (_hotkeysRegistered)
            return true;

        Keys[] keys = [Keys.F9, Keys.F10, Keys.F11, Keys.F12];
        for (int i = 0; i < keys.Length; i++)
        {
            if (!RegisterHotKey(Handle, 6101 + i, HotkeyModifiers, (uint)keys[i]))
            {
                for (int j = 0; j < i; j++)
                    UnregisterHotKey(Handle, 6101 + j);
                return false;
            }
        }

        _hotkeysRegistered = true;
        return true;
    }

    private void UnregisterGlobalHotkeys()
    {
        if (!_hotkeysRegistered)
            return;

        for (int i = 0; i < 4; i++)
            UnregisterHotKey(Handle, 6101 + i);

        _hotkeysRegistered = false;
    }

    private void SetGlobalHotkeys(bool enable)
    {
        if (enable && !TryRegisterGlobalHotkeys())
        {
            SetStatus("Unable to enable hotkeys · One or more shortcuts are in use");
            return;
        }

        if (!enable)
            UnregisterGlobalHotkeys();

        _settings.GlobalHotkeysEnabled = enable;
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Could not save global hotkey preference.", ex);
            SetStatus($"Could not save hotkey preference: {ex.Message}");
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized && _trayIcon is not null && !Disposing)
        {
            _trayIcon.Visible = true;
            Hide();
        }
    }

    private void RestoreFromTray()
    {
        if (IsDisposed || Disposing)
            return;

        Show();
        WindowState = FormWindowState.Normal;
        if (_trayIcon is not null)
            _trayIcon.Visible = false;
        Activate();
        _surface.Focus();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _closing = true;
        UnregisterGlobalHotkeys();
        _reconnectTimer.Stop();
        _reconnectTimer.Dispose();
        _sleepDeadline = null;
        _sleepTimer.Stop();
        _sleepTimer.Dispose();
        _connectCancellation?.Cancel();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _trayMenu?.Dispose();
        _remote.Dispose();
        base.OnFormClosed(e);
    }

    private async Task HandleActionAsync(string action)
    {
        switch (action)
        {
            case "device":
                await ShowSettingsAsync();
                return;
            case "help":
                ShowHelp();
                return;
            case "options":
                ShowOptions();
                return;
            case "keyboard":
                await ShowTextInputAsync();
                return;
            case "power":
                await HandlePowerAsync();
                return;
            case "apps":
                ShowAppsMenu();
                return;
            case "netflix":
                await RunAsync(async () =>
                {
                    await _remote.LaunchAppAsync("3201907018807");
                    SetStatus("Netflix launch requested · TV support varies");
                });
                return;
        }

        string? key = action switch
        {
            "input" => "KEY_SOURCE",
            "up" => "KEY_UP",
            "down" => "KEY_DOWN",
            "left" => "KEY_LEFT",
            "right" => "KEY_RIGHT",
            "ok" => "KEY_ENTER",
            "back" => "KEY_RETURN",
            "home" => "KEY_HOME",
            "vol_up" => "KEY_VOLUP",
            "vol_down" => "KEY_VOLDOWN",
            "mute" => "KEY_MUTE",
            "info" => "KEY_INFO",
            "ch_up" => "KEY_CHUP",
            "ch_down" => "KEY_CHDOWN",
            "rewind" => "KEY_REWIND",
            "play" => "KEY_PLAY",
            "forward" => "KEY_FF",
            _ => null
        };
        if (key != null) await SendAsync(key);
    }

    private async Task HandlePowerAsync()
    {
        if (_powerRequestInProgress || DateTimeOffset.UtcNow - _lastPowerRequest < TimeSpan.FromSeconds(2))
            return;

        _powerRequestInProgress = true;
        try
        {
            if (_remote.IsConnected)
            {
                await RunAsync(async () =>
                {
                    await _remote.SendKeyAsync("KEY_POWER");
                    AppLog.Info("Samsung power toggle command sent. TV response is not confirmed.");
                    SetStatus("Power toggle sent · Check the TV");
                });
            }
            else
            {
                await WakeAsync();
            }

            _lastPowerRequest = DateTimeOffset.UtcNow;
        }
        finally
        {
            _powerRequestInProgress = false;
        }
    }

    private Task SendAsync(string key) => RunAsync(() => _remote.SendKeyAsync(key));

    private async Task HandleRepeatAsync(string action)
    {
        if (_repeatCommandInFlight || !_remote.IsConnected || _closing)
            return;

        string? key = action switch
        {
            "vol_up" => "KEY_VOLUP",
            "vol_down" => "KEY_VOLDOWN",
            "ch_up" => "KEY_CHUP",
            "ch_down" => "KEY_CHDOWN",
            _ => null
        };
        if (key is null)
            return;

        _repeatCommandInFlight = true;
        try
        {
            await SendAsync(key);
        }
        finally
        {
            _repeatCommandInFlight = false;
        }
    }

    private void DisconnectManually()
    {
        _manualDisconnected = true;
        _connectCancellation?.Cancel();
        _remote.Disconnect();
    }

    private async Task ManualReconnectAsync()
    {
        _manualDisconnected = false;
        _nextReconnectAt = DateTimeOffset.MinValue;
        await RunAsync(ConnectAsync);
    }

    private async Task ShowSettingsAsync()
    {
        using var dialog = new ConnectionDialog(_settings, _remote.IsConnected);
        DialogResult result = dialog.ShowDialog(this);
        _surface.Focus();
        if (result == DialogResult.Cancel) return;

        if (result == DialogResult.Abort)
        {
            DisconnectManually();
            SetStatus($"Disconnected · Tap {_settings.ActiveProfile.Name} to connect");
            return;
        }

        if (result == DialogResult.Retry)
        {
            ClearPairing();
            return;
        }

        if (result != DialogResult.OK) return;

        _manualDisconnected = true;
        _connectCancellation?.Cancel();
        _remote.Disconnect();
        _settings.ActiveProfileId = dialog.SelectedProfile.Id;
        _surface.SetTvName(dialog.SelectedProfile.Name);
        string ip = dialog.TvIp;
        bool secure = dialog.Secure;
        if (!string.Equals(_settings.TvIp, ip, StringComparison.OrdinalIgnoreCase) || _settings.Secure != secure)
            _settings.Token = "";
        _settings.TvIp = ip;
        _settings.MacAddress = dialog.Mac;
        _settings.Secure = secure;
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Could not save TV settings.", ex);
            SetStatus($"Could not save TV settings: {ex.Message}");
            return;
        }
        await ManualReconnectAsync();
    }

    private async Task ConnectAsync()
    {
        if (_connecting) return;
        if (string.IsNullOrWhiteSpace(_settings.TvIp))
        {
            await ShowSettingsAsync();
            return;
        }
        _connecting = true;
        _lastConnectAt = DateTimeOffset.UtcNow;
        var elapsed = Stopwatch.StartNew();
        using var cancellation = new CancellationTokenSource();
        _connectCancellation = cancellation;
        try
        {
            string? newToken = await _remote.ConnectAsync(_settings.TvIp, _settings.Secure, _settings.Token, cancellation.Token);
            if (cancellation.IsCancellationRequested || _manualDisconnected || _closing)
                return;

            if (!string.IsNullOrWhiteSpace(newToken))
                UpdateToken(newToken);

            _reconnectDelaySeconds = 2;
            _lastConnectionError = "None";
            _nextReconnectAt = DateTimeOffset.MinValue;
            SetStatus($"Connected · {_settings.ActiveProfile.Name}");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _lastConnectionError = ex.Message;
            _nextReconnectAt = DateTimeOffset.UtcNow.AddSeconds(_reconnectDelaySeconds);
            _reconnectDelaySeconds = Math.Min(30, _reconnectDelaySeconds * 2);
            throw;
        }
        finally
        {
            elapsed.Stop();
            _lastConnectDuration = elapsed.Elapsed;
            if (ReferenceEquals(_connectCancellation, cancellation))
                _connectCancellation = null;
            _connecting = false;
        }
    }

    private async Task WakeAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.MacAddress))
        {
            SetStatus("Add the TV MAC address in connection settings to wake it");
            await ShowSettingsAsync();
            return;
        }
        await RunAsync(async () =>
        {
            await SamsungRemoteClient.WakeAsync(_settings.MacAddress);
            AppLog.Info("Wake-on-LAN magic packet broadcast for configured TV.");
            SetStatus("Wake packet sent · Power-on requires TV Wake-on-LAN support");
        });
    }

    private void ClearPairing()
    {
        if (MessageBox.Show(this, "Forget this TV's saved pairing token? You will need to approve the connection again on the TV.",
            "Forget pairing", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        DisconnectManually();
        _settings.Token = "";
        try
        {
            _settings.Save();
            SetStatus("Pairing cleared · Connect again to approve access");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Could not clear the TV pairing token.", ex);
            SetStatus($"Could not clear pairing: {ex.Message}");
        }
    }

    private void ShowOptions()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Surface,
            ForeColor = Foreground,
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 10)
        };
        menu.Items.Add("Connection settings", null, async (_, _) => await ShowSettingsAsync());
        menu.Items.Add("Reconnect", null, async (_, _) => await ManualReconnectAsync());
        menu.Items.Add("Wake TV", null, async (_, _) => await WakeAsync());
        menu.Items.Add("Open diagnostic log", null, (_, _) => OpenDiagnosticLog());
        menu.Items.Add("Connection diagnostics", null, (_, _) => ShowConnectionDiagnostics());
        menu.Items.Add("Favorite apps", null, (_, _) => ConfigureFavoriteApps());
        menu.Items.Add("Numeric channel keypad", null, (_, _) => ShowNumericKeypad());
        menu.Items.Add(_sleepDeadline is null
            ? "Set sleep timer"
            : $"Sleep timer · {Math.Max(1, (int)Math.Ceiling((_sleepDeadline.Value - DateTimeOffset.UtcNow).TotalMinutes))} minutes remaining",
            null, (_, _) => ShowSleepTimerDialog());
        if (_sleepDeadline is not null)
            menu.Items.Add("Cancel sleep timer", null, (_, _) =>
            {
                _sleepDeadline = null;
                _sleepTimer.Stop();
                SetStatus("Sleep timer canceled");
            });
        menu.Items.Add("Keyboard shortcuts", null, (_, _) => ShowKeyboardHelp());
        var hotkeys = new ToolStripMenuItem("Global hotkeys (Ctrl+Alt+Shift+F9–F12)")
        {
            Checked = _hotkeysRegistered
        };
        hotkeys.Click += (_, _) => SetGlobalHotkeys(!_hotkeysRegistered);
        menu.Items.Add(hotkeys);
        var alwaysOnTop = new ToolStripMenuItem("Always on top")
        {
            Checked = TopMost,
            CheckOnClick = true
        };
        alwaysOnTop.CheckedChanged += (_, _) => SetAlwaysOnTop(alwaysOnTop.Checked);
        menu.Items.Add(alwaysOnTop);
        menu.Items.Add("Samsung Menu", null, async (_, _) => await SendAsync("KEY_MENU"));
        menu.Items.Add("Pause playback", null, async (_, _) => await SendAsync("KEY_PAUSE"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Disconnect", null, (_, _) =>
        {
            DisconnectManually();
            SetStatus($"Disconnected · Tap {_settings.ActiveProfile.Name} to reconnect");
        });
        menu.Items.Add("Forget pairing", null, (_, _) => ClearPairing());
        menu.Show(_surface, new Point(Math.Max(10, _surface.Width / 2 - 80), Math.Max(30, _surface.Height / 2)));
    }

    private void ShowAppsMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Surface,
            ForeColor = Foreground,
            ShowImageMargin = false
        };
        menu.Items.Add("Samsung Home", null, async (_, _) => await SendAsync("KEY_HOME"));
        menu.Items.Add("Netflix", null, async (_, _) => await LaunchFavoriteAsync("3201907018807", "Netflix"));

        foreach (AppShortcut shortcut in _settings.ActiveProfile.FavoriteApps ?? [])
        {
            string id = shortcut.AppId;
            string name = shortcut.Name;
            menu.Items.Add(name, null, async (_, _) => await LaunchFavoriteAsync(id, name));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Manage favorites", null, (_, _) => ConfigureFavoriteApps());
        menu.Show(_surface, new Point(Math.Max(10, _surface.Width / 2 - 70), 150));
    }

    private Task LaunchFavoriteAsync(string appId, string name) => RunAsync(async () =>
    {
        await _remote.LaunchAppAsync(appId);
        SetStatus($"{name} launch requested · TV response not confirmed");
    });

    private void ConfigureFavoriteApps()
    {
        using var dialog = new FavoriteAppsDialog(_settings.ActiveProfile.FavoriteApps ?? []);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _settings.ActiveProfile.FavoriteApps = dialog.Result;
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Could not save favorite apps.", ex);
            SetStatus($"Could not save favorites: {ex.Message}");
        }
        _surface.Focus();
    }

    private void ShowNumericKeypad()
    {
        using var dialog = new Form
        {
            Text = "Numeric channel keypad",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(260, 382),
            BackColor = Background,
            ForeColor = Foreground,
            MaximizeBox = false,
            MinimizeBox = false
        };

        string[] digits = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "", "0", "Enter"];
        for (int i = 0; i < digits.Length; i++)
        {
            string digit = digits[i];
            if (digit.Length == 0) continue;
            var button = new Button
            {
                Text = digit,
                Bounds = new Rectangle(18 + (i % 3) * 76, 16 + (i / 3) * 66, 70, 58),
                BackColor = Surface,
                ForeColor = Foreground,
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += async (_, _) =>
                await SendAsync(digit == "Enter" ? "KEY_ENTER" : $"KEY_{digit}");
            dialog.Controls.Add(button);
        }

        dialog.Controls.Add(new Label
        {
            Text = "Some TV apps and inputs ignore numeric keys.",
            Bounds = new Rectangle(18, 286, 224, 39),
            ForeColor = Foreground
        });
        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(145, 334, 97, 35)
        };
        dialog.Controls.Add(close);
        dialog.AcceptButton = close;
        dialog.ShowDialog(this);
        _surface.Focus();
    }

    private void ShowSleepTimerDialog()
    {
        using var dialog = new Form
        {
            Text = "Sleep timer",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(385, 240),
            BackColor = Background,
            ForeColor = Foreground,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var remaining = new Label
        {
            Text = _sleepDeadline is null
                ? "No sleep timer currently scheduled."
                : $"Current timer: {Math.Max(1, (int)Math.Ceiling((_sleepDeadline.Value - DateTimeOffset.UtcNow).TotalMinutes))} minutes remaining",
            Bounds = new Rectangle(18, 12, 345, 31),
            ForeColor = Foreground
        };
        dialog.Controls.Add(remaining);
        var warning = new Label
        {
            Text = "The TV power key is a toggle, not a guaranteed off command. Verify the TV state before scheduling.",
            Bounds = new Rectangle(18, 47, 345, 62),
            ForeColor = Foreground
        };
        dialog.Controls.Add(warning);

        var minutes = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 240,
            Value = 30,
            Bounds = new Rectangle(18, 117, 100, 30)
        };
        dialog.Controls.Add(minutes);
        dialog.Controls.Add(new Label
        {
            Text = "minutes",
            Bounds = new Rectangle(125, 117, 90, 30),
            ForeColor = Foreground
        });
        var set = new Button
        {
            Text = "Set timer",
            Bounds = new Rectangle(254, 165, 109, 36),
            DialogResult = DialogResult.OK
        };
        dialog.Controls.Add(set);
        var cancelTimer = new Button
        {
            Text = "Cancel timer",
            Enabled = _sleepDeadline is not null,
            Bounds = new Rectangle(18, 165, 125, 36),
            DialogResult = DialogResult.Retry
        };
        dialog.Controls.Add(cancelTimer);

        if (dialog.ShowDialog(this) == DialogResult.Retry)
        {
            _sleepDeadline = null;
            _sleepTimer.Stop();
            SetStatus("Sleep timer canceled");
        }
        else if (dialog.DialogResult == DialogResult.OK)
        {
            _sleepDeadline = DateTimeOffset.UtcNow.AddMinutes((double)minutes.Value);
            _sleepTimer.Start();
            SetStatus($"Sleep timer scheduled for {minutes.Value} minutes · Power is a toggle");
        }

        _surface.Focus();
    }

    private void SetAlwaysOnTop(bool enabled)
    {
        TopMost = enabled;
        _settings.AlwaysOnTop = enabled;

        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Could not save always-on-top preference.", ex);
            SetStatus($"Could not save window preference: {ex.Message}");
        }
    }

    private async Task ShowTextInputAsync()
    {
        if (!_remote.IsConnected)
        {
            SetStatus("Connect to the TV before sending text.");
            return;
        }

        using var dialog = new TextInputDialog();
        DialogResult result = dialog.ShowDialog(this);
        _surface.Focus();
        if (result != DialogResult.OK) return;

        await RunAsync(async () =>
        {
            await _remote.SendTextAsync(dialog.EnteredText);
            SetStatus("Text command sent · TV support is not confirmed");
        });
    }

    private void ShowKeyboardHelp()
    {
        MessageBox.Show(this,
            "Use your Windows keyboard when the remote window is focused:\n\n" +
            "Arrow keys   Move around the TV\n" +
            "Enter         Select / OK\n" +
            "Esc           Back\n" +
            "Home          Samsung Home\n" +
            "+ / −          Volume up / down\n" +
            "M             Mute\n" +
            "PgUp / PgDn   Channel up / down\n" +
            "Space         Play\n\n" +
            "Optional global shortcuts (enable in Options):\n" +
            "Ctrl+Alt+Shift+F9    Volume up\n" +
            "Ctrl+Alt+Shift+F10  Volume down\n" +
            "Ctrl+Alt+Shift+F11  Mute\n" +
            "Ctrl+Alt+Shift+F12  Play\n\n" +
            "Click the VOL up/down arrows to change the TV volume. The TV does not report its current volume level.\n\n" +
            "To type text, click Type on the remote while a supported TV text field is focused. Some streaming apps do not support this.",
            "Keyboard control", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _surface.Focus();
    }

    private void ShowHelp()
    {
        MessageBox.Show(this,
            "Clean TV Remote · Samsung TU690T\n\n" +
            "Tap Living Room TV to enter your TV IP and pair. Approve the request on your Samsung TV. " +
            "Use the purple OK button and the surrounding navigation controls to operate it.\n\n" +
            "When connected, Power sends Samsung's power toggle key. This may turn the TV off or on, depending on its current state. " +
            "When disconnected, Power broadcasts a Wake-on-LAN packet using the configured wired or wireless MAC address. " +
            "Enable Power On with Mobile in the TV's Network Expert Settings if available. Wi-Fi wake support varies by model. " +
            "Apps opens Samsung Home; actual commands available vary by firmware.\n\n" +
            "Connection message:\n" + _settings.TvIp + "\n\n" +
            "The TV and PC must be on the same local network.",
            "About this remote", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _surface.Focus();
    }

    private void UpdateToken(string token)
    {
        if (IsDisposed || Disposing) return;
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("TV paired, but the token could not be saved.", ex);
            SetStatus($"Paired, but could not save token: {ex.Message}");
        }
    }

    private void SetStatus(string message)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetStatus(message)));
            return;
        }
        _surface.SetConnection(message, _remote.IsConnected);
    }

    private async Task RunAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            AppLog.Error("An operation failed.", ex);
            SetStatus($"Error: {ex.Message}");
        }
    }

    private void ShowConnectionDiagnostics()
    {
        string status = _remote.IsConnected ? "Connected" :
            _manualDisconnected ? "Disconnected by user" :
            _connecting ? "Connecting" : "Offline (automatic retry enabled)";

        string details =
            $"TV: {_settings.ActiveProfile.Name}{Environment.NewLine}" +
            $"IP: {_settings.TvIp}{Environment.NewLine}" +
            $"Port: {(_settings.Secure ? 8002 : 8001)}{Environment.NewLine}" +
            $"Status: {status}{Environment.NewLine}" +
            $"Last connection attempt: {_lastConnectAt?.ToLocalTime().ToString("G") ?? "Never"}{Environment.NewLine}" +
            $"Last connection duration: {_lastConnectDuration?.TotalSeconds.ToString("F1") ?? "N/A"} seconds{Environment.NewLine}" +
            $"Next automatic retry: {(_manualDisconnected || _remote.IsConnected ? "Not scheduled" : _nextReconnectAt.ToLocalTime().ToString("G"))}{Environment.NewLine}" +
            $"Last error: {_lastConnectionError}{Environment.NewLine}" +
            $"Diagnostic log: {AppLog.LogFilePath}";
        
        using var dialog = new Form
        {
            Text = "Connection diagnostics",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(510, 315),
            BackColor = Background,
            ForeColor = Foreground,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var output = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Bounds = new Rectangle(14, 14, 482, 235),
            Text = details
        };
        dialog.Controls.Add(output);
        var copy = new Button { Text = "Copy diagnostics", Bounds = new Rectangle(14, 261, 160, 38) };
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(details);
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                AppLog.Error("Clipboard copy failed.", ex);
            }
        };
        dialog.Controls.Add(copy);
        var close = new Button
        {
            Text = "Close",
            Bounds = new Rectangle(396, 261, 100, 38),
            DialogResult = DialogResult.OK
        };
        dialog.Controls.Add(close);
        dialog.AcceptButton = close;
        dialog.ShowDialog(this);
        _surface.Focus();
    }

    private void OpenDiagnosticLog()
    {
        try
        {
            var startInfo = new ProcessStartInfo("notepad.exe") { UseShellExecute = false };
            startInfo.ArgumentList.Add(AppLog.LogFilePath);
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not open the diagnostic log.", ex);
            SetStatus($"Could not open log: {ex.Message}");
        }
    }
}
