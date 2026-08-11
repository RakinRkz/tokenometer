namespace Tokenometer;

internal sealed class GaugeSettingsForm : Form
{
    private readonly NumericUpDown _amberInput = new() { Minimum = 1, Maximum = 99, Width = 80 };
    private readonly NumericUpDown _redInput = new() { Minimum = 1, Maximum = 99, Width = 80 };
    private readonly Button _saveButton = new() { Text = "Save" };
    private readonly Button _cancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };

    public GaugeThresholds? Result { get; private set; }

    public GaugeSettingsForm(GaugeThresholds current)
    {
        Text = "Gauge Colors";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(340, 200);
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

        var resetButton = new Button { Text = "Reset to Defaults", AutoSize = true, Location = new Point(15, 125) };
        resetButton.Click += (_, _) =>
        {
            _amberInput.Value = (decimal)GaugeThresholds.Default.AmberAt;
            _redInput.Value = (decimal)GaugeThresholds.Default.RedAt;
        };

        _saveButton.Location = new Point(150, 160);
        _cancelButton.Location = new Point(235, 160);
        _saveButton.Click += (_, _) =>
        {
            if (_amberInput.Value >= _redInput.Value)
            {
                MessageBox.Show(this, "The amber threshold must be lower than the red threshold.", "Tokenometer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = new GaugeThresholds((double)_amberInput.Value, (double)_redInput.Value);
            DialogResult = DialogResult.OK;
        };

        Controls.Add(info);
        Controls.Add(amberLabel);
        Controls.Add(_amberInput);
        Controls.Add(redLabel);
        Controls.Add(_redInput);
        Controls.Add(resetButton);
        Controls.Add(_saveButton);
        Controls.Add(_cancelButton);
    }
}
