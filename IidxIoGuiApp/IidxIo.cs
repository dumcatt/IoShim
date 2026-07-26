using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace IidxIoGuiApp
{
    public static class IidxIo
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogFormatterDelegate(IntPtr message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ThreadCreateDelegate(IntPtr threadFunc, IntPtr ctx);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ThreadJoinDelegate(IntPtr threadId, out IntPtr result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ThreadDestroyDelegate(IntPtr threadId);

        // Keep references to delegates so they don't get garbage collected
        private static LogFormatterDelegate _logMisc, _logInfo, _logWarning, _logFatal;
        private static ThreadCreateDelegate _threadCreate;
        private static ThreadJoinDelegate _threadJoin;
        private static ThreadDestroyDelegate _threadDestroy;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr hModule);

        private static IntPtr _dllHandle = IntPtr.Zero;

        // Function Pointers
        private delegate void SetLoggersDelegate(IntPtr misc, IntPtr info, IntPtr warning, IntPtr fatal);
        private delegate bool InitDelegate(IntPtr create, IntPtr join, IntPtr destroy);
        private delegate void FiniDelegate();
        private delegate void Ep1SetDeckLightsDelegate(ushort lights);
        private delegate void Ep1SetPanelLightsDelegate(byte lights);
        private delegate void Ep1SetTopLampsDelegate(byte lamps);
        private delegate void Ep1SetTopNeonsDelegate(bool neons);
        private delegate bool Ep1SendDelegate();
        private delegate bool Ep2RecvDelegate();
        private delegate byte Ep2GetTurntableDelegate(byte playerNo);
        private delegate byte Ep2GetSliderDelegate(byte sliderNo);
        private delegate byte Ep2GetSysDelegate();
        private delegate byte Ep2GetPanelDelegate();
        private delegate ushort Ep2GetKeysDelegate();
        private delegate bool Ep3Write16SegDelegate([MarshalAs(UnmanagedType.LPStr)] string text);

        private static SetLoggersDelegate _setLoggers;
        private static InitDelegate _init;
        private static FiniDelegate _fini;
        private static Ep1SetDeckLightsDelegate _ep1SetDeckLights;
        private static Ep1SetPanelLightsDelegate _ep1SetPanelLights;
        private static Ep1SetTopLampsDelegate _ep1SetTopLamps;
        private static Ep1SetTopNeonsDelegate _ep1SetTopNeons;
        private static Ep1SendDelegate _ep1Send;
        private static Ep2RecvDelegate _ep2Recv;
        private static Ep2GetTurntableDelegate _ep2GetTurntable;
        private static Ep2GetSliderDelegate _ep2GetSlider;
        private static Ep2GetSysDelegate _ep2GetSys;
        private static Ep2GetPanelDelegate _ep2GetPanel;
        private static Ep2GetKeysDelegate _ep2GetKeys;
        private static Ep3Write16SegDelegate _ep3Write16Seg;

        public static bool Load(string path)
        {
            _dllHandle = LoadLibrary(path);
            if (_dllHandle == IntPtr.Zero) return false;

            _setLoggers = LoadFunction<SetLoggersDelegate>("iidx_io_set_loggers");
            _init = LoadFunction<InitDelegate>("iidx_io_init");
            _fini = LoadFunction<FiniDelegate>("iidx_io_fini");
            _ep1SetDeckLights = LoadFunction<Ep1SetDeckLightsDelegate>("iidx_io_ep1_set_deck_lights");
            _ep1SetPanelLights = LoadFunction<Ep1SetPanelLightsDelegate>("iidx_io_ep1_set_panel_lights");
            _ep1SetTopLamps = LoadFunction<Ep1SetTopLampsDelegate>("iidx_io_ep1_set_top_lamps");
            _ep1SetTopNeons = LoadFunction<Ep1SetTopNeonsDelegate>("iidx_io_ep1_set_top_neons");
            _ep1Send = LoadFunction<Ep1SendDelegate>("iidx_io_ep1_send");
            _ep2Recv = LoadFunction<Ep2RecvDelegate>("iidx_io_ep2_recv");
            _ep2GetTurntable = LoadFunction<Ep2GetTurntableDelegate>("iidx_io_ep2_get_turntable");
            _ep2GetSlider = LoadFunction<Ep2GetSliderDelegate>("iidx_io_ep2_get_slider");
            _ep2GetSys = LoadFunction<Ep2GetSysDelegate>("iidx_io_ep2_get_sys");
            _ep2GetPanel = LoadFunction<Ep2GetPanelDelegate>("iidx_io_ep2_get_panel");
            _ep2GetKeys = LoadFunction<Ep2GetKeysDelegate>("iidx_io_ep2_get_keys");
            _ep3Write16Seg = LoadFunction<Ep3Write16SegDelegate>("iidx_io_ep3_write_16seg");

            return _init != null;
        }

        public static void Unload()
        {
            if (_dllHandle != IntPtr.Zero)
            {
                FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
            }
        }

        private static T LoadFunction<T>(string name) where T : Delegate
        {
            IntPtr ptr = GetProcAddress(_dllHandle, name);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CThreadFunc(IntPtr ctx);

        public static bool Init()
        {
            if (_init == null) return false;

            _logMisc = msg => Console.WriteLine("[MISC] " + Marshal.PtrToStringAnsi(msg));
            _logInfo = msg => Console.WriteLine("[INFO] " + Marshal.PtrToStringAnsi(msg));
            _logWarning = msg => Console.WriteLine("[WARN] " + Marshal.PtrToStringAnsi(msg));
            _logFatal = msg => Console.WriteLine("[FATAL] " + Marshal.PtrToStringAnsi(msg));

            if (_setLoggers != null)
            {
                _setLoggers(
                    Marshal.GetFunctionPointerForDelegate(_logMisc),
                    Marshal.GetFunctionPointerForDelegate(_logInfo),
                    Marshal.GetFunctionPointerForDelegate(_logWarning),
                    Marshal.GetFunctionPointerForDelegate(_logFatal));
            }

            _threadCreate = (funcPtr, ctx) =>
            {
                var threadFunc = Marshal.GetDelegateForFunctionPointer<CThreadFunc>(funcPtr);
                var thread = new Thread(() => threadFunc(ctx));
                thread.Start();
                return (IntPtr)thread.ManagedThreadId; 
            };

            _threadJoin = (IntPtr id, out IntPtr outResult) => { outResult = IntPtr.Zero; };
            _threadDestroy = (IntPtr id) => { };

            return _init(
                Marshal.GetFunctionPointerForDelegate(_threadCreate),
                Marshal.GetFunctionPointerForDelegate(_threadJoin),
                Marshal.GetFunctionPointerForDelegate(_threadDestroy));
        }

        public static void Fini() { _fini?.Invoke(); }
        public static void SetDeckLights(ushort lights) { _ep1SetDeckLights?.Invoke(lights); }
        public static void SetPanelLights(byte lights) { _ep1SetPanelLights?.Invoke(lights); }
        public static void SetTopLamps(byte lamps) { _ep1SetTopLamps?.Invoke(lamps); }
        public static void SetTopNeons(bool neons) { _ep1SetTopNeons?.Invoke(neons); }
        public static bool Send() { return _ep1Send?.Invoke() ?? false; }
        public static bool Recv() { return _ep2Recv?.Invoke() ?? false; }
        public static byte GetTurntable(byte playerNo) { return _ep2GetTurntable?.Invoke(playerNo) ?? 0; }
        public static byte GetSlider(byte sliderNo) { return _ep2GetSlider?.Invoke(sliderNo) ?? 0; }
        public static byte GetSys() { return _ep2GetSys?.Invoke() ?? 0; }
        public static byte GetPanel() { return _ep2GetPanel?.Invoke() ?? 0; }
        public static ushort GetKeys() { return _ep2GetKeys?.Invoke() ?? 0; }
        public static bool Write16Seg(string text) { return _ep3Write16Seg?.Invoke(text) ?? false; }
    }
}
