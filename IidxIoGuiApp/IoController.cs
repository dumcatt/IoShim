using System;
using System.Collections.Generic;
using System.Threading;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using IidxIoGuiApp;

namespace IidxIoGuiApp
{
    public class IoController : IDisposable
    {
        private ViGEmClient _client;
        private IXbox360Controller _pad1, _pad2, _pad3;
        private Thread _pollThread;
        private bool _running = false;
        
        public bool IsRunning => _running;

        private int _16SegScrollOffset = 0;
        private int _lastNeonToggleTime = 0;
        private bool _neonState = false;

        private AppSettings _settings;

        // Turntable state
        private byte[] _lastTtRaw = new byte[2];
        private int[] _ttStateBtn = new int[2];
        private int[] _ttStateAnalog = new int[2];

        // Keep track of which keys are currently pressed so we can release them properly
        private HashSet<(ushort vk, string name)> _pressedKeys = new HashSet<(ushort vk, string name)>();
        private HashSet<(ushort vk, string name)> _newPressedKeys = new HashSet<(ushort vk, string name)>();

        private DateTime _lastScrollTime = DateTime.Now;

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
                // In Nefarius.ViGEm.Client, buttons might not have a simple Name property accessible this way, 
                // but we can just map the strings manually.
                // We will just do a switch on the string.
                if (string.Equals(name, "Up", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.Up) { btn = b; return true; }
                if (string.Equals(name, "Down", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.Down) { btn = b; return true; }
                if (string.Equals(name, "Left", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.Left) { btn = b; return true; }
                if (string.Equals(name, "Right", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.Right) { btn = b; return true; }
                if (string.Equals(name, "Start", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.Start) { btn = b; return true; }
                if (string.Equals(name, "Back", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.Back) { btn = b; return true; }
                if (string.Equals(name, "LeftThumb", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.LeftThumb) { btn = b; return true; }
                if (string.Equals(name, "RightThumb", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.RightThumb) { btn = b; return true; }
                if (string.Equals(name, "LeftShoulder", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.LeftShoulder) { btn = b; return true; }
                if (string.Equals(name, "RightShoulder", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.RightShoulder) { btn = b; return true; }
                if (string.Equals(name, "Guide", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.Guide) { btn = b; return true; }
                if (string.Equals(name, "A", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.A) { btn = b; return true; }
                if (string.Equals(name, "B", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.B) { btn = b; return true; }
                if (string.Equals(name, "X", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.X) { btn = b; return true; }
                if (string.Equals(name, "Y", StringComparison.OrdinalIgnoreCase) && b == Xbox360Button.Y) { btn = b; return true; }
            }
            btn = Xbox360Button.A;
            return false;
        }

        private static bool TryParseXboxSlider(string name, out Xbox360Slider slider)
        {
            if (string.Equals(name, "LeftTrigger", StringComparison.OrdinalIgnoreCase)) { slider = Xbox360Slider.LeftTrigger; return true; }
            if (string.Equals(name, "RightTrigger", StringComparison.OrdinalIgnoreCase)) { slider = Xbox360Slider.RightTrigger; return true; }
            slider = Xbox360Slider.LeftTrigger;
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
                if (!IidxIo.Load(_settings.DllPath))
                {
                    Console.WriteLine("Failed to load IidxIo backend from " + _settings.DllPath);
                    return false;
                }

                if (!IidxIo.Init())
                {
                    Console.WriteLine("Failed to init IidxIo backend");
                    IidxIo.Unload();
                    return false;
                }
            
                _client = new ViGEmClient();
                _pad1 = _client.CreateXbox360Controller();
                _pad2 = _client.CreateXbox360Controller();
                _pad3 = _client.CreateXbox360Controller();

                _pad1.AutoSubmitReport = false;
                _pad2.AutoSubmitReport = false;
                _pad3.AutoSubmitReport = false;

                _pad1.Connect();
                _pad2.Connect();
                _pad3.Connect();

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
            
            if (_pad1 != null) { _pad1.Disconnect(); _pad1 = null; }
            if (_pad2 != null) { _pad2.Disconnect(); _pad2 = null; }
            if (_pad3 != null) { _pad3.Disconnect(); _pad3 = null; }

            if (_client != null) { _client.Dispose(); _client = null; }

            try
            {
                // Release all keys
                foreach (var k in _pressedKeys)
                {
                    KeyboardHelper.SendKey(k.vk, k.name, false, _settings.UseInterception);
                }
                _pressedKeys.Clear();
                
                IidxIo.Fini();
                IidxIo.Unload();
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
        }

        private void PollLoop()
        {
            while (_running)
            {
                if (!IidxIo.Recv())
                    continue;

                if (_settings.NeonMode == NeonMode.On)
                {
                    IidxIo.SetTopNeons(true);
                }
                else if (_settings.NeonMode == NeonMode.Off)
                {
                    IidxIo.SetTopNeons(false);
                }
                else if (_settings.NeonMode == NeonMode.Cycle)
                {
                    if (Environment.TickCount - _lastNeonToggleTime >= _settings.NeonCycleIntervalMs)
                    {
                        _neonState = !_neonState;
                        IidxIo.SetTopNeons(_neonState);
                        _lastNeonToggleTime = Environment.TickCount;
                    }
                }

                ushort keys = IidxIo.GetKeys();
                byte panel = IidxIo.GetPanel();
                byte sys = IidxIo.GetSys();
                
                if (_settings.EnableReactiveButtonLights)
                {
                    IidxIo.SetDeckLights(keys);
                    IidxIo.SetPanelLights(panel);
                }

                byte[] tt = new byte[] { IidxIo.GetTurntable(0), IidxIo.GetTurntable(1) };

                // Clear previous state for controllers
                ResetPad(_pad1);
                ResetPad(_pad2);
                ResetPad(_pad3);
                _newPressedKeys.Clear();

                // Check digital inputs
                CheckInput(IidxInput.P1_1, IsBitSet(keys, 0), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P1_2, IsBitSet(keys, 1), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P1_3, IsBitSet(keys, 2), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P1_4, IsBitSet(keys, 3), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P1_5, IsBitSet(keys, 4), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P1_6, IsBitSet(keys, 5), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P1_7, IsBitSet(keys, 6), _pad1, _pad2, _pad3);

                CheckInput(IidxInput.P2_1, IsBitSet(keys, 7), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P2_2, IsBitSet(keys, 8), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P2_3, IsBitSet(keys, 9), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P2_4, IsBitSet(keys, 10), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P2_5, IsBitSet(keys, 11), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P2_6, IsBitSet(keys, 12), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P2_7, IsBitSet(keys, 13), _pad1, _pad2, _pad3);

                CheckInput(IidxInput.P1_Start, IsBitSet(panel, 0), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.P2_Start, IsBitSet(panel, 1), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.Vefx, IsBitSet(panel, 2), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.Effect, IsBitSet(panel, 3), _pad1, _pad2, _pad3);

                CheckInput(IidxInput.Sys_Test, IsBitSet(sys, 0), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.Sys_Service, IsBitSet(sys, 1), _pad1, _pad2, _pad3);
                CheckInput(IidxInput.Sys_Coin, IsBitSet(sys, 2), _pad1, _pad2, _pad3);

                // Handle Turntable
                HandleTurntable(0, tt[0], _pad1, _pad2, _pad3, IidxInput.TT1_Inc, IidxInput.TT1_Dec, _settings.TT1Analog);
                HandleTurntable(1, tt[1], _pad1, _pad2, _pad3, IidxInput.TT2_Inc, IidxInput.TT2_Dec, _settings.TT2Analog);

                // Handle Fader Sliders
                for (byte i = 0; i < 5; i++)
                {
                    var map = _settings.Sliders[i];
                    if (map.Enabled && map.OutputType != OutputType.None && map.OutputType != OutputType.Keyboard)
                    {
                        byte sliderVal = IidxIo.GetSlider(i); // 0 to 15
                        IXbox360Controller pad = map.OutputType == OutputType.XboxPad1 ? _pad1 : map.OutputType == OutputType.XboxPad2 ? _pad2 : _pad3;

                        if (TryParseXboxSlider(map.XboxAnalogTarget, out var targetSlider))
                        {
                            byte val = (byte)(sliderVal * 255 / 15);
                            pad.SetSliderValue(targetSlider, val);
                        }
                        else if (TryParseXboxAxis(map.XboxAnalogTarget, out var targetAxis))
                        {
                            short val = (short)((sliderVal * 65535 / 15) - 32768);
                            pad.SetAxisValue(targetAxis, val);
                        }
                    }
                }

                // Send Xbox reports
                _pad1.SubmitReport();
                _pad2.SubmitReport();
                _pad3.SubmitReport();

                // Resolve Keyboards releases
                foreach (var k in _pressedKeys)
                {
                    if (!_newPressedKeys.Contains(k))
                    {
                        KeyboardHelper.SendKey(k.vk, k.name, false, _settings.UseInterception);
                    }
                }
                foreach (var k in _newPressedKeys)
                {
                    if (!_pressedKeys.Contains(k))
                    {
                        KeyboardHelper.SendKey(k.vk, k.name, true, _settings.UseInterception);
                    }
                }
                _pressedKeys = new HashSet<(ushort vk, string name)>(_newPressedKeys);

                // Handle 16Seg Scroll
                if (!string.IsNullOrEmpty(_settings.Text16Seg) && _settings.ScrollSpeedMs > 0)
                {
                    if ((DateTime.Now - _lastScrollTime).TotalMilliseconds >= _settings.ScrollSpeedMs)
                    {
                        _lastScrollTime = DateTime.Now;
                        _16SegScrollOffset++;
                        string padded = _settings.Text16Seg + "         ";
                        if (_16SegScrollOffset >= _settings.Text16Seg.Length + 9)
                            _16SegScrollOffset = 0;

                        string disp = "";
                        for (int i = 0; i < 9; i++)
                        {
                            int idx = (_16SegScrollOffset + i) % padded.Length;
                            disp += padded[idx];
                        }
                        IidxIo.Write16Seg(disp);
                    }
                }
                else if (!string.IsNullOrEmpty(_settings.Text16Seg))
                {
                    string txt = (_settings.Text16Seg + "         ").Substring(0, 9);
                    IidxIo.Write16Seg(txt);
                }

                // Try to send lights based on keys just for visual feedback (simple approach)
                IidxIo.SetDeckLights(keys);
                IidxIo.SetPanelLights(panel);
                if (!IidxIo.Send())
                {
                    Console.WriteLine("IidxIo send failed");
                    break;
                }

                Thread.Sleep(1);
            }
        }

        private bool IsBitSet(uint value, int pos)
        {
            return (value & (1 << pos)) != 0;
        }

        private void ResetPad(IXbox360Controller pad)
        {
            foreach (var btn in AllXboxButtons) pad.SetButtonState(btn, false);
            pad.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
            pad.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            pad.SetAxisValue(Xbox360Axis.RightThumbX, 0);
            pad.SetAxisValue(Xbox360Axis.RightThumbY, 0);
            pad.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
            pad.SetSliderValue(Xbox360Slider.RightTrigger, 0);
        }

        private void CheckInput(IidxInput input, bool isPressed, IXbox360Controller r1, IXbox360Controller r2, IXbox360Controller r3)
        {
            if (!isPressed) return;
            if (!_settings.Bindings.TryGetValue(input, out var map)) return;

            ApplyMap(map, r1, r2, r3);
        }

        private void ApplyMap(Mapping map, IXbox360Controller r1, IXbox360Controller r2, IXbox360Controller r3)
        {
            if (map.OutputType == OutputType.Keyboard)
            {
                ushort vk = KeyboardHelper.GetVkCode(map.KeyboardKey);
                if (vk > 0)
                {
                    _newPressedKeys.Add((vk, map.KeyboardKey));
                }
            }
            else if (map.OutputType == OutputType.XboxPad1 || map.OutputType == OutputType.XboxPad2 || map.OutputType == OutputType.XboxPad3)
            {
                if (TryParseXboxButton(map.XboxButton, out var btn))
                {
                    if (map.OutputType == OutputType.XboxPad1) r1.SetButtonState(btn, true);
                    else if (map.OutputType == OutputType.XboxPad2) r2.SetButtonState(btn, true);
                    else if (map.OutputType == OutputType.XboxPad3) r3.SetButtonState(btn, true);
                }
            }
        }

        private void HandleTurntable(int idx, byte raw, IXbox360Controller r1, IXbox360Controller r2, IXbox360Controller r3, IidxInput incInput, IidxInput decInput, TtAnalogMapping analogMap)
        {
            // Simple button translation for Turntable like vigem-iidxio
            int lastRaw = _lastTtRaw[idx];
            int delta = (sbyte)(raw - lastRaw); // Wrapping difference
            
            // Basic debounce and state tracker for button emu
            int state = _ttStateBtn[idx];
            if (delta > 0 && delta >= 2 && state < 0) state = 0;
            else if (delta < 0 && Math.Abs(delta) >= 2 && state > 0) state = 0;

            int maxBtn = 40;
            int inc = 5;
            int threshold = 10;

            if (delta > 0 && state < maxBtn) state += inc;
            else if (delta < 0 && Math.Abs(state) < maxBtn) state -= inc;

            if (state > 0) state--;
            else if (state < 0) state++;

            _ttStateBtn[idx] = state;
            _lastTtRaw[idx] = raw;

            if (state > threshold) CheckInput(incInput, true, r1, r2, r3);
            else if (state < -threshold) CheckInput(decInput, true, r1, r2, r3);

            // Analog translation
            if (analogMap.Enabled && analogMap.OutputType != OutputType.None)
            {
                short axisVal = 0;
                if (analogMap.Relative)
                {
                    int ast = _ttStateAnalog[idx];
                    if (delta == 0) ast /= 2;
                    else
                    {
                        ast += delta * analogMap.Sensitivity;
                        if (ast > short.MaxValue) ast = short.MaxValue;
                        if (ast < short.MinValue) ast = short.MinValue;
                    }
                    _ttStateAnalog[idx] = ast;
                    axisVal = (short)ast;
                }
                else
                {
                    axisVal = (short)((raw * 256) - 32768); // Map 0-255 to short
                }

                if (analogMap.OutputType == OutputType.XboxPad1) r1.SetAxisValue(Xbox360Axis.LeftThumbX, axisVal);
                else if (analogMap.OutputType == OutputType.XboxPad2) r2.SetAxisValue(Xbox360Axis.LeftThumbX, axisVal);
                else if (analogMap.OutputType == OutputType.XboxPad3) r3.SetAxisValue(Xbox360Axis.LeftThumbX, axisVal);
            }
        }
    }
}
