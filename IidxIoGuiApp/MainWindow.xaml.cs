using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using Icon = System.Drawing.Icon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using System.Diagnostics;

namespace IidxIoGuiApp
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private IoController _controller;
        private string _settingsPath = "vigem-iidxio.json";
        
        private NotifyIcon _notifyIcon;
        private Icon _iconActive;
        private Icon _iconInactive;
        private Icon _iconDefault;
        private bool _isActuallyClosing = false;
        
        private class MappingRow
        {
            public IidxInput InputType { get; set; }
            public ComboBox CmbOutput { get; set; }
            public ComboBox CmbTarget { get; set; }
        }
        
        private class SliderRow
        {
            public int Index { get; set; }
            public CheckBox ChkEnabled { get; set; }
            public ComboBox CmbOutput { get; set; }
            public ComboBox CmbTarget { get; set; }
        }

        private List<MappingRow> _rows = new List<MappingRow>();
        private List<SliderRow> _sliderRows = new List<SliderRow>();

        private bool _isPopulating = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            // Setup Icons
            _iconDefault = System.Drawing.Icon.FromHandle(new System.Drawing.Bitmap("Assets/iidxio.png").GetHicon());
            _iconActive = System.Drawing.Icon.FromHandle(new System.Drawing.Bitmap("Assets/iidxio_active.png").GetHicon());
            _iconInactive = System.Drawing.Icon.FromHandle(new System.Drawing.Bitmap("Assets/iidxio_inactive.png").GetHicon());

            // Setup NotifyIcon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = _iconInactive;
            _notifyIcon.Text = "IIDX IO Configurator";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            var startStopItem = contextMenu.Items.Add("Start iidxio", null, (s, e) => BtnStartStop_Click(this, new RoutedEventArgs()));
            contextMenu.Items.Add("iidxio", null, (s, e) => ShowWindow());
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

        private readonly string[] XboxAnalogs = new string[] {
            "LeftTrigger", "RightTrigger", "LeftThumbX", "LeftThumbY", "RightThumbX", "RightThumbY"
        };

        private readonly string[] KeyboardKeys = new string[] {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "D0", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9",
            "Escape", "Enter", "Tab", "Space", "LeftShift", "RightShift", "LeftCtrl", "RightCtrl",
            "Up", "Down", "Left", "Right"
        };

        private void PopulateUI()
        {
            _isPopulating = true;
            
            PopulateCmbOutputs(cmbTT1Output);
            cmbMasterMode.ItemsSource = Enum.GetValues(typeof(MasterMode));
            cmbNeonMode.ItemsSource = Enum.GetValues(typeof(NeonMode));

            cmbMasterMode.SelectedItem = _settings.Mode;

            txtDllPath.Text = _settings.DllPath;
            txt16Seg.Text = _settings.Text16Seg;
            txtScrollSpeed.Text = _settings.ScrollSpeedMs.ToString();

            // Turntable Analog
            chkTT1AnalogEnabled.IsChecked = _settings.TT1Analog.Enabled;
            chkTT1Relative.IsChecked = _settings.TT1Analog.Relative;
            txtTT1Sens.Text = _settings.TT1Analog.Sensitivity.ToString();
            PopulateCmbOutputs(cmbTT1Output);
            cmbTT1Output.SelectedItem = _settings.TT1Analog.OutputType;

            chkTT2AnalogEnabled.IsChecked = _settings.TT2Analog.Enabled;
            chkTT2Relative.IsChecked = _settings.TT2Analog.Relative;
            txtTT2Sens.Text = _settings.TT2Analog.Sensitivity.ToString();
            PopulateCmbOutputs(cmbTT2Output);
            cmbTT2Output.SelectedItem = _settings.TT2Analog.OutputType;

            cmbNeonMode.SelectedItem = _settings.NeonMode;
            txtNeonInterval.Text = _settings.NeonCycleIntervalMs.ToString();
            chkReactiveLights.IsChecked = _settings.EnableReactiveButtonLights;

            chkStartWithWindows.IsChecked = _settings.StartWithWindows;
            chkStartMinimized.IsChecked = _settings.StartMinimized;
            chkMinimizeToTray.IsChecked = _settings.MinimizeToTray;
            chkUseInterception.IsChecked = _settings.UseInterception;

            // Generate rows
            MappingsPanel.Children.Clear();
            _rows.Clear();

            foreach (IidxInput input in Enum.GetValues(typeof(IidxInput)))
            {
                var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                
                var lbl = new TextBlock { Text = input.ToString(), Width = 100, VerticalAlignment = VerticalAlignment.Center };
                
                var cmb = new ComboBox { Width = 120, Margin = new Thickness(5, 0, 5, 0) };
                PopulateCmbOutputs(cmb);
                
                var cmbTarget = new ComboBox { Width = 120, VerticalAlignment = VerticalAlignment.Center, IsEditable = true };
                
                Mapping map = _settings.Bindings[input];
                cmb.SelectedItem = map.OutputType;
                
                UpdateMappingTargetOptions(cmbTarget, map.OutputType);
                cmbTarget.Text = map.OutputType == OutputType.Keyboard ? map.KeyboardKey : map.XboxButton;

                cmb.SelectionChanged += (s, e) => {
                    if (cmb.SelectedItem is OutputType ot)
                    {
                        UpdateMappingTargetOptions(cmbTarget, ot);
                        CheckMixedMode();
                    }
                };

                rowPanel.Children.Add(lbl);
                rowPanel.Children.Add(cmb);
                rowPanel.Children.Add(cmbTarget);
                
                MappingsPanel.Children.Add(rowPanel);
                
                _rows.Add(new MappingRow { InputType = input, CmbOutput = cmb, CmbTarget = cmbTarget });
            }

            // Generate slider rows
            SlidersPanel.Children.Clear();
            _sliderRows.Clear();
            for (int i = 0; i < 5; i++)
            {
                var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                
                var lbl = new TextBlock { Text = $"Slider {i}", Width = 60, VerticalAlignment = VerticalAlignment.Center };
                var chk = new CheckBox { Content = "Enabled", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                
                var lblOut = new TextBlock { Text = "Output: ", VerticalAlignment = VerticalAlignment.Center };
                var cmb = new ComboBox { Width = 100, Margin = new Thickness(0, 0, 10, 0) };
                PopulateCmbOutputs(cmb);
                
                var lblTarget = new TextBlock { Text = "Target: ", VerticalAlignment = VerticalAlignment.Center };
                var cmbTarget = new ComboBox { Width = 100, VerticalAlignment = VerticalAlignment.Center, IsEditable = true };
                cmbTarget.ItemsSource = XboxAnalogs;
                
                var map = _settings.Sliders[i];
                chk.IsChecked = map.Enabled;
                cmb.SelectedItem = map.OutputType;
                cmbTarget.Text = map.XboxAnalogTarget;

                rowPanel.Children.Add(lbl);
                rowPanel.Children.Add(chk);
                rowPanel.Children.Add(lblOut);
                rowPanel.Children.Add(cmb);
                rowPanel.Children.Add(lblTarget);
                rowPanel.Children.Add(cmbTarget);

                SlidersPanel.Children.Add(rowPanel);
                _sliderRows.Add(new SliderRow { Index = i, ChkEnabled = chk, CmbOutput = cmb, CmbTarget = cmbTarget });
            }

            _isPopulating = false;
        }

        private void UpdateMappingTargetOptions(ComboBox cmbTarget, OutputType ot)
        {
            if (ot == OutputType.Keyboard)
            {
                cmbTarget.ItemsSource = KeyboardKeys;
            }
            else if (ot == OutputType.XboxPad1 || ot == OutputType.XboxPad2 || ot == OutputType.XboxPad3)
            {
                cmbTarget.ItemsSource = XboxButtons;
            }
            else
            {
                cmbTarget.ItemsSource = null;
            }
        }

        private void CheckMixedMode()
        {
            if (_isPopulating) return;
            
            bool hasKeyboard = false;
            bool hasGamepad = false;

            foreach (var row in _rows)
            {
                if (row.CmbOutput.SelectedItem is OutputType ot)
                {
                    if (ot == OutputType.Keyboard) hasKeyboard = true;
                    if (ot == OutputType.XboxPad1 || ot == OutputType.XboxPad2 || ot == OutputType.XboxPad3) hasGamepad = true;
                }
            }

            if (hasKeyboard && hasGamepad)
            {
                cmbMasterMode.SelectedItem = MasterMode.Custom;
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
                    PopulateUI();
                }
                else if (mode == MasterMode.Keyboard)
                {
                    AppSettings.ApplyKeyboardPreset(_settings);
                    PopulateUI();
                }
            }
        }

        private void PopulateCmbOutputs(ComboBox cmb)
        {
            foreach (OutputType ot in Enum.GetValues(typeof(OutputType)))
            {
                cmb.Items.Add(ot);
            }
        }

        private void SaveSettingsFromUI()
        {
            _settings.DllPath = txtDllPath.Text;
            _settings.Text16Seg = txt16Seg.Text;
            if (int.TryParse(txtScrollSpeed.Text, out int spd)) _settings.ScrollSpeedMs = spd;

            _settings.TT1Analog.Enabled = chkTT1AnalogEnabled.IsChecked == true;
            _settings.TT1Analog.Relative = chkTT1Relative.IsChecked == true;
            if (short.TryParse(txtTT1Sens.Text, out short s1)) _settings.TT1Analog.Sensitivity = s1;
            if (cmbTT1Output.SelectedItem is OutputType ot1) _settings.TT1Analog.OutputType = ot1;

            _settings.TT2Analog.Enabled = chkTT2AnalogEnabled.IsChecked == true;
            _settings.TT2Analog.Relative = chkTT2Relative.IsChecked == true;
            if (short.TryParse(txtTT2Sens.Text, out short s2)) _settings.TT2Analog.Sensitivity = s2;
            if (cmbTT2Output.SelectedItem is OutputType ot2) _settings.TT2Analog.OutputType = ot2;

            foreach (var row in _rows)
            {
                var map = _settings.Bindings[row.InputType];
                if (row.CmbOutput.SelectedItem is OutputType ot)
                {
                    map.OutputType = ot;
                    if (ot == OutputType.Keyboard)
                    {
                        map.KeyboardKey = row.CmbTarget.Text;
                    }
                    else
                    {
                        map.XboxButton = row.CmbTarget.Text;
                    }
                }
            }

            foreach (var row in _sliderRows)
            {
                var map = _settings.Sliders[row.Index];
                map.Enabled = row.ChkEnabled.IsChecked == true;
                if (row.CmbOutput.SelectedItem is OutputType ot) map.OutputType = ot;
                map.XboxAnalogTarget = row.CmbTarget.Text;
            }

            if (cmbNeonMode.SelectedItem is NeonMode nm) _settings.NeonMode = nm;
            if (int.TryParse(txtNeonInterval.Text, out int nInterval)) _settings.NeonCycleIntervalMs = nInterval;
            
            _settings.EnableReactiveButtonLights = chkReactiveLights.IsChecked == true;

            _settings.StartWithWindows = chkStartWithWindows.IsChecked == true;
            _settings.StartMinimized = chkStartMinimized.IsChecked == true;
            _settings.MinimizeToTray = chkMinimizeToTray.IsChecked == true;
            _settings.UseInterception = chkUseInterception.IsChecked == true;

            _settings.Save(_settingsPath);
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void BtnResetConfig_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Are you sure you want to reset all configurations to default?", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                if (System.IO.File.Exists(_settingsPath))
                {
                    System.IO.File.Delete(_settingsPath);
                }
                _settings = AppSettings.Load(_settingsPath);
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
                string linkPath = System.IO.Path.Combine(startupPath, "2DXShim.lnk");
                // Remove old named shortcut if it exists
                string oldLinkPath = System.IO.Path.Combine(startupPath, "IidxIoGuiApp.lnk");
                if (System.IO.File.Exists(oldLinkPath))
                {
                    System.IO.File.Delete(oldLinkPath);
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
                            shortcut.TargetPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "2DXShim.exe");
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
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                txtDllPath.Text = dlg.FileName;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            MessageBox.Show("Configuration saved to " + _settingsPath, "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_controller == null)
            {
                try
                {
                    SaveSettingsFromUI();
                    _controller = new IoController(_settings);
                    if (_controller.Start())
                    {
                        btnStartStop.Content = "Stop";
                        txtStatus.Text = "Status: Running";
                        txtStatus.Foreground = Brushes.Green;
                        _notifyIcon.Icon = _iconActive;
                        _notifyIcon.ContextMenuStrip.Items[0].Text = "Stop iidxio";
                    }
                    else
                    {
                        MessageBox.Show("Failed to start IoController. Ensure iidxio.dll is valid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _controller.Dispose();
                        _controller = null;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                _controller.Stop();
                _controller.Dispose();
                _controller = null;
                btnStartStop.Content = "Start";
                txtStatus.Text = "Status: Stopped";
                txtStatus.Foreground = Brushes.Red;
                _notifyIcon.Icon = _iconInactive;
                _notifyIcon.ContextMenuStrip.Items[0].Text = "Start iidxio";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_controller != null)
            {
                _controller.Stop();
                _controller.Dispose();
            }
            base.OnClosed(e);
        }
    }
}