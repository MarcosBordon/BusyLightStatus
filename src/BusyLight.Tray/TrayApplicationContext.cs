using System.Net;
using Microsoft.Identity.Client;

namespace BusyLight.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config = AppConfig.Load();
    private readonly UserSettings _settings = UserSettings.Load();
    private readonly NotifyIcon _icon;
    private readonly CancellationTokenSource _cts = new();
    private readonly SerialDeviceService _serial;
    private readonly GraphPresenceService? _graph;

    private bool _graphAvailable;
    private AppMode _mode;
    private string _last = Strings.Starting;

    private readonly ToolStripMenuItem _autoItem;
    private readonly ToolStripMenuItem _manualItem;
    private readonly ToolStripMenuItem _customItem;
    private readonly ToolStripMenuItem _textItem;
    private readonly Dictionary<ManualState, ToolStripMenuItem> _stateItems = new();
    private bool _customOpen;
    private bool _textOpen;
    private bool _gameOpen;

    private static readonly (ManualState State, string Text)[] ManualOptions =
    {
        (ManualState.Available,    Strings.StateAvailable),
        (ManualState.Busy,         Strings.StateBusy),
        (ManualState.InAMeeting,   Strings.StateInAMeeting),
        (ManualState.DoNotDisturb, Strings.StateDoNotDisturb),
        (ManualState.Presenting,   Strings.StatePresenting),
        (ManualState.Away,         Strings.StateAway),
    };

    public TrayApplicationContext()
    {
        _serial = new SerialDeviceService(_config.SerialBaudRate);
        _graphAvailable = _config.IsGraphConfigured;
        if (_graphAvailable) _graph = new GraphPresenceService(_config);
        _mode = _graphAvailable ? _settings.Mode : AppMode.Manual;

        // Menu: Mode
        _autoItem   = new ToolStripMenuItem(Strings.MenuAuto, null, async (_, _) => await SetModeAsync(AppMode.Auto));
        _manualItem = new ToolStripMenuItem(Strings.MenuManual, null, async (_, _) => await SetModeAsync(AppMode.Manual));
        var modeMenu = new ToolStripMenuItem(Strings.MenuMode);
        modeMenu.DropDownItems.AddRange(new ToolStripItem[] { _autoItem, _manualItem });

        // Menu: Manual status
        var stateMenu = new ToolStripMenuItem(Strings.MenuManualStatus);
        foreach (var (state, text) in ManualOptions)
        {
            var item = new ToolStripMenuItem(text, null, async (_, _) => await SelectManualStateAsync(state));
            _stateItems[state] = item;
            stateMenu.DropDownItems.Add(item);
        }

        _customItem = new ToolStripMenuItem(Strings.MenuCustomColor, null, async (_, _) => await OpenCustomColorAsync());
        _textItem   = new ToolStripMenuItem(Strings.MenuTextMessage, null, async (_, _) => await OpenTextMessageAsync());

        var menu = new ContextMenuStrip();
        menu.Items.Add(modeMenu);
        menu.Items.Add(stateMenu);
        menu.Items.Add(_customItem);
        menu.Items.Add(_textItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuRefresh, null, async (_, _) => await SafeAsync(RefreshAsync));
        menu.Items.Add(Strings.MenuReconnect, null, async (_, _) => await SafeAsync(ReconnectAsync));
        menu.Items.Add(Strings.MenuStatus, null, (_, _) => MessageBox.Show(_last, Strings.AppName));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuExit, null, (_, _) => Exit());

        _icon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = Strings.AppName,
            Visible = true,
            ContextMenuStrip = menu
        };

        UpdateMenu();
        _ = RunAsync(_cts.Token);
    }

    // ---------- Main loop ----------

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            if (!_config.IsGraphConfigured)
                Notify(Strings.ManualEnabledNotice(Strings.NoEntraConfig));

            if (_mode == AppMode.Auto)
                await TrySignInAsync(ct);

            await _serial.ConnectAsync(ct);
            await RefreshAsync(ct);

            // 10-30 s: in manual mode the ping always arrives before the 60 s watchdog
            var interval = TimeSpan.FromSeconds(Math.Clamp(_config.PollSeconds, 10, 30));

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(interval, ct);

                if (_gameOpen) continue;   // the game window polls the ATOM itself

                if (_customOpen || _mode == AppMode.Manual) await KeepAliveAsync(ct);   // don't overwrite the preview
                else await UpdatePresenceAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus(Strings.Error(ex.Message)); }
    }

    private Task RefreshAsync(CancellationToken ct) =>
        _mode == AppMode.Auto ? UpdatePresenceAsync(ct) : ApplyManualStateAsync(ct);

    // ---------- Automatic mode ----------

    private async Task<bool> TrySignInAsync(CancellationToken ct)
    {
        if (_graph is null)
        {
            DisableAuto(Strings.NoEntraConfig);
            return false;
        }

        try
        {
            await _graph.SignInAsync(ct);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsAuthorizationProblem(ex))
        {
            DisableAuto(Strings.NotAuthorized);
            return false;
        }
        catch (Exception ex)
        {
            SetStatus(Strings.SignInError(ex.Message));   // transient: stays in automatic mode
            return false;
        }
    }

    private async Task UpdatePresenceAsync(CancellationToken ct)
    {
        if (_graph is null) return;

        try
        {
            var p = await _graph.GetMyPresenceAsync(ct);
            if (_mode != AppMode.Auto) return;   // user switched mode in the meantime

            await _serial.SendAsync(PresenceMapper.Map(p, _config.Brightness), ct);
            SetStatus(Strings.StatusAuto(p.Availability, p.Activity, _serial.PortName));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsAuthorizationProblem(ex))
        {
            DisableAuto(Strings.NotAuthorized);
            await ApplyManualStateAsync(ct);
        }
        catch (Exception ex)
        {
            SetStatus(Strings.Error(ex.Message));   // network or Graph: retry on next cycle
        }
    }

    private static bool IsAuthorizationProblem(Exception ex) => ex switch
    {
        MsalUiRequiredException => true,
        MsalClientException c when c.ErrorCode == MsalError.AuthenticationCanceledError => true,
        MsalServiceException s when s.Message.Contains("AADSTS65001")    // no consent
                                 || s.Message.Contains("AADSTS90094")    // admin approval required
                                 || s.Message.Contains("AADSTS50105")    // user not assigned
                                 || s.Message.Contains("AADSTS700016")   // application not found
                                 => true,
        HttpRequestException h when h.StatusCode is HttpStatusCode.Forbidden
                                                 or HttpStatusCode.Unauthorized => true,
        _ => false
    };

    private void DisableAuto(string reason)
    {
        _graphAvailable = false;
        _mode = AppMode.Manual;   // not saved: Teams is retried on next start
        SetStatus(Strings.ManualActiveStatus(reason));
        Notify(Strings.ManualEnabledNotice(reason));
        UpdateMenu();
    }

    // ---------- Manual mode ----------

    private async Task ApplyManualStateAsync(CancellationToken ct)
    {
        try
        {
            object command = _settings.ManualState switch
            {
                ManualState.Custom => (object)PresenceMapper.MapCustom(_settings.CustomColor, _settings.CustomBrightness),
                ManualState.Text   => (object)PresenceMapper.MapText(_settings.TextMessage, _settings.TextColor,
                                                                     _settings.TextRandom, _settings.TextBrightness),
                _                  => (object)PresenceMapper.MapManual(_settings.ManualState)
            };

            await _serial.SendAsync(command, ct);
            SetStatus(Strings.StatusManual(LabelOf(_settings.ManualState), _serial.PortName));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { SetStatus(Strings.Error(ex.Message)); }
    }

    private async Task KeepAliveAsync(CancellationToken ct)
    {
        try
        {
            var reconnected = await _serial.SendAsync(new PingCommand("ping"), ct);
            if (reconnected) await RefreshAsync(ct);   // ATOM reconnected: resend state
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { SetStatus(Strings.Error(ex.Message)); }
    }

    // ---------- Menu actions ----------

    private async Task SetModeAsync(AppMode mode)
    {
        if (mode == AppMode.Auto && !_graphAvailable) return;

        _mode = mode;
        _settings.Mode = mode;
        _settings.Save();
        UpdateMenu();

        await SafeAsync(async ct =>
        {
            if (mode == AppMode.Auto) await TrySignInAsync(ct);
            await RefreshAsync(ct);   // if sign-in failed, we are already in manual mode
        });
    }

    private async Task SelectManualStateAsync(ManualState state)
    {
        _settings.ManualState = state;
        _settings.Mode = AppMode.Manual;
        _settings.Save();
        _mode = AppMode.Manual;
        UpdateMenu();

        await SafeAsync(ApplyManualStateAsync);
    }

    private async Task OpenCustomColorAsync()
    {
        if (_customOpen) return;
        _customOpen = true;

        try
        {
            using var form = new CustomColorForm(_settings.CustomColor, _settings.CustomBrightness,
                (color, brightness) => _serial.SendAsync(PresenceMapper.MapCustom(color, brightness), _cts.Token));

            if (form.ShowDialog() == DialogResult.OK)
            {
                _settings.CustomColor = form.SelectedColor;
                _settings.CustomBrightness = form.SelectedBrightness;
                await SelectManualStateAsync(ManualState.Custom);
            }
            else
            {
                await SafeAsync(RefreshAsync);   // Cancel: restore previous state
            }
        }
        finally
        {
            _customOpen = false;
        }
    }

    private async Task OpenTextMessageAsync()
    {
        if (_textOpen) return;
        _textOpen = true;

        try
        {
            bool easterEgg;

            using (var form = new TextMessageForm(_settings.TextMessage, _settings.TextColor,
                                                  _settings.TextRandom, _settings.TextBrightness))
            {
                if (form.ShowDialog() != DialogResult.OK) return;   // Cancel: nothing changes

                easterEgg = form.IsEasterEgg;
                if (!easterEgg)
                {
                    _settings.TextMessage = form.MessageText;
                    _settings.TextColor = form.SelectedColor;
                    _settings.TextRandom = form.SelectedRandom;
                    _settings.TextBrightness = form.SelectedBrightness;
                }
            }

            // Hidden game: "SNAKE" is never saved as the message
            if (easterEgg) await OpenSnakeGameAsync();
            else await SelectManualStateAsync(ManualState.Text);
        }
        finally
        {
            _textOpen = false;
        }
    }

    private async Task OpenSnakeGameAsync()
    {
        if (_gameOpen) return;
        _gameOpen = true;

        try
        {
            using var form = new SnakeGameForm(command => _serial.SendAndReadAsync(command, _cts.Token));
            form.ShowDialog();
        }
        finally
        {
            _gameOpen = false;

            // Stop the game and restore the previous state (manual or Teams)
            await SafeAsync(async ct =>
            {
                await _serial.SendAsync(new GameCommand("gameStop"), ct);
                await RefreshAsync(ct);
            });
        }
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        var ok = await _serial.ConnectAsync(ct);
        if (!ok)
        {
            SetStatus(Strings.DeviceNotFound);
            return;
        }
        await RefreshAsync(ct);
    }

    private async Task SafeAsync(Func<CancellationToken, Task> action)
    {
        try { await action(_cts.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus(Strings.Error(ex.Message)); }
    }

    // ---------- UI ----------

    private void UpdateMenu()
    {
        _autoItem.Enabled = _graphAvailable;
        _autoItem.Text = _graphAvailable ? Strings.MenuAuto : Strings.MenuAutoUnavailable;
        _autoItem.Checked = _mode == AppMode.Auto;
        _manualItem.Checked = _mode == AppMode.Manual;

        foreach (var (state, item) in _stateItems)
            item.Checked = _mode == AppMode.Manual && state == _settings.ManualState;

        _customItem.Checked = _mode == AppMode.Manual && _settings.ManualState == ManualState.Custom;
        _textItem.Checked   = _mode == AppMode.Manual && _settings.ManualState == ManualState.Text;
    }

    private string LabelOf(ManualState state) => state switch
    {
        ManualState.Custom => Strings.StateCustom,
        ManualState.Text   => Strings.StateText(_settings.TextMessage),
        _                  => ManualOptions.First(o => o.State == state).Text
    };

    private void Notify(string text) =>
        _icon.ShowBalloonTip(5000, Strings.AppName, text, ToolTipIcon.Info);

    private static Icon LoadTrayIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private void SetStatus(string value)
    {
        _last = value;
        _icon.Text = value.Length <= 63 ? value : value[..63];   // Windows tray tooltip limit
    }

    private void Exit()
    {
        _cts.Cancel();
        _serial.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        ExitThread();
    }
}
