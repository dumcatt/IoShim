using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using Icon = System.Drawing.Icon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SdvxIoGuiApp
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private IoController _controller;
        private string _settingsPath = "vigem-sdvxio.json";
        
        private NotifyIcon _notifyIcon;
        private Icon _iconActive;
        private Icon _iconInactive;
        private Icon _iconDefault;
        private bool _isActuallyClosing = false;
        
        private class MappingRow
        {
            public SdvxInput InputType { get; set; }
            public ComboBox CmbOutput { get; set; }
            public ComboBox CmbTarget { get; set; }
        }
        
        private class RgbRow
        {
            public int Index { get; set; }
            public ComboBox CmbMode { get; set; }
            public TextBox TxtHex { get; set; }
            public Button BtnColor { get; set; }
            public TextBox TxtSpeed { get; set; }
            public TextBlock LblHex { get; set; }
            public TextBlock LblSpeed { get; set; }
            public TextBlock LblBrightness { get; set; }
            public TextBox TxtBrightness { get; set; }
        }

        private List<MappingRow> _rows = new List<MappingRow>();
        private List<RgbRow> _rgbRows = new List<RgbRow>();

        private bool _isPopulating = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            // Setup Icons
            try
            {
                _iconDefault = System.Drawing.Icon.FromHandle(new System.Drawing.Bitmap("Assets/iidxio.png").GetHicon());
                _iconActive = System.Drawing.Icon.FromHandle(new System.Drawing.Bitmap("Assets/iidxio_active.png").GetHicon());
                _iconInactive = System.Drawing.Icon.FromHandle(new System.Drawing.Bitmap("Assets/iidxio_inactive.png").GetHicon());
            }
            catch { }

            // Setup NotifyIcon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = _iconInactive ?? System.Drawing.SystemIcons.Application;
            _notifyIcon.Text = "KFCShim Configurator";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            var startStopItem = contextMenu.Items.Add("Start KFCShim", null, (s, e) => BtnStartStop_Click(this, new RoutedEventArgs()));
            contextMenu.Items.Add("KFCShim", null, (s, e) => ShowWindow());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, e) => { _isActuallyClosing = true; Close(); });
            _notifyIcon.ContextMenuStrip = contextMenu;

            PopulateUI();

            if (_settings.StartMinimized)
            {
                this.WindowState = WindowState.Minimized;
                if (_settings.MinimizeToTray)
                {
                    this.Hide();
                }
            }

            if (_settings.StartEnabled)
            {
                BtnStartStop_Click(this, new RoutedEventArgs());
            }
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void LoadSettings()
        {
            _settings = AppSettings.Load(_settingsPath);
        }

        private readonly string[] XboxButtons = new string[] {
            "A", "B", "X", "Y", "LeftShoulder", "RightShoulder", "Start", "Back",
            "LeftThumb", "RightThumb", "Guide", "Up", "Down", "Left", "Right"
        };

        private readonly string[] KeyboardKeys = new string[] {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "D0", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9",
            "Escape", "Enter", "Tab", "Space", "LeftShift", "RightShift", "LeftCtrl", "RightCtrl",
            "Insert", "Delete", "Home", "End", "PageUp", "PageDown",
            "Up", "Down", "Left", "Right"
        };

        private void PopulateUI()
        {
            _isPopulating = true;
            
            cmbVolLOut.ItemsSource = Enum.GetValues(typeof(OutputType));
            cmbVolROut.ItemsSource = Enum.GetValues(typeof(OutputType));
            
            cmbVolLAxis.ItemsSource = Enum.GetValues(typeof(AnalogAxis));
            cmbVolRAxis.ItemsSource = Enum.GetValues(typeof(AnalogAxis));

            // Set bindings for Analog Tab
            chkVolL.IsChecked = _settings.VolLAnalog.Enabled;
            cmbVolLOut.SelectedItem = _settings.VolLAnalog.OutputType;
            cmbVolLAxis.SelectedItem = _settings.VolLAnalog.Axis;
            chkVolLRelative.IsChecked = _settings.VolLAnalog.Relative;
            txtVolLSens.Text = _settings.VolLAnalog.Sensitivity.ToString();
            txtVolLDeadzone.Text = _settings.VolLAnalog.Deadzone.ToString();

            chkVolR.IsChecked = _settings.VolRAnalog.Enabled;
            cmbVolROut.SelectedItem = _settings.VolRAnalog.OutputType;
            cmbVolRAxis.SelectedItem = _settings.VolRAnalog.Axis;
            chkVolRRelative.IsChecked = _settings.VolRAnalog.Relative;
            txtVolRSens.Text = _settings.VolRAnalog.Sensitivity.ToString();
            txtVolRDeadzone.Text = _settings.VolRAnalog.Deadzone.ToString();

            // Outputs
            cmbMasterMode.ItemsSource = Enum.GetValues(typeof(MasterMode));
            cmbMasterMode.SelectedItem = _settings.Mode;

            txtDllPath.Text = _settings.DllPath;

            chkEnableButtonLights.IsChecked = _settings.EnableButtonLights;
            slPrimaryVol.Value = _settings.PrimaryVol;
            slHeadphoneVol.Value = _settings.HeadphoneVol;
            slSubwooferVol.Value = _settings.SubwooferVol;

            chkStartWithWindows.IsChecked = _settings.StartWithWindows;
            chkStartMinimized.IsChecked = _settings.StartMinimized;
            chkMinimizeToTray.IsChecked = _settings.MinimizeToTray;
            chkStartEnabled.IsChecked = _settings.StartEnabled;
            chkUseInterception.IsChecked = _settings.UseInterception;

            PopulateInputsTab();
            PopulateRgbTab();

            _isPopulating = false;
        }

        private void PopulateCmbOutputs(ComboBox cmb, bool allowMouse = true)
        {
            if (allowMouse)
                cmb.ItemsSource = Enum.GetValues(typeof(OutputType));
            else
                cmb.ItemsSource = Enum.GetValues(typeof(OutputType)).Cast<OutputType>().Where(o => o != OutputType.Mouse).ToArray();
        }

        private void PopulateInputsTab()
        {
            MappingsPanel.Children.Clear();
            _rows.Clear();

            foreach (SdvxInput input in Enum.GetValues(typeof(SdvxInput)))
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                
                string labelText = input.ToString();
                if (input == SdvxInput.A || input == SdvxInput.B || input == SdvxInput.C || input == SdvxInput.D)
                    labelText = "BT-" + labelText;

                var lbl = new TextBlock { Text = labelText, Width = 100, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(lbl);

                var cmbOut = new ComboBox { Width = 120, Margin = new Thickness(0, 0, 10, 0) };
                PopulateCmbOutputs(cmbOut, allowMouse: false);
                
                var cmbTarget = new ComboBox { Width = 150 };

                cmbOut.SelectionChanged += (s, e) =>
                {
                    UpdateTargetCombo(cmbOut, cmbTarget);
                };

                Mapping map = _settings.Bindings[input];
                cmbOut.SelectedItem = map.OutputType;
                UpdateTargetCombo(cmbOut, cmbTarget);
                
                if (map.OutputType == OutputType.Keyboard)
                    cmbTarget.SelectedItem = map.KeyboardKey;
                else if (map.OutputType != OutputType.None)
                    cmbTarget.SelectedItem = map.XboxButton;

                sp.Children.Add(cmbOut);
                sp.Children.Add(cmbTarget);

                MappingsPanel.Children.Add(sp);
                _rows.Add(new MappingRow { InputType = input, CmbOutput = cmbOut, CmbTarget = cmbTarget });
            }
        }

        private void PopulateRgbTab()
        {
            RgbPanel.Children.Clear();
            _rgbRows.Clear();

            string[] rgbNames = new string[] {
                "Wing Upper L:", "Wing Upper R:", "Wing Lower L:", "Wing Lower R:", "Subwoofer:", "Controller:"
            };

            for (int i = 0; i < 6; i++)
            {
                var row = new RgbRow { Index = i };
                var map = _settings.RgbLights[i];

                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
                sp.Children.Add(new TextBlock { Text = rgbNames[i], Width = 100, VerticalAlignment = VerticalAlignment.Center });

                row.CmbMode = new ComboBox { Width = 100, Margin = new Thickness(0, 0, 10, 0) };
                row.CmbMode.ItemsSource = Enum.GetValues(typeof(RgbMode));
                row.CmbMode.SelectedItem = map.Mode;

                row.LblHex = new TextBlock { Text = "Hex: ", VerticalAlignment = VerticalAlignment.Center };
                row.TxtHex = new TextBox { Width = 70, Text = map.HexColor, Margin = new Thickness(0, 0, 5, 0) };
                row.BtnColor = new Button { Content = "Color...", Width = 50, Margin = new Thickness(0, 0, 10, 0) };
                row.BtnColor.Click += (s, e) => {
                    using (var colorDialog = new System.Windows.Forms.ColorDialog())
                    {
                        try { colorDialog.Color = System.Drawing.ColorTranslator.FromHtml(row.TxtHex.Text); } catch { }
                        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            row.TxtHex.Text = System.Drawing.ColorTranslator.ToHtml(colorDialog.Color);
                        }
                    }
                };

                row.LblSpeed = new TextBlock { Text = "Speed (ms): ", VerticalAlignment = VerticalAlignment.Center };
                row.TxtSpeed = new TextBox { Width = 50, Text = map.SpeedMs.ToString(), Margin = new Thickness(0, 0, 10, 0) };

                row.LblBrightness = new TextBlock { Text = "Brightness: ", VerticalAlignment = VerticalAlignment.Center };
                row.TxtBrightness = new TextBox { Width = 30, Text = map.Brightness.ToString() };

                row.CmbMode.SelectionChanged += (s, e) => UpdateRgbVisibility(row);

                sp.Children.Add(row.CmbMode);
                sp.Children.Add(row.LblHex);
                sp.Children.Add(row.TxtHex);
                sp.Children.Add(row.BtnColor);
                sp.Children.Add(row.LblSpeed);
                sp.Children.Add(row.TxtSpeed);
                sp.Children.Add(row.LblBrightness);
                sp.Children.Add(row.TxtBrightness);

                UpdateRgbVisibility(row);

                RgbPanel.Children.Add(sp);
                _rgbRows.Add(row);
            }
        }

        private void UpdateRgbVisibility(RgbRow row)
        {
            var mode = (RgbMode)row.CmbMode.SelectedItem;
            bool isOff = mode == RgbMode.Off;
            
            row.LblHex.Visibility = mode == RgbMode.Solid ? Visibility.Visible : Visibility.Collapsed;
            row.TxtHex.Visibility = mode == RgbMode.Solid ? Visibility.Visible : Visibility.Collapsed;
            row.BtnColor.Visibility = mode == RgbMode.Solid ? Visibility.Visible : Visibility.Collapsed;

            row.LblSpeed.Visibility = (mode == RgbMode.Breathing || mode == RgbMode.Cycle) ? Visibility.Visible : Visibility.Collapsed;
            row.TxtSpeed.Visibility = (mode == RgbMode.Breathing || mode == RgbMode.Cycle) ? Visibility.Visible : Visibility.Collapsed;
            
            row.LblBrightness.Visibility = !isOff ? Visibility.Visible : Visibility.Collapsed;
            row.TxtBrightness.Visibility = !isOff ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateTargetCombo(ComboBox cmbOut, ComboBox cmbTarget)
        {
            var outType = (OutputType)cmbOut.SelectedItem;
            if (outType == OutputType.Keyboard)
            {
                cmbTarget.ItemsSource = KeyboardKeys;
                cmbTarget.IsEnabled = true;
            }
            else if (outType == OutputType.None)
            {
                cmbTarget.ItemsSource = null;
                cmbTarget.IsEnabled = false;
            }
            else // Xbox pads
            {
                cmbTarget.ItemsSource = XboxButtons;
                cmbTarget.IsEnabled = true;
            }
        }

        private void SaveSettingsFromUI()
        {
            _settings.DllPath = txtDllPath.Text;
            
            _settings.VolLAnalog.Enabled = chkVolL.IsChecked == true;
            _settings.VolLAnalog.OutputType = (OutputType)cmbVolLOut.SelectedItem;
            if (cmbVolLAxis.SelectedItem != null)
                _settings.VolLAnalog.Axis = (AnalogAxis)cmbVolLAxis.SelectedItem;
            _settings.VolLAnalog.Relative = chkVolLRelative.IsChecked == true;
            short.TryParse(txtVolLSens.Text, out short lSens);
            _settings.VolLAnalog.Sensitivity = lSens;
            short.TryParse(txtVolLDeadzone.Text, out short lDeadzone);
            _settings.VolLAnalog.Deadzone = lDeadzone;

            _settings.VolRAnalog.Enabled = chkVolR.IsChecked == true;
            _settings.VolRAnalog.OutputType = (OutputType)cmbVolROut.SelectedItem;
            if (cmbVolRAxis.SelectedItem != null)
                _settings.VolRAnalog.Axis = (AnalogAxis)cmbVolRAxis.SelectedItem;
            _settings.VolRAnalog.Relative = chkVolRRelative.IsChecked == true;
            short.TryParse(txtVolRSens.Text, out short rSens);
            _settings.VolRAnalog.Sensitivity = rSens;
            short.TryParse(txtVolRDeadzone.Text, out short rDeadzone);
            _settings.VolRAnalog.Deadzone = rDeadzone;

            _settings.EnableButtonLights = chkEnableButtonLights.IsChecked == true;

            _settings.PrimaryVol = (byte)slPrimaryVol.Value;
            _settings.HeadphoneVol = (byte)slHeadphoneVol.Value;
            _settings.SubwooferVol = (byte)slSubwooferVol.Value;

            _settings.StartWithWindows = chkStartWithWindows.IsChecked == true;
            _settings.StartMinimized = chkStartMinimized.IsChecked == true;
            _settings.MinimizeToTray = chkMinimizeToTray.IsChecked == true;
            _settings.StartEnabled = chkStartEnabled.IsChecked == true;
            _settings.UseInterception = chkUseInterception.IsChecked == true;

            foreach (var row in _rows)
            {
                var map = _settings.Bindings[row.InputType];
                map.OutputType = (OutputType)row.CmbOutput.SelectedItem;
                
                string target = row.CmbTarget.SelectedItem?.ToString() ?? "";
                if (map.OutputType == OutputType.Keyboard)
                    map.KeyboardKey = target;
                else if (map.OutputType != OutputType.None)
                    map.XboxButton = target;
            }

            foreach (var row in _rgbRows)
            {
                _settings.RgbLights[row.Index].Mode = (RgbMode)row.CmbMode.SelectedItem;
                _settings.RgbLights[row.Index].HexColor = row.TxtHex.Text;
                if (int.TryParse(row.TxtSpeed.Text, out int speed))
                    _settings.RgbLights[row.Index].SpeedMs = speed;
                if (byte.TryParse(row.TxtBrightness.Text, out byte bright))
                    _settings.RgbLights[row.Index].Brightness = bright;
            }

            _settings.Save(_settingsPath);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            MessageBox.Show("Settings saved!");
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                txtDllPath.Text = dlg.FileName;
            }
        }

        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_controller != null && _controller.IsRunning)
            {
                _controller.Stop();
                _controller = null;
                txtStatus.Text = "Status: Stopped";
                txtStatus.Foreground = Brushes.Red;
                btnStartStop.Content = "Start";
                btnStartStop.Background = Brushes.Green;
                if (_notifyIcon != null && _iconInactive != null)
                    _notifyIcon.Icon = _iconInactive;
                if (_notifyIcon != null)
                    _notifyIcon.ContextMenuStrip.Items[0].Text = "Start KFCShim";
            }
            else
            {
                SaveSettingsFromUI();
                _controller = new IoController(_settings);
                bool res = _controller.Start();
                if (res)
                {
                    txtStatus.Text = "Status: Running";
                    txtStatus.Foreground = Brushes.Green;
                    btnStartStop.Content = "Stop";
                    btnStartStop.Background = Brushes.Red;
                    if (_notifyIcon != null && _iconActive != null)
                        _notifyIcon.Icon = _iconActive;
                    if (_notifyIcon != null)
                        _notifyIcon.ContextMenuStrip.Items[0].Text = "Stop KFCShim";
                }
                else
                {
                    _controller = null;
                    MessageBox.Show("Failed to start IoController. Check DLL path or drivers.");
                }
            }
        }

        private void CmbMasterMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPopulating) return;
            if (cmbMasterMode.SelectedItem is MasterMode mode)
            {
                _settings.Mode = mode;
                if (mode == MasterMode.Gamepad)
                {
                    AppSettings.ApplyGamepadPreset(_settings);
                }
                else if (mode == MasterMode.Keyboard)
                {
                    AppSettings.ApplyKeyboardPreset(_settings);
                }
                else
                {
                    return; // Custom does not override current
                }

                PopulateUI();
                SaveSettingsFromUI();
            }
        }

        private void ChkStartWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            if (_isPopulating) return;
            UpdateStartupShortcut(chkStartWithWindows.IsChecked == true);
        }

        private void UpdateStartupShortcut(bool enable)
        {
            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string linkPath = System.IO.Path.Combine(startupPath, "KFCShim.lnk");
                
                string[] oldLinkPaths = { 
                    System.IO.Path.Combine(startupPath, "IidxIoGuiApp.lnk"),
                    System.IO.Path.Combine(startupPath, "2DXShim.lnk") 
                };
                foreach(var oldPath in oldLinkPaths)
                {
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                if (enable)
                {
                    if (!System.IO.File.Exists(linkPath))
                    {
                        Type t = Type.GetTypeFromProgID("WScript.Shell");
                        if (t != null)
                        {
                            dynamic shell = Activator.CreateInstance(t);
                            var shortcut = shell.CreateShortcut(linkPath);
                            shortcut.TargetPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KFCShim.exe");
                            shortcut.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                            shortcut.Save();
                        }
                    }
                }
                else
                {
                    if (System.IO.File.Exists(linkPath))
                    {
                        System.IO.File.Delete(linkPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to modify shortcut: " + ex.Message);
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized && chkMinimizeToTray.IsChecked == true)
            {
                this.Hide();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isActuallyClosing && chkMinimizeToTray.IsChecked == true)
            {
                e.Cancel = true;
                this.WindowState = WindowState.Minimized;
                this.Hide();
            }
            else
            {
                if (_controller != null && _controller.IsRunning)
                {
                    _controller.Stop();
                }
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
            }
        }

        private void BtnResetConfig_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Are you sure you want to reset all configuration to Gamepad defaults?", "Reset Config", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
            {
                _settingsPath = "vigem-sdvxio.json";
                if (System.IO.File.Exists(_settingsPath))
                {
                    System.IO.File.Delete(_settingsPath);
                }
                _settings = AppSettings.Load(_settingsPath);
                PopulateUI();
                SaveSettingsFromUI();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
