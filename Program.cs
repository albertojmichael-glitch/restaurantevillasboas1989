using System;
using System.Runtime.InteropServices;

namespace PizzariaTycoon
{
    internal class Program
    {
        // Importações da API do Windows para capturar o Mouse no Console
        private const int STD_INPUT_HANDLE = -10;
        private const uint ENABLE_MOUSE_INPUT = 0x0010;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;
        private const ushort MOUSE_EVENT = 0x0002;
        private const uint FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001;

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
            [FieldOffset(4)] public MOUSE_EVENT_RECORD MouseEvent;
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
            Console.Title = "Pizzaria Tycoon - TUI Mouse Edition";
            Console.CursorVisible = false;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Console.SetWindowSize(120, 30);
                    Console.SetBufferSize(120, 30);
                }
            }
            catch
            {
                // Ignora se o terminal do usuário estiver maximizado ou em Windows Terminal moderno
            }

            // Ativa captura de cliques do mouse no Console do Windows
            IntPtr handleIn = GetStdHandle(STD_INPUT_HANDLE);
            if (GetConsoleMode(handleIn, out uint mode))
            {
                mode &= ~ENABLE_QUICK_EDIT_MODE; // Desativa seleção de texto com mouse
                mode |= ENABLE_EXTENDED_FLAGS;
                mode |= ENABLE_MOUSE_INPUT;
                SetConsoleMode(handleIn, mode);
            }

            var engine = new GameEngine();
            var ui = new ConsoleUI(engine);
            ui.Render();

            INPUT_RECORD[] recordBuffer = new INPUT_RECORD[1];
            bool botaoPressionadoAnteriormente = false;

            while (true)
            {
                if (ReadConsoleInput(handleIn, recordBuffer, 1, out uint _))
                {
                    if (recordBuffer[0].EventType == MOUSE_EVENT)
                    {
                        var mouse = recordBuffer[0].MouseEvent;
                        bool cliqueEsquerdo = (mouse.dwButtonState & FROM_LEFT_1ST_BUTTON_PRESSED) != 0;

                        // Dispara apenas no momento do clique (evita múltiplos cliques ao arrastar)
                        if (cliqueEsquerdo && !botaoPressionadoAnteriormente && mouse.dwEventFlags == 0)
                        {
                            ui.HandleMouseClick(mouse.dwMousePosition.X, mouse.dwMousePosition.Y);
                        }

                        botaoPressionadoAnteriormente = cliqueEsquerdo;
                    }
                }
            }
        }
    }
}