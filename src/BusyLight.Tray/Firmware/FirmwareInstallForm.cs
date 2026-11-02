namespace BusyLight.Tray.Firmware;

public sealed class FirmwareInstallForm : Form
{
    private readonly Func<IProgress<FirmwareInstallProgress>, CancellationToken, Task<FirmwareInstallResult>> _operation;
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Width = 390, Height = 22 };
    private readonly Label _message = new() { AutoSize = false, Width = 390, Height = 42 };
    private readonly Label _detail = new() { AutoSize = false, Width = 390, Height = 46, ForeColor = SystemColors.GrayText };
    private readonly Button _cancel = new() { Text = "Cancel", Width = 90 };
    private readonly CancellationTokenSource _cts = new();
    private bool _running;

    public FirmwareInstallResult? Result { get; private set; }

    public FirmwareInstallForm(
        Func<IProgress<FirmwareInstallProgress>, CancellationToken, Task<FirmwareInstallResult>> operation)
    {
        _operation = operation;

        Text = "Install firmware";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ControlBox = false;
        ClientSize = new Size(430, 190);

        _message.Location = new Point(20, 20);
        _progress.Location = new Point(20, 68);
        _detail.Location = new Point(20, 100);
        _cancel.Location = new Point(320, 150);

        Controls.AddRange(new Control[] { _message, _progress, _detail, _cancel });
        _cancel.Click += (_, _) => _cts.Cancel();
        Shown += async (_, _) => await RunAsync();
        FormClosing += (_, e) => { if (_running) { _cts.Cancel(); e.Cancel = true; } };
    }

    private async Task RunAsync()
    {
        if (_running) return;
        _running = true;
        _message.Text = "Starting firmware installation…";

        var progress = new Progress<FirmwareInstallProgress>(value =>
        {
            _progress.Value = Math.Clamp(value.Percent, 0, 100);
            _message.Text = value.Message;
            _detail.Text = value.Detail ?? "";
            // Once write_flash preparation starts, interruption is unsafe.
            _cancel.Enabled = value.Percent < 8;
        });

        try
        {
            Result = await _operation(progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Result = FirmwareInstallResult.Failed("", "Firmware installation was cancelled.");
        }
        catch (Exception ex)
        {
            Result = FirmwareInstallResult.Failed("", ex.Message);
        }
        finally
        {
            _running = false;
            DialogResult = Result?.Success == true ? DialogResult.OK : DialogResult.Abort;
            Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _cts.Dispose();
        base.Dispose(disposing);
    }
}
