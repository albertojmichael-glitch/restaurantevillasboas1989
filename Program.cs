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
        private const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
        private const uint ENABLE_WINDOW_INPUT = 0x0008;
        private const uint FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001;
        private const uint MOUSE_MOVED = 0x0001;
        private const uint SHIFT_PRESSED = 0x0010;

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
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "Pizzaria Tycoon - Edição Expandida (Use Ctrl + Roda do Mouse para Zoom)";
            Console.CursorVisible = false;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    int targetW = Math.Min(150, Console.LargestWindowWidth);
                    int targetH = Math.Min(38, Console.LargestWindowHeight);
                    Console.SetBufferSize(Math.Max(targetW, Console.BufferWidth), Math.Max(targetH, Console.BufferHeight));
                    Console.SetWindowSize(targetW, targetH);
                }
                catch { }
            }

            Console.Clear();

            var persistence = new PersistenceService();
            var engine = new GameEngine(seed: null, persistence: persistence);
            var ui = new ConsoleUI(engine);

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    RunWindowsMouseAndKeyboardLoop(ui, engine);
                }
                else
                {
                    RunCrossPlatformKeyboardLoop(ui, engine);
                }
            }
            catch (Exception ex)
            {
                persistence.LogError("MainLoopFatal", ex);
                try { engine.SalvarAutomatico("emergencia"); } catch { }
                Console.ResetColor();
                Console.CursorVisible = true;
                Console.Clear();
                Console.WriteLine("Ocorreu um erro inesperado, mas o jogo salvou um ponto de recuperação em saves/autosave.json.");
                Console.WriteLine($"Detalhes registrados em: {persistence.ErrorLogPath}");
                Console.WriteLine("Pressione qualquer tecla para encerrar.");
                try { Console.ReadKey(true); } catch { }
            }
        }

        private static void RunWindowsMouseAndKeyboardLoop(ConsoleUI ui, GameEngine engine)
        {
            IntPtr handleIn = GetStdHandle(STD_INPUT_HANDLE);
            if (GetConsoleMode(handleIn, out uint mode))
            {
                mode &= ~ENABLE_QUICK_EDIT_MODE;
                mode |= ENABLE_EXTENDED_FLAGS;
                mode |= ENABLE_MOUSE_INPUT;
                mode |= ENABLE_WINDOW_INPUT;
                SetConsoleMode(handleIn, mode);
            }

            ui.Render();

            INPUT_RECORD[] recordBuffer = new INPUT_RECORD[1];
            bool botaoPressionadoAnteriormente = false;

            while (true)
            {
                if (ReadConsoleInput(handleIn, recordBuffer, 1, out uint _))
                {
                    try
                    {
                        var rec = recordBuffer[0];

                        if (rec.EventType == WINDOW_BUFFER_SIZE_EVENT)
                        {
                            ui.HandleResize();
                        }
                        else if (rec.EventType == MOUSE_EVENT)
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
                            bool shift = (rec.KeyEvent.dwControlKeyState & SHIFT_PRESSED) != 0;
                            ui.HandleKeyPress(key, rec.KeyEvent.UnicodeChar, shift);
                        }
                    }
                    catch (Exception ex)
                    {
                        engine.Persistence.LogError("WindowsEventHandler", ex);
                        engine.RegistrarFeedbackExterno("Sistema", "Proteção de entrada",
                            "Uma entrada inesperada foi interceptada com segurança e registrada em logs/errors.log.",
                            FeedbackSeverity.Aviso);
                        ui.Render();
                    }
                }
            }
        }

        private static void RunCrossPlatformKeyboardLoop(ConsoleUI ui, GameEngine engine)
        {
            ui.Render();
            while (true)
            {
                try
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    bool shift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;
                    ui.HandleKeyPress(keyInfo.Key, keyInfo.KeyChar, shift);
                }
                catch (Exception ex)
                {
                    engine.Persistence.LogError("CrossPlatformEventHandler", ex);
                    engine.RegistrarFeedbackExterno("Sistema", "Proteção de entrada",
                        "Uma entrada inesperada foi interceptada com segurança e registrada em logs/errors.log.",
                        FeedbackSeverity.Aviso);
                    ui.Render();
                }
            }
        }
    }
}
