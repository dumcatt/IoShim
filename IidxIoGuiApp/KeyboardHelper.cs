using System;
using System.Runtime.InteropServices;
using InputInterceptorNS;
using System.Collections.Generic;

namespace IidxIoGuiApp
{
    public static class KeyboardHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        private static bool _interceptionInitialized = false;
        private static KeyboardHook _hook = null;

        public static void SendKey(ushort vkCode, string keyName, bool isDown, bool useInterception = false)
        {
            if (useInterception)
            {
                if (!_interceptionInitialized)
                {
                    if (InputInterceptor.Initialize())
                    {
                        _interceptionInitialized = true;
                        _hook = new KeyboardHook(KeyboardFilter.None, (ref KeyStroke s) => { });
                    }
                    else
                    {
                        // Fallback or silently fail if driver is not installed.
                        // We will fallback to SendInput if initialization fails.
                    }
                }

                if (_interceptionInitialized && _hook != null)
                {
                    KeyCode icCode = GetInterceptionKeyCode(keyName);
                    if (icCode != 0) // Assume 0 is invalid/unmapped for our switch
                    {
                        if (isDown)
                            _hook.SimulateKeyDown(icCode);
                        else
                            _hook.SimulateKeyUp(icCode);
                        
                        return; // Successfully sent with interception
                    }
                }
            }

            // Fallback to SendInput
            var inputs = new INPUT[1];
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vkCode,
                        dwFlags = isDown ? 0 : KEYEVENTF_KEYUP
                    }
                }
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static KeyCode GetInterceptionKeyCode(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return (KeyCode)0;

            if (Enum.TryParse<KeyCode>(keyName, true, out var exactMatch))
            {
                return exactMatch;
            }

            return keyName.ToUpper() switch
            {
                "D0" => KeyCode.Zero,
                "D1" => KeyCode.One,
                "D2" => KeyCode.Two,
                "D3" => KeyCode.Three,
                "D4" => KeyCode.Four,
                "D5" => KeyCode.Five,
                "D6" => KeyCode.Six,
                "D7" => KeyCode.Seven,
                "D8" => KeyCode.Eight,
                "D9" => KeyCode.Nine,
                "LEFTCTRL" => KeyCode.Control,
                "RIGHTCTRL" => KeyCode.Control,
                _ => (KeyCode)0
            };
        }

        public static ushort GetVkCode(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return 0;
            if (Enum.TryParse<System.Windows.Input.Key>(keyName, true, out var key))
            {
                return (ushort)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
            }
            // fallback for simple chars like 'A'
            if (keyName.Length == 1)
            {
                return (ushort)char.ToUpper(keyName[0]);
            }
            return 0;
        }
    }
}
