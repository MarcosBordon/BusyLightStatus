using System.IO.Ports;
using System.Text.Json;

namespace BusyLight.Tray;

public sealed class SerialDeviceService : IDisposable
{
    private const string IdentifyCommand = "{\"cmd\":\"identify\"}";
    private const string DeviceSignature = "BUSYLIGHT-ATOM";

    private readonly int _baudRate;
    private readonly SemaphoreSlim _lock = new(1, 1);   // one write at a time
    private SerialPort? _port;

    public string? PortName => _port?.PortName;

    public SerialDeviceService(int baudRate) => _baudRate = baudRate;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try { return await ConnectCoreAsync(ct); }
        finally { _lock.Release(); }
    }

    // Sends a command. Returns true if a new connection had to be opened.
    public async Task<bool> SendAsync(object command, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var reconnected = false;

            if (_port is not { IsOpen: true })
            {
                if (!await ConnectCoreAsync(ct))
                    throw new IOException(Strings.DeviceNotFoundUsb);
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

    // Sends a command and returns the device's one-line reply (null if no device or no reply)
    public async Task<string?> SendAndReadAsync(object command, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_port is not { IsOpen: true } && !await ConnectCoreAsync(ct))
                return null;

            var port = _port!;
            try
            {
                port.DiscardInBuffer();
                port.WriteLine(JsonSerializer.Serialize(command, command.GetType()));
                return await Task.Run(() => port.ReadLine(), ct);
            }
            catch (TimeoutException)
            {
                return null;   // device busy or old firmware: keep the port open
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                DisposePort();
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // Probes every COM port and keeps the one that answers with the device signature
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
                    DtrEnable = false,   // avoid resetting the ATOM on open
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
                p?.Dispose();   // not our device: try the next port
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
