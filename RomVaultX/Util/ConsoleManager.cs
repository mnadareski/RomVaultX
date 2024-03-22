using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

namespace RomVaultX.Util
{
    /// <summary>
    /// Sourced from https://stackoverflow.com/questions/160587/no-output-to-console-from-a-wpf-application
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public static class ConsoleManager
    {
        private const string Kernel32_DllName = "kernel32.dll";

        [DllImport(Kernel32_DllName)]
        private static extern bool AllocConsole();

        [DllImport(Kernel32_DllName)]
        private static extern bool FreeConsole();

        [DllImport(Kernel32_DllName)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport(Kernel32_DllName)]
        private static extern int GetConsoleOutputCP();

        public static bool HasConsole
        {
            get { return GetConsoleWindow() != IntPtr.Zero; }
        }

        /// <summary>
        /// Creates a new console instance if the process is not attached to a console already.
        /// </summary>
        public static void Show()
        {
            //#if DEBUG
            if (!HasConsole)
            {
                AllocConsole();
                InvalidateOutAndError();
            }
            //#endif
        }

        /// <summary>
        /// If the process has a console attached to it, it will be detached and no longer visible. Writing to the System.Console is still possible, but no output will be shown.
        /// </summary>
        public static void Hide()
        {
            //#if DEBUG
            if (HasConsole)
            {
                SetOutAndErrorNull();
                FreeConsole();
            }
            //#endif
        }

        public static void Toggle()
        {
            if (HasConsole)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        static void InvalidateOutAndError()
        {
            Type type = typeof(Console);

            FieldInfo? _out = type.GetField("_out", BindingFlags.Static | BindingFlags.NonPublic)
                ?? type.GetField("s_out", BindingFlags.Static | BindingFlags.NonPublic);
            Debug.Assert(_out != null);

            FieldInfo? _error = type.GetField("_error", BindingFlags.Static | BindingFlags.NonPublic)
                ?? type.GetField("s_error", BindingFlags.Static | BindingFlags.NonPublic);
            Debug.Assert(_error != null);

            MethodInfo? _InitializeStdOutError = type.GetMethod("InitializeStdOutError", BindingFlags.Static | BindingFlags.NonPublic);
            //Debug.Assert(_InitializeStdOutError != null);

            _out?.SetValue(null, null);
            _error?.SetValue(null, null);

            _InitializeStdOutError?.Invoke(null, [true]);
        }

        static void SetOutAndErrorNull()
        {
            Console.SetOut(System.IO.TextWriter.Null);
            Console.SetError(System.IO.TextWriter.Null);
        }
    }
}
