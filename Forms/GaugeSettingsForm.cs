namespace Tokenometer;

internal sealed class GaugeSettingsForm : Form
{
    private readonly NumericUpDown _amberInput = new() { Minimum = 1, Maximum = 99, Width = 80 };
    private readonly NumericUpDown _redInput = new() { Minimum = 1, Maximum = 99, Width = 80 };
    private readonly CheckBox _invertInput = new() { AutoSize = true };
    private readonly Button _saveButton = new() { Text = "Save" };
    private readonly Button _cancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };

    public GaugeThresholds? Result { get; private set; }

    public GaugeSettingsForm(GaugeThresholds current)
    {
        Text = "Gauge Display";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(340, 260);
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        var info = new Label
        {
            Text = "Gauges turn amber, then red, once usage crosses these percentages.",
            AutoSize = true,
            MaximumSize = new Size(310, 0),
            Location = new Point(15, 15),
        };

        var amberLabel = new Label { Text = "Amber at (%):", AutoSize = true, Location = new Point(15, 58) };
        _amberInput.Location = new Point(150, 55);
        _amberInput.Value = (decimal)current.AmberAt;

        var redLabel = new Label { Text = "Red at (%):", AutoSize = true, Location = new Point(15, 88) };
        _redInput.Location = new Point(150, 85);
        _redInput.Value = (decimal)current.RedAt;

        _invertInput.Text = "Show remaining instead of used";
        _invertInput.Location = new Point(15, 120);
        _invertInput.Checked = current.Invert;

        var invertHint = new Label
        {
            Text = "Gauges count down from 100% instead of up. Colors still follow usage, "
                   + "so a nearly spent limit stays red.",
            AutoSize = true,
            MaximumSize = new Size(310, 0),
            ForeColor = SystemColors.GrayText,
            Location = new Point(32, 143),
        };

        var resetButton = new Button { Text = "Reset to Defaults", AutoSize = true, Location = new Point(15, 185) };
        resetButton.Click += (_, _) =>
        {
            _amberInput.Value = (decimal)GaugeThresholds.Default.AmberAt;
            _redInput.Value = (decimal)GaugeThresholds.Default.RedAt;
            _invertInput.Checked = GaugeThresholds.Default.Invert;
        };

        _saveButton.Location = new Point(150, 220);
        _cancelButton.Location = new Point(235, 220);
        _saveButton.Click += (_, _) =>
        {
            var candidate = new GaugeThresholds((double)_amberInput.Value, (double)_redInput.Value, _invertInput.Checked);
            if (!GaugeThresholds.IsValid(candidate))
            {
                MessageBox.Show(this, "The amber threshold must be lower than the red threshold.", "Tokenometer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = candidate;
            DialogResult = DialogResult.OK;
        };

        Controls.Add(info);
        Controls.Add(amberLabel);
        Controls.Add(_amberInput);
        Controls.Add(redLabel);
        Controls.Add(_redInput);
        Controls.Add(_invertInput);
        Controls.Add(invertHint);
        Controls.Add(resetButton);
        Controls.Add(_saveButton);
        Controls.Add(_cancelButton);
    }
}
