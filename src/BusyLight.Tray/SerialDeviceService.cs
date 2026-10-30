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
    private bool _disposed;

    public string? PortName => _port?.PortName;

    public SerialDeviceService(int baudRate) => _baudRate = baudRate;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await _lock.WaitAsync(ct);
        try
        {
            return await ConnectCoreAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    // Sends a command. Returns true if a new serial connection had to be opened.
    public async Task<bool> SendAsync(object command, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(command);

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

    // Sends a command and reads the device's one-line JSON reply.
    public async Task<string?> SendAndReadAsync(object command, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(command);

        await _lock.WaitAsync(ct);
        try
        {
            if (_port is not { IsOpen: true } && !await ConnectCoreAsync(ct))
                return null;

            try
            {
                _port!.DiscardInBuffer();
                _port.WriteLine(JsonSerializer.Serialize(command, command.GetType()));
                return await ReadLineAsync(_port, ct);
            }
            catch (TimeoutException)
            {
                return null;
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

    /// <summary>
    /// Gives another component exclusive ownership of the serial device.
    /// The current COM port is remembered and closed while the callback runs.
    /// The callback may execute esptool without competing with this service.
    /// This method intentionally does not reconnect automatically; the caller
    /// reconnects after esptool has fully exited and then restores app state.
    /// </summary>
    public async Task<T> WithPortReleasedAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(operation);

        await _lock.WaitAsync(ct);
        try
        {
            DisposePort();
            ct.ThrowIfCancellationRequested();
            return await operation();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Non-generic overload for exclusive serial-port operations that do not
    /// return a value. Useful for the future write_flash stage.
    /// </summary>
    public async Task WithPortReleasedAsync(
        Func<Task> operation,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(operation);

        await _lock.WaitAsync(ct);
        try
        {
            DisposePort();
            ct.ThrowIfCancellationRequested();
            await operation();
        }
        finally
        {
            _lock.Release();
        }
    }

    // Probes every COM port and keeps the one that answers with the device signature.
    private async Task<bool> ConnectCoreAsync(CancellationToken ct)
    {
        DisposePort();

        foreach (var name in SerialPort.GetPortNames().OrderBy(PortSortKey).ThenBy(x => x))
        {
            ct.ThrowIfCancellationRequested();
            SerialPort? candidate = null;

            try
            {
                candidate = CreatePort(name);
                candidate.Open();

                // Some ATOM/USB bridges need a short settle time after opening.
                await Task.Delay(1500, ct);

                candidate.DiscardInBuffer();
                candidate.WriteLine(IdentifyCommand);
                var answer = await ReadLineAsync(candidate, ct);

                if (answer.Contains(DeviceSignature, StringComparison.OrdinalIgnoreCase))
                {
                    _port = candidate;
                    return true;
                }

                candidate.Dispose();
            }
            catch (OperationCanceledException)
            {
                candidate?.Dispose();
                throw;
            }
            catch
            {
                candidate?.Dispose();
            }
        }

        return false;
    }

    private SerialPort CreatePort(string name) => new(name, _baudRate)
    {
        NewLine = "\n",
        ReadTimeout = 2000,
        WriteTimeout = 2000,
        DtrEnable = false,
        RtsEnable = false
    };

    private static async Task<string> ReadLineAsync(SerialPort port, CancellationToken ct)
    {
        // SerialPort.ReadLine is synchronous. Task.Run allows cancellation of
        // the caller; ReadTimeout guarantees that the worker will also finish.
        return await Task.Run(port.ReadLine, ct);
    }

    private static int PortSortKey(string portName)
    {
        var digits = new string(portName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : int.MaxValue;
    }

    private void DisposePort()
    {
        var port = _port;
        _port = null;

        if (port is null) return;

        try
        {
            if (port.IsOpen) port.Close();
        }
        catch { }

        try { port.Dispose(); }
        catch { }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposePort();
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
