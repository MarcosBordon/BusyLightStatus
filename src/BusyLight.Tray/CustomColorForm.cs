namespace BusyLight.Tray;

public sealed class CustomColorForm : Form
{
    private readonly Func<string, int, Task> _preview;

    private readonly NumericUpDown _r = NewByteBox();
    private readonly NumericUpDown _g = NewByteBox();
    private readonly NumericUpDown _b = NewByteBox();
    private readonly TrackBar _brightness = new()
    {
        Minimum = 10, Maximum = 100, SmallChange = 5, LargeChange = 10,
        TickFrequency = 10, Width = 260
    };
    private readonly Label _brightnessLabel = new() { AutoSize = true };
    private readonly Label _hexLabel = new() { AutoSize = true };
    private readonly Panel _swatch = new() { Width = 120, Height = 60, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckBox _livePreview = new() { Text = "Vista previa en el ATOM", Checked = true, AutoSize = true };
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 150 };
    private bool _loading;

    public string SelectedColor => $"#{(int)_r.Value:X2}{(int)_g.Value:X2}{(int)_b.Value:X2}";
    public int SelectedBrightness => _brightness.Value;

    public CustomColorForm(string initialColor, int initialBrightness, Func<string, int, Task> preview)
    {
        _preview = preview;

        Text = "Color personalizado";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(370, 295);

        var pick = new Button { Text = "Elegir color…", Width = 180, Location = new Point(160, 20) };
        var apply = new Button { Text = "Aplicar", Width = 80, Location = new Point(180, 250), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", Width = 80, Location = new Point(270, 250), DialogResult = DialogResult.Cancel };

        _swatch.Location = new Point(20, 20);
        _hexLabel.Location = new Point(160, 58);
        _r.Location = new Point(40, 100);
        _g.Location = new Point(135, 100);
        _b.Location = new Point(230, 100);
        _brightness.Location = new Point(20, 165);
        _brightnessLabel.Location = new Point(290, 172);
        _livePreview.Location = new Point(20, 215);

        Controls.AddRange(new Control[]
        {
            _swatch, pick, _hexLabel,
            new Label { Text = "R", AutoSize = true, Location = new Point(20, 103) }, _r,
            new Label { Text = "G", AutoSize = true, Location = new Point(115, 103) }, _g,
            new Label { Text = "B", AutoSize = true, Location = new Point(210, 103) }, _b,
            new Label { Text = "Brillo (10-100)", AutoSize = true, Location = new Point(20, 145) },
            _brightness, _brightnessLabel, _livePreview, apply, cancel
        });

        AcceptButton = apply;
        CancelButton = cancel;

        // Valores iniciales
        _loading = true;
        var rgb = ParseHex(initialColor);
        _r.Value = (rgb >> 16) & 0xFF;
        _g.Value = (rgb >> 8) & 0xFF;
        _b.Value = rgb & 0xFF;
        _brightness.Value = Math.Clamp(initialBrightness / 5 * 5, 10, 100);
        _loading = false;
        UpdateDisplay();

        // Eventos
        _r.ValueChanged += (_, _) => OnChanged();
        _g.ValueChanged += (_, _) => OnChanged();
        _b.ValueChanged += (_, _) => OnChanged();
        _brightness.ValueChanged += (_, _) =>
        {
            var snapped = _brightness.Value / 5 * 5;
            if (snapped != _brightness.Value) { _brightness.Value = Math.Max(10, snapped); return; }
            OnChanged();
        };
        pick.Click += (_, _) => PickColor();

        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            try { await _preview(SelectedColor, SelectedBrightness); } catch { }
        };

        FormClosing += (_, _) => _debounce.Stop();
        FormClosed += (_, _) => _debounce.Dispose();
    }

    private void PickColor()
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = Color.FromArgb((int)_r.Value, (int)_g.Value, (int)_b.Value)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _loading = true;
        _r.Value = dialog.Color.R;
        _g.Value = dialog.Color.G;
        _b.Value = dialog.Color.B;
        _loading = false;
        OnChanged();
    }

    private void OnChanged()
    {
        if (_loading) return;
        UpdateDisplay();

        if (_livePreview.Checked)
        {
            _debounce.Stop();
            _debounce.Start();
        }
    }

    private void UpdateDisplay()
    {
        var pct = _brightness.Value;
        _swatch.BackColor = Color.FromArgb(
            (int)_r.Value * pct / 100,
            (int)_g.Value * pct / 100,
            (int)_b.Value * pct / 100);
        _hexLabel.Text = $"{SelectedColor}  ({pct} %)";
        _brightnessLabel.Text = $"{pct} %";
    }

    private static NumericUpDown NewByteBox() => new() { Minimum = 0, Maximum = 255, Width = 60 };

    private static int ParseHex(string? hex)
    {
        if (hex is { Length: 7 } && hex[0] == '#' &&
            int.TryParse(hex[1..], System.Globalization.NumberStyles.HexNumber, null, out var value))
            return value;
        return 0x00A0FF;
    }
}