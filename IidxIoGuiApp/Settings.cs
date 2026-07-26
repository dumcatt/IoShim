using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IidxIoGuiApp
{
    public enum OutputType
    {
        None,
        Keyboard,
        XboxPad1,
        XboxPad2,
        XboxPad3,
        XboxPad4
    }

    public enum MasterMode
    {
        Gamepad,
        Keyboard,
        Custom
    }

    public enum NeonMode
    {
        Off,
        On,
        Cycle
    }

    public enum IidxInput
    {
        P1_1, P1_2, P1_3, P1_4, P1_5, P1_6, P1_7,
        P2_1, P2_2, P2_3, P2_4, P2_5, P2_6, P2_7,
        P1_Start, P2_Start, Vefx, Effect,
        Sys_Test, Sys_Service, Sys_Coin,
        TT1_Inc, TT1_Dec,
        TT2_Inc, TT2_Dec
    }

    public class Mapping
    {
        public OutputType OutputType { get; set; } = OutputType.None;
        
        // For Xbox, we will store the enum name as string, e.g. "A", "B", "X", "Y", "Start", "Back", "LeftShoulder"
        public string XboxButton { get; set; } = "";
        
        // For Keyboard, Virtual Key code integer or string representation (e.g. WPF Key enum string)
        public string KeyboardKey { get; set; } = "";
    }

    public class TtAnalogMapping
    {
        public bool Enabled { get; set; } = false;
        public OutputType OutputType { get; set; } = OutputType.XboxPad1; // e.g. XboxPad1 ThumbLX
        public bool Relative { get; set; } = true;
        public short Sensitivity { get; set; } = 400;
    }

    public class SliderMapping
    {
        public bool Enabled { get; set; } = false;
        public OutputType OutputType { get; set; } = OutputType.None;
        // String representing the target axis/slider on the xbox pad (e.g. "LeftThumbY", "LeftTrigger")
        public string XboxAnalogTarget { get; set; } = "";
    }

    public class AppSettings
    {
        public MasterMode Mode { get; set; } = MasterMode.Gamepad;
        public string DllPath { get; set; } = "iidxio.dll";
        
        // 16 segment settings
        public string Text16Seg { get; set; } = "HELLO IO";
        public int ScrollSpeedMs { get; set; } = 500;

        // Button Mappings
        public Dictionary<IidxInput, Mapping> Bindings { get; set; } = new Dictionary<IidxInput, Mapping>();

        // Outputs
        public NeonMode NeonMode { get; set; } = NeonMode.On;
        public int NeonCycleIntervalMs { get; set; } = 500;
        public bool EnableReactiveButtonLights { get; set; } = true;

        // Turntable Analog Mappings
        public TtAnalogMapping TT1Analog { get; set; } = new TtAnalogMapping { OutputType = OutputType.XboxPad1 };
        public TtAnalogMapping TT2Analog { get; set; } = new TtAnalogMapping { OutputType = OutputType.XboxPad2 };

        // Slider Mappings
        public SliderMapping[] Sliders { get; set; } = new SliderMapping[5] 
        {
            new SliderMapping(), new SliderMapping(), new SliderMapping(), new SliderMapping(), new SliderMapping()
        };

        // System Settings
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool UseInterception { get; set; } = false;

        public static AppSettings Load(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }) ?? CreateDefault();
                }
                catch { }
            }
            return CreateDefault();
        }

        public void Save(string path)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private static AppSettings CreateDefault()
        {
            var settings = new AppSettings();
            ApplyGamepadPreset(settings);
            return settings;
        }

        public static void ApplyGamepadPreset(AppSettings settings)
        {
            settings.Mode = MasterMode.Gamepad;
            
            settings.Bindings[IidxInput.P1_1] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "A" };
            settings.Bindings[IidxInput.P1_2] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "B" };
            settings.Bindings[IidxInput.P1_3] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "X" };
            settings.Bindings[IidxInput.P1_4] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "Y" };
            settings.Bindings[IidxInput.P1_5] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "LeftShoulder" };
            settings.Bindings[IidxInput.P1_6] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "RightShoulder" };
            settings.Bindings[IidxInput.P1_7] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "Back" };
            settings.Bindings[IidxInput.P1_Start] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "Start" };
            settings.Bindings[IidxInput.TT1_Inc] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "LeftThumb" };
            settings.Bindings[IidxInput.TT1_Dec] = new Mapping { OutputType = OutputType.XboxPad1, XboxButton = "RightThumb" };

            settings.Bindings[IidxInput.P2_1] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "A" };
            settings.Bindings[IidxInput.P2_2] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "B" };
            settings.Bindings[IidxInput.P2_3] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "X" };
            settings.Bindings[IidxInput.P2_4] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "Y" };
            settings.Bindings[IidxInput.P2_5] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "LeftShoulder" };
            settings.Bindings[IidxInput.P2_6] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "RightShoulder" };
            settings.Bindings[IidxInput.P2_7] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "Back" };
            settings.Bindings[IidxInput.P2_Start] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "Start" };
            settings.Bindings[IidxInput.TT2_Inc] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "LeftThumb" };
            settings.Bindings[IidxInput.TT2_Dec] = new Mapping { OutputType = OutputType.XboxPad2, XboxButton = "RightThumb" };

            settings.Bindings[IidxInput.Sys_Test] = new Mapping { OutputType = OutputType.XboxPad3, XboxButton = "X" };
            settings.Bindings[IidxInput.Sys_Service] = new Mapping { OutputType = OutputType.XboxPad3, XboxButton = "Y" };
            settings.Bindings[IidxInput.Sys_Coin] = new Mapping { OutputType = OutputType.XboxPad3, XboxButton = "Start" };
            settings.Bindings[IidxInput.Vefx] = new Mapping { OutputType = OutputType.XboxPad3, XboxButton = "B" };
            settings.Bindings[IidxInput.Effect] = new Mapping { OutputType = OutputType.XboxPad3, XboxButton = "A" };

            EnsureAllBindings(settings);
        }

        public static void ApplyKeyboardPreset(AppSettings settings)
        {
            settings.Mode = MasterMode.Keyboard;

            settings.Bindings[IidxInput.P1_1] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Z" };
            settings.Bindings[IidxInput.P1_2] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "S" };
            settings.Bindings[IidxInput.P1_3] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "X" };
            settings.Bindings[IidxInput.P1_4] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "D" };
            settings.Bindings[IidxInput.P1_5] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "C" };
            settings.Bindings[IidxInput.P1_6] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "F" };
            settings.Bindings[IidxInput.P1_7] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "V" };
            settings.Bindings[IidxInput.P1_Start] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Q" };
            
            settings.Bindings[IidxInput.P2_1] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "H" };
            settings.Bindings[IidxInput.P2_2] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "U" };
            settings.Bindings[IidxInput.P2_3] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "J" };
            settings.Bindings[IidxInput.P2_4] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "I" };
            settings.Bindings[IidxInput.P2_5] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "K" };
            settings.Bindings[IidxInput.P2_6] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "O" };
            settings.Bindings[IidxInput.P2_7] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "L" };
            settings.Bindings[IidxInput.P2_Start] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "W" };
            
            settings.Bindings[IidxInput.Vefx] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "E" };
            settings.Bindings[IidxInput.Effect] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "R" };

            settings.Bindings[IidxInput.Sys_Test] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Escape" };
            settings.Bindings[IidxInput.Sys_Service] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Enter" };
            settings.Bindings[IidxInput.Sys_Coin] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Tab" };

            settings.Bindings[IidxInput.TT1_Inc] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "LeftShift" };
            settings.Bindings[IidxInput.TT1_Dec] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "LeftCtrl" };
            settings.Bindings[IidxInput.TT2_Inc] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "RightShift" };
            settings.Bindings[IidxInput.TT2_Dec] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "RightCtrl" };

            EnsureAllBindings(settings);
        }

        private static void EnsureAllBindings(AppSettings settings)
        {
            foreach (IidxInput val in Enum.GetValues(typeof(IidxInput)))
            {
                if (!settings.Bindings.ContainsKey(val))
                {
                    settings.Bindings[val] = new Mapping { OutputType = OutputType.None };
                }
            }
        }
    }
}
