using System;
using System.Collections.Generic;
using System.Threading;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace SdvxIoGuiApp
{
    public class IoController : IDisposable
    {
        private ViGEmClient _client;
        private IXbox360Controller _pad;
        private Thread _pollThread;
        private bool _running = false;
        
        public bool IsRunning => _running;

        private AppSettings _settings;

        // Spinner state
        private ushort[] _lastSpinnerRaw = new ushort[2];
        private int[] _spinnerAcc = new int[2];
        private int[] _absSpinnerPos = new int[2];
        private int[] _deadzoneAcc = new int[2];
        private int[] _spinnerHoldTimer = new int[2];
        private int[] _spinnerHoldDir = new int[2];

        // Keep track of which keys are currently pressed so we can release them properly
        private HashSet<(ushort vk, string name)> _pressedKeys = new HashSet<(ushort vk, string name)>();
        private HashSet<(ushort vk, string name)> _newPressedKeys = new HashSet<(ushort vk, string name)>();

        private static readonly Xbox360Button[] AllXboxButtons = new Xbox360Button[]
        {
            Xbox360Button.Up, Xbox360Button.Down, Xbox360Button.Left, Xbox360Button.Right,
            Xbox360Button.Start, Xbox360Button.Back, Xbox360Button.LeftThumb, Xbox360Button.RightThumb,
            Xbox360Button.LeftShoulder, Xbox360Button.RightShoulder, Xbox360Button.Guide,
            Xbox360Button.A, Xbox360Button.B, Xbox360Button.X, Xbox360Button.Y
        };

        private static bool TryParseXboxButton(string name, out Xbox360Button btn)
        {
            foreach (var b in AllXboxButtons)
            {
                if (string.Equals(name, b.ToString(), StringComparison.OrdinalIgnoreCase)) { btn = b; return true; }
            }
            btn = Xbox360Button.A;
            return false;
        }

        private static bool TryParseXboxAxis(string name, out Xbox360Axis axis)
        {
            if (string.Equals(name, "LeftThumbX", StringComparison.OrdinalIgnoreCase)) { axis = Xbox360Axis.LeftThumbX; return true; }
            if (string.Equals(name, "LeftThumbY", StringComparison.OrdinalIgnoreCase)) { axis = Xbox360Axis.LeftThumbY; return true; }
            if (string.Equals(name, "RightThumbX", StringComparison.OrdinalIgnoreCase)) { axis = Xbox360Axis.RightThumbX; return true; }
            if (string.Equals(name, "RightThumbY", StringComparison.OrdinalIgnoreCase)) { axis = Xbox360Axis.RightThumbY; return true; }
            axis = Xbox360Axis.LeftThumbX;
            return false;
        }

        public IoController(AppSettings settings)
        {
            _settings = settings;
        }

        public bool Start()
        {
            if (_running) return true;

            try
            {
                if (!SdvxIo.Init())
                {
                    Console.WriteLine("Failed to init SDVX IO backend");
                    return false;
                }
            
                _client = new ViGEmClient();
                _pad = _client.CreateXbox360Controller();

                _pad.AutoSubmitReport = false;
                _pad.Connect();

                SdvxIo.SetAmpVolume(_settings.PrimaryVol, _settings.HeadphoneVol, _settings.SubwooferVol);

                _running = true;
                _pollThread = new Thread(PollLoop);
                _pollThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception starting IoController: " + ex.Message);
                Stop();
                return false;
            }
        }

        public void Stop()
        {
            _running = false;
            if (_pollThread != null && _pollThread.IsAlive)
            {
                _pollThread.Join(2000);
            }
            
            if (_pad != null) { _pad.Disconnect(); _pad = null; }
            if (_client != null) { _client.Dispose(); _client = null; }
            
            foreach (var key in _pressedKeys)
            {
                KeyboardHelper.ReleaseKey(key.vk, key.name);
            }
            _pressedKeys.Clear();
            
            SdvxIo.Fini();
        }

        public void Dispose()
        {
            Stop();
        }

        private void PollLoop()
        {
            int rgbTick = 0;
            while (_running)
            {
                if (SdvxIo.ReadInput())
                {
                    _newPressedKeys.Clear();

                    byte sys = SdvxIo.GetSys();
                    ushort bank0 = SdvxIo.GetGpio(0);
                    ushort bank1 = SdvxIo.GetGpio(1);

                    // Map Input State
                    bool isTest = (sys & (1 << 5)) != 0;
                    bool isService = (sys & (1 << 4)) != 0;
                    bool isCoin = (sys & (1 << 2)) != 0;

                    bool isC = (bank0 & (1 << 0)) != 0;
                    bool isB = (bank0 & (1 << 1)) != 0;
                    bool isA = (bank0 & (1 << 2)) != 0;
                    bool isStart = (bank0 & (1 << 3)) != 0;
                    
                    bool isFxR = (bank1 & (1 << 3)) != 0;
                    bool isFxL = (bank1 & (1 << 4)) != 0;
                    bool isD = (bank1 & (1 << 5)) != 0;

                    // Read Spinners
                    ushort spinL = SdvxIo.GetSpinner(0);
                    ushort spinR = SdvxIo.GetSpinner(1);

                    int deltaL = ProcessSpinner(0, spinL, out bool spinLLeft, out bool spinLRight);
                    int deltaR = ProcessSpinner(1, spinR, out bool spinRLeft, out bool spinRRight);

                    // Reset Pads
                    if (_pad != null) { _pad.SetAxisValue(Xbox360Axis.LeftThumbX, 0); _pad.SetAxisValue(Xbox360Axis.LeftThumbY, 0); _pad.SetAxisValue(Xbox360Axis.RightThumbX, 0); _pad.SetAxisValue(Xbox360Axis.RightThumbY, 0); }

                    // Process Spinners Analog
                    if (_settings.VolLAnalog.Enabled)
                    {
                        int dz = _settings.VolLAnalog.Deadzone;
                        _deadzoneAcc[0] += deltaL;
                        
                        int dL = 0;
                        if (_deadzoneAcc[0] > dz)
                        {
                            dL = _deadzoneAcc[0] - dz;
                            _deadzoneAcc[0] = dz;
                        }
                        else if (_deadzoneAcc[0] < -dz)
                        {
                            dL = _deadzoneAcc[0] + dz;
                            _deadzoneAcc[0] = -dz;
                        }

                        if (_settings.VolLAnalog.OutputType == OutputType.Mouse)
                        {
                            int dx = _settings.VolLAnalog.Axis == AnalogAxis.X ? dL * _settings.VolLAnalog.Sensitivity / 100 : 0;
                            int dy = _settings.VolLAnalog.Axis == AnalogAxis.Y ? dL * _settings.VolLAnalog.Sensitivity / 100 : 0;
                            if (dx != 0 || dy != 0) KeyboardHelper.MoveMouse(dx, dy);
                        }
                        else if (_settings.VolLAnalog.OutputType != OutputType.None && _settings.VolLAnalog.OutputType != OutputType.Keyboard)
                        {
                            var pad = GetPad(_settings.VolLAnalog.OutputType);
                            if (pad != null) 
                            {
                                short axisVal = 0;
                                if (_settings.VolLAnalog.Relative)
                                {
                                    axisVal = (short)Math.Clamp(dL * _settings.VolLAnalog.Sensitivity, short.MinValue, short.MaxValue);
                                }
                                else
                                {
                                    _absSpinnerPos[0] += dL * _settings.VolLAnalog.Sensitivity;
                                    
                                    if (_absSpinnerPos[0] > short.MaxValue) _absSpinnerPos[0] -= 65536;
                                    else if (_absSpinnerPos[0] < short.MinValue) _absSpinnerPos[0] += 65536;
                                    
                                    axisVal = (short)_absSpinnerPos[0];
                                }
                                pad.SetAxisValue(GetAxisFromEnum(_settings.VolLAnalog.Axis), axisVal);
                            }
                        }
                    }

                    if (_settings.VolRAnalog.Enabled)
                    {
                        int dz = _settings.VolRAnalog.Deadzone;
                        _deadzoneAcc[1] += deltaR;
                        
                        int dR = 0;
                        if (_deadzoneAcc[1] > dz)
                        {
                            dR = _deadzoneAcc[1] - dz;
                            _deadzoneAcc[1] = dz;
                        }
                        else if (_deadzoneAcc[1] < -dz)
                        {
                            dR = _deadzoneAcc[1] + dz;
                            _deadzoneAcc[1] = -dz;
                        }

                        if (_settings.VolRAnalog.OutputType == OutputType.Mouse)
                        {
                            int dx = _settings.VolRAnalog.Axis == AnalogAxis.X ? dR * _settings.VolRAnalog.Sensitivity / 100 : 0;
                            int dy = _settings.VolRAnalog.Axis == AnalogAxis.Y ? dR * _settings.VolRAnalog.Sensitivity / 100 : 0;
                            if (dx != 0 || dy != 0) KeyboardHelper.MoveMouse(dx, dy);
                        }
                        else if (_settings.VolRAnalog.OutputType != OutputType.None && _settings.VolRAnalog.OutputType != OutputType.Keyboard)
                        {
                            var pad = GetPad(_settings.VolRAnalog.OutputType);
                            if (pad != null)
                            {
                                short axisVal = 0;
                                if (_settings.VolRAnalog.Relative)
                                {
                                    axisVal = (short)Math.Clamp(dR * _settings.VolRAnalog.Sensitivity, short.MinValue, short.MaxValue);
                                }
                                else
                                {
                                    _absSpinnerPos[1] += dR * _settings.VolRAnalog.Sensitivity;
                                    
                                    if (_absSpinnerPos[1] > short.MaxValue) _absSpinnerPos[1] -= 65536;
                                    else if (_absSpinnerPos[1] < short.MinValue) _absSpinnerPos[1] += 65536;
                                    
                                    axisVal = (short)_absSpinnerPos[1];
                                }
                                pad.SetAxisValue(GetAxisFromEnum(_settings.VolRAnalog.Axis), axisVal);
                            }
                        }
                    }

                    // Process Buttons
                    ProcessButton(SdvxInput.A, isA);
                    ProcessButton(SdvxInput.B, isB);
                    ProcessButton(SdvxInput.C, isC);
                    ProcessButton(SdvxInput.D, isD);
                    ProcessButton(SdvxInput.FX_L, isFxL);
                    ProcessButton(SdvxInput.FX_R, isFxR);
                    ProcessButton(SdvxInput.Start, isStart);
                    ProcessButton(SdvxInput.Sys_Test, isTest);
                    ProcessButton(SdvxInput.Sys_Service, isService);
                    ProcessButton(SdvxInput.Sys_Coin, isCoin);
                    
                    ProcessButton(SdvxInput.Vol_L_Left, spinLLeft);
                    ProcessButton(SdvxInput.Vol_L_Right, spinLRight);
                    ProcessButton(SdvxInput.Vol_R_Left, spinRLeft);
                    ProcessButton(SdvxInput.Vol_R_Right, spinRRight);

                    // Release keys not in _newPressedKeys
                    var toRelease = new List<(ushort vk, string name)>();
                    foreach (var key in _pressedKeys)
                    {
                        if (!_newPressedKeys.Contains(key))
                        {
                            KeyboardHelper.ReleaseKey(key.vk, key.name);
                            toRelease.Add(key);
                        }
                    }
                    foreach (var key in toRelease)
                        _pressedKeys.Remove(key);

                    // Submit pad reports
                    if (_pad != null) _pad.SubmitReport();
                    
                    // Outputs (Lights)
                    uint gpioLights = 0;
                    if (_settings.EnableButtonLights)
                    {
                        if (isD) gpioLights |= (1u << 0);
                        if (isFxL) gpioLights |= (1u << 1);
                        if (isFxR) gpioLights |= (1u << 2);
                        if (isStart) gpioLights |= (1u << 12);
                        if (isA) gpioLights |= (1u << 13);
                        if (isB) gpioLights |= (1u << 14);
                        if (isC) gpioLights |= (1u << 15);
                    }
                    SdvxIo.SetGpioLights(gpioLights);

                    // Process RGB Lights
                    rgbTick++;
                    for (int i = 0; i < 6; i++)
                    {
                        var rgb = _settings.RgbLights[i];
                        byte r = 0, g = 0, b = 0;

                        if (rgb.Mode != RgbMode.Off)
                        {
                            System.Drawing.Color c = System.Drawing.Color.White;
                            try { c = System.Drawing.ColorTranslator.FromHtml(rgb.HexColor); } catch { }

                            if (rgb.Mode == RgbMode.Solid)
                            {
                                r = c.R; g = c.G; b = c.B;
                            }
                            else if (rgb.Mode == RgbMode.Breathing)
                            {
                                double cycle = (rgbTick * 16.666 / Math.Max(100, rgb.SpeedMs)) * Math.PI;
                                double mult = (Math.Sin(cycle) + 1) / 2.0;
                                r = (byte)(c.R * mult); g = (byte)(c.G * mult); b = (byte)(c.B * mult);
                            }
                            else if (rgb.Mode == RgbMode.Cycle)
                            {
                                double hue = (rgbTick * 16.666 / Math.Max(100, rgb.SpeedMs)) % 1.0;
                                ColorFromHSV(hue, 1.0, 1.0, out r, out g, out b);
                            }

                            r = (byte)(r * rgb.Brightness / 255);
                            g = (byte)(g * rgb.Brightness / 255);
                            b = (byte)(b * rgb.Brightness / 255);
                        }

                        // Each group uses 3 PWM channels
                        byte baseCh = (byte)(i * 3);
                        SdvxIo.SetPwmLight(baseCh, r);
                        SdvxIo.SetPwmLight((byte)(baseCh + 1), g);
                        SdvxIo.SetPwmLight((byte)(baseCh + 2), b);
                    }

                    SdvxIo.WriteOutput();
                }

                Thread.Sleep(2);
            }
        }

        private int ProcessSpinner(int index, ushort currentRaw, out bool isLeft, out bool isRight)
        {
            isLeft = false;
            isRight = false;
            
            short delta = (short)(currentRaw - _lastSpinnerRaw[index]);
            // Handle 10-bit wrap around (1024)
            // But usually just reading short delta works since it's 16-bit? Wait, if it's 10-bit, mask to 1023
            currentRaw &= 0x3FF;
            delta = (short)(currentRaw - (_lastSpinnerRaw[index] & 0x3FF));
            
            if (delta > 512) delta -= 1024;
            else if (delta < -512) delta += 1024;

            _lastSpinnerRaw[index] = currentRaw;

            _spinnerAcc[index] += delta;
            
            // Digital knob turns threshold
            if (_spinnerAcc[index] > 10)
            {
                _spinnerHoldDir[index] = 1;
                _spinnerHoldTimer[index] = 25; // 50ms hold
                _spinnerAcc[index] = 0;
            }
            else if (_spinnerAcc[index] < -10)
            {
                _spinnerHoldDir[index] = -1;
                _spinnerHoldTimer[index] = 25;
                _spinnerAcc[index] = 0;
            }
            else
            {
                // decay
                if (_spinnerAcc[index] > 0) _spinnerAcc[index]--;
                if (_spinnerAcc[index] < 0) _spinnerAcc[index]++;
            }

            if (_spinnerHoldTimer[index] > 0)
            {
                _spinnerHoldTimer[index]--;
                if (_spinnerHoldDir[index] == 1) isRight = true;
                else if (_spinnerHoldDir[index] == -1) isLeft = true;
            }

            return delta;
        }

        private void ProcessButton(SdvxInput input, bool state)
        {
            if (!_settings.Bindings.TryGetValue(input, out var map)) return;
            if (map.OutputType == OutputType.None) return;

            if (map.OutputType == OutputType.Keyboard)
            {
                if (state && !string.IsNullOrEmpty(map.KeyboardKey))
                {
                    KeyboardHelper.TryParseKey(map.KeyboardKey, out ushort vk);
                    _newPressedKeys.Add((vk, map.KeyboardKey));
                    if (!_pressedKeys.Contains((vk, map.KeyboardKey)))
                    {
                        KeyboardHelper.PressKey(vk, map.KeyboardKey);
                        _pressedKeys.Add((vk, map.KeyboardKey));
                    }
                }
            }
            else
            {
                var pad = GetPad(map.OutputType);
                if (pad != null && TryParseXboxButton(map.XboxButton, out var xboxBtn))
                {
                    pad.SetButtonState(xboxBtn, state);
                }
            }
        }

        private IXbox360Controller GetPad(OutputType type)
        {
            if (type == OutputType.XboxPad) return _pad;
            return null;
        }

        private static void ColorFromHSV(double hue, double saturation, double value, out byte r, out byte g, out byte b)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / (60.0 / 360.0))) % 6;
            double f = hue / (60.0 / 360.0) - Math.Floor(hue / (60.0 / 360.0));

            value = value * 255;
            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));

            if (hi == 0) { r = v; g = t; b = p; }
            else if (hi == 1) { r = q; g = v; b = p; }
            else if (hi == 2) { r = p; g = v; b = t; }
            else if (hi == 3) { r = p; g = q; b = v; }
            else if (hi == 4) { r = t; g = p; b = v; }
            else { r = v; g = p; b = q; }
        }
        private Xbox360Axis GetAxisFromEnum(AnalogAxis axis)
        {
            switch (axis)
            {
                case AnalogAxis.X: return Xbox360Axis.LeftThumbX;
                case AnalogAxis.Y: return Xbox360Axis.LeftThumbY;
                case AnalogAxis.RX: return Xbox360Axis.RightThumbX;
                case AnalogAxis.RY: return Xbox360Axis.RightThumbY;
                default: return Xbox360Axis.LeftThumbX;
            }
        }
    }
}
