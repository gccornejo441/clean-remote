using System.Drawing;
using System.Diagnostics;

namespace CleanTVRemote;

internal sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(19, 18, 24);
    private static readonly Color Surface = Color.FromArgb(44, 42, 55);
    private static readonly Color Foreground = Color.FromArgb(242, 240, 249);

    private readonly SamsungRemoteClient _remote = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly RemoteSurface _surface = new();
    private bool _connecting;

    public MainForm()
    {
        Text = "Clean TV Remote";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(365, 798);
        MinimumSize = new Size(342, 746);
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        _surface.Dock = DockStyle.Fill;
        _surface.SetTvName(_settings.ActiveProfile.Name);
        _surface.SetConnection(string.IsNullOrWhiteSpace(_settings.TvIp)
            ? $"Tap {_settings.ActiveProfile.Name} to set up your TV"
            : $"Ready to connect · Tap {_settings.ActiveProfile.Name}", false);
        _surface.ActionRequested += async action => await HandleActionAsync(action);
        Controls.Add(_surface);

        _remote.StatusChanged += SetStatus;
        _remote.PairingTokenChanged += UpdateToken;
        Shown += async (_, _) =>
        {
            _surface.Focus();
            if (!string.IsNullOrWhiteSpace(_settings.TvIp))
                await RunAsync(ConnectAsync);
        };
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
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
                ShowKeyboardHelp();
                return;
            case "power":
                if (_remote.IsConnected)
                    await RunAsync(() => _remote.SendKeyAsync("KEY_POWEROFF"));
                else
                    await WakeAsync();
                return;
            case "apps":
                await SendAsync("KEY_HOME");
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

    private Task SendAsync(string key) => RunAsync(() => _remote.SendKeyAsync(key));

    private async Task ShowSettingsAsync()
    {
        using var dialog = new ConnectionDialog(_settings, _remote.IsConnected);
        DialogResult result = dialog.ShowDialog(this);
        _surface.Focus();
        if (result == DialogResult.Cancel) return;

        if (result == DialogResult.Abort)
        {
            _remote.Disconnect();
            SetStatus($"Disconnected · Tap {_settings.ActiveProfile.Name} to connect");
            return;
        }

        if (result == DialogResult.Retry)
        {
            ClearPairing();
            return;
        }

        if (result != DialogResult.OK) return;

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
        await RunAsync(ConnectAsync);
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
        try
        {
            string? newToken = await _remote.ConnectAsync(_settings.TvIp, _settings.Secure, _settings.Token);
            if (!string.IsNullOrWhiteSpace(newToken)) UpdateToken(newToken);
            SetStatus($"Connected · {_settings.ActiveProfile.Name}");
        }
        finally
        {
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
            SetStatus("Wake signal sent · TV power-on is not guaranteed");
        });
    }

    private void ClearPairing()
    {
        if (MessageBox.Show(this, "Forget this TV's saved pairing token? You will need to approve the connection again on the TV.",
            "Forget pairing", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _remote.Disconnect();
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
        menu.Items.Add("Reconnect", null, async (_, _) => await RunAsync(ConnectAsync));
        menu.Items.Add("Wake TV", null, async (_, _) => await WakeAsync());
        menu.Items.Add("Open diagnostic log", null, (_, _) => OpenDiagnosticLog());
        menu.Items.Add("Samsung Menu", null, async (_, _) => await SendAsync("KEY_MENU"));
        menu.Items.Add("Pause playback", null, async (_, _) => await SendAsync("KEY_PAUSE"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Disconnect", null, (_, _) =>
        {
            _remote.Disconnect();
            SetStatus($"Disconnected · Tap {_settings.ActiveProfile.Name} to reconnect");
        });
        menu.Items.Add("Forget pairing", null, (_, _) => ClearPairing());
        menu.Show(_surface, new Point(Math.Max(10, _surface.Width / 2 - 80), Math.Max(30, _surface.Height / 2)));
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
            "Drag the volume slider up or down to send volume steps. The TV does not report its current level, so the slider adjusts volume relatively.\n\n" +
            "Typing text into Samsung apps is not implemented. This is a keyboard shortcut guide, not a text-entry feature.",
            "Keyboard control", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _surface.Focus();
    }

    private void ShowHelp()
    {
        MessageBox.Show(this,
            "Clean TV Remote · Samsung TU690T\n\n" +
            "Tap Living Room TV to enter your TV IP and pair. Approve the request on your Samsung TV. " +
            "Use the purple OK button and the surrounding navigation controls to operate it.\n\n" +
            "When connected, Power sends Power Off. When disconnected, Power sends a Wake-on-LAN packet only if a TV MAC address is configured. " +
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
