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
    private string _last = "Iniciando";

    private readonly ToolStripMenuItem _autoItem;
    private readonly ToolStripMenuItem _manualItem;
    private readonly ToolStripMenuItem _customItem;
    private readonly ToolStripMenuItem _textItem;
    private readonly Dictionary<ManualState, ToolStripMenuItem> _stateItems = new();
    private bool _customOpen;
    private bool _textOpen;

    private static readonly (ManualState State, string Text)[] ManualOptions =
    {
        (ManualState.Available,    "Disponible"),
        (ManualState.Busy,         "Ocupado"),
        (ManualState.InAMeeting,   "En reunión"),
        (ManualState.DoNotDisturb, "No molestar"),
        (ManualState.Presenting,   "Presentando"),
        (ManualState.Away,         "Ausente"),
    };

    public TrayApplicationContext()
    {
        _serial = new SerialDeviceService(_config.SerialBaudRate);
        _graphAvailable = _config.IsGraphConfigured;
        if (_graphAvailable) _graph = new GraphPresenceService(_config);
        _mode = _graphAvailable ? _settings.Mode : AppMode.Manual;

        // Menú: Modo
        _autoItem   = new ToolStripMenuItem("Automático (Teams)", null, async (_, _) => await SetModeAsync(AppMode.Auto));
        _manualItem = new ToolStripMenuItem("Manual Use", null, async (_, _) => await SetModeAsync(AppMode.Manual));
        var modeMenu = new ToolStripMenuItem("Modo");
        modeMenu.DropDownItems.AddRange(new ToolStripItem[] { _autoItem, _manualItem });

        // Menú: Estado manual
        var stateMenu = new ToolStripMenuItem("Estado manual");
        foreach (var (state, text) in ManualOptions)
        {
            var item = new ToolStripMenuItem(text, null, async (_, _) => await SelectManualStateAsync(state));
            _stateItems[state] = item;
            stateMenu.DropDownItems.Add(item);
        }

        _customItem = new ToolStripMenuItem("Color personalizado…", null, async (_, _) => await OpenCustomColorAsync());
        _textItem   = new ToolStripMenuItem("Mensaje de texto…", null, async (_, _) => await OpenTextMessageAsync());

        var menu = new ContextMenuStrip();
        menu.Items.Add(modeMenu);
        menu.Items.Add(stateMenu);
        menu.Items.Add(_customItem);
        menu.Items.Add(_textItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Actualizar ahora", null, async (_, _) => await SafeAsync(RefreshAsync));
        menu.Items.Add("Reconectar dispositivo", null, async (_, _) => await SafeAsync(ReconnectAsync));
        menu.Items.Add("Estado", null, (_, _) => MessageBox.Show(_last, "BusyLightStatus"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Exit());

        _icon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "BusyLightStatus",
            Visible = true,
            ContextMenuStrip = menu
        };

        UpdateMenu();
        _ = RunAsync(_cts.Token);
    }

    // ---------- Ciclo principal ----------

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            if (!_config.IsGraphConfigured)
                Notify("Sin configuración de Entra ID. Se activó el modo Manual Use.");

            if (_mode == AppMode.Auto)
                await TrySignInAsync(ct);

            await _serial.ConnectAsync(ct);
            await RefreshAsync(ct);

            // Entre 10 y 30 s: en manual, el ping siempre llega antes del watchdog de 60 s
            var interval = TimeSpan.FromSeconds(Math.Clamp(_config.PollSeconds, 10, 30));

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(interval, ct);

                if (_customOpen || _mode == AppMode.Manual) await KeepAliveAsync(ct);   // no pisar la vista previa
                else await UpdatePresenceAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus("Error: " + ex.Message); }
    }

    private Task RefreshAsync(CancellationToken ct) =>
        _mode == AppMode.Auto ? UpdatePresenceAsync(ct) : ApplyManualStateAsync(ct);

    // ---------- Modo automático ----------

    private async Task<bool> TrySignInAsync(CancellationToken ct)
    {
        if (_graph is null)
        {
            DisableAuto("Sin configuración de Entra ID");
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
            DisableAuto("Sin autorización para leer la presencia de Teams");
            return false;
        }
        catch (Exception ex)
        {
            SetStatus("Error de inicio de sesión: " + ex.Message);   // temporal: sigue en automático
            return false;
        }
    }

    private async Task UpdatePresenceAsync(CancellationToken ct)
    {
        if (_graph is null) return;

        try
        {
            var p = await _graph.GetMyPresenceAsync(ct);
            if (_mode != AppMode.Auto) return;   // el usuario cambió de modo mientras tanto

            await _serial.SendAsync(PresenceMapper.Map(p, _config.Brightness), ct);
            SetStatus($"Automático: {p.Availability} / {p.Activity} | {_serial.PortName}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsAuthorizationProblem(ex))
        {
            DisableAuto("Sin autorización para leer la presencia de Teams");
            await ApplyManualStateAsync(ct);
        }
        catch (Exception ex)
        {
            SetStatus("Error: " + ex.Message);   // red o Graph: reintenta en el próximo ciclo
        }
    }

    private static bool IsAuthorizationProblem(Exception ex) => ex switch
    {
        MsalUiRequiredException => true,
        MsalClientException c when c.ErrorCode == MsalError.AuthenticationCanceledError => true,
        MsalServiceException s when s.Message.Contains("AADSTS65001")    // sin consentimiento
                                 || s.Message.Contains("AADSTS90094")    // requiere aprobación de admin
                                 || s.Message.Contains("AADSTS50105")    // usuario no asignado
                                 || s.Message.Contains("AADSTS700016")   // aplicación no encontrada
                                 => true,
        HttpRequestException h when h.StatusCode is HttpStatusCode.Forbidden
                                                 or HttpStatusCode.Unauthorized => true,
        _ => false
    };

    private void DisableAuto(string reason)
    {
        _graphAvailable = false;
        _mode = AppMode.Manual;   // no se guarda: al reiniciar se vuelve a intentar Teams
        SetStatus(reason + ". Modo Manual Use activo.");
        Notify(reason + ". Se activó el modo Manual Use.");
        UpdateMenu();
    }

    // ---------- Modo manual ----------

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
            SetStatus($"Manual: {LabelOf(_settings.ManualState)} | {_serial.PortName}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { SetStatus("Error: " + ex.Message); }
    }

    private async Task KeepAliveAsync(CancellationToken ct)
    {
        try
        {
            var reconnected = await _serial.SendAsync(new PingCommand("ping"), ct);
            if (reconnected) await RefreshAsync(ct);   // ATOM reconectado: reenviar estado
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { SetStatus("Error: " + ex.Message); }
    }

    // ---------- Acciones del menú ----------

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
            await RefreshAsync(ct);   // si el inicio de sesión falló, ya está en manual
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
                await SafeAsync(RefreshAsync);   // Cancelar: vuelve al estado anterior
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
            using var form = new TextMessageForm(_settings.TextMessage, _settings.TextColor,
                                                 _settings.TextRandom, _settings.TextBrightness);

            if (form.ShowDialog() != DialogResult.OK) return;   // Cancelar: no cambia nada

            _settings.TextMessage = form.MessageText;
            _settings.TextColor = form.SelectedColor;
            _settings.TextRandom = form.SelectedRandom;
            _settings.TextBrightness = form.SelectedBrightness;
            await SelectManualStateAsync(ManualState.Text);
        }
        finally
        {
            _textOpen = false;
        }
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        var ok = await _serial.ConnectAsync(ct);
        if (!ok)
        {
            SetStatus("Dispositivo no encontrado");
            return;
        }
        await RefreshAsync(ct);
    }

    private async Task SafeAsync(Func<CancellationToken, Task> action)
    {
        try { await action(_cts.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus("Error: " + ex.Message); }
    }

    // ---------- Interfaz ----------

    private void UpdateMenu()
    {
        _autoItem.Enabled = _graphAvailable;
        _autoItem.Text = _graphAvailable ? "Automático (Teams)" : "Automático (Teams) - no disponible";
        _autoItem.Checked = _mode == AppMode.Auto;
        _manualItem.Checked = _mode == AppMode.Manual;

        foreach (var (state, item) in _stateItems)
            item.Checked = _mode == AppMode.Manual && state == _settings.ManualState;

        _customItem.Checked = _mode == AppMode.Manual && _settings.ManualState == ManualState.Custom;
        _textItem.Checked   = _mode == AppMode.Manual && _settings.ManualState == ManualState.Text;
    }

    private string LabelOf(ManualState state) => state switch
    {
        ManualState.Custom => "Color personalizado",
        ManualState.Text   => $"Mensaje \"{_settings.TextMessage}\"",
        _                  => ManualOptions.First(o => o.State == state).Text
    };

    private void Notify(string text) =>
        _icon.ShowBalloonTip(5000, "BusyLightStatus", text, ToolTipIcon.Info);

    private static Icon LoadTrayIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "BusyLightStatus.ico");
            return new Icon(path, SystemInformation.SmallIconSize);
        }
        catch
        {
            return SystemIcons.Information;
        }
    }

    private void SetStatus(string value)
    {
        _last = value;
        _icon.Text = value.Length <= 63 ? value : value[..63];
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
