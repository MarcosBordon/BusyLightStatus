using System.Text;

namespace BusyLight.Tray;

public sealed class TextMessageForm : Form
{
    public const int MaxChars = 15;
    private const string Allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 !?.-:";
    private const char Enye = '\u00D1';   // Ñ

    private readonly TextBox _text = new()
    {
        MaxLength = MaxChars, Width = 250, CharacterCasing = CharacterCasing.Upper
    };
    private readonly Label _counter = new() { AutoSize = true };
    private readonly Label _preview = new() { AutoSize = true };
    private readonly RadioButton _fixed = new() { Text = "Color fijo", AutoSize = true };
    private readonly RadioButton _random = new() { Text = "Random (colores aleatorios)", AutoSize = true };
    private readonly Button _pick = new() { Text = "Elegir color…", Width = 110 };
    private readonly Panel _swatch = new() { Width = 40, Height = 24, BorderStyle = BorderStyle.FixedSingle };
    private readonly TrackBar _brightness = new()
    {
        Minimum = 10, Maximum = 100, SmallChange = 5, LargeChange = 10,
        TickFrequency = 10, Width = 260
    };
    private readonly Label _brightnessLabel = new() { AutoSize = true };
    private string _color;

    public string MessageText => Normalize(_text.Text).TrimEnd();
    public string SelectedColor => _color;
    public bool SelectedRandom => _random.Checked;
    public int SelectedBrightness => _brightness.Value;

    public TextMessageForm(string initialText, string initialColor, bool initialRandom, int initialBrightness)
    {
        _color = IsHex(initialColor) ? initialColor : "#00A0FF";

        Text = "Mensaje de texto";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(370, 305);

        var send = new Button { Text = "Enviar", Width = 80, Location = new Point(180, 265) };
        var cancel = new Button { Text = "Cancelar", Width = 80, Location = new Point(270, 265), DialogResult = DialogResult.Cancel };

        _text.Location = new Point(20, 35);
        _counter.Location = new Point(280, 38);
        _preview.Location = new Point(20, 65);
        _fixed.Location = new Point(20, 100);
        _pick.Location = new Point(130, 96);
        _swatch.Location = new Point(250, 96);
        _random.Location = new Point(20, 130);
        _brightness.Location = new Point(20, 185);
        _brightnessLabel.Location = new Point(290, 192);

        Controls.AddRange(new Control[]
        {
            new Label { Text = $"Mensaje (máx. {MaxChars} caracteres)", AutoSize = true, Location = new Point(20, 15) },
            _text, _counter, _preview,
            _fixed, _pick, _swatch, _random,
            new Label { Text = "Brillo (10-100)", AutoSize = true, Location = new Point(20, 165) },
            _brightness, _brightnessLabel,
            new Label { Text = "Cada letra se muestra 500 ms y la frase se repite.", AutoSize = true,
                        ForeColor = SystemColors.GrayText, Location = new Point(20, 235) },
            send, cancel
        });

        AcceptButton = send;
        CancelButton = cancel;

        // Valores iniciales
        _text.Text = initialText ?? "";
        _random.Checked = initialRandom;
        _fixed.Checked = !initialRandom;
        _brightness.Value = Math.Clamp(initialBrightness / 5 * 5, 10, 100);
        UpdateDisplay();

        // Eventos
        _text.TextChanged += (_, _) => UpdateDisplay();
        _fixed.CheckedChanged += (_, _) => UpdateDisplay();
        _pick.Click += (_, _) => PickColor();
        _brightness.ValueChanged += (_, _) =>
        {
            var snapped = _brightness.Value / 5 * 5;
            if (snapped != _brightness.Value) { _brightness.Value = Math.Max(10, snapped); return; }
            UpdateDisplay();
        };
        send.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(MessageText))
            {
                MessageBox.Show(this, "Escribe al menos una letra o número.", "Mensaje de texto",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _text.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
        };
    }

    // Mayúsculas, sin acentos, conserva la Ñ; lo no soportado pasa a espacio
    public static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        var sb = new StringBuilder();
        foreach (var ch in input.ToUpperInvariant())
        {
            if (sb.Length >= MaxChars) break;
            if (ch == Enye) { sb.Append(Enye); continue; }

            var basic = ch.ToString().Normalize(NormalizationForm.FormD)[0];
            sb.Append(Allowed.Contains(basic) ? basic : ' ');
        }
        return sb.ToString();
    }

    private void PickColor()
    {
        using var dialog = new ColorDialog { FullOpen = true, Color = ToColor(_color) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        _fixed.Checked = true;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        _counter.Text = $"{_text.Text.Length}/{MaxChars}";
        var shown = Normalize(_text.Text).TrimEnd();
        _preview.Text = shown.Length > 0 ? $"Se mostrará: {shown}" : "Se mostrará: (vacío)";
        _pick.Enabled = _fixed.Checked;
        _swatch.BackColor = _fixed.Checked ? ToColor(_color) : SystemColors.Control;
        _brightnessLabel.Text = $"{_brightness.Value} %";
    }

    private static bool IsHex(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' &&
        int.TryParse(hex[1..], System.Globalization.NumberStyles.HexNumber, null, out _);

    private static Color ToColor(string hex)
    {
        var v = int.Parse(hex[1..], System.Globalization.NumberStyles.HexNumber);
        return Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
    }
}