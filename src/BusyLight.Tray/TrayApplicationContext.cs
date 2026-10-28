namespace BusyLight.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config = AppConfig.Load();
    private readonly NotifyIcon _icon;
    private readonly CancellationTokenSource _cts = new();
    private readonly GraphPresenceService _graph;
    private readonly SerialDeviceService _serial;
    private string _last = "Iniciando";

    public TrayApplicationContext()
    {
        _graph = new GraphPresenceService(_config);
        _serial = new SerialDeviceService(_config.SerialBaudRate);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Actualizar ahora", null, async (_, _) => await UpdateOnceAsync());
        menu.Items.Add("Reconectar dispositivo", null, async (_, _) => await ReconnectAsync());
        menu.Items.Add("Estado", null, (_, _) => MessageBox.Show(_last, "Busy Light Status"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Exit());
        _icon = new NotifyIcon { Icon = SystemIcons.Information, Text = "Busy Light Status", Visible = true, ContextMenuStrip = menu };
        _ = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await _graph.SignInAsync(ct);
            await _serial.ConnectAsync(ct);
            while (!ct.IsCancellationRequested)
            {
                await UpdateOnceAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _config.PollSeconds)), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus("Error: " + ex.Message); }
    }

    private async Task UpdateOnceAsync(CancellationToken ct = default)
    {
        try
        {
            var p = await _graph.GetMyPresenceAsync(ct);
            var command = PresenceMapper.Map(p, _config.Brightness);
            await _serial.SendAsync(command, ct);
            SetStatus($"{p.Availability} / {p.Activity} | {_serial.PortName}");
        }
        catch (Exception ex) { SetStatus("Error: " + ex.Message); }
    }

    private async Task ReconnectAsync()
    {
        var ok = await _serial.ConnectAsync(_cts.Token);
        SetStatus(ok ? $"Dispositivo conectado en {_serial.PortName}" : "Dispositivo no encontrado");
    }

    private void SetStatus(string value)
    {
        _last = value;
        _icon.Text = value.Length <= 63 ? value : value[..63];
    }

    private void Exit()
    {
        _cts.Cancel(); _serial.Dispose(); _icon.Visible = false; _icon.Dispose(); ExitThread();
    }
}
