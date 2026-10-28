using System.IO.Ports;
using System.Text.Json;

namespace BusyLight.Tray;

public sealed class SerialDeviceService : IDisposable
{
    private const string IdentifyCommand = "{\"cmd\":\"identify\"}";
    private const string DeviceSignature = "BUSYLIGHT-ATOM";

    private readonly int _baudRate;
    private SerialPort? _port;

    public string? PortName => _port?.PortName;

    public SerialDeviceService(int baudRate) => _baudRate = baudRate;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        DisposePort();

        foreach (var name in SerialPort.GetPortNames().OrderBy(x => x))
        {
            ct.ThrowIfCancellationRequested();
            SerialPort? p = null;

            try
            {
                p = new SerialPort(name, _baudRate)
                {
                    NewLine = "\n",
                    ReadTimeout = 2000,
                    WriteTimeout = 2000,
                    DtrEnable = false,
                    RtsEnable = false
                };

                p.Open();
                await Task.Delay(1500, ct);
                p.DiscardInBuffer();
                p.WriteLine(IdentifyCommand);

                var answer = await Task.Run(() => p.ReadLine(), ct);

                if (answer.Contains(DeviceSignature, StringComparison.OrdinalIgnoreCase))
                {
                    _port = p;
                    return true;
                }

                p.Dispose();
            }
            catch (OperationCanceledException)
            {
                p?.Dispose();
                throw;
            }
            catch
            {
                p?.Dispose();
            }
        }

        return false;
    }

    public async Task SendAsync(DeviceCommand command, CancellationToken ct = default)
    {
        if (_port is not { IsOpen: true } && !await ConnectAsync(ct))
            throw new IOException("No se encontró el Busy Light por USB.");

        try
        {
            _port!.WriteLine(JsonSerializer.Serialize(command));
        }
        catch
        {
            DisposePort();
            throw;
        }
    }

    private void DisposePort()
    {
        try { _port?.Dispose(); } catch { }
        _port = null;
    }

    public void Dispose() => DisposePort();
}