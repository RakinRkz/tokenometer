namespace Tokenometer;

internal sealed class PromptForm : Form
{
    private readonly TextBox _textBox = new() { Width = 320 };
    private readonly Button _okButton = new() { Text = "OK", DialogResult = DialogResult.OK };
    private readonly Button _cancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };

    public string InputText => _textBox.Text.Trim();

    public PromptForm(string title, string message, string initialValue = "")
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(360, 130);
        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        var label = new Label { Text = message, AutoSize = true, Location = new Point(15, 15), MaximumSize = new Size(330, 0) };
        _textBox.Text = initialValue;
        _textBox.Location = new Point(15, 55);
        _okButton.Location = new Point(190, 90);
        _cancelButton.Location = new Point(275, 90);

        Controls.Add(label);
        Controls.Add(_textBox);
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);
    }
}
