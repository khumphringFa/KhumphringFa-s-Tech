namespace UsbGuard;

/// <summary>
/// A convenience-only screen. It is not a Windows security boundary and can be
/// bypassed with Windows system shortcuts or by terminating the process.
/// </summary>
internal sealed class UsbPresenceScreen : Form
{
    public event EventHandler? StopRequested;

    public UsbPresenceScreen()
    {
        Text = "USB Guard — USB missing";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.FromArgb(18, 28, 47);
        ForeColor = Color.White;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;

        var heading = new Label {
            Text = "USB GUARD",
            AutoSize = true,
            Font = new Font("Segoe UI", 30, FontStyle.Bold),
            ForeColor = Color.White
        };
        var instruction = new Label {
            Text = "USB key removed\n\nReinsert your paired USB to dismiss this screen automatically.",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16),
            ForeColor = Color.White
        };
        var warning = new Label {
            Text = "Convenience screen only — this is NOT the Windows security lock.\nFor stronger protection use Native Windows Lock mode.",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro
        };
        var dismiss = new Button {
            Text = "Stop protection / close screen",
            AutoSize = true,
            MinimumSize = new Size(250, 42)
        };
        dismiss.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);

        var stack = new FlowLayoutPanel {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = BackColor,
            Anchor = AnchorStyles.None,
            Margin = Padding.Empty
        };
        foreach (Control c in new Control[] { heading, instruction, warning, dismiss }) {
            c.Margin = new Padding(10, 12, 10, 12);
            stack.Controls.Add(c);
        }
        Controls.Add(stack);
        Resize += (_, _) => CenterStack();
        Shown += (_, _) => { CenterStack(); Activate(); };
    }

    private void CenterStack()
    {
        if (Controls.Count == 0) return;
        var c = Controls[0];
        c.Location = new Point(
            Math.Max(0, (ClientSize.Width - c.Width) / 2),
            Math.Max(0, (ClientSize.Height - c.Height) / 2));
    }
}
