using System;
using System.Runtime.InteropServices;

namespace SdvxIoGuiApp
{
    public static class SdvxIo
    {
        private const string DllName = "sdvxio.dll";

        public delegate void log_formatter_t(string module, string fmt, string message);
        public delegate IntPtr thread_create_t(IntPtr thread_main, IntPtr ctx, string thread_name, uint stack_sz);
        public delegate void thread_join_t(IntPtr thread, ref int result);
        public delegate void thread_destroy_t(IntPtr thread);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void sdvx_io_set_loggers(
            log_formatter_t misc,
            log_formatter_t info,
            log_formatter_t warning,
            log_formatter_t fatal);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool sdvx_io_init(
            thread_create_t thread_create,
            thread_join_t thread_join,
            thread_destroy_t thread_destroy);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void sdvx_io_fini();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void sdvx_io_set_gpio_lights(uint gpio_lights);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void sdvx_io_set_pwm_light(byte light_no, byte intensity);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool sdvx_io_write_output();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool sdvx_io_read_input();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte sdvx_io_get_input_gpio_sys();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort sdvx_io_get_input_gpio(byte gpio_bank);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort sdvx_io_get_spinner_pos(byte spinner_no);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool sdvx_io_set_amp_volume(
            byte primary, byte headphone, byte subwoofer);


        // Safe Wrappers
        public static bool Init()
        {
            try
            {
                return sdvx_io_init(null, null, null);
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }

        public static void Fini()
        {
            try
            {
                sdvx_io_fini();
            }
            catch { }
        }

        public static bool ReadInput()
        {
            try
            {
                return sdvx_io_read_input();
            }
            catch { return false; }
        }

        public static bool WriteOutput()
        {
            try
            {
                return sdvx_io_write_output();
            }
            catch { return false; }
        }

        public static byte GetSys()
        {
            try { return sdvx_io_get_input_gpio_sys(); }
            catch { return 0; }
        }

        public static ushort GetGpio(byte bank)
        {
            try { return sdvx_io_get_input_gpio(bank); }
            catch { return 0; }
        }

        public static ushort GetSpinner(byte spinnerNo)
        {
            try { return sdvx_io_get_spinner_pos(spinnerNo); }
            catch { return 0; }
        }

        public static void SetGpioLights(uint lights)
        {
            try { sdvx_io_set_gpio_lights(lights); }
            catch { }
        }

        public static void SetPwmLight(byte lightNo, byte intensity)
        {
            try { sdvx_io_set_pwm_light(lightNo, intensity); }
            catch { }
        }

        public static void SetAmpVolume(byte primary, byte headphone, byte subwoofer)
        {
            try { sdvx_io_set_amp_volume(primary, headphone, subwoofer); }
            catch { }
        }
    }
}
