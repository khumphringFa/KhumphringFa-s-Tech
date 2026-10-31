using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace UsbGuard;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new GuardForm());
    }
}

internal sealed class GuardForm : Form
{
    private const string KeyFileName = ".khumphring-usbguard.key";
    private readonly string verifierPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KhumphringFaTech", "UsbGuard", "verifier.dat");
    private readonly ComboBox drives = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly Label status = new() { AutoSize = true, Text = "Not monitoring." };
    private readonly Button pair = new() { Text = "Pair selected USB", AutoSize = true };
    private readonly Button start = new() { Text = "Start monitoring", AutoSize = true };
    private readonly Button stop = new() { Text = "Stop monitoring", AutoSize = true, Enabled = false };
    private readonly Button refresh = new() { Text = "Refresh drives", AutoSize = true };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "KhumphringFaUsbGuard";
    private readonly CheckBox startWithWindows = new() { Text = "Launch with Windows (monitoring remains off)", AutoSize = true };
    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Shield, Text = "USB Guard: not monitoring", Visible = true };
    private readonly ContextMenuStrip trayMenu = new();
    private byte[]? expectedHash;
    private int missingChecks;
    private readonly NumericUpDown lockDelay = new() { Minimum = 3, Maximum = 30, Value = 3, Width = 58 };
    private readonly Button openLogs = new() { Text = "View event log", AutoSize = true };
    private readonly string logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KhumphringFaTech", "UsbGuard", "events.log");
    private bool wasMissing;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    public GuardForm()
    {
        Text = "KhumphringFa's Tech — USB Guard";
        Width = 540;
        Height = 345;
        MinimumSize = new Size(540, 345);
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(18), AutoScroll = true
        };
        layout.Controls.Add(new Label {
            Text = "USB Guard • Windows native lock",
            Font = new Font(Font.FontFamily, 13, FontStyle.Bold),
            AutoSize = true, Margin = new Padding(3, 0, 3, 12)
        });
        var firstRow = new FlowLayoutPanel { AutoSize = true };
        firstRow.Controls.Add(drives);
        firstRow.Controls.Add(refresh);
        layout.Controls.Add(firstRow);
        var secondRow = new FlowLayoutPanel { AutoSize = true };
        secondRow.Controls.Add(pair);
        secondRow.Controls.Add(start);
        secondRow.Controls.Add(stop);
        layout.Controls.Add(secondRow);
        var settingsRow = new FlowLayoutPanel { AutoSize = true };
        settingsRow.Controls.Add(new Label { Text = "Lock after USB missing (seconds):", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        settingsRow.Controls.Add(lockDelay);
        settingsRow.Controls.Add(openLogs);
        layout.Controls.Add(settingsRow);
        layout.Controls.Add(startWithWindows);
        layout.Controls.Add(status);
        layout.Controls.Add(new Label {
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Text = "Removing the paired USB while monitoring locks Windows. " +
                   "Use your Windows Hello PIN, fingerprint, face recognition, or password to sign back in. " +
                   "Inserting the USB does not unlock a native Windows session."
        });
        Controls.Add(layout);

        refresh.Click += (_, _) => RefreshDrives();
        pair.Click += (_, _) => PairSelected();
        start.Click += (_, _) => StartMonitoring();
        stop.Click += (_, _) => StopMonitoring();
        timer.Tick += (_, _) => CheckKey();
        openLogs.Click += (_, _) => {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                if (!File.Exists(logPath)) File.WriteAllText(logPath, "");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = "notepad.exe",
                    Arguments = string.Concat((char)34, logPath, (char)34),
                    UseShellExecute = true
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) {
                MessageBox.Show("Cannot open log: " + ex.Message);
            }
        };
        trayMenu.Items.Add("Show USB Guard", null, (_, _) => ShowFromTray());
        trayMenu.Items.Add("Start monitoring", null, (_, _) => StartMonitoring());
        trayMenu.Items.Add("Stop monitoring", null, (_, _) => StopMonitoring());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit USB Guard", null, (_, _) => {
            timer.Stop();
            Close();
        });
        tray.ContextMenuStrip = trayMenu;
        tray.DoubleClick += (_, _) => ShowFromTray();
        Resize += (_, _) => {
            if (WindowState == FormWindowState.Minimized) {
                Hide();
                tray.BalloonTipTitle = "USB Guard";
                tray.BalloonTipText = "USB Guard is running in the notification area.";
                tray.ShowBalloonTip(1500);
            }
        };
        FormClosing += (_, _) => {
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
        };
        try {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath);
            startWithWindows.Checked = key?.GetValue(StartupValueName) is string;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) {
            status.Text = "Could not read Windows startup settings.";
        }
        startWithWindows.CheckedChanged += (_, _) => SetStartWithWindows();
        RefreshDrives();
        LoadVerifier();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void SetStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
            if (key is null) throw new UnauthorizedAccessException("Startup registry key not accessible.");
            if (startWithWindows.Checked)
                key.SetValue(StartupValueName, "\"" + Application.ExecutablePath + "\"");
            else
                key.DeleteValue(StartupValueName, false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            MessageBox.Show("Cannot change Windows startup: " + ex.Message);
        }
    }

    private void RefreshDrives()
    {
        var previous = drives.SelectedItem?.ToString();
        drives.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    drives.Items.Add(drive.Name);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        if (previous is not null && drives.Items.Contains(previous))
            drives.SelectedItem = previous;
        else if (drives.Items.Count > 0)
            drives.SelectedIndex = 0;
    }

    private void LoadVerifier()
    {
        expectedHash = null;
        if (!File.Exists(verifierPath)) return;
        try
        {
            var encrypted = File.ReadAllBytes(verifierPath);
            var decoded = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            if (decoded.Length == 32) expectedHash = decoded;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            status.Text = "Could not read paired key. Re-pair the drive.";
        }
    }

    private void PairSelected()
    {
        if (timer.Enabled || drives.SelectedItem is not string root)
        {
            MessageBox.Show("Stop monitoring and select a removable USB drive first.");
            return;
        }
        if (MessageBox.Show("Write a new key to " + root +
            "? This will replace a prior USB Guard key on that drive.",
            "Pair USB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        try
        {
            var token = RandomNumberGenerator.GetBytes(32);
            var keyPath = Path.Combine(root, KeyFileName);
            File.WriteAllText(keyPath, Convert.ToBase64String(token), Encoding.ASCII);
            var verifier = SHA256.HashData(token);
            var encrypted = ProtectedData.Protect(verifier, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(verifierPath)!);
            File.WriteAllBytes(verifierPath, encrypted);
            expectedHash = verifier;
            status.Text = "USB paired. You may start monitoring.";
            LogEvent("USB paired");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            MessageBox.Show("Pairing failed: " + ex.Message, "USB Guard",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool IsPairedUsbPresent()
    {
        if (expectedHash is null) return false;
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Removable || !drive.IsReady) continue;
                var keyPath = Path.Combine(drive.RootDirectory.FullName, KeyFileName);
                if (!File.Exists(keyPath)) continue;
                var value = File.ReadAllText(keyPath, Encoding.ASCII).Trim();
                var hash = SHA256.HashData(Convert.FromBase64String(value));
                if (CryptographicOperations.FixedTimeEquals(hash, expectedHash)) return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                     or FormatException or CryptographicException) { }
        }
        return false;
    }

    private void StartMonitoring()
    {
        LoadVerifier();
        if (expectedHash is null)
        {
            MessageBox.Show("Pair a USB drive before starting.");
            return;
        }
        if (!IsPairedUsbPresent())
        {
            MessageBox.Show("Insert the paired USB drive before starting.");
            return;
        }
        missingChecks = 0;
        wasMissing = false;
        timer.Start();
        pair.Enabled = false;
        start.Enabled = false;
        stop.Enabled = true;
        status.Text = "Monitoring active. Paired USB detected.";
        tray.Text = "USB Guard: monitoring";
        LogEvent("Monitoring started");
    }

    private void StopMonitoring()
    {
        timer.Stop();
        pair.Enabled = true;
        start.Enabled = true;
        stop.Enabled = false;
        status.Text = "Monitoring stopped.";
        tray.Text = "USB Guard: not monitoring";
        LogEvent("Monitoring stopped");
        missingChecks = 0;
    }

    private void LogEvent(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") +
                " | " + message + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Failure to write a diagnostic log must never break device monitoring.
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    private void CheckKey()
    {
        if (IsPairedUsbPresent())
        {
            if (wasMissing) LogEvent("Paired USB reconnected before lock");
            wasMissing = false;
            missingChecks = 0;
            status.Text = "Monitoring active. Paired USB detected.";
            return;
        }
        if (!wasMissing) LogEvent("Paired USB became unavailable");
        wasMissing = true;
        missingChecks++;
        status.Text = "USB missing (" + missingChecks + "/" + lockDelay.Value + " seconds).";
        if (missingChecks < (int)lockDelay.Value) return;
        // Lock only once. A real Windows account unlock still requires Windows authentication.
        StopMonitoring();
        status.Text = "USB removed. Windows lock requested. Monitoring stopped.";
        tray.Text = "USB Guard: USB removed";
        bool locked = LockWorkStation();
        LogEvent(locked ? "Windows native lock requested" : "Windows native lock failed");
        if (!locked)
            MessageBox.Show("Windows could not be locked. Check session permissions.");
    }
}
