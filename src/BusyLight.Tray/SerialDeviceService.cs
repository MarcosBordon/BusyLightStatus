using System.IO.Ports;
using System.Text.Json;

namespace BusyLight.Tray;

public sealed class SerialDeviceService : IDisposable
{
    private const string IdentifyCommand = "{\"cmd\":\"identify\"}";
    private const string DeviceSignature = "BUSYLIGHT-ATOM";

    private readonly int _baudRate;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SerialPort? _port;

    public string? PortName => _port?.PortName;

    public SerialDeviceService(int baudRate) => _baudRate = baudRate;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try { return await ConnectCoreAsync(ct); }
        finally { _lock.Release(); }
    }

    // Envía un comando. Devuelve true si tuvo que abrir una conexión nueva.
    public async Task<bool> SendAsync(object command, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var reconnected = false;

            if (_port is not { IsOpen: true })
            {
                if (!await ConnectCoreAsync(ct))
                    throw new IOException("No se encontró el Busy Light por USB.");
                reconnected = true;
            }

            try
            {
                _port!.DiscardInBuffer();
                _port.WriteLine(JsonSerializer.Serialize(command, command.GetType()));
            }
            catch
            {
                DisposePort();
                throw;
            }

            return reconnected;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> ConnectCoreAsync(CancellationToken ct)
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

    private void DisposePort()
    {
        try { _port?.Dispose(); } catch { }
        _port = null;
    }

    public void Dispose() => DisposePort();
}