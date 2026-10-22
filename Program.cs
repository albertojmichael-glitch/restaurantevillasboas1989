using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PizzariaTycoon
{
    internal class Program
    {
        private const int STD_INPUT_HANDLE = -10;
        private const uint ENABLE_MOUSE_INPUT = 0x0010;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;
        private const ushort KEY_EVENT = 0x0001;
        private const ushort MOUSE_EVENT = 0x0002;
        private const uint FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001;
        private const uint MOUSE_MOVED = 0x0001;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
        private static extern bool ReadConsoleInput(
            IntPtr hConsoleInput,
            [Out] INPUT_RECORD[] lpBuffer,
            uint nLength,
            out uint lpNumberOfEventsRead);

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT_RECORD
        {
            [FieldOffset(0)] public ushort EventType;
            [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
            [FieldOffset(4)] public MOUSE_EVENT_RECORD MouseEvent;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct KEY_EVENT_RECORD
        {
            [MarshalAs(UnmanagedType.Bool)] public bool bKeyDown;
            public ushort wRepeatCount;
            public ushort wVirtualKeyCode;
            public ushort wVirtualScanCode;
            public char UnicodeChar;
            public uint dwControlKeyState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSE_EVENT_RECORD
        {
            public COORD dwMousePosition;
            public uint dwButtonState;
            public uint dwControlKeyState;
            public uint dwEventFlags;
        }

        static void Main(string[] args)
        {
            // Habilita acentuação completa (UTF-8) no terminal (Corrige Bug 9)
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "Pizzaria Tycoon - Edição Completa";
            Console.CursorVisible = false;
            Console.Clear();

            var engine = new GameEngine();
            var ui = new ConsoleUI(engine);

            // Executa API de Mouse nativa apenas no Windows; usa fallback multiplataforma em Linux/macOS (Corrige Bug 7)
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    if (Console.WindowWidth < 120 || Console.WindowHeight < 30)
                    {
                        Console.SetWindowSize(120, 30);
                        Console.SetBufferSize(120, 30);
                    }
                }
                catch { }

                RunWindowsMouseAndKeyboardLoop(ui);
            }
            else
            {
                RunCrossPlatformKeyboardLoop(ui);
            }
        }

        private static void RunWindowsMouseAndKeyboardLoop(ConsoleUI ui)
        {
            IntPtr handleIn = GetStdHandle(STD_INPUT_HANDLE);
            if (GetConsoleMode(handleIn, out uint mode))
            {
                mode &= ~ENABLE_QUICK_EDIT_MODE;
                mode |= ENABLE_EXTENDED_FLAGS;
                mode |= ENABLE_MOUSE_INPUT;
                SetConsoleMode(handleIn, mode);
            }

            ui.Render();

            INPUT_RECORD[] recordBuffer = new INPUT_RECORD[1];
            bool botaoPressionadoAnteriormente = false;

            while (true)
            {
                if (ReadConsoleInput(handleIn, recordBuffer, 1, out uint _))
                {
                    var rec = recordBuffer[0];

                    if (rec.EventType == MOUSE_EVENT)
                    {
                        var mouse = rec.MouseEvent;
                        bool cliqueEsquerdo = (mouse.dwButtonState & FROM_LEFT_1ST_BUTTON_PRESSED) != 0;

                        if (mouse.dwEventFlags == MOUSE_MOVED)
                        {
                            ui.HandleMouseMove(mouse.dwMousePosition.X, mouse.dwMousePosition.Y);
                        }
                        else if (cliqueEsquerdo && !botaoPressionadoAnteriormente && mouse.dwEventFlags == 0)
                        {
                            ui.HandleMouseClick(mouse.dwMousePosition.X, mouse.dwMousePosition.Y);
                        }

                        botaoPressionadoAnteriormente = cliqueEsquerdo;
                    }
                    else if (rec.EventType == KEY_EVENT && rec.KeyEvent.bKeyDown)
                    {
                        ConsoleKey key = (ConsoleKey)rec.KeyEvent.wVirtualKeyCode;
                        ui.HandleKeyPress(key);
                    }
                }
            }
        }

        private static void RunCrossPlatformKeyboardLoop(ConsoleUI ui)
        {
            ui.Render();
            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                ui.HandleKeyPress(keyInfo.Key);
            }
        }
    }
}