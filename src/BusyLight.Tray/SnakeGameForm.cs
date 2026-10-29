using System.Text.Json;

namespace BusyLight.Tray;

// Hidden Snake game window. The game runs on the ATOM; this window sends
// directions and polls the state every 300 ms (which also keeps the watchdog quiet).
public sealed class SnakeGameForm : Form
{
    private const int SpeedMs = 450;

    private readonly Func<object, Task<string?>> _sendAndRead;

    private readonly Label _length = new() { AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold) };
    private readonly Label _best   = new() { AutoSize = true, Font = new Font("Segoe UI", 10f) };
    private readonly Label _status = new() { AutoSize = true };
    private readonly Label _note   = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly CheckBox _tilt = new() { Text = Strings.GameTilt, AutoSize = true };
    private readonly Button _start = new() { Text = Strings.GameStart, Width = 90 };
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 300 };

    private bool _started;
    private bool _busy;

    public SnakeGameForm(Func<object, Task<string?>> sendAndRead)
    {
        _sendAndRead = sendAndRead;

        Text = Strings.GameTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        KeyPreview = true;
        ClientSize = new Size(370, 250);

        var help = new Label { Text = Strings.GameHelp, AutoSize = true, Location = new Point(20, 15) };
        var close = new Button { Text = Strings.Close, Width = 90, Location = new Point(260, 205), DialogResult = DialogResult.Cancel };

        _length.Location = new Point(20, 65);
        _best.Location   = new Point(260, 72);
        _status.Location = new Point(20, 105);
        _note.Location   = new Point(20, 130);
        _tilt.Location   = new Point(20, 165);
        _start.Location  = new Point(160, 205);

        Controls.AddRange(new Control[] { help, _length, _best, _status, _note, _tilt, _start, close });
        CancelButton = close;

        _length.Text = Strings.GameLength(0);
        _best.Text   = Strings.GameBest(0);
        _status.Text = Strings.GameReady;

        _start.Click += async (_, _) => await StartAsync();
        _poll.Tick += async (_, _) => await PollAsync();
        FormClosing += (_, _) => _poll.Stop();
        FormClosed += (_, _) => _poll.Dispose();
    }

    // Arrow keys and WASD go to the game, whatever control has the focus
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        string? dir = (keyData & Keys.KeyCode) switch
        {
            Keys.Up    or Keys.W => "up",
            Keys.Down  or Keys.S => "down",
            Keys.Left  or Keys.A => "left",
            Keys.Right or Keys.D => "right",
            _ => null
        };

        if (dir is null || !_started) return base.ProcessCmdKey(ref msg, keyData);

        _ = SendDirAsync(dir);
        return true;
    }

    private async Task StartAsync()
    {
        _start.Enabled = false;
        _note.Text = "";

        try
        {
            var control = _tilt.Checked ? "tilt" : "keys";
            var info = Parse(await _sendAndRead(new GameStartCommand("gameStart", SpeedMs, control)));

            if (info is null)
            {
                _status.Text = Strings.DeviceNotFoundUsb;
                return;
            }

            if (_tilt.Checked && info.Control == "keys") _note.Text = Strings.GameNoTilt;

            _started = true;
            ShowInfo(info);
            _poll.Start();
            ActiveControl = null;   // keep the focus away from the buttons
        }
        catch (Exception ex)
        {
            _status.Text = Strings.Error(ex.Message);
        }
        finally
        {
            _start.Enabled = true;
            if (_started) _start.Text = Strings.GameRestart;
        }
    }

    private async Task SendDirAsync(string dir)
    {
        try { await _sendAndRead(new GameDirCommand("gameDir", dir)); }
        catch { /* next poll shows the state */ }
    }

    private async Task PollAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var info = Parse(await _sendAndRead(new GameCommand("gameState")));
            if (info is not null) ShowInfo(info);
        }
        catch { /* keep polling */ }
        finally
        {
            _busy = false;
        }
    }

    private void ShowInfo(GameInfo info)
    {
        _length.Text = Strings.GameLength(info.Length);
        _best.Text   = Strings.GameBest(info.Best);
        _status.Text = info.State switch
        {
            "win"  => Strings.GameWin,
            "over" => Strings.GameOver,
            _      => Strings.GameRunning
        };
    }

    private sealed record GameInfo(string State, int Length, int Best, string Control);

    // {"status":"ok","state":"running","length":3,"best":5,"control":"keys"}
    private static GameInfo? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (!r.TryGetProperty("state", out var state)) return null;

            return new GameInfo(
                state.GetString() ?? "",
                r.TryGetProperty("length", out var l) ? l.GetInt32() : 0,
                r.TryGetProperty("best", out var b) ? b.GetInt32() : 0,
                r.TryGetProperty("control", out var c) ? c.GetString() ?? "" : "");
        }
        catch
        {
            return null;
        }
    }
}
