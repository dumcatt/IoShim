using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SdvxIoGuiApp
{
    public enum OutputType
    {
        None,
        Keyboard,
        Mouse,
        XboxPad
    }

    public enum AnalogAxis
    {
        X,
        Y,
        RX,
        RY
    }

    public enum MasterMode
    {
        Gamepad,
        Keyboard,
        Custom
    }

    public enum RgbMode
    {
        Off,
        Solid,
        Cycle,
        Breathing
    }

    public enum SdvxInput
    {
        A, B, C, D,
        FX_L, FX_R,
        Start,
        Sys_Test, Sys_Service, Sys_Coin,
        Vol_L_Left, Vol_L_Right,
        Vol_R_Left, Vol_R_Right
    }

    public class Mapping
    {
        public OutputType OutputType { get; set; } = OutputType.None;
        public string XboxButton { get; set; } = "";
        public string KeyboardKey { get; set; } = "";
    }

    public class VolAnalogMapping
    {
        public bool Enabled { get; set; } = false;
        public OutputType OutputType { get; set; } = OutputType.XboxPad;
        public AnalogAxis Axis { get; set; } = AnalogAxis.X;
        public bool Relative { get; set; } = true;
        public short Sensitivity { get; set; } = 400;
        public short Deadzone { get; set; } = 0;
    }

    public class RgbSetting
    {
        public RgbMode Mode { get; set; } = RgbMode.Solid;
        public string HexColor { get; set; } = "#FFFFFF";
        public int SpeedMs { get; set; } = 1000;
        public byte Brightness { get; set; } = 255;
    }

    public class AppSettings
    {
        public MasterMode Mode { get; set; } = MasterMode.Gamepad;
        public string DllPath { get; set; } = "sdvxio.dll";
        
        // Button Mappings
        public Dictionary<SdvxInput, Mapping> Bindings { get; set; } = new Dictionary<SdvxInput, Mapping>();

        // Button LEDs
        public bool EnableButtonLights { get; set; } = true;

        // RGB Lights (6 groups)
        public RgbSetting[] RgbLights { get; set; } = new RgbSetting[6]
        {
            new RgbSetting(), new RgbSetting(), new RgbSetting(),
            new RgbSetting(), new RgbSetting(), new RgbSetting()
        };

        // Amp Volume
        public byte PrimaryVol { get; set; } = 0;   // 0 = Max, 96 = Min
        public byte HeadphoneVol { get; set; } = 0;
        public byte SubwooferVol { get; set; } = 0;

        // Vol Analog Mappings
        public VolAnalogMapping VolLAnalog { get; set; } = new VolAnalogMapping { OutputType = OutputType.XboxPad };
        public VolAnalogMapping VolRAnalog { get; set; } = new VolAnalogMapping { OutputType = OutputType.XboxPad };

        // System Settings
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool StartEnabled { get; set; } = false;
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
            
            settings.Bindings[SdvxInput.A] = new Mapping { OutputType = OutputType.XboxPad, XboxButton = "A" };
            settings.Bindings[SdvxInput.B] = new Mapping { OutputType = OutputType.XboxPad, XboxButton = "B" };
            settings.Bindings[SdvxInput.C] = new Mapping { OutputType = OutputType.XboxPad, XboxButton = "X" };
            settings.Bindings[SdvxInput.D] = new Mapping { OutputType = OutputType.XboxPad, XboxButton = "Y" };
            settings.Bindings[SdvxInput.FX_L] = new Mapping { OutputType = OutputType.XboxPad, XboxButton = "LeftShoulder" };
            settings.Bindings[SdvxInput.FX_R] = new Mapping { OutputType = OutputType.XboxPad, XboxButton = "RightShoulder" };
            settings.Bindings[SdvxInput.Start] = new Mapping { OutputType = OutputType.XboxPad, XboxButton = "Start" };
            
            settings.Bindings[SdvxInput.Sys_Test] = new Mapping { OutputType = OutputType.XboxPad, XboxButton = "Back" };
            settings.Bindings[SdvxInput.Sys_Service] = new Mapping { OutputType = OutputType.None };
            settings.Bindings[SdvxInput.Sys_Coin] = new Mapping { OutputType = OutputType.None };
            
            settings.Bindings[SdvxInput.Vol_L_Left] = new Mapping { OutputType = OutputType.None };
            settings.Bindings[SdvxInput.Vol_L_Right] = new Mapping { OutputType = OutputType.None };
            settings.Bindings[SdvxInput.Vol_R_Left] = new Mapping { OutputType = OutputType.None };
            settings.Bindings[SdvxInput.Vol_R_Right] = new Mapping { OutputType = OutputType.None };

            settings.VolLAnalog.OutputType = OutputType.XboxPad;
            settings.VolLAnalog.Axis = AnalogAxis.X;
            settings.VolRAnalog.OutputType = OutputType.XboxPad;
            settings.VolRAnalog.Axis = AnalogAxis.Y;

            EnsureAllBindings(settings);
        }

        public static void ApplyKeyboardPreset(AppSettings settings)
        {
            settings.Mode = MasterMode.Keyboard;

            // default keymap for kb mode should be DFJK for ABCD, CM for FXL and FXR and enter for start.
            // for test, service and coin use TAB, ESC and INSERT 
            settings.Bindings[SdvxInput.A] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "D" };
            settings.Bindings[SdvxInput.B] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "F" };
            settings.Bindings[SdvxInput.C] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "J" };
            settings.Bindings[SdvxInput.D] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "K" };
            settings.Bindings[SdvxInput.FX_L] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "C" };
            settings.Bindings[SdvxInput.FX_R] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "M" };
            settings.Bindings[SdvxInput.Start] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Enter" };
            
            settings.Bindings[SdvxInput.Sys_Test] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Escape" };
            settings.Bindings[SdvxInput.Sys_Service] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Tab" };
            settings.Bindings[SdvxInput.Sys_Coin] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "Insert" };
            
            settings.Bindings[SdvxInput.Vol_L_Left] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "W" };
            settings.Bindings[SdvxInput.Vol_L_Right] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "E" };
            settings.Bindings[SdvxInput.Vol_R_Left] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "I" };
            settings.Bindings[SdvxInput.Vol_R_Right] = new Mapping { OutputType = OutputType.Keyboard, KeyboardKey = "O" };

            settings.VolLAnalog.OutputType = OutputType.Mouse;
            settings.VolLAnalog.Axis = AnalogAxis.X;
            settings.VolRAnalog.OutputType = OutputType.Mouse;
            settings.VolRAnalog.Axis = AnalogAxis.Y;

            EnsureAllBindings(settings);
        }

        private static void EnsureAllBindings(AppSettings settings)
        {
            foreach (SdvxInput val in Enum.GetValues(typeof(SdvxInput)))
            {
                if (!settings.Bindings.ContainsKey(val))
                {
                    settings.Bindings[val] = new Mapping { OutputType = OutputType.None };
                }
            }
        }
    }
}
