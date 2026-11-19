using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace PizzariaTycoon
{
    public class ClickableZone
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Label { get; set; } = "";
        public Action OnClick { get; set; }

        public bool Contains(int mouseX, int mouseY)
        {
            return mouseX >= X && mouseX < (X + Width) &&
                   mouseY >= Y && mouseY < (Y + Height);
        }
    }

    public struct ScreenCell
    {
        public char Char;
        public ConsoleColor Fg;
        public ConsoleColor Bg;
    }

    public class ConsoleUI
    {
        public const int MaxBufferWidth = 500;
        public const int MaxBufferHeight = 200;
        public const int MaxLayoutWidth = 150;
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public int ScreenWidth { get; private set; } = 148;
        public int ScreenHeight { get; private set; } = 38;

        private int _physW = 148;
        private int _ox = 0;

        private readonly GameEngine _game;
        private readonly CommandProcessor _commands;
        private readonly ScreenCell[,] _backBuffer = new ScreenCell[MaxBufferWidth, MaxBufferHeight];
        private readonly ScreenCell[,] _frontBuffer = new ScreenCell[MaxBufferWidth, MaxBufferHeight];
        private bool _forceFullRedraw = true;

        public ScreenTab AbaAtual { get; private set; } = ScreenTab.Cozinha;
        public List<ClickableZone> BotoesAtivos { get; private set; } = new List<ClickableZone>();
        public int HoverX { get; private set; } = -1;
        public int HoverY { get; private set; } = -1;
        public int IndiceBotaoFocadoTeclado { get; private set; } = -1;

        // Estados de Modais
        public bool MostrarTodosIngredientesCozinha { get; private set; } = false;
        public bool ConfirmandoSaida { get; private set; } = false;
        public bool ConfirmandoRecomecar { get; private set; } = false;
        public bool ExibirOpcoes { get; private set; } = false;
        public bool ExibirGerenciadorSaves { get; private set; } = false;
        public HelpSection SecaoAjudaAtual { get; private set; } = HelpSection.TutorialCurto;

        // Seleção no modal de Nova Partida
        private GameMode _novoModoSelecionado = GameMode.Carreira;
        private DifficultyMode _novaDificuldadeSelecionada = DifficultyMode.Normal;

        // Confirmação destrutiva de slot (sobrescrever ou apagar)
        private string _slotAcaoPendenteTipo = null; // "SALVAR" ou "APAGAR"
        private string _slotAcaoPendenteId = null;

        // Paginação e Histórico
        public int PaginaCozinha { get; private set; } = 0;
        public int PaginaEstoque { get; private set; } = 0;
        public int PaginaReceitas { get; private set; } = 0;
        public string FiltroHistorico { get; private set; } = "TODOS"; // TODOS, SUCESSO, ERRO, COMANDOS, CONQUISTAS
        public int IndiceHistoricoSelecionado { get; private set; } = 0;

        // Barra de Comando Natural
        public bool ModoComandoAtivo { get; private set; } = false;
        public string TextoComandoAtual { get; private set; } = "";
        private int _cursorHistoricoComandos = -1;

        // Controle de efeito de velocidade de texto
        private string _ultimaRespostaAnimada = "";

        private struct Seg
        {
            public string T;
            public ConsoleColor C;
            public Seg(string t, ConsoleColor c) { T = t; C = c; }
        }

        private sealed record DevBtn(string Text, ConsoleColor Bg, Action Act);

        private static readonly Dictionary<string, IngredientInfo> IngPorNome =
            Catalog.TodosIngredientes.GroupBy(i => i.Nome).ToDictionary(g => g.Key, g => g.First());

        private static readonly ScreenTab[] TodasAbas =
        {
            ScreenTab.Cozinha,
            ScreenTab.Entregas,
            ScreenTab.LivroReceitas,
            ScreenTab.EstoqueCatalogo,
            ScreenTab.Administracao,
            ScreenTab.Banco,
            ScreenTab.Historico,
            ScreenTab.Terminal
        };

        // Dimensões dinâmicas da tela unificada (suporta redimensionamento até 100x28!)
        private bool MostrarPainelLateral => ScreenWidth >= 114;
        private int ContentTop => 5;
        private int ContentBottom => ScreenHeight - 6;
        private int SideWidth => !MostrarPainelLateral ? 0 : (ScreenWidth >= 138 ? 40 : (ScreenWidth >= 124 ? 35 : 31));
        private int MainWidth => MostrarPainelLateral ? (ScreenWidth - SideWidth - 3) : (ScreenWidth - 2);
        private int SideX => MainWidth + 2;

        private AccessibilitySettings Cfg => _game.Configuracoes;
        public CommandProcessor CommandProcessor => _commands;

        public ConsoleUI(GameEngine game)
        {
            _game = game;
            _commands = new CommandProcessor(game);
            _novoModoSelecionado = game.ModoJogo;
            _novaDificuldadeSelecionada = game.Dificuldade;
            SyncScreenDimensions();
            ClearBackBuffer();
        }

        public void SetVirtualDimensions(int width, int height)
        {
            _physW = Math.Clamp(width, 100, MaxBufferWidth - 1);
            ScreenWidth = Math.Min(_physW, MaxLayoutWidth);
            ScreenHeight = Math.Clamp(height, 28, MaxBufferHeight - 1);
            _ox = (_physW - ScreenWidth) / 2;
            ClearBackBuffer();
        }

        public void RenderToBufferOnly()
        {
            BotoesAtivos.Clear();
            ClearBackBuffer();
            ComposeFrame();
        }

        // ------------------------------------------------------------------
        //  Infra de desenho e acessibilidade
        // ------------------------------------------------------------------
        private string Sym(string grafico, string semantico) => Cfg.ModoSemantico ? semantico : grafico;

        private ConsoleColor MapFg(ConsoleColor c)
        {
            if (!Cfg.AltoContraste) return c;
            return c switch
            {
                ConsoleColor.DarkGray => ConsoleColor.Gray,
                ConsoleColor.DarkBlue => ConsoleColor.Cyan,
                ConsoleColor.DarkCyan => ConsoleColor.Cyan,
                ConsoleColor.DarkGreen => ConsoleColor.Green,
                ConsoleColor.DarkRed => ConsoleColor.Red,
                ConsoleColor.DarkMagenta => ConsoleColor.Magenta,
                ConsoleColor.DarkYellow => ConsoleColor.Yellow,
                _ => c
            };
        }

        private ConsoleColor MapBg(ConsoleColor c)
        {
            if (!Cfg.AltoContraste) return c;
            return c switch
            {
                ConsoleColor.DarkGray => ConsoleColor.DarkBlue,
                _ => c
            };
        }

        private bool SyncScreenDimensions()
        {
            try
            {
                int pw = Math.Clamp(Math.Min(Console.WindowWidth, Console.BufferWidth), 100, MaxBufferWidth - 1);
                int h = Math.Clamp(Math.Min(Console.WindowHeight, Console.BufferHeight), 28, MaxBufferHeight - 1);
                int lw = Math.Min(pw, MaxLayoutWidth);
                int ox = (pw - lw) / 2;

                if (pw != _physW || h != ScreenHeight || lw != ScreenWidth || ox != _ox)
                {
                    _physW = pw;
                    ScreenWidth = lw;
                    ScreenHeight = h;
                    _ox = ox;
                    _forceFullRedraw = true;
                    Console.ResetColor();
                    Console.Clear();
                    return true;
                }
            }
            catch { }
            return false;
        }

        private void ClearBackBuffer()
        {
            for (int y = 0; y < ScreenHeight; y++)
                for (int x = 0; x < _physW; x++)
                    _backBuffer[x, y] = new ScreenCell { Char = ' ', Fg = ConsoleColor.Gray, Bg = ConsoleColor.Black };
        }

        private void Write(int x, int y, string text, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black)
        {
            if (string.IsNullOrEmpty(text) || y < 0 || y >= ScreenHeight || x >= ScreenWidth) return;

            ConsoleColor finalFg = bg == ConsoleColor.Black ? MapFg(fg) : fg;
            ConsoleColor finalBg = MapBg(bg);

            for (int i = 0; i < text.Length; i++)
            {
                int cx = x + i;
                if (cx < 0) continue;
                if (cx >= ScreenWidth) break;
                _backBuffer[cx + _ox, y] = new ScreenCell { Char = text[i], Fg = finalFg, Bg = finalBg };
            }
        }

        private static ConsoleColor Brighten(ConsoleColor c)
        {
            switch (c)
            {
                case ConsoleColor.Black: return ConsoleColor.DarkGray;
                case ConsoleColor.DarkGray: return ConsoleColor.Gray;
                case ConsoleColor.Gray: return ConsoleColor.White;
                case ConsoleColor.White: return ConsoleColor.White;
                default: return (int)c < 8 ? (ConsoleColor)((int)c + 8) : c;
            }
        }

        private static bool IsLight(ConsoleColor c) =>
            c == ConsoleColor.Yellow || c == ConsoleColor.Cyan || c == ConsoleColor.Green ||
            c == ConsoleColor.White || c == ConsoleColor.Gray || c == ConsoleColor.DarkYellow ||
            c == ConsoleColor.Magenta;

        private static string Fit(string s, int w)
        {
            if (string.IsNullOrEmpty(s) || w <= 0) return "";
            if (s.Length <= w) return s;
            if (w == 1) return ".";
            return s.Substring(0, w - 1) + "…";
        }

        private static string Money(double v) => "R$ " + v.ToString("N2", PtBr);

        private static List<string> Wrap(string text, int width, int maxLines = 99)
        {
            var res = new List<string>();
            if (string.IsNullOrEmpty(text) || width < 4) return res;

            var cur = new StringBuilder();
            foreach (var word in text.Split(' '))
            {
                if (cur.Length > 0 && cur.Length + 1 + word.Length > width)
                {
                    res.Add(cur.ToString());
                    cur.Clear();
                }
                if (cur.Length > 0) cur.Append(' ');
                cur.Append(word);
            }
            if (cur.Length > 0) res.Add(cur.ToString());

            if (res.Count > maxLines)
            {
                res = res.Take(maxLines).ToList();
                res[maxLines - 1] = Fit(res[maxLines - 1] + " …", width);
            }
            return res;
        }

        private void DrawPanel(int x, int y, int w, int h, string title, ConsoleColor border, bool dbl = false)
        {
            if (w < 4 || h < 2) return;

            char tl, tr, bl, br, hz, vt;
            if (Cfg.ModoSemantico)
            {
                tl = tr = bl = br = '+';
                hz = dbl ? '=' : '-';
                vt = '|';
            }
            else
            {
                tl = dbl ? '╔' : '┌'; tr = dbl ? '╗' : '┐';
                bl = dbl ? '╚' : '└'; br = dbl ? '╝' : '┘';
                hz = dbl ? '═' : '─'; vt = dbl ? '║' : '│';
            }

            ConsoleColor bCol = Cfg.AltoContraste ? Brighten(border) : border;

            string blank = new string(' ', w);
            for (int r = 0; r < h; r++) Write(x, y + r, blank);

            Write(x, y, tl + new string(hz, w - 2) + tr, bCol);
            Write(x, y + h - 1, bl + new string(hz, w - 2) + br, bCol);
            for (int r = 1; r < h - 1; r++)
            {
                Write(x, y + r, vt.ToString(), bCol);
                Write(x + w - 1, y + r, vt.ToString(), bCol);
            }

            if (!string.IsNullOrEmpty(title))
                Write(x + 2, y, " " + Fit(title, Math.Max(4, w - 6)) + " ", Brighten(bCol));
        }

        private void DrawBar(int x, int y, int w, double ratio, ConsoleColor filled, ConsoleColor empty = ConsoleColor.DarkGray)
        {
            if (double.IsNaN(ratio)) ratio = 0;
            ratio = Math.Clamp(ratio, 0, 1);
            int f = (int)Math.Round(ratio * w);
            char chFull = Cfg.ModoSemantico ? '#' : '█';
            char chEmpty = Cfg.ModoSemantico ? '-' : '░';
            if (f > 0) Write(x, y, new string(chFull, f), filled);
            if (w - f > 0) Write(x + f, y, new string(chEmpty, w - f), empty);
        }

        private void AddButton(int x, int y, string label, ConsoleColor bg, Action action)
        {
            if (y < 0 || y >= ScreenHeight || x < 0 || x >= ScreenWidth) return;

            int btnIndex = BotoesAtivos.Count;
            int hx = HoverX - _ox;
            bool isMouseHover = !Cfg.DesativarEfeitosVisuais && hx >= x && hx < (x + label.Length) && HoverY == y;
            bool isKeyFocus = IndiceBotaoFocadoTeclado == btnIndex;

            ConsoleColor finalBg = MapBg(bg);
            if (isMouseHover) finalBg = Brighten(finalBg);
            if (isKeyFocus) finalBg = ConsoleColor.Yellow;

            ConsoleColor finalFg = IsLight(finalBg) ? ConsoleColor.Black : ConsoleColor.White;

            Write(x, y, label, finalFg, finalBg);

            BotoesAtivos.Add(new ClickableZone
            {
                X = x,
                Y = y,
                Width = label.Length,
                Height = 1,
                Label = label.Trim(),
                OnClick = action
            });
        }

        private int IndexAt(int x, int y)
        {
            for (int i = 0; i < BotoesAtivos.Count; i++)
                if (BotoesAtivos[i].Contains(x - _ox, y)) return i;
            return -1;
        }

        private static ConsoleColor IngFg(string nome)
        {
            return IngPorNome.TryGetValue(nome, out var info)
                ? Brighten(Catalog.ObterCorCategoria(info.Categoria))
                : ConsoleColor.Gray;
        }

        private static List<List<Seg>> Flow(List<Seg> items, int width, string sep, ConsoleColor sepColor)
        {
            var lines = new List<List<Seg>> { new List<Seg>() };
            int len = 0;
            for (int i = 0; i < items.Count; i++)
            {
                string tail = i < items.Count - 1 ? sep : "";
                int need = items[i].T.Length + tail.Length;
                if (len + need > width && len > 0)
                {
                    lines.Add(new List<Seg>());
                    len = 0;
                }
                lines[lines.Count - 1].Add(items[i]);
                if (tail.Length > 0) lines[lines.Count - 1].Add(new Seg(tail, sepColor));
                len += need;
            }
            return lines;
        }

        private int DrawFlow(int x, int y, List<List<Seg>> lines, int maxLines)
        {
            int n = Math.Min(lines.Count, maxLines);
            for (int l = 0; l < n; l++)
            {
                int cx = x;
                foreach (var s in lines[l])
                {
                    Write(cx, y + l, s.T, s.C);
                    cx += s.T.Length;
                }
                if (l == n - 1 && lines.Count > maxLines)
                    Write(cx, y + l, "…", ConsoleColor.DarkGray);
            }
            return n;
        }

        private void FlushBufferToConsole()
        {
            if (Console.IsOutputRedirected) return;

            int maxW, maxH;
            try
            {
                maxW = Math.Min(_physW, Console.BufferWidth);
                maxH = Math.Min(ScreenHeight, Console.BufferHeight);
            }
            catch
            {
                return;
            }

            ConsoleColor curFg = (ConsoleColor)(-1);
            ConsoleColor curBg = (ConsoleColor)(-1);

            for (int y = 0; y < maxH; y++)
            {
                for (int x = 0; x < maxW; x++)
                {
                    if (y == maxH - 1 && x == maxW - 1) continue;

                    var next = _backBuffer[x, y];
                    var prev = _frontBuffer[x, y];

                    if (!_forceFullRedraw && next.Char == prev.Char && next.Fg == prev.Fg && next.Bg == prev.Bg)
                        continue;

                    try
                    {
                        Console.SetCursorPosition(x, y);
                        if (next.Fg != curFg) { Console.ForegroundColor = next.Fg; curFg = next.Fg; }
                        if (next.Bg != curBg) { Console.BackgroundColor = next.Bg; curBg = next.Bg; }
                        Console.Write(next.Char);
                        _frontBuffer[x, y] = next;
                    }
                    catch { }
                }
            }

            Console.ResetColor();
            _forceFullRedraw = false;

            // Efeito opcional de velocidade do texto na linha de resposta (respeita DesativarEfeitosVisuais)
            AnimarRespostaSeAtivo();
        }

        private void AnimarRespostaSeAtivo()
        {
            string respAtual = _game.FeedbackAtual?.Resposta ?? "";
            if (Cfg.DesativarEfeitosVisuais || Cfg.VelocidadeTexto == TextSpeed.Instantaneo || string.IsNullOrEmpty(respAtual))
            {
                _ultimaRespostaAnimada = respAtual;
                return;
            }
            if (respAtual == _ultimaRespostaAnimada) return;
            _ultimaRespostaAnimada = respAtual;

            int delayMs = Cfg.VelocidadeTexto switch
            {
                TextSpeed.Rapido => 2,
                TextSpeed.Normal => 6,
                TextSpeed.Lento => 14,
                _ => 0
            };
            if (delayMs <= 0) return;

            int yResp = ScreenHeight - 4;
            if (yResp < 0 || yResp >= Console.BufferHeight) return;

            try
            {
                int maxSteps = Math.Min(20, Math.Max(4, respAtual.Length / 4));
                int stepDelay = Math.Min(delayMs, 180 / Math.Max(1, maxSteps));
                for (int s = 0; s < maxSteps; s++)
                {
                    if (Console.KeyAvailable) break;
                    Thread.Sleep(stepDelay);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------
        //  Render principal
        // ------------------------------------------------------------------
        public void Render()
        {
            SyncScreenDimensions();
            BotoesAtivos.Clear();
            ClearBackBuffer();

            ComposeFrame();

            if (IndiceBotaoFocadoTeclado >= BotoesAtivos.Count)
                IndiceBotaoFocadoTeclado = BotoesAtivos.Count - 1;

            FlushBufferToConsole();
        }

        private void ComposeFrame()
        {
            if (_game.GameOver || _game.JogoVencido)
            {
                RenderEndScreen();
            }
            else
            {
                bool modalAtivo = _game.ExibirTutorial || _game.ProcessoAtivo != null || _game.ResumoDiarioAtivo != null
                                  || ConfirmandoRecomecar || ExibirOpcoes || ExibirGerenciadorSaves;
                RenderHeader(!modalAtivo && !ConfirmandoSaida);

                if (_game.ExibirTutorial)
                {
                    RenderModalAjudaContextual();
                }
                else if (_game.ProcessoAtivo != null)
                {
                    RenderModalTribunal();
                }
                else if (_game.ResumoDiarioAtivo != null)
                {
                    RenderModalResumoDiario();
                }
                else if (ExibirOpcoes)
                {
                    RenderModalOpcoes();
                }
                else if (ExibirGerenciadorSaves)
                {
                    RenderModalGerenciadorSaves();
                }
                else if (ConfirmandoRecomecar)
                {
                    RenderModalRecomecar();
                }
                else
                {
                    RenderNavigationTabs();
                    switch (AbaAtual)
                    {
                        case ScreenTab.Cozinha: RenderCozinha(); break;
                        case ScreenTab.Entregas: RenderEntregas(); break;
                        case ScreenTab.LivroReceitas: RenderLivroReceitas(); break;
                        case ScreenTab.EstoqueCatalogo: RenderEstoqueCatalogo(); break;
                        case ScreenTab.Administracao: RenderAdministracao(); break;
                        case ScreenTab.Banco: RenderBanco(); break;
                        case ScreenTab.Historico: RenderHistorico(); break;
                        case ScreenTab.Terminal: RenderTerminalDev(); break;
                    }
                    if (MostrarPainelLateral) RenderSideGuidancePanel();
                }

                RenderFooter(!modalAtivo && !ConfirmandoSaida);
            }

            if (ConfirmandoSaida) RenderModalSaida();
        }

        // ------------------------------------------------------------------
        //  Cabeçalho Unificado e Painel Compacto de Estado
        // ------------------------------------------------------------------
        private int StatChip(int x, int y, string label, string value, ConsoleColor valueColor, int maxX)
        {
            string sep = Cfg.ModoSemantico ? " | " : " │ ";
            int need = label.Length + 1 + value.Length + sep.Length;
            if (x + need > maxX) return x;
            Write(x, y, label + " ", ConsoleColor.DarkGray);
            x += label.Length + 1;
            Write(x, y, value, valueColor);
            x += value.Length;
            Write(x, y, sep, ConsoleColor.DarkGray);
            return x + sep.Length;
        }

        private void RenderHeader(bool interativo)
        {
            int W = ScreenWidth;

            // Linha 0: Título, Fase, Modo, Dificuldade e Tempo/Horário
            Write(0, 0, new string(' ', W), ConsoleColor.White, ConsoleColor.DarkBlue);
            string icone = Sym("♦", "*");
            string modoDif = $"{LocalityCatalog.ObterNomeModo(_game.ModoJogo)}/{LocalityCatalog.ObterNomeDificuldade(_game.Dificuldade)}";
            string esquerda = $"{icone} PIZZARIA TYCOON · Fase {_game.NivelAtual}/5 ({_game.ConfigAtual.Titulo}) · {modoDif}";
            string tempo = $"{_game.HorarioTurnoFormatado} · {_game.DataHojeFormatada} · Mês {_game.MesesJogados} Dia {_game.DiaSemana}/4";
            Write(2, 0, Fit(esquerda, Math.Max(20, W - tempo.Length - 6)), ConsoleColor.Yellow, ConsoleColor.DarkBlue);
            Write(W - tempo.Length - 2, 0, tempo, ConsoleColor.White, ConsoleColor.DarkBlue);

            // Linha 1: Painel Compacto de Estado
            int x = 2;
            int maxX = W - 1;
            string sep = Cfg.ModoSemantico ? " | " : " │ ";
            x = StatChip(x, 1, "DINHEIRO", Money(_game.Dinheiro), _game.Dinheiro >= 0 ? ConsoleColor.Green : ConsoleColor.Red, maxX);

            var inv = _game.ObterResumoInventario();
            string invTxt = $"{inv.TotalUnidades}/{inv.CapacidadeTotal}";
            if (inv.Esgotados > 0) invTxt += $" !{inv.Esgotados}";
            if (inv.Contaminados > 0) invTxt += $" {Sym("▲", "X")}{inv.Contaminados}";
            ConsoleColor corInv = inv.Contaminados > 0 ? ConsoleColor.Magenta : (inv.Esgotados > 0 ? ConsoleColor.Red : ConsoleColor.Cyan);
            x = StatChip(x, 1, "INV", invTxt, corInv, maxX);

            x = StatChip(x, 1, "FUNC", $"{_game.Funcionarios}/{_game.ConfigAtual.RecFuncionarios}", ConsoleColor.White, maxX);

            if (x + 17 < maxX)
            {
                Write(x, 1, "REP ", ConsoleColor.DarkGray);
                x += 4;
                ConsoleColor corRep = _game.Reputacao >= 4 ? ConsoleColor.Green : (_game.Reputacao >= 2.5 ? ConsoleColor.Yellow : ConsoleColor.Red);
                DrawBar(x, 1, 5, _game.Reputacao / 5.0, corRep);
                x += 6;
                string repStr = $"{_game.Reputacao:F1}*";
                Write(x, 1, repStr, corRep);
                x += repStr.Length;
                Write(x, 1, sep, ConsoleColor.DarkGray);
                x += sep.Length;
            }

            int maxAc = Math.Max(1, _game.AcoesMaximasDia);
            int resta = Math.Clamp(_game.AcoesRestantesDia, 0, maxAc);
            int dots = Math.Min(8, maxAc);
            int dotsCheios = (int)Math.Round((double)resta / maxAc * dots);
            if (x + 20 < maxX)
            {
                Write(x, 1, "ENERGIA ", ConsoleColor.DarkGray);
                x += 8;
                ConsoleColor corEn = resta <= 1 ? ConsoleColor.Red : (resta <= 3 ? ConsoleColor.Yellow : ConsoleColor.Green);
                Write(x, 1, new string(Cfg.ModoSemantico ? '#' : '●', dotsCheios), corEn);
                Write(x + dotsCheios, 1, new string(Cfg.ModoSemantico ? '.' : '○', Math.Max(0, dots - dotsCheios)), ConsoleColor.DarkGray);
                x += dots;
                string enTxt = $" {resta}/{maxAc}";
                Write(x, 1, enTxt, corEn);
                x += enTxt.Length;
                Write(x, 1, sep, ConsoleColor.DarkGray);
                x += sep.Length;
            }

            int ideal = _game.ObterLimpezaIdeal();
            x = StatChip(x, 1, "LIMP", $"{_game.Limpeza}/{ideal}", _game.Limpeza >= ideal ? ConsoleColor.Green : ConsoleColor.Yellow, maxX);
            x = StatChip(x, 1, "MORAL", $"{_game.Narrativa.MoralEquipe}", _game.Narrativa.MoralEquipe >= 70 ? ConsoleColor.Green : (_game.Narrativa.MoralEquipe <= 35 ? ConsoleColor.Red : ConsoleColor.Gray), maxX);

            if (_game.DividaTotalBanco > 0)
                StatChip(x, 1, "DÍVIDA", Money(_game.DividaTotalBanco), ConsoleColor.Red, maxX);

            // Linha 2: Evento do dia à esquerda + Botões Globais à direita
            var botoes = new List<(string label, ConsoleColor bg, Action act)>
            {
                (" [H] AJUDA ", ConsoleColor.DarkCyan, () => AbrirAjuda(HelpSection.LocalidadeAtual)),
                (" [O] OPÇÕES ", ConsoleColor.DarkBlue, () => AbrirOpcoes()),
                (" [G] SLOTS ", ConsoleColor.DarkGreen, () => AbrirGerenciadorSaves()),
                (" [Z] DESFAZER ", _game.PodeDesfazer ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray, () => _game.DesfazerUltimaAcao()),
                (" [R] NOVO ", ConsoleColor.DarkMagenta, () => AbrirModalRecomecar()),
                ($" [F] DIA {Sym("►►", ">>")} ", ConsoleColor.DarkYellow, () => _game.AvancarDia())
            };

            int total = botoes.Sum(b => b.label.Length) + (botoes.Count - 1);
            int bx = W - 2 - total;
            int primeiroBotaoX = bx;
            foreach (var b in botoes)
            {
                if (interativo)
                    AddButton(bx, 2, b.label, b.bg, b.act);
                else
                    Write(bx, 2, b.label, ConsoleColor.DarkGray, ConsoleColor.Black);
                bx += b.label.Length + 1;
            }

            Write(2, 2, $"EVENTO {Sym("►", ">")} ", ConsoleColor.DarkCyan);
            Write(11, 2, Fit(_game.EventoDoDiaDescricao, Math.Max(10, primeiroBotaoX - 13)), ConsoleColor.Cyan);
        }

        private void RenderNavigationTabs()
        {
            int x = 1;
            int y = 3;
            int ativoX = 0, ativoW = 0;

            for (int i = 0; i < TodasAbas.Length; i++)
            {
                var tab = TodasAbas[i];
                bool ativo = AbaAtual == tab;
                string nome = LocalityCatalog.ObterNomeCurto(tab);
                string label = ScreenWidth < 126 ? $" {i + 1}:{nome} " : $" [{i + 1}] {nome} ";

                ConsoleColor bg = ativo
                    ? (tab == ScreenTab.Terminal ? ConsoleColor.Magenta : ConsoleColor.Cyan)
                    : ConsoleColor.DarkGray;

                var alvo = tab;
                AddButton(x, y, label, bg, () => TrocarAba(alvo, viaUsuario: true));

                if (ativo) { ativoX = x; ativoW = label.Length; }
                x += label.Length + 1;
            }

            Write(0, 4, new string(Cfg.ModoSemantico ? '-' : '─', ScreenWidth), ConsoleColor.DarkGray);
            if (ativoW > 0)
                Write(ativoX, 4, new string(Cfg.ModoSemantico ? '=' : '═', ativoW), AbaAtual == ScreenTab.Terminal ? ConsoleColor.Magenta : ConsoleColor.Cyan);
        }

        // ------------------------------------------------------------------
        //  PAINEL LATERAL FIXO (Situação, Objetivos, Próximos Passos, Ações)
        // ------------------------------------------------------------------
        private void RenderSideGuidancePanel()
        {
            int sx = SideX;
            int sw = SideWidth;
            int sy = ContentTop;
            int sh = ContentBottom - ContentTop + 1;
            int inner = sw - 4;

            DrawPanel(sx, sy, sw, sh, "PAINEL DO GERENTE", ConsoleColor.Cyan);
            var guia = _game.ObterGuiaContextual(AbaAtual);

            int y = sy + 1;
            int maxY = sy + sh - 2;
            bool compactoV = sh <= 27;

            if (y <= maxY) Write(sx + 2, y++, "SITUAÇÃO ATUAL", ConsoleColor.Yellow);
            if (y <= maxY) Write(sx + 2, y++, Fit("Local: " + guia.LocalidadeNome, inner), ConsoleColor.White);
            if (y <= maxY) Write(sx + 2, y++, Fit("Aqui: " + guia.PersonagemLocal, inner), ConsoleColor.Cyan);
            foreach (var l in Wrap(guia.SituacaoAtual, inner, 2))
            {
                if (y > maxY) break;
                Write(sx + 2, y++, l, ConsoleColor.Gray);
            }

            if (y + 2 <= maxY)
            {
                if (!compactoV) y++;
                Write(sx + 2, y++, "OBJETIVOS ATIVOS", ConsoleColor.Green);
                foreach (var obj in guia.ObjetivosAtivos.Take(2))
                {
                    foreach (var l in Wrap(Sym("• ", "* ") + obj, inner, 2))
                    {
                        if (y > maxY) break;
                        Write(sx + 2, y++, l, ConsoleColor.White);
                    }
                }
            }

            if (y + 2 <= maxY)
            {
                if (!compactoV) y++;
                Write(sx + 2, y++, "PRÓXIMOS PASSOS", ConsoleColor.Cyan);
                int maxPassos = compactoV ? 2 : 3;
                foreach (var passo in guia.ProximosPassos.Take(maxPassos))
                {
                    foreach (var l in Wrap(passo, inner, 2))
                    {
                        if (y > maxY) break;
                        Write(sx + 2, y++, l, ConsoleColor.Gray);
                    }
                }
            }

            if (y + 2 <= maxY)
            {
                if (!compactoV) y++;
                Write(sx + 2, y++, "DISPONÍVEL AGORA", ConsoleColor.Green);
                int maxDisp = guia.AcoesBloqueadas.Count > 0 ? 3 : 4;
                foreach (var ac in guia.AcoesDisponiveis.Take(maxDisp))
                {
                    if (y > maxY) break;
                    string txt = $"{Sym("✓", "[OK]")} [{ac.Tecla}] {ac.Titulo}";
                    Write(sx + 2, y++, Fit(txt, inner), ConsoleColor.Green);
                }
            }

            if (guia.AcoesBloqueadas.Count > 0 && y + 1 <= maxY)
            {
                if (!compactoV && y + 3 <= maxY) y++;
                if (y + 1 <= maxY)
                {
                    Write(sx + 2, y++, "BLOQUEADO AGORA", ConsoleColor.Red);
                    foreach (var bl in guia.AcoesBloqueadas.Take(3))
                    {
                        if (y > maxY) break;
                        string txt = $"{Sym("✕", "[X]")} {bl.Titulo}: {bl.MotivoBloqueio}";
                        Write(sx + 2, y++, Fit(txt, inner), ConsoleColor.DarkGray);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        //  Rodapé Unificado (AÇÃO / RESPOSTA / ESTADO / COMANDO / ATALHOS+FOCO)
        // ------------------------------------------------------------------
        private void RenderFooter(bool interativo)
        {
            int W = ScreenWidth;
            int yAcao = ScreenHeight - 5;
            int yResp = ScreenHeight - 4;
            int yEstado = ScreenHeight - 3;
            int yCmd = ScreenHeight - 2;
            int yAtalhos = ScreenHeight - 1;

            var fb = _game.FeedbackAtual ?? new ActionFeedback();
            string seta = Sym("►", ">");

            // Linha H-5: AÇÃO
            Write(0, yAcao, new string(' ', W), ConsoleColor.Gray, ConsoleColor.Black);
            Write(1, yAcao, $"AÇÃO     {seta} ", ConsoleColor.Yellow);
            string acaoTxt = fb.Acao ?? "";
            if (!string.IsNullOrEmpty(fb.CorrecaoOrtografica))
                acaoTxt += $"  ({fb.CorrecaoOrtografica})";

            string undoStatus = _game.PodeDesfazer
                ? $"[Z] Desfazer: {Fit(_game.DescricaoProximoDesfazer, 24)}"
                : $"Slot: {_game.SlotAtivo.ToUpperInvariant()}" + (_game.UltimoAutosaveLocal.HasValue ? $" · Auto {_game.UltimoAutosaveLocal.Value:HH:mm}" : "");
            int maxAcaoW = Math.Max(15, W - undoStatus.Length - 16);
            Write(12, yAcao, Fit(acaoTxt, maxAcaoW), ConsoleColor.White);
            Write(W - undoStatus.Length - 2, yAcao, undoStatus, _game.PodeDesfazer ? ConsoleColor.Yellow : ConsoleColor.DarkGray);

            // Linha H-4: RESPOSTA DO JOGO
            ConsoleColor bgResp = fb.Severidade switch
            {
                FeedbackSeverity.Sucesso => ConsoleColor.DarkGreen,
                FeedbackSeverity.Erro => ConsoleColor.DarkRed,
                FeedbackSeverity.Aviso => ConsoleColor.DarkYellow,
                _ => ConsoleColor.DarkBlue
            };
            ConsoleColor fgResp = bgResp == ConsoleColor.DarkYellow ? ConsoleColor.Black : ConsoleColor.White;
            string tagSem = fb.Severidade switch
            {
                FeedbackSeverity.Sucesso => Sym("●", "[OK]"),
                FeedbackSeverity.Erro => Sym("▲", "[ERRO]"),
                FeedbackSeverity.Aviso => Sym("◆", "[AVISO]"),
                _ => Sym("►", "[INFO]")
            };

            Write(0, yResp, new string(' ', W), fgResp, bgResp);
            string prefixoResp = $"RESPOSTA {tagSem} ";
            Write(1, yResp, prefixoResp, bgResp == ConsoleColor.DarkYellow ? ConsoleColor.Black : ConsoleColor.Yellow, bgResp);
            string respCompleta = string.IsNullOrWhiteSpace(fb.Sugestao)
                ? fb.Resposta
                : $"{fb.Resposta}  {Sym("→", "->")} Dica: {fb.Sugestao}";
            Write(1 + prefixoResp.Length, yResp, Fit(respCompleta, W - prefixoResp.Length - 3), fgResp, bgResp);

            // Linha H-3: MUDANÇA DE ESTADO
            Write(0, yEstado, new string(' ', W), ConsoleColor.Gray, ConsoleColor.Black);
            Write(1, yEstado, $"ESTADO   {seta} ", ConsoleColor.Cyan);
            if (fb.Deltas == null || fb.Deltas.Count == 0)
            {
                Write(12, yEstado, "Sem alterações de estado nesta ação.", ConsoleColor.DarkGray);
            }
            else
            {
                int dx = 12;
                for (int i = 0; i < fb.Deltas.Count; i++)
                {
                    var d = fb.Deltas[i];
                    ConsoleColor c = d.Sinal > 0 ? ConsoleColor.Green : (d.Sinal < 0 ? ConsoleColor.Red : ConsoleColor.Cyan);
                    string seg = d.Texto;
                    if (dx + seg.Length >= W - 2) break;
                    Write(dx, yEstado, seg, c);
                    dx += seg.Length;
                    if (i < fb.Deltas.Count - 1 && dx + 3 < W - 2)
                    {
                        Write(dx, yEstado, " | ", ConsoleColor.DarkGray);
                        dx += 3;
                    }
                }
            }

            // Linha H-2: BARRA DE COMANDO NATURAL
            ConsoleColor bgCmd = ModoComandoAtivo ? ConsoleColor.DarkCyan : ConsoleColor.Black;
            ConsoleColor fgCmd = ModoComandoAtivo ? ConsoleColor.White : ConsoleColor.Gray;
            Write(0, yCmd, new string(' ', W), fgCmd, bgCmd);

            if (ModoComandoAtivo)
            {
                Write(1, yCmd, $"COMANDO  {seta} ", ConsoleColor.Yellow, bgCmd);
                string btnEnviar = " [Enter] ENVIAR ";
                string btnCanc = " [Esc] FECHAR ";
                int campoW = Math.Max(15, W - 14 - btnEnviar.Length - btnCanc.Length - 4);
                string mostrar = TextoComandoAtual.Length > campoW - 1
                    ? TextoComandoAtual.Substring(TextoComandoAtual.Length - (campoW - 1))
                    : TextoComandoAtual;
                Write(12, yCmd, mostrar + "_", ConsoleColor.White, bgCmd);

                AddButton(W - btnCanc.Length - 2, yCmd, btnCanc, ConsoleColor.DarkRed, FecharModoComando);
                AddButton(W - btnCanc.Length - btnEnviar.Length - 3, yCmd, btnEnviar, ConsoleColor.DarkGreen, EnviarComandoDigitado);
            }
            else
            {
                string promptLabel = $" [/] COMANDO {seta} ";
                string placeholder = "Clique ou aperte / (ex: \"falar com Pedro\", \"abrir docas\", \"usar caixa\", \"conquistas\", \"alto contraste\")";
                string linhaCmd = promptLabel + Fit(placeholder, Math.Max(10, W - promptLabel.Length - 3));
                if (interativo)
                    AddButton(1, yCmd, linhaCmd, ConsoleColor.DarkGray, AbrirModoComando);
                else
                    Write(1, yCmd, linhaCmd, ConsoleColor.DarkGray, ConsoleColor.Black);
            }

            // Linha H-1: Foco acessível atual OU legenda de atalhos
            if (IndiceBotaoFocadoTeclado >= 0 && IndiceBotaoFocadoTeclado < BotoesAtivos.Count)
            {
                string focoTxt = $"FOCO [{IndiceBotaoFocadoTeclado + 1}/{BotoesAtivos.Count}]: {BotoesAtivos[IndiceBotaoFocadoTeclado].Label}  (Espaço/Enter ativa · Tab próximo · Shift+Tab anterior)";
                Write(2, yAtalhos, Fit(focoTxt, W - 4), ConsoleColor.Yellow);
                return;
            }

            var dicas = new List<(string k, string d)>
            {
                ("1-8", "Locais"),
                ("/", "Comando"),
                ("Tab", "Foco"),
                ("O", "Opções/Som"),
                ("G", "Slots"),
                ("Z", "Desfazer"),
                ("H", "Ajuda"),
                ("R", "Novo"),
                ("Esc", "Sair")
            };

            int ax = 2;
            foreach (var d in dicas)
            {
                int need = d.k.Length + 1 + d.d.Length + 2;
                if (ax + need >= W - 1) break;
                Write(ax, yAtalhos, d.k, ConsoleColor.Yellow);
                Write(ax + d.k.Length + 1, yAtalhos, d.d, ConsoleColor.DarkGray);
                ax += need;
            }
        }

        // ------------------------------------------------------------------
        //  MODAIS (Ajuda, Opções/Acessibilidade, Slots, Novo Jogo, Resumo, Tribunal, Saída)
        // ------------------------------------------------------------------
        private void DrawModalFrame(int w, int h, string title, ConsoleColor cor, out int x, out int y)
        {
            w = Math.Min(w, ScreenWidth - 4);
            x = Math.Max(1, (ScreenWidth - w) / 2);
            int areaH = ContentBottom - ContentTop + 1;
            h = Math.Min(h, Math.Max(10, areaH));
            y = Math.Max(ContentTop, ContentTop + (areaH - h) / 2);
            DrawPanel(x, y, w, h, "", cor, dbl: true);
            if (!string.IsNullOrEmpty(title))
            {
                string t = " " + Fit(title, w - 6) + " ";
                Write(x + (w - t.Length) / 2, y, t, Brighten(cor));
            }
        }

        private void FecharModaisSecundarios()
        {
            ExibirOpcoes = false;
            ExibirGerenciadorSaves = false;
            ConfirmandoRecomecar = false;
            _slotAcaoPendenteTipo = null;
            _slotAcaoPendenteId = null;
        }

        private void AbrirAjuda(HelpSection secao)
        {
            FecharModaisSecundarios();
            SecaoAjudaAtual = secao;
            _game.ExibirTutorial = true;
            ModoComandoAtivo = false;
            IndiceBotaoFocadoTeclado = -1;
        }

        private void AbrirOpcoes()
        {
            FecharModaisSecundarios();
            _game.ExibirTutorial = false;
            ExibirOpcoes = true;
            ModoComandoAtivo = false;
            IndiceBotaoFocadoTeclado = -1;
        }

        private void AbrirGerenciadorSaves()
        {
            FecharModaisSecundarios();
            _game.ExibirTutorial = false;
            ExibirGerenciadorSaves = true;
            ModoComandoAtivo = false;
            IndiceBotaoFocadoTeclado = -1;
        }

        private void AbrirModalRecomecar()
        {
            FecharModaisSecundarios();
            _game.ExibirTutorial = false;
            _novoModoSelecionado = _game.ModoJogo;
            _novaDificuldadeSelecionada = _game.Dificuldade;
            ConfirmandoRecomecar = true;
            ModoComandoAtivo = false;
            IndiceBotaoFocadoTeclado = -1;
        }

        private void RenderModalOpcoes()
        {
            int w = Math.Min(92, ScreenWidth - 6);
            int h = 16;
            DrawModalFrame(w, h, "OPÇÕES · ACESSIBILIDADE & ÁUDIO", ConsoleColor.Cyan, out int x, out int y);

            Write(x + 3, y + 2, "Ajuste conforto visual, leitura acessível e áudio. Salvo automaticamente em settings.json.", ConsoleColor.Gray);

            void RowOpt(int ry, string num, string nome, string estado, bool ativo, Action act)
            {
                Write(x + 3, ry, $"[{num}] {nome}", ConsoleColor.White);
                AddButton(x + 42, ry, $" {estado} ", ativo ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray, () =>
                {
                    act();
                    _game.SalvarConfiguracoes();
                });
            }

            RowOpt(y + 4, "1", "Alto Contraste", Cfg.AltoContraste ? "ATIVADO" : "DESATIVADO", Cfg.AltoContraste, () => Cfg.AltoContraste = !Cfg.AltoContraste);
            RowOpt(y + 6, "2", "Modo Semântico (ASCII acessível)", Cfg.ModoSemantico ? "ATIVADO" : "DESATIVADO", Cfg.ModoSemantico, () => Cfg.ModoSemantico = !Cfg.ModoSemantico);
            RowOpt(y + 8, "3", "Efeitos Visuais", Cfg.DesativarEfeitosVisuais ? "DESATIVADOS" : "ATIVADOS", !Cfg.DesativarEfeitosVisuais, () => Cfg.DesativarEfeitosVisuais = !Cfg.DesativarEfeitosVisuais);
            RowOpt(y + 10, "4", "Velocidade do Texto", Cfg.VelocidadeTexto.ToString().ToUpperInvariant(), true, () => Cfg.VelocidadeTexto = (TextSpeed)(((int)Cfg.VelocidadeTexto + 1) % 4));
            RowOpt(y + 12, "5", "Sons Opcionais (Console Beep)", Cfg.SonsAtivos ? "ATIVADOS" : "MUDO", Cfg.SonsAtivos, () => Cfg.SonsAtivos = !Cfg.SonsAtivos);

            string btn = " VOLTAR AO JOGO [Esc/Enter] ";
            AddButton(x + (w - btn.Length) / 2, y + h - 2, btn, ConsoleColor.DarkGreen, () => ExibirOpcoes = false);
        }

        private void RenderModalGerenciadorSaves()
        {
            int w = Math.Min(110, ScreenWidth - 6);
            int h = 18;
            DrawModalFrame(w, h, "GERENCIADOR DE PARTIDAS · MÚLTIPLOS SLOTS & AUTOSAVE (v2)", ConsoleColor.Green, out int x, out int y);

            var slots = _game.ListarSlotsSave();
            Write(x + 3, y + 2, $"Slot ativo: {_game.SlotAtivo.ToUpperInvariant()}  ·  Saves possuem checksum SHA-256 e backup automático (.bak).", ConsoleColor.Gray);

            int ry = y + 4;
            foreach (var sl in slots)
            {
                bool ehAuto = sl.SlotId == "autosave";
                bool ehAtivo = sl.SlotId == _game.SlotAtivo;
                ConsoleColor corTitulo = sl.Corrompido ? ConsoleColor.Red : (ehAtivo ? ConsoleColor.Yellow : ConsoleColor.White);

                Write(x + 3, ry, $"{(ehAtivo ? Sym("► ", "> ") : "  ")}{sl.Titulo}", corTitulo);
                Write(x + 32, ry, Fit(sl.Resumo, Math.Max(12, w - 66)), sl.Corrompido ? ConsoleColor.Red : (sl.Existe ? ConsoleColor.Cyan : ConsoleColor.DarkGray));

                int bx = x + w - 32;
                string idCopy = sl.SlotId;
                bool existeCopy = sl.Existe;

                if (!ehAuto)
                {
                    AddButton(bx, ry, " SALVAR ", ConsoleColor.DarkGreen, () =>
                    {
                        if (existeCopy)
                        {
                            _slotAcaoPendenteTipo = "SALVAR";
                            _slotAcaoPendenteId = idCopy;
                        }
                        else
                        {
                            _game.SalvarJogo(idCopy);
                        }
                    });
                }
                else
                {
                    Write(bx, ry, "  AUTO  ", ConsoleColor.DarkGray);
                }

                AddButton(bx + 9, ry, " CARREGAR ", (sl.Existe || sl.TemBackup) ? ConsoleColor.DarkBlue : ConsoleColor.DarkGray, () =>
                {
                    _game.CarregarJogo(idCopy);
                    ExibirGerenciadorSaves = false;
                });

                if (!ehAuto)
                {
                    AddButton(bx + 20, ry, " APAGAR ", sl.Existe ? ConsoleColor.DarkRed : ConsoleColor.DarkGray, () =>
                    {
                        if (existeCopy)
                        {
                            _slotAcaoPendenteTipo = "APAGAR";
                            _slotAcaoPendenteId = idCopy;
                        }
                    });
                }

                ry += 2;
            }

            // Confirmação explícita para operação destrutiva em slot (sobrescrever ou apagar)
            if (!string.IsNullOrEmpty(_slotAcaoPendenteTipo) && !string.IsNullOrEmpty(_slotAcaoPendenteId))
            {
                string tituloSlot = PersistenceService.ObterTituloSlot(_slotAcaoPendenteId);
                string pergunta = _slotAcaoPendenteTipo == "SALVAR"
                    ? $"CONFIRMAR: Sobrescrever {tituloSlot}?"
                    : $"CONFIRMAR: Apagar permanentemente {tituloSlot}?";
                Write(x + 3, y + h - 4, pergunta, ConsoleColor.Yellow);
                AddButton(x + 3, y + h - 3, " [S] CONFIRMAR ", ConsoleColor.DarkRed, () =>
                {
                    if (_slotAcaoPendenteTipo == "SALVAR") _game.SalvarJogo(_slotAcaoPendenteId);
                    else _game.ExcluirSlotSave(_slotAcaoPendenteId);
                    _slotAcaoPendenteTipo = null;
                    _slotAcaoPendenteId = null;
                });
                AddButton(x + 20, y + h - 3, " [N] CANCELAR ", ConsoleColor.DarkGray, () =>
                {
                    _slotAcaoPendenteTipo = null;
                    _slotAcaoPendenteId = null;
                });
            }
            else
            {
                string btn = " VOLTAR AO JOGO [Esc] ";
                AddButton(x + (w - btn.Length) / 2, y + h - 2, btn, ConsoleColor.DarkGray, () => ExibirGerenciadorSaves = false);
            }
        }

        private void RenderModalAjudaContextual()
        {
            int w = Math.Min(114, ScreenWidth - 6);
            int h = Math.Min(22, ContentBottom - ContentTop + 1);
            int inner = w - 8;

            string titulo = SecaoAjudaAtual switch
            {
                HelpSection.LocalidadeAtual => $"AJUDA CONTEXTUAL · {LocalityCatalog.ObterNome(AbaAtual).ToUpperInvariant()}",
                HelpSection.TutorialCurto => "TUTORIAL CURTO · 3 PASSOS PARA COMEÇAR",
                _ => "COMANDOS NATURAIS & CORREÇÃO INTELIGENTE"
            };

            DrawModalFrame(w, h, titulo, ConsoleColor.Cyan, out int x, out int y);

            int tx = x + 3;
            AddButton(tx, y + 2, " [1] Ajuda desta Tela ", SecaoAjudaAtual == HelpSection.LocalidadeAtual ? ConsoleColor.Cyan : ConsoleColor.DarkGray, () => SecaoAjudaAtual = HelpSection.LocalidadeAtual);
            tx += 23;
            AddButton(tx, y + 2, " [2] Tutorial Curto ", SecaoAjudaAtual == HelpSection.TutorialCurto ? ConsoleColor.Cyan : ConsoleColor.DarkGray, () => SecaoAjudaAtual = HelpSection.TutorialCurto);
            tx += 21;
            AddButton(tx, y + 2, " [3] Comandos Naturais ", SecaoAjudaAtual == HelpSection.ComandosNaturais ? ConsoleColor.Cyan : ConsoleColor.DarkGray, () => SecaoAjudaAtual = HelpSection.ComandosNaturais);

            var linhas = new List<(string t, ConsoleColor c)>();
            void AddSec(string s, ConsoleColor c) { if (linhas.Count > 0) linhas.Add(("", ConsoleColor.Gray)); linhas.Add((s, c)); }
            void AddItem(string s, ConsoleColor c)
            {
                var ws = Wrap(s, inner - 2);
                for (int i = 0; i < ws.Count; i++) linhas.Add(((i == 0 ? Sym("• ", "* ") : "  ") + ws[i], c));
            }

            if (SecaoAjudaAtual == HelpSection.TutorialCurto)
            {
                AddSec("PASSO 1 · ABRA O TURNO DAS 18H NA COZINHA ([1])", ConsoleColor.Green);
                AddItem("Monte cada prato na ordem do GUIA (pulando itens 'SEM'). Pedidos perfeitos em sequência ativam Combo de Lucro (+5% a +20%)!", ConsoleColor.White);

                AddSec("PASSO 2 · AUDITE AS ENTREGAS NAS DOCAS ([2])", ConsoleColor.Cyan);
                AddItem("Aceite caixas válidas e recuse cidades/fornecedores/datas erradas ou frios > 5°C. Auditorias impecáveis aumentam a confiança dos fornecedores.", ConsoleColor.White);

                AddSec("PASSO 3 · GERENCIE E EVOLUA NO ESCRITÓRIO ([5])", ConsoleColor.Magenta);
                AddItem("Equilibre equipe, limpeza, Moral e folha salarial (cobrada no Dia 4). Use [G] para múltiplos slots, [O] para Acessibilidade/Som e [R] para modos de jogo.", ConsoleColor.White);
            }
            else if (SecaoAjudaAtual == HelpSection.ComandosNaturais)
            {
                AddSec("EQUIPE, NAVEGAÇÃO & OPERAÇÃO", ConsoleColor.Yellow);
                AddItem("\"falar com Pedro\" · \"falar com Ana\" · \"falar com Carlos\" · \"falar com cliente\" · \"abrir cozinha\" · \"abrir docas\" · \"usar caixa\".", ConsoleColor.White);
                AddItem("\"adicionar massa\" · \"comprar faltantes\" · \"entregar prato\" · \"abrir caixa 1\" · \"recusar caixa 2\" · \"contratar 2 funcionarios\" · \"melhorar forno\".", ConsoleColor.Gray);

                AddSec("SISTEMA, SLOTS, CONQUISTAS & ACESSIBILIDADE", ConsoleColor.Cyan);
                AddItem("\"salvar slot 2\" · \"carregar autosave\" · \"partidas\" · \"conquistas\" · \"opcoes\" · \"alto contraste\" · \"modo semantico\" · \"som off\" · \"velocidade texto\" · \"desfazer\".", ConsoleColor.White);
            }
            else
            {
                var guia = _game.ObterGuiaContextual(AbaAtual);
                AddSec($"LOCAL: {guia.LocalidadeNome.ToUpperInvariant()} ({guia.SubtituloLocalidade})", ConsoleColor.Yellow);
                AddItem($"Situação: {guia.SituacaoAtual}", ConsoleColor.White);
                AddItem($"Equipe no local: {guia.PersonagemLocal}", ConsoleColor.Cyan);

                AddSec("AÇÕES DISPONÍVEIS AGORA", ConsoleColor.Green);
                foreach (var ac in guia.AcoesDisponiveis)
                    AddItem($"[{ac.Tecla}] {ac.Titulo}", ConsoleColor.White);

                if (guia.AcoesBloqueadas.Count > 0)
                {
                    AddSec("BLOQUEADO NO MOMENTO", ConsoleColor.Red);
                    foreach (var bl in guia.AcoesBloqueadas)
                        AddItem($"{bl.Titulo} — motivo: {bl.MotivoBloqueio}", ConsoleColor.Gray);
                }
            }

            int cy = y + 4;
            int maxCy = y + h - 4;
            foreach (var (t, c) in linhas)
            {
                if (cy > maxCy) break;
                Write(x + 4, cy++, t, c);
            }

            string btnFechar = " ENTENDI · VOLTAR AO JOGO [Enter] ";
            AddButton(x + (w - btnFechar.Length) / 2, y + h - 2, btnFechar, ConsoleColor.DarkGreen, () => _game.ExibirTutorial = false);
        }

        private void RenderModalRecomecar()
        {
            int w = Math.Min(86, ScreenWidth - 6);
            int h = 15;
            DrawModalFrame(w, h, "NOVA PARTIDA · MODO & DIFICULDADE (CONFIRMAÇÃO)", ConsoleColor.Magenta, out int x, out int y);

            Write(x + 3, y + 2, "ATENÇÃO: Iniciar nova partida descarta alterações não salvas na sessão atual.", ConsoleColor.Yellow);

            Write(x + 3, y + 4, "MODO DE JOGO:", ConsoleColor.Cyan);
            AddButton(x + 18, y + 4, " Carreira ", _novoModoSelecionado == GameMode.Carreira ? ConsoleColor.Cyan : ConsoleColor.DarkGray, () => _novoModoSelecionado = GameMode.Carreira);
            AddButton(x + 30, y + 4, " Campanha (12 Dias) ", _novoModoSelecionado == GameMode.CampanhaDias ? ConsoleColor.Cyan : ConsoleColor.DarkGray, () => _novoModoSelecionado = GameMode.CampanhaDias);
            AddButton(x + 52, y + 4, " Gestão Livre ", _novoModoSelecionado == GameMode.GestaoLivre ? ConsoleColor.Cyan : ConsoleColor.DarkGray, () => _novoModoSelecionado = GameMode.GestaoLivre);

            string descModo = _novoModoSelecionado switch
            {
                GameMode.CampanhaDias => "Meta intensa: alcance Fase 3, R$ 8k líquidos e 4,0* em até 12 dias!",
                GameMode.GestaoLivre => "Sandbox relaxado: R$ 5.000 iniciais, sem falência e sem limite de tempo.",
                _ => "Carreira clássica: evolua até a Fase 5, R$ 50k líquidos e 4,5* de reputação."
            };
            Write(x + 3, y + 6, Fit(descModo, w - 6), ConsoleColor.Gray);

            Write(x + 3, y + 8, "DIFICULDADE :", ConsoleColor.Cyan);
            AddButton(x + 18, y + 8, " Fácil ", _novaDificuldadeSelecionada == DifficultyMode.Facil ? ConsoleColor.Green : ConsoleColor.DarkGray, () => _novaDificuldadeSelecionada = DifficultyMode.Facil);
            AddButton(x + 27, y + 8, " Normal ", _novaDificuldadeSelecionada == DifficultyMode.Normal ? ConsoleColor.Yellow : ConsoleColor.DarkGray, () => _novaDificuldadeSelecionada = DifficultyMode.Normal);
            AddButton(x + 37, y + 8, " Difícil ", _novaDificuldadeSelecionada == DifficultyMode.Dificil ? ConsoleColor.Red : ConsoleColor.DarkGray, () => _novaDificuldadeSelecionada = DifficultyMode.Dificil);

            string descDif = _novaDificuldadeSelecionada switch
            {
                DifficultyMode.Facil => "Fácil: R$ 1.500 iniciais, +2 Energia/dia, multas -30%, +1 inspeção nas docas.",
                DifficultyMode.Dificil => "Difícil: R$ 850 iniciais, -1 Energia/dia, custos +20%, multas +25%, juros 4% a.m.",
                _ => "Normal: equilíbrio calibrado entre lucro, folha salarial, estoque e eventos."
            };
            Write(x + 3, y + 10, Fit(descDif, w - 6), ConsoleColor.Gray);

            string b1 = " [1] INICIAR C/ TUTORIAL ";
            string b2 = " [2] INICIAR DIRETO ";
            string b3 = " [Esc] CANCELAR ";
            int total = b1.Length + b2.Length + b3.Length + 4;
            int bx = x + Math.Max(2, (w - total) / 2);
            int by = y + h - 2;

            AddButton(bx, by, b1, ConsoleColor.DarkCyan, () => { ConfirmandoRecomecar = false; AbaAtual = ScreenTab.Cozinha; SecaoAjudaAtual = HelpSection.TutorialCurto; _game.IniciarNovaRun(true, _novoModoSelecionado, _novaDificuldadeSelecionada); });
            AddButton(bx + b1.Length + 2, by, b2, ConsoleColor.DarkGreen, () => { ConfirmandoRecomecar = false; AbaAtual = ScreenTab.Cozinha; _game.IniciarNovaRun(false, _novoModoSelecionado, _novaDificuldadeSelecionada); });
            AddButton(bx + b1.Length + b2.Length + 4, by, b3, ConsoleColor.DarkGray, () => ConfirmandoRecomecar = false);
        }

        private void RenderModalResumoDiario()
        {
            var r = _game.ResumoDiarioAtivo;
            int w = Math.Min(104, ScreenWidth - 6);
            int inner = w - 8;

            var mes = string.IsNullOrEmpty(r.ResumoViradaMes) ? null : Wrap(r.ResumoViradaMes, inner - 2, 2);
            var risco = Wrap(r.MaiorRiscoAtual ?? "", inner - 3, 2);
            var meta = Wrap(r.ProximaMeta ?? "", inner - 3, 2);

            int blocks = (mes != null ? mes.Count + 2 : 0) + (risco.Count + 2) + (meta.Count + 2);
            int h = Math.Min(ContentBottom - ContentTop + 1, 11 + blocks);

            DrawModalFrame(w, h, $"FECHAMENTO DO DIA {r.DiaConcluido}/4 · MÊS {r.MesRef} (AUTOSAVE OK)", ConsoleColor.Yellow, out int x, out int y);

            double saldoDia = Math.Round(r.ReceitaDia - r.DespesaDia, 2);
            string sinalSaldo = saldoDia >= 0 ? "+" : "-";
            int colW = (w - 6) / 3;

            Write(x + 3, y + 2, "GANHOS", ConsoleColor.DarkGray);
            Write(x + 3, y + 3, "+" + Money(r.ReceitaDia), ConsoleColor.Green);
            Write(x + 3 + colW, y + 2, "GASTOS / MULTAS", ConsoleColor.DarkGray);
            Write(x + 3 + colW, y + 3, "-" + Money(r.DespesaDia), ConsoleColor.Red);
            Write(x + 3 + colW * 2, y + 2, "BALANÇO DO DIA", ConsoleColor.DarkGray);
            Write(x + 3 + colW * 2, y + 3, sinalSaldo + Money(Math.Abs(saldoDia)), saldoDia >= 0 ? ConsoleColor.Green : ConsoleColor.Red);

            string sinalRep = r.VariacaoReputacao >= 0 ? "+" : "";
            Write(x + 3, y + 5, "REPUTAÇÃO", ConsoleColor.DarkGray);
            Write(x + 13, y + 5, $"{sinalRep}{r.VariacaoReputacao:F2}*", r.VariacaoReputacao >= 0 ? ConsoleColor.Cyan : ConsoleColor.Red);
            DrawBar(x + 23, y + 5, 10, _game.Reputacao / 5.0, ConsoleColor.Cyan);
            Write(x + 34, y + 5, $"{_game.Reputacao:F1}/5", ConsoleColor.Gray);

            if (r.BonusEficiencia > 0)
                Write(x + 44, y + 5, $"BÔNUS EFICIÊNCIA: +{Money(r.BonusEficiencia)}!", ConsoleColor.Green);

            Write(x + 3, y + 6, "FOLHA PREVISTA", ConsoleColor.DarkGray);
            Write(x + 18, y + 6, "-" + Money(_game.CalcularDespesaMensalFixa()) + "  (cobrada ao fim do Dia 4)", ConsoleColor.Gray);

            int cy = y + 8;
            int maxCy = y + h - 3;
            if (mes != null && cy <= maxCy)
            {
                Write(x + 3, cy++, "FECHAMENTO DO MÊS", ConsoleColor.Magenta);
                foreach (var l in mes) if (cy <= maxCy) Write(x + 5, cy++, l, ConsoleColor.White);
                cy++;
            }

            if (cy <= maxCy)
            {
                Write(x + 3, cy++, "MAIOR RISCO ATUAL", ConsoleColor.Red);
                foreach (var l in risco) if (cy <= maxCy) Write(x + 5, cy++, Sym("→ ", "-> ") + l, ConsoleColor.White);
                cy++;
            }

            if (cy <= maxCy)
            {
                Write(x + 3, cy++, "PRÓXIMA META", ConsoleColor.Green);
                foreach (var l in meta) if (cy <= maxCy) Write(x + 5, cy++, Sym("→ ", "-> ") + l, ConsoleColor.White);
            }

            string btn = " ABRIR PRÓXIMO EXPEDIENTE ÀS 18H [Enter] ";
            AddButton(x + (w - btn.Length) / 2, y + h - 2, btn, ConsoleColor.DarkGreen, () => _game.FecharResumoDiario());
        }

        private void RenderModalTribunal()
        {
            var proc = _game.ProcessoAtivo;
            int w = Math.Min(108, ScreenWidth - 6);
            int h = 17;

            DrawModalFrame(w, h, "", ConsoleColor.Red, out int x, out int y);

            string titulo = " INTIMAÇÃO JUDICIAL · TRIBUNAL ";
            Write(x + 1, y + 1, new string(' ', w - 2), ConsoleColor.White, ConsoleColor.DarkRed);
            Write(x + (w - titulo.Length) / 2, y + 1, titulo, ConsoleColor.White, ConsoleColor.DarkRed);

            Write(x + 3, y + 3, "PROCESSO: " + Fit(proc.Titulo, w - 16), ConsoleColor.Red);
            var desc = Wrap(proc.Descricao, w - 8, 2);
            for (int i = 0; i < desc.Count; i++) Write(x + 3, y + 4 + i, desc[i], ConsoleColor.White);

            Write(x + 3, y + 7, "Valor da causa:", ConsoleColor.DarkGray);
            Write(x + 19, y + 7, Money(proc.ValorCausa), ConsoleColor.Yellow);

            ConsoleColor corChance = proc.ChanceDeGanhar >= 55 ? ConsoleColor.Green : (proc.ChanceDeGanhar >= 40 ? ConsoleColor.Yellow : ConsoleColor.Red);
            Write(x + 3, y + 8, "Chance de vitória:", ConsoleColor.DarkGray);
            DrawBar(x + 22, y + 8, 22, proc.ChanceDeGanhar / 100.0, corChance);
            Write(x + 45, y + 8, $"{proc.ChanceDeGanhar}%", corChance);
            Write(x + 51, y + 8, "(>55% compensa julgar)", ConsoleColor.DarkGray);

            Write(x + 3, y + 10, "[1] ACORDO EXTRAJUDICIAL", ConsoleColor.Yellow);
            Write(x + 5, y + 11, $"Paga {Money(proc.ValorAcordo)} (55% da causa) e encerra o processo imediatamente.", ConsoleColor.Gray);
            Write(x + 3, y + 12, "[2] IR A JULGAMENTO", ConsoleColor.Green);
            Write(x + 5, y + 13, $"Advogado custa 10% ({Money(proc.CustoAdvogado)}). Se vencer, evita a condenação.", ConsoleColor.Gray);

            string b1 = $" [1] FAZER ACORDO ({Money(proc.ValorAcordo)}) ";
            string b2 = $" [2] IR A JULGAMENTO ({proc.ChanceDeGanhar}%) ";
            int bx = x + Math.Max(2, (w - (b1.Length + 3 + b2.Length)) / 2);
            AddButton(bx, y + h - 2, b1, ConsoleColor.DarkYellow, () => _game.FazerAcordoJudicial());
            AddButton(bx + b1.Length + 3, y + h - 2, b2, ConsoleColor.DarkGreen, () => _game.IrParaJulgamento());
        }

        private void RenderModalSaida()
        {
            for (int yy = 0; yy < ScreenHeight; yy++)
                for (int xx = 0; xx < _physW; xx++)
                {
                    var c = _backBuffer[xx, yy];
                    c.Fg = ConsoleColor.DarkGray;
                    if (c.Bg != ConsoleColor.Black) c.Bg = ConsoleColor.Black;
                    _backBuffer[xx, yy] = c;
                }
            BotoesAtivos.Clear();

            int w = Math.Min(86, ScreenWidth - 4);
            int h = 12;
            DrawModalFrame(w, h, "DECISÃO ANTES DE ENCERRAR O JOGO", ConsoleColor.Yellow, out int x, out int y);

            string resumo = $"Fase {_game.NivelAtual}/5 · Caixa {Money(_game.Dinheiro)} · Mês {_game.MesesJogados} Dia {_game.DiaSemana}/4 · Conquistas {_game.ConquistasDesbloqueadas.Count}/{Catalog.Conquistas.Count}";
            Write(x + 3, y + 2, Fit(resumo, w - 6), ConsoleColor.Cyan);
            Write(x + 3, y + 4, "Como deseja encerrar sua sessão? Progresso não salvo será perdido.", ConsoleColor.White);

            string b1 = $" [1] SALVAR ({_game.SlotAtivo.ToUpperInvariant()}) E SAIR ";
            string b2 = " [2] AUTOSAVE E SAIR ";
            string b3 = " [3] SAIR SEM SALVAR ";
            string b4 = " [Esc] VOLTAR ";

            int by1 = y + 6;
            int by2 = y + 8;
            AddButton(x + 4, by1, b1, ConsoleColor.DarkGreen, () => { _game.SalvarJogo(); Sair(); });
            AddButton(x + 6 + b1.Length, by1, b2, ConsoleColor.DarkCyan, () => { _game.SalvarAutomatico("saida"); Sair(); });
            AddButton(x + 4, by2, b3, ConsoleColor.DarkRed, Sair);
            AddButton(x + 6 + b3.Length, by2, b4, ConsoleColor.DarkGray, () => ConfirmandoSaida = false);
        }

        private static void Sair()
        {
            Console.ResetColor();
            Console.Clear();
            Console.CursorVisible = true;
            Environment.Exit(0);
        }

        // ------------------------------------------------------------------
        //  LOCALIDADE 1 · COZINHA
        // ------------------------------------------------------------------
        private void RenderCozinha()
        {
            var pedido = _game.PedidoAtual;
            int W = MainWidth;
            int inner = W - 4;
            int top = ContentTop;

            DrawPanel(1, top, W, 6, $"PEDIDO DO CLIENTE · COZINHA ({_game.HorarioTurnoFormatado})", ConsoleColor.Yellow);

            int tx = 1 + W - 2;
            string btnPedro = " [P] PEDRO ";
            tx -= btnPedro.Length;
            AddButton(tx, top, btnPedro, ConsoleColor.DarkCyan, () => _game.FalarComPersonagem("pedro"));
            tx -= 1;

            if (_game.SequenciaPerfeita >= 2)
            {
                string t = $" COMBO {_game.SequenciaPerfeita}x ";
                tx -= t.Length;
                Write(tx, top, t, ConsoleColor.Black, ConsoleColor.Yellow);
                tx -= 1;
            }
            if (pedido.EClienteCritico)
            {
                string t = " CRÍTICA ";
                tx -= t.Length;
                Write(tx, top, t, ConsoleColor.White, ConsoleColor.DarkRed);
                tx -= 1;
            }
            else if (pedido.EInfluenciador)
            {
                string t = " INFLUENCER ";
                tx -= t.Length;
                Write(tx, top, t, ConsoleColor.White, ConsoleColor.DarkMagenta);
                tx -= 1;
            }
            if (pedido.EPedidoSazonalBonus)
            {
                string t = " +35% ";
                tx -= t.Length;
                Write(tx, top, t, ConsoleColor.Black, ConsoleColor.Green);
            }

            var fala = Wrap(pedido.FalaDoCliente, inner, 2);
            for (int i = 0; i < fala.Count; i++)
                Write(3, top + 1 + i, fala[i], ConsoleColor.White);

            string valorStr = Money(_game.CalcularValorVendaPrato());
            Write(3, top + 3, "Valor ", ConsoleColor.DarkGray);
            Write(9, top + 3, valorStr, ConsoleColor.Green);
            Write(9 + valorStr.Length + 1, top + 3, "· -1 Energia", ConsoleColor.DarkGray);
            if (pedido.IngredientesExcluidos.Count > 0)
            {
                int sx = 9 + valorStr.Length + 15;
                if (sx < W - 12)
                    Write(sx, top + 3, Fit("SEM: " + string.Join(", ", pedido.IngredientesExcluidos), W - sx), ConsoleColor.Red);
            }

            var guia = new List<Seg>();
            void AddGuia(string nome)
            {
                bool sem = pedido.IngredientesExcluidos.Contains(nome);
                guia.Add(new Seg(sem ? nome + "(sem)" : nome, sem ? ConsoleColor.DarkGray : IngFg(nome)));
            }
            AddGuia(pedido.Receita.IngredienteBaseTopo);
            foreach (var ing in pedido.Receita.IngredientesPadrao) AddGuia(ing);
            if (!string.IsNullOrEmpty(pedido.Receita.IngredienteBaseFundo)) AddGuia(pedido.Receita.IngredienteBaseFundo);

            string rotuloGuia = "GUIA ";
            Write(3, top + 4, rotuloGuia, ConsoleColor.DarkCyan);
            DrawFlow(3 + rotuloGuia.Length, top + 4, Flow(guia, inner - rotuloGuia.Length, Sym(" → ", " > "), ConsoleColor.DarkGray), 1);

            // Bancada
            int byPanel = top + 6;
            DrawPanel(1, byPanel, W, 5, $"NA BANCADA · {pedido.PratoMontado.Count}/{pedido.OrdemExigida.Count} ingrediente(s)", ConsoleColor.Green);
            if (pedido.PratoMontado.Count == 0)
            {
                Write(3, byPanel + 1, Fit($"Clique nos ingredientes abaixo começando por [{pedido.OrdemExigida.FirstOrDefault()}] ou digite \"adicionar ...\".", inner), ConsoleColor.DarkGray);
            }
            else
            {
                var plate = new List<Seg>();
                for (int i = 0; i < pedido.PratoMontado.Count; i++)
                    plate.Add(new Seg($"{i + 1}.{pedido.PratoMontado[i]}", IngFg(pedido.PratoMontado[i])));
                DrawFlow(3, byPanel + 1, Flow(plate, inner, Sym(" → ", " > "), ConsoleColor.DarkGray), 2);
            }

            int bx = 3;
            bool podeEntregar = pedido.PratoMontado.Count > 0;
            string b1 = podeEntregar ? $" {Sym("►", ">")} [E] ENTREGAR " : $" {Sym("✕", "X")} [E] ENTREGAR (vazio) ";
            AddButton(bx, byPanel + 3, b1, podeEntregar ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray, () => _game.EntregarPedido());
            bx += b1.Length + 1;
            string b2 = $" {Sym("◄", "<")} [Z] DESFAZER ";
            AddButton(bx, byPanel + 3, b2, _game.PodeDesfazer ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray, () => _game.DesfazerUltimaAcao());
            bx += b2.Length + 1;
            string b3 = " [X] LIMPAR ";
            AddButton(bx, byPanel + 3, b3, podeEntregar ? ConsoleColor.DarkRed : ConsoleColor.DarkGray, () => _game.LimparPratoAtual());
            bx += b3.Length + 1;

            bool temFaltante = pedido.OrdemExigida.Any(ing => !_game.Estoque.TryGetValue(ing, out int q) || q <= 0);
            if (temFaltante && bx + 18 < W - 18)
            {
                AddButton(bx, byPanel + 3, " [B] REPOR FALT. ", ConsoleColor.DarkRed, () => _game.ComprarIngredientesFaltantesPedido());
            }

            string modo = MostrarTodosIngredientesCozinha ? " [M] TODOS " : " [M] INTELIGENTE ";
            AddButton(1 + W - 2 - modo.Length, byPanel + 3, modo, ConsoleColor.DarkMagenta, () =>
            {
                MostrarTodosIngredientesCozinha = !MostrarTodosIngredientesCozinha;
                PaginaCozinha = 0;
            });

            // Ingredientes
            int py = byPanel + 5;
            int ph = Math.Max(4, ContentBottom - py);
            List<string> lista = MostrarTodosIngredientesCozinha
                ? Catalog.TodosIngredientes.Where(i => i.Nivel <= _game.NivelAtual).Select(i => i.Nome).ToList()
                : pedido.OpcoesBancada;

            int rows = Math.Max(1, ph - 2);
            int nameW = lista.Count == 0 ? 8 : Math.Min(19, Math.Max(8, lista.Max(n => n.Length)));
            int btnW = nameW + 9;
            int cols = Math.Max(1, (W - 4 + 1) / (btnW + 1));
            int perPage = cols * rows;
            int totalPag = Math.Max(1, (int)Math.Ceiling(lista.Count / (double)perPage));
            PaginaCozinha = Math.Clamp(PaginaCozinha, 0, totalPag - 1);

            DrawPanel(1, py, W, ph, $"INGREDIENTES · pág {PaginaCozinha + 1}/{totalPag}", ConsoleColor.Cyan);
            if (totalPag > 1)
            {
                string nx = $" PRÓX {Sym("►", ">")} ";
                string pv = $" {Sym("◄", "<")} ANT ";
                AddButton(1 + W - 2 - nx.Length, py, nx, ConsoleColor.DarkGray, () => PaginaCozinha = Math.Min(totalPag - 1, PaginaCozinha + 1));
                AddButton(1 + W - 2 - nx.Length - 1 - pv.Length, py, pv, ConsoleColor.DarkGray, () => PaginaCozinha = Math.Max(0, PaginaCozinha - 1));
            }

            var itensPagina = lista.Skip(PaginaCozinha * perPage).Take(perPage).ToList();
            for (int idx = 0; idx < itensPagina.Count; idx++)
            {
                string nome = itensPagina[idx];
                int c = idx % cols;
                int r = idx / cols;
                int bxx = 3 + c * (btnW + 1);
                int byy = py + 1 + r;

                int qtd = _game.Estoque.ContainsKey(nome) ? _game.Estoque[nome] : 0;
                int contam = _game.EstoqueContaminado.ContainsKey(nome) ? _game.EstoqueContaminado[nome] : 0;

                string qtxt = qtd > 0
                    ? $"x{qtd}" + (qtd <= 2 ? "!" : "") + (contam > 0 ? Sym("▲", "X") : "")
                    : "+BUY";

                string label = " " + Fit(nome, nameW).PadRight(nameW) + " " + qtxt.PadRight(6) + " ";
                string copia = nome;

                if (qtd > 0)
                {
                    var info = IngPorNome.TryGetValue(nome, out var inf) ? inf : null;
                    var corCat = info != null ? Catalog.ObterCorCategoria(info.Categoria) : ConsoleColor.DarkCyan;
                    AddButton(bxx, byy, label, corCat, () => _game.AdicionarIngredienteAoPrato(copia));
                }
                else
                {
                    AddButton(bxx, byy, label, ConsoleColor.DarkRed, () => _game.CompraEmergencialIngrediente(copia));
                }
            }

            int lx = 2;
            int ly = ContentBottom;
            var cats = new (string nome, IngredientCategory cat)[]
            {
                ("Base", IngredientCategory.Base), ("Prot", IngredientCategory.Proteina),
                ("Lat", IngredientCategory.Laticinio), ("Veg", IngredientCategory.Vegetal),
                ("Molho", IngredientCategory.MolhoTempero), ("Doce", IngredientCategory.DoceFruta)
            };
            foreach (var (nome, cat) in cats)
            {
                if (lx + nome.Length + 4 > W) break;
                Write(lx, ly, Sym("■ ", "# "), Brighten(Catalog.ObterCorCategoria(cat)));
                Write(lx + 2, ly, nome, ConsoleColor.Gray);
                lx += nome.Length + 4;
            }
            if (lx + 16 <= W)
                Write(lx + 1, ly, $"! baixo  {Sym("▲", "X")} contam.", ConsoleColor.DarkGray);
        }

        // ------------------------------------------------------------------
        //  LOCALIDADE 2 · DOCAS (ENTREGAS)
        // ------------------------------------------------------------------
        private void RenderEntregas()
        {
            int W = MainWidth;
            int top = ContentTop;
            int h = ContentBottom - top + 1;
            int n = _game.EntregaAtual.Count;

            DrawPanel(1, top, W, h, "AUDITORIA DE ENTREGA · DOCAS", ConsoleColor.Yellow);

            string btnAna = " [N] ANA ";
            AddButton(1 + W - 2 - btnAna.Length, top, btnAna, ConsoleColor.DarkCyan, () => _game.FalarComPersonagem("ana"));

            Write(3, top + 1, $"Hoje: {_game.DataHojeFormatada} · Válidas: SP, Curitiba, RJ", ConsoleColor.White);
            int max = _game.InspecoesCaixaMaximas;
            int rest = Math.Clamp(_game.InspecoesCaixaRestantes, 0, max);
            int ix = Math.Min(W - 22, 54);
            Write(ix, top + 1, "Insp: ", ConsoleColor.DarkGray);
            Write(ix + 6, top + 1, new string(Cfg.ModoSemantico ? '#' : '●', rest), rest == 0 ? ConsoleColor.Red : ConsoleColor.Cyan);
            Write(ix + 6 + rest, top + 1, new string(Cfg.ModoSemantico ? '.' : '○', max - rest), ConsoleColor.DarkGray);

            Write(3, top + 2, Fit("Abra caixas suspeitas (Físico, Lote LT-XXX, Frios ≤5°C). Comando: \"abrir caixa 1\", \"recusar caixa 2\".", W - 4), ConsoleColor.DarkGray);

            bool compacto = W < 96;
            int nw = compacto ? 14 : 18;
            int fw = compacto ? 14 : 17;
            int cidW = compacto ? 10 : 14;

            int cIdx = 3;
            int cNota = cIdx + 3;
            int cQtd = cNota + nw + 2;
            int cForn = cQtd + 4;
            int cCid = cForn + fw + 2;
            int cData = cCid + cidW + 2;
            int cCaixa = cData + 11;
            int cDec = cCaixa + (compacto ? 9 : 11);

            int yHeader = top + 3;
            Write(2, yHeader, new string(' ', W - 2), ConsoleColor.White, ConsoleColor.DarkBlue);
            Write(cIdx, yHeader, "#", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cNota, yHeader, "NOTA", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cQtd, yHeader, "QTD", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cForn, yHeader, "FORNECEDOR", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cCid, yHeader, "CIDADE", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cData, yHeader, "DATA", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cCaixa, yHeader, "CAIXA", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            if (cDec + 6 < W) Write(cDec, yHeader, "DECISÃO", ConsoleColor.Cyan, ConsoleColor.DarkBlue);

            int maxRowsY = ContentBottom - 2;
            for (int i = 0; i < n; i++)
            {
                int y = yHeader + 1 + i * 2;
                if (y > maxRowsY) break;

                var item = _game.EntregaAtual[i];
                int idx = i;

                Write(cIdx, y, (i + 1).ToString(), ConsoleColor.Yellow);
                Write(cNota, y, Fit(item.NomeNota, nw), ConsoleColor.White);
                Write(cQtd, y, $"+{item.Quantidade}", ConsoleColor.Green);
                Write(cForn, y, Fit(item.Fornecedor, fw), ConsoleColor.White);
                Write(cCid, y, Fit(item.Cidade, cidW), ConsoleColor.White);
                Write(cData, y, Fit(item.DataNota, 10), ConsoleColor.White);

                if (!item.CaixaAberta)
                {
                    string lblAbrir = compacto ? $" ABR({rest}) " : $" ABRIR({rest}) ";
                    AddButton(cCaixa, y, lblAbrir, rest > 0 ? ConsoleColor.DarkCyan : ConsoleColor.DarkGray, () => _game.InspecionarCaixaEntrega(idx));
                }
                else
                {
                    Write(cCaixa, y, " aberta ", ConsoleColor.Cyan);
                }

                string lblDec = item.MarcadoComoValidoPeloJogador
                    ? (compacto ? " OK  " : " ACEITAR ")
                    : (compacto ? " NÃO " : " RECUSAR ");
                AddButton(cDec, y, lblDec, item.MarcadoComoValidoPeloJogador ? ConsoleColor.DarkGreen : ConsoleColor.DarkRed, () => _game.AlternarAprovacaoItemEntrega(idx));

                if (y + 1 <= maxRowsY)
                {
                    Write(cNota, y + 1, Sym("└─", "+-"), ConsoleColor.DarkGray);
                    if (item.CaixaAberta)
                    {
                        string det = $"Físico: {item.NomeFisico} · Lote: {item.Lote} · Temp: {item.Temperatura}";
                        Write(cNota + 3, y + 1, Fit(det, W - cNota - 4), ConsoleColor.Cyan);
                    }
                    else
                    {
                        Write(cNota + 3, y + 1, "caixa fechada · abra ou decida pela nota", ConsoleColor.DarkGray);
                    }
                }
            }

            AddButton(3, ContentBottom - 1, " [A] CONCLUIR AUDITORIA E GUARDAR NO ESTOQUE (-1 Energia) ", ConsoleColor.DarkBlue, () => _game.FinalizarConferenciaEntrega());
        }

        // ------------------------------------------------------------------
        //  LOCALIDADE 3 · CARDÁPIO & RECEITAS
        // ------------------------------------------------------------------
        private void RenderLivroReceitas()
        {
            int W = MainWidth;
            int top = ContentTop;
            int h = ContentBottom - top + 1;
            var pratos = Catalog.Pratos.Where(p => p.NivelDesbloqueio <= _game.NivelAtual).ToList();

            int listTop = top + 1;
            int listBottom = ContentBottom - 2;
            int avail = Math.Max(3, listBottom - listTop + 1);
            int flowW = Math.Max(20, W - 16);

            var cards = new List<(DishRecipe p, List<List<Seg>> lines)>();
            foreach (var p in pratos)
            {
                var segs = new List<Seg> { new Seg(p.IngredienteBaseTopo, IngFg(p.IngredienteBaseTopo)) };
                foreach (var ing in p.IngredientesPadrao) segs.Add(new Seg(ing, IngFg(ing)));
                if (!string.IsNullOrEmpty(p.IngredienteBaseFundo)) segs.Add(new Seg(p.IngredienteBaseFundo, IngFg(p.IngredienteBaseFundo)));
                cards.Add((p, Flow(segs, flowW, Sym(" → ", " > "), ConsoleColor.DarkGray)));
            }

            var paginas = new List<(int start, int count)>();
            int s = 0;
            while (s < cards.Count)
            {
                int used = 0, c = 0;
                while (s + c < cards.Count)
                {
                    int hgt = 1 + Math.Min(cards[s + c].lines.Count, 2);
                    if (used + hgt > avail && c > 0) break;
                    used += hgt;
                    c++;
                }
                paginas.Add((s, c));
                s += c;
            }
            if (paginas.Count == 0) paginas.Add((0, 0));

            int totalPag = paginas.Count;
            PaginaReceitas = Math.Clamp(PaginaReceitas, 0, totalPag - 1);

            DrawPanel(1, top, W, h, $"CARDÁPIO & RECEITAS · pág {PaginaReceitas + 1}/{totalPag}", ConsoleColor.Yellow);
            if (totalPag > 1)
            {
                string nx = $" PRÓX {Sym("►", ">")} ";
                string pv = $" {Sym("◄", "<")} ANT ";
                AddButton(1 + W - 2 - nx.Length, top, nx, ConsoleColor.DarkGray, () => PaginaReceitas = Math.Min(totalPag - 1, PaginaReceitas + 1));
                AddButton(1 + W - 2 - nx.Length - 1 - pv.Length, top, pv, ConsoleColor.DarkGray, () => PaginaReceitas = Math.Max(0, PaginaReceitas - 1));
            }

            int row = listTop;
            var pg = paginas[PaginaReceitas];
            for (int i = pg.start; i < pg.start + pg.count; i++)
            {
                var (p, lines) = cards[i];
                bool ativo = _game.PratosAtivosNoCardapio.Contains(p.Nome);
                string nomeCopy = p.Nome;

                AddButton(3, row, ativo ? "  ATIVO  " : " INATIVO ", ativo ? ConsoleColor.DarkGreen : ConsoleColor.DarkRed, () => _game.AlternarPratoNoCardapio(nomeCopy));

                int nx2 = 14;
                Write(nx2, row, p.Nome, ativo ? ConsoleColor.White : ConsoleColor.DarkGray);
                nx2 += p.Nome.Length + 2;
                Write(nx2, row, $"Fase {p.NivelDesbloqueio}", ConsoleColor.DarkGray);
                nx2 += 8;

                if (p.EstacaoFavorita == "INVERNO") Write(nx2, row, "INVERNO", ConsoleColor.Cyan);
                else if (p.EstacaoFavorita == "VERAO") Write(nx2, row, "VERÃO", ConsoleColor.Yellow);
                else if (p.EstacaoFavorita == "EVENTO") Write(nx2, row, "EVENTO", ConsoleColor.Magenta);
                row++;

                int used = DrawFlow(14, row, lines, 2);
                row += used;
            }

            Write(3, ContentBottom - 1, Fit("Clique em ATIVO/INATIVO ou digite \"ativar pizza\" / \"desativar salada\" (mínimo 2 ativos).", W - 4), ConsoleColor.DarkGray);
        }

        // ------------------------------------------------------------------
        //  LOCALIDADE 4 · DESPENSA & ESTOQUE
        // ------------------------------------------------------------------
        private void RenderEstoqueCatalogo()
        {
            int W = MainWidth;
            int top = ContentTop;
            int h = ContentBottom - top + 1;
            var disponiveis = Catalog.TodosIngredientes.Where(i => i.Nivel <= _game.NivelAtual).ToList();
            int cap = _game.CapacidadeMaximaEstoque;

            int gridTop = top + 4;
            int gridBottom = ContentBottom - 2;
            int rows = Math.Max(1, gridBottom - gridTop + 1);
            int cellW = W >= 96 ? 32 : 26;
            int cols = Math.Max(1, (W - 4 + 1) / (cellW + 1));
            int perPage = cols * rows;
            int totalPag = Math.Max(1, (int)Math.Ceiling(disponiveis.Count / (double)perPage));
            PaginaEstoque = Math.Clamp(PaginaEstoque, 0, totalPag - 1);

            DrawPanel(1, top, W, h, $"DESPENSA & ESTOQUE · pág {PaginaEstoque + 1}/{totalPag}", ConsoleColor.Yellow);
            if (totalPag > 1)
            {
                string nx = $" PRÓX {Sym("►", ">")} ";
                string pv = $" {Sym("◄", "<")} ANT ";
                AddButton(1 + W - 2 - nx.Length, top, nx, ConsoleColor.DarkGray, () => PaginaEstoque = Math.Min(totalPag - 1, PaginaEstoque + 1));
                AddButton(1 + W - 2 - nx.Length - 1 - pv.Length, top, pv, ConsoleColor.DarkGray, () => PaginaEstoque = Math.Max(0, PaginaEstoque - 1));
            }

            var inv = _game.ObterResumoInventario();
            double ocup = inv.CapacidadeTotal > 0 ? (double)inv.TotalUnidades / inv.CapacidadeTotal : 0;
            Write(3, top + 1, "OCUPAÇÃO", ConsoleColor.DarkGray);
            DrawBar(12, top + 1, 22, ocup, ConsoleColor.Cyan);
            Write(35, top + 1, $"{inv.TotalUnidades}/{inv.CapacidadeTotal} un. ({ocup * 100:F0}%) · Cap/item: {cap}", ConsoleColor.Gray);

            Write(3, top + 2, "Esgotados: ", ConsoleColor.DarkGray);
            Write(14, top + 2, inv.Esgotados.ToString(), inv.Esgotados > 0 ? ConsoleColor.Red : ConsoleColor.Green);
            Write(19, top + 2, "Baixos: ", ConsoleColor.DarkGray);
            Write(27, top + 2, inv.Baixos.ToString(), inv.Baixos > 0 ? ConsoleColor.Yellow : ConsoleColor.Green);
            Write(32, top + 2, "Contaminados: ", ConsoleColor.DarkGray);
            Write(46, top + 2, inv.Contaminados.ToString(), inv.Contaminados > 0 ? ConsoleColor.Magenta : ConsoleColor.Green);

            var pagina = disponiveis.Skip(PaginaEstoque * perPage).Take(perPage).ToList();
            int nameMax = cellW - 14;
            for (int idx = 0; idx < pagina.Count; idx++)
            {
                var ing = pagina[idx];
                int c = idx / rows;
                int r = idx % rows;
                int cx = 3 + c * (cellW + 1);
                int cy = gridTop + r;

                int qtd = _game.Estoque.ContainsKey(ing.Nome) ? _game.Estoque[ing.Nome] : 0;
                int contam = _game.EstoqueContaminado.ContainsKey(ing.Nome) ? _game.EstoqueContaminado[ing.Nome] : 0;

                ConsoleColor corNome = qtd == 0 ? ConsoleColor.Red : (contam > 0 ? ConsoleColor.Magenta : Brighten(Catalog.ObterCorCategoria(ing.Categoria)));
                double ratio = cap > 0 ? (double)qtd / cap : 0;
                ConsoleColor corBar = ratio < 0.25 ? ConsoleColor.Red : (ratio < 0.6 ? ConsoleColor.Yellow : ConsoleColor.Green);

                if (contam > 0) Write(cx, cy, Sym("▲", "X"), ConsoleColor.Magenta);
                Write(cx + 1, cy, Fit(ing.Nome, nameMax).PadRight(nameMax), corNome);
                DrawBar(cx + nameMax + 2, cy, 5, ratio, corBar);
                Write(cx + nameMax + 8, cy, $"{qtd}/{cap}", qtd == 0 ? ConsoleColor.Red : ConsoleColor.Gray);
            }

            Write(3, ContentBottom - 1, Fit("Fornecedores autorizados: AgroFoods Brasil · Distribuidora Sul · Laticínios Real · Horta Fresca SA", W - 4), ConsoleColor.Cyan);
        }

        // ------------------------------------------------------------------
        //  LOCALIDADE 5 · ESCRITÓRIO (GERÊNCIA)
        // ------------------------------------------------------------------
        private void ProgressRow(int y, string label, int valor, int alvo, string sufixo, int maxW)
        {
            Write(3, y, label.PadRight(14), ConsoleColor.Gray);
            double r = alvo > 0 ? (double)valor / alvo : 1;
            ConsoleColor col = valor >= alvo ? ConsoleColor.Green : (r >= 0.6 ? ConsoleColor.Yellow : ConsoleColor.Red);
            DrawBar(18, y, 20, r, col);
            Write(39, y, $"{valor}/{alvo}".PadRight(9), col);
            Write(48, y, Fit(sufixo, Math.Max(8, maxW - 49)), ConsoleColor.DarkGray);
        }

        private void RenderAdministracao()
        {
            int W = MainWidth;
            int top = ContentTop;
            var cfg = _game.ConfigAtual;
            var next = _game.NivelAtual < 5 ? Catalog.Levels[_game.NivelAtual] : null;

            DrawPanel(1, top, W, 7, next != null ? $"PROGRESSO · FASE {next.Nivel}/5 ({next.Titulo})" : "FASE MÁXIMA · META FINAL", ConsoleColor.Green);
            string btnCarlos = " [C] CARLOS ";
            AddButton(1 + W - 2 - btnCarlos.Length, top, btnCarlos, ConsoleColor.DarkCyan, () => _game.FalarComPersonagem("carlos"));

            Write(3, top + 1, Fit(_game.ObterTextoProximaMeta(), W - 4), ConsoleColor.Yellow);
            if (next != null)
            {
                ProgressRow(top + 2, "Funcionários", _game.Funcionarios, next.MinFuncionarios, $"rec. Fase {cfg.Nivel}: {cfg.RecFuncionarios}", W);
                ProgressRow(top + 3, "Limpeza", _game.Limpeza, next.MinLimpeza, $"rec. Fase {cfg.Nivel}: {cfg.RecLimpeza}", W);
                ProgressRow(top + 4, "Upgrades", _game.TotalUpgrades, next.MinUpgrades, $"rec. Fase {cfg.Nivel}: {cfg.RecUpgrades}", W);
            }
            else
            {
                ProgressRow(top + 2, "Funcionários", _game.Funcionarios, cfg.RecFuncionarios, "recomendado da fase", W);
                ProgressRow(top + 3, "Limpeza", _game.Limpeza, cfg.RecLimpeza, "recomendado da fase", W);
                ProgressRow(top + 4, "Upgrades", _game.TotalUpgrades, cfg.RecUpgrades, "recomendado da fase", W);
            }
            ProgressRow(top + 5, "Limpeza ideal", _game.Limpeza, Math.Max(1, _game.ObterLimpezaIdeal()), "abaixo gera multas sanitárias", W);

            int bx = Math.Max(52, W - 21);

            int eqY = top + 7;
            DrawPanel(1, eqY, W, 4, "EQUIPE & HIGIENE", ConsoleColor.Cyan);
            bool podeFunc = _game.Dinheiro >= cfg.CustoPorFuncionario || _game.ModoJogo == GameMode.GestaoLivre;
            Write(3, eqY + 1, "Funcionários", ConsoleColor.Gray);
            Write(16, eqY + 1, _game.Funcionarios.ToString(), ConsoleColor.White);
            Write(22, eqY + 1, $"{Money(cfg.CustoPorFuncionario)}/un", podeFunc ? ConsoleColor.Gray : ConsoleColor.Red);
            AddButton(bx, eqY + 1, " +1 ", podeFunc ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray, () => _game.ContratarFuncionariosLote(1));
            AddButton(bx + 5, eqY + 1, " +10 ", podeFunc ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray, () => _game.ContratarFuncionariosLote(10));
            AddButton(bx + 11, eqY + 1, " +MAX ", podeFunc ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray, () => _game.ContratarFuncionariosLote(999));

            bool podeLimp = _game.Dinheiro >= cfg.CustoPorLimpeza || _game.ModoJogo == GameMode.GestaoLivre;
            Write(3, eqY + 2, "Limpeza", ConsoleColor.Gray);
            Write(16, eqY + 2, _game.Limpeza.ToString(), ConsoleColor.White);
            Write(22, eqY + 2, $"{Money(cfg.CustoPorLimpeza)}/un", podeLimp ? ConsoleColor.Gray : ConsoleColor.Red);
            AddButton(bx, eqY + 2, " +1 ", podeLimp ? ConsoleColor.DarkCyan : ConsoleColor.DarkGray, () => _game.ComprarLimpezaLote(1));
            AddButton(bx + 5, eqY + 2, " +10 ", podeLimp ? ConsoleColor.DarkCyan : ConsoleColor.DarkGray, () => _game.ComprarLimpezaLote(10));
            AddButton(bx + 11, eqY + 2, " +MAX ", podeLimp ? ConsoleColor.DarkCyan : ConsoleColor.DarkGray, () => _game.ComprarLimpezaLote(999));

            int upY = eqY + 4;
            DrawPanel(1, upY, W, 6, "UPGRADES DO RESTAURANTE", ConsoleColor.Magenta);
            RenderUpgradeRow(upY + 1, "FORNO", "Forno Turbo", _game.UpgForno, $"+Energia ({_game.AcoesMaximasDia}/dia)", bx);
            RenderUpgradeRow(upY + 2, "GELADEIRA", "Câmara Fria", _game.UpgGeladeira, $"cap. {_game.CapacidadeMaximaEstoque} un/item", bx);
            RenderUpgradeRow(upY + 3, "BALCAO", "Balcão VIP", _game.UpgBalcao, "+gorjeta e reputação", bx);
            RenderUpgradeRow(upY + 4, "LOGISTICA", "Logística", _game.UpgLogistica, $"{_game.InspecoesCaixaMaximas} insp./carga", bx);

            int spY = upY + 6;
            if (spY + 2 <= ContentBottom)
            {
                DrawPanel(1, spY, W, 3, $"SALÁRIOS & PREÇOS (Moral {_game.Narrativa.MoralEquipe}/100 · reversíveis com [Z])", ConsoleColor.Yellow);
                Write(3, spY + 1, "Salários", ConsoleColor.Gray);
                Write(12, spY + 1, $"{_game.MultiplicadorSalario * 100:F0}%", ConsoleColor.White);
                AddButton(18, spY + 1, " -10% ", ConsoleColor.DarkRed, () => _game.AjustarSalario(-0.10));
                AddButton(25, spY + 1, " +10% ", ConsoleColor.DarkGreen, () => _game.AjustarSalario(0.10));

                int px = Math.Min(40, W - 36);
                Write(px, spY + 1, "Preços", ConsoleColor.Gray);
                Write(px + 7, spY + 1, $"{_game.MultiplicadorPrecoPrato * 100:F0}%", ConsoleColor.White);
                AddButton(px + 13, spY + 1, " -10% ", ConsoleColor.DarkYellow, () => _game.AjustarPrecoPratos(-0.10));
                AddButton(px + 20, spY + 1, " +10% ", ConsoleColor.DarkGreen, () => _game.AjustarPrecoPratos(0.10));
            }
        }

        private void RenderUpgradeRow(int y, string codigo, string nome, int nivelAtual, string efeito, int bx)
        {
            double custo = _game.ObterCustoUpgradeEspecifico(nivelAtual);
            bool pode = _game.Dinheiro >= custo || _game.ModoJogo == GameMode.GestaoLivre;
            Write(3, y, nome.PadRight(12), ConsoleColor.White);
            Write(16, y, $"Nv.{nivelAtual}".PadRight(6), ConsoleColor.Yellow);
            Write(22, y, Fit(efeito, Math.Max(8, bx - 36)), ConsoleColor.DarkGray);

            string c = Money(custo);
            Write(bx - 1 - c.Length, y, c, pode ? ConsoleColor.Gray : ConsoleColor.Red);

            ConsoleColor bg = pode ? ConsoleColor.DarkMagenta : ConsoleColor.DarkGray;
            AddButton(bx, y, " +1 ", bg, () => _game.ComprarUpgradeNomeado(codigo, 1));
            AddButton(bx + 5, y, " +5 ", bg, () => _game.ComprarUpgradeNomeado(codigo, 5));
            AddButton(bx + 10, y, " +MAX ", bg, () => _game.ComprarUpgradeNomeado(codigo, 999));
        }

        // ------------------------------------------------------------------
        //  LOCALIDADE 6 · BANCO EMPRESARIAL
        // ------------------------------------------------------------------
        private void RenderBanco()
        {
            int W = MainWidth;
            int top = ContentTop;

            DrawPanel(1, top, W, 8, "BANCO EMPRESARIAL & CAIXA", ConsoleColor.Yellow);

            double limite = _game.LimiteCreditoBanco;
            double divida = _game.DividaTotalBanco;
            double livre = Math.Max(0, Math.Round(limite - divida, 2));
            double uso = limite > 0 ? divida / limite : (divida > 0 ? 1 : 0);

            Write(3, top + 1, "Limite de crédito", ConsoleColor.DarkGray);
            Write(22, top + 1, Money(limite), ConsoleColor.Green);
            Write(40, top + 1, $"Livre: {Money(livre)}", ConsoleColor.Cyan);

            Write(3, top + 3, "Dívida total", ConsoleColor.DarkGray);
            Write(22, top + 3, Money(divida), divida > 0 ? ConsoleColor.Red : ConsoleColor.Green);
            DrawBar(40, top + 3, 22, uso, uso > 0.8 ? ConsoleColor.Red : (uso > 0.4 ? ConsoleColor.Yellow : ConsoleColor.Green));
            Write(63, top + 3, $"{uso * 100:F0}%", ConsoleColor.Gray);

            Write(3, top + 4, "Prazo restante", ConsoleColor.DarkGray);
            Write(22, top + 4, $"{_game.MesesRestantesDivida} meses", ConsoleColor.White);
            Write(40, top + 4, $"Parcela mensal: {Money(_game.ParcelaMensalBanco)}", ConsoleColor.White);

            Write(3, top + 6, Fit($"Juros de {_game.TaxaJurosMensal * 100:F1}% a.m. sobre o saldo devedor. Quitar antes economiza juros!", W - 4), ConsoleColor.Cyan);

            DrawPanel(1, top + 8, W, 4, "TOMAR EMPRÉSTIMO (ou digite \"pedir emprestimo 1500\")", ConsoleColor.Cyan);
            AddButton(3, top + 9, " + R$ 1.500 ", livre >= 1500 ? ConsoleColor.DarkCyan : ConsoleColor.DarkGray, () => _game.PedirEmprestimo(1500));
            AddButton(17, top + 9, " + R$ 4.500 ", livre >= 4500 ? ConsoleColor.DarkCyan : ConsoleColor.DarkGray, () => _game.PedirEmprestimo(4500));
            AddButton(31, top + 9, " + R$ 15.000 ", livre >= 15000 ? ConsoleColor.DarkCyan : ConsoleColor.DarkGray, () => _game.PedirEmprestimo(15000));

            bool temDiv = divida > 0;
            DrawPanel(1, top + 12, W, 4, "AMORTIZAÇÃO ANTECIPADA (ou digite \"quitar divida\")", ConsoleColor.Green);
            AddButton(3, top + 13, " PAGAR R$ 500 ", temDiv && (_game.Dinheiro >= Math.Min(500, divida) || _game.ModoJogo == GameMode.GestaoLivre) ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray, () => _game.AmortizarDivida(500));
            AddButton(19, top + 13, " PAGAR R$ 2.000 ", temDiv && (_game.Dinheiro >= Math.Min(2000, divida) || _game.ModoJogo == GameMode.GestaoLivre) ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray, () => _game.AmortizarDivida(2000));
            AddButton(37, top + 13, $" QUITAR TUDO ({Money(divida)}) ", temDiv && (_game.Dinheiro >= divida || _game.ModoJogo == GameMode.GestaoLivre) ? ConsoleColor.Green : ConsoleColor.DarkGray, () => _game.AmortizarDivida(_game.DividaTotalBanco));
        }

        // ------------------------------------------------------------------
        //  LOCALIDADE 7 · HISTÓRICO, CONQUISTAS & ÚLTIMOS COMANDOS
        // ------------------------------------------------------------------
        private void RenderHistorico()
        {
            int W = MainWidth;
            int top = ContentTop;
            int h = ContentBottom - top + 1;

            DrawPanel(1, top, W, h, $"DIÁRIO, CONQUISTAS ({_game.ConquistasDesbloqueadas.Count}/{Catalog.Conquistas.Count}) & COMANDOS", ConsoleColor.Yellow);

            int fx = 3;
            AddButton(fx, top + 1, " [T] TODOS ", FiltroHistorico == "TODOS" ? ConsoleColor.Cyan : ConsoleColor.DarkGray, () => { FiltroHistorico = "TODOS"; IndiceHistoricoSelecionado = 0; });
            fx += 12;
            AddButton(fx, top + 1, " [V] SUCESSOS ", FiltroHistorico == "SUCESSO" ? ConsoleColor.Green : ConsoleColor.DarkGray, () => { FiltroHistorico = "SUCESSO"; IndiceHistoricoSelecionado = 0; });
            fx += 15;
            AddButton(fx, top + 1, " [E] ALERTAS ", FiltroHistorico == "ERRO" ? ConsoleColor.Red : ConsoleColor.DarkGray, () => { FiltroHistorico = "ERRO"; IndiceHistoricoSelecionado = 0; });
            fx += 14;
            AddButton(fx, top + 1, " [C] COMANDOS ", FiltroHistorico == "COMANDOS" ? ConsoleColor.Yellow : ConsoleColor.DarkGray, () => { FiltroHistorico = "COMANDOS"; IndiceHistoricoSelecionado = 0; });
            fx += 15;
            AddButton(fx, top + 1, " [Q] CONQUISTAS ", FiltroHistorico == "CONQUISTAS" ? ConsoleColor.Magenta : ConsoleColor.DarkGray, () => { FiltroHistorico = "CONQUISTAS"; IndiceHistoricoSelecionado = 0; });

            if (FiltroHistorico == "CONQUISTAS")
            {
                int listY = top + 3;
                int maxY = ContentBottom - 4;
                Write(3, listY++, $"Conquistas: {_game.ConquistasDesbloqueadas.Count}/{Catalog.Conquistas.Count}  ·  Moral {_game.Narrativa.MoralEquipe}/100  ·  Selo Sanitário {_game.Narrativa.HistoricoSanitario}/100  ·  Fornecedores {_game.Narrativa.ConfiancaFornecedores}/100", ConsoleColor.Cyan);

                foreach (var c in Catalog.Conquistas)
                {
                    if (listY > maxY) break;
                    bool ok = _game.ConquistasDesbloqueadas.Contains(c.Id);
                    string mark = ok ? Sym("✓", "[OK]") : Sym("○", "[ ]");
                    string linha = $"{mark} {c.Titulo} (+{c.BonusPontos} pts) — {c.Descricao}";
                    Write(3, listY++, Fit(linha, W - 4), ok ? ConsoleColor.Green : ConsoleColor.DarkGray);
                }

                // Últimas crônicas narrativas no rodapé da aba
                int cy = ContentBottom - 3;
                Write(2, cy, new string(Cfg.ModoSemantico ? '-' : '─', W - 2), ConsoleColor.DarkGray);
                Write(3, cy + 1, "CRÔNICA DA PARTIDA (consequências duráveis):", ConsoleColor.Yellow);
                var ult = _game.Narrativa.Cronicas.TakeLast(1).FirstOrDefault() ?? "Nenhuma crônica registrada ainda.";
                Write(3, cy + 2, Fit(ult, W - 4), ConsoleColor.White);
                return;
            }

            if (FiltroHistorico == "COMANDOS")
            {
                var cmds = _commands.UltimosComandos.Reverse().ToList();
                Write(3, top + 3, $"Comandos digitados nesta sessão ({cmds.Count}). Clique em REEXECUTAR ou use ↑/↓ na barra de comando.", ConsoleColor.Gray);

                if (cmds.Count == 0)
                {
                    Write(3, top + 5, "Nenhum comando de texto foi digitado ainda.", ConsoleColor.DarkGray);
                    Write(3, top + 6, "Experimente apertar [/] e digitar \"falar com Pedro\" ou \"usar caixa\".", ConsoleColor.Cyan);
                    return;
                }

                int listStartY = top + 4;
                int maxVis = Math.Max(1, ContentBottom - listStartY - 1);
                IndiceHistoricoSelecionado = Math.Clamp(IndiceHistoricoSelecionado, 0, cmds.Count - 1);
                int start = Math.Max(0, IndiceHistoricoSelecionado - maxVis + 1);

                for (int i = 0; i < maxVis && (start + i) < cmds.Count; i++)
                {
                    int idx = start + i;
                    string cmd = cmds[idx];
                    bool sel = idx == IndiceHistoricoSelecionado;
                    int ry = listStartY + i;
                    Write(3, ry, (sel ? Sym("► ", "> ") : "  ") + $"{idx + 1}. {Fit(cmd, W - 24)}", sel ? ConsoleColor.Yellow : ConsoleColor.White);
                    string copia = cmd;
                    AddButton(W - 15, ry, $" {Sym("►", ">")} REEXECUTAR ", ConsoleColor.DarkGreen, () => ExecutarComando(copia));
                }
                return;
            }

            var eventos = _game.HistoricoEventos
                .AsEnumerable()
                .Reverse()
                .Where(e => FiltroHistorico == "TODOS" ||
                            (FiltroHistorico == "SUCESSO" && e.Severidade == FeedbackSeverity.Sucesso) ||
                            (FiltroHistorico == "ERRO" && (e.Severidade == FeedbackSeverity.Erro || e.Severidade == FeedbackSeverity.Aviso)))
                .ToList();

            if (eventos.Count == 0)
            {
                Write(3, top + 4, "Nenhum acontecimento encontrado para este filtro.", ConsoleColor.DarkGray);
                return;
            }

            IndiceHistoricoSelecionado = Math.Clamp(IndiceHistoricoSelecionado, 0, eventos.Count - 1);

            int detailH = 5;
            int listTopY = top + 3;
            int listMaxRows = Math.Max(3, ContentBottom - listTopY - detailH - 1);
            int offset = Math.Max(0, IndiceHistoricoSelecionado - listMaxRows + 1);

            if (W > 88)
            {
                AddButton(W - 18, top + 1, " ▲ ANT ", ConsoleColor.DarkGray, () => IndiceHistoricoSelecionado = Math.Max(0, IndiceHistoricoSelecionado - 1));
                AddButton(W - 10, top + 1, " ▼ PRÓX ", ConsoleColor.DarkGray, () => IndiceHistoricoSelecionado = Math.Min(eventos.Count - 1, IndiceHistoricoSelecionado + 1));
            }

            for (int i = 0; i < listMaxRows && (offset + i) < eventos.Count; i++)
            {
                int idx = offset + i;
                var ev = eventos[idx];
                bool sel = idx == IndiceHistoricoSelecionado;
                int ry = listTopY + i;

                ConsoleColor corSev = ev.Severidade switch
                {
                    FeedbackSeverity.Sucesso => ConsoleColor.Green,
                    FeedbackSeverity.Erro => ConsoleColor.Red,
                    FeedbackSeverity.Aviso => ConsoleColor.Yellow,
                    _ => ConsoleColor.Cyan
                };

                string linha = $"#{ev.Numero:000} D{ev.DiaSemana}/M{ev.Mes} [{Fit(ev.Localidade, 10)}] {ev.Acao}";
                if (sel)
                {
                    Write(2, ry, new string(' ', W - 2), ConsoleColor.Black, ConsoleColor.DarkCyan);
                    Write(3, ry, Sym("► ", "> ") + Fit(linha, W - 6), ConsoleColor.White, ConsoleColor.DarkCyan);
                }
                else
                {
                    Write(3, ry, "  " + Fit(linha, W - 6), corSev);
                }

                int captura = idx;
                BotoesAtivos.Add(new ClickableZone { X = 2, Y = ry, Width = W - 2, Height = 1, Label = linha, OnClick = () => IndiceHistoricoSelecionado = captura });
            }

            var selEv = eventos[IndiceHistoricoSelecionado];
            int dy = ContentBottom - detailH;
            Write(2, dy, new string(Cfg.ModoSemantico ? '-' : '─', W - 2), ConsoleColor.DarkGray);
            Write(3, dy + 1, $"DETALHE #{selEv.Numero} · {selEv.DataFormatada} (Dia {selEv.DiaSemana}/4 · Mês {selEv.Mes}) · {selEv.Localidade}", ConsoleColor.Yellow);
            Write(3, dy + 2, Fit("Ação: " + selEv.Acao, W - 4), ConsoleColor.White);
            Write(3, dy + 3, Fit("Resposta: " + selEv.Resposta, W - 4), ConsoleColor.Gray);
            Write(3, dy + 4, Fit("Estado: " + selEv.ResumoDeltas, W - 4), ConsoleColor.Cyan);
        }

        // ------------------------------------------------------------------
        //  LOCALIDADE 8 · TERMINAL DEV (senha: xit89)
        // ------------------------------------------------------------------
        private void DevRow(int y, string label, params DevBtn[] btns)
        {
            Write(3, y, Fit(label, 28), ConsoleColor.Yellow);
            int x = 32;
            foreach (var b in btns)
            {
                if (x + b.Text.Length >= MainWidth) break;
                AddButton(x, y, b.Text, b.Bg, b.Act);
                x += b.Text.Length + 1;
            }
        }

        private void RenderTerminalDev()
        {
            int W = MainWidth;
            int top = ContentTop;
            int h = ContentBottom - top + 1;

            if (!_game.TerminalDesbloqueado)
            {
                DrawPanel(1, top, W, Math.Min(13, h), "TERMINAL DE DESENVOLVEDOR · ACESSO RESTRITO", ConsoleColor.Magenta);
                Write(3, top + 2, "Este terminal exige autenticação de desenvolvedor.", ConsoleColor.Yellow);
                Write(3, top + 3, "Digite a senha no teclado e aperte Enter, ou clique nas teclas abaixo.", ConsoleColor.Gray);

                string mascara = new string(Cfg.ModoSemantico ? '*' : '•', _game.InputSenhaTerminal.Length);
                Write(3, top + 5, "SENHA", ConsoleColor.DarkGray);
                Write(10, top + 5, "[ " + mascara.PadRight(14) + " ]", ConsoleColor.Cyan);

                string[] teclas = { "x", "i", "t", "8", "9" };
                int kx = 3;
                foreach (var t in teclas)
                {
                    string tt = t;
                    AddButton(kx, top + 7, $"  {t}  ", ConsoleColor.DarkGray, () => _game.InputSenhaTerminal += tt);
                    kx += 6;
                }

                AddButton(3, top + 9, " AUTENTICAR [Enter] ", ConsoleColor.DarkGreen, () => _game.TentarDesbloquearTerminal());
                AddButton(25, top + 9, " LIMPAR ", ConsoleColor.DarkRed, () => _game.InputSenhaTerminal = "");
                return;
            }

            DrawPanel(1, top, W, h, "TERMINAL DE DESENVOLVEDOR · AUTENTICADO", ConsoleColor.Magenta);
            Write(3, top + 1, "Altere qualquer variável do jogo em tempo real.", ConsoleColor.Green);

            DevRow(top + 3, $"1. CAIXA ({Money(_game.Dinheiro)})",
                new DevBtn(" +1k ", ConsoleColor.DarkGreen, () => _game.DevAlterarDinheiro(1000)),
                new DevBtn(" +10k ", ConsoleColor.DarkGreen, () => _game.DevAlterarDinheiro(10000)),
                new DevBtn(" +100k ", ConsoleColor.DarkGreen, () => _game.DevAlterarDinheiro(100000)),
                new DevBtn(" -1k ", ConsoleColor.DarkRed, () => _game.DevAlterarDinheiro(-1000)),
                new DevBtn(" ZERAR ", ConsoleColor.Red, () => _game.DevDefinirDinheiro(0)));

            DevRow(top + 5, $"2. FUNC ({_game.Funcionarios})",
                new DevBtn(" +1 ", ConsoleColor.DarkGreen, () => _game.DevAlterarFuncionarios(1)),
                new DevBtn(" +10 ", ConsoleColor.DarkGreen, () => _game.DevAlterarFuncionarios(10)),
                new DevBtn(" +50 ", ConsoleColor.DarkGreen, () => _game.DevAlterarFuncionarios(50)),
                new DevBtn(" -1 ", ConsoleColor.DarkRed, () => _game.DevAlterarFuncionarios(-1)));

            DevRow(top + 7, $"3. LIMP ({_game.Limpeza})",
                new DevBtn(" +1 ", ConsoleColor.DarkCyan, () => _game.DevAlterarLimpeza(1)),
                new DevBtn(" +10 ", ConsoleColor.DarkCyan, () => _game.DevAlterarLimpeza(10)),
                new DevBtn(" +50 ", ConsoleColor.DarkCyan, () => _game.DevAlterarLimpeza(50)),
                new DevBtn(" -1 ", ConsoleColor.DarkRed, () => _game.DevAlterarLimpeza(-1)));

            DevRow(top + 9, $"4. UPG ({_game.TotalUpgrades})",
                new DevBtn(" +1 TODOS ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("TODOS", 1)),
                new DevBtn(" +10 TODOS ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("TODOS", 10)),
                new DevBtn(" -1 TODOS ", ConsoleColor.DarkRed, () => _game.DevAlterarUpgrades("TODOS", -1)));

            DevRow(top + 11, "   individuais",
                new DevBtn($" +Forno({_game.UpgForno}) ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("FORNO", 1)),
                new DevBtn($" +Gel({_game.UpgGeladeira}) ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("GELADEIRA", 1)),
                new DevBtn($" +Balc({_game.UpgBalcao}) ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("BALCAO", 1)),
                new DevBtn($" +Log({_game.UpgLogistica}) ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("LOGISTICA", 1)));

            DevRow(top + 13, $"5. REP ({_game.Reputacao:F1}) & FASE ({_game.NivelAtual})",
                new DevBtn(" +0.5 Rep ", ConsoleColor.DarkGreen, () => _game.DevAlterarReputacao(0.5)),
                new DevBtn(" -0.5 Rep ", ConsoleColor.DarkRed, () => _game.DevAlterarReputacao(-0.5)),
                new DevBtn(" +1 FASE ", ConsoleColor.DarkBlue, () => _game.DevAlterarNivel(1)),
                new DevBtn(" -1 FASE ", ConsoleColor.DarkRed, () => _game.DevAlterarNivel(-1)));

            DevRow(top + 15, "6. UTILITÁRIOS",
                new DevBtn(" ENCHER ESTOQUE ", ConsoleColor.DarkCyan, () => _game.DevEncherEstoqueTotal()),
                new DevBtn(" ZERAR DÍVIDA ", ConsoleColor.DarkGreen, () => _game.DevPerdoarDivida()),
                new DevBtn(" EVENTO ", ConsoleColor.DarkYellow, () => _game.SortearEventoDoDia()));

            AddButton(3, Math.Min(top + 17, ContentBottom - 1), " BLOQUEAR TERMINAL (LOGOUT) ", ConsoleColor.DarkRed, () => _game.BloquearTerminal());
        }

        // ------------------------------------------------------------------
        //  Tela final
        // ------------------------------------------------------------------
        private void RenderEndScreen()
        {
            int w = Math.Min(100, ScreenWidth - 8);
            int h = 16;

            ConsoleColor cor = _game.JogoVencido ? ConsoleColor.Green : ConsoleColor.Red;
            string titulo = _game.JogoVencido ? "VITÓRIA! IMPÉRIO GASTRONÔMICO CONCLUÍDO" : "FIM DE JOGO · GAME OVER";

            DrawModalFrame(w, h, titulo, cor, out int x, out int y);

            var motivo = Wrap(_game.MotivoFimDeJogo ?? "", w - 10, 3);
            for (int i = 0; i < motivo.Count; i++)
                Write(x + 5, y + 3 + i, motivo[i], ConsoleColor.White);

            Write(x + 5, y + 7, "PONTUAÇÃO FINAL", ConsoleColor.DarkGray);
            Write(x + 22, y + 7, $"{_game.CalcularPontuacaoFinal()} pts", ConsoleColor.Yellow);
            Write(x + 40, y + 7, "RECORDE", ConsoleColor.DarkGray);
            Write(x + 48, y + 7, $"{_game.RecordePontuacao} pts", ConsoleColor.Yellow);

            Write(x + 5, y + 9, $"Fase {_game.NivelAtual}/5 · Rep {_game.Reputacao:F1}* · Perfeitos {_game.PedidosPerfeitosTotal} · Conquistas {_game.ConquistasDesbloqueadas.Count}/{Catalog.Conquistas.Count}", ConsoleColor.Gray);

            string b1 = " [Enter] NOVA PARTIDA ";
            string b2 = " [L] CARREGAR AUTOSAVE ";
            string b3 = " [Esc] SAIR ";
            int bx = x + 5;
            int by = y + h - 3;
            AddButton(bx, by, b1, ConsoleColor.DarkGreen, () => { AbaAtual = ScreenTab.Cozinha; _game.IniciarNovaRun(mostrarTutorial: false); });
            AddButton(bx + b1.Length + 2, by, b2, ConsoleColor.DarkCyan, () => _game.CarregarJogo("autosave"));
            AddButton(bx + b1.Length + b2.Length + 4, by, b3, ConsoleColor.DarkRed, Sair);
        }

        // ------------------------------------------------------------------
        //  Entrada (Mouse, Teclado e Barra de Comando Natural)
        // ------------------------------------------------------------------
        public void HandleMouseMove(int x, int y)
        {
            if (SyncScreenDimensions()) { Render(); return; }
            if (x == HoverX && y == HoverY) return;

            int antes = IndexAt(HoverX, HoverY);
            HoverX = x;
            HoverY = y;
            int depois = IndexAt(x, y);

            if (antes != depois) Render();
        }

        public void HandleResize()
        {
            _forceFullRedraw = true;
            Render();
        }

        public void HandleMouseClick(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _physW || y >= ScreenHeight) return;
            foreach (var btn in BotoesAtivos.ToList())
            {
                if (btn.Contains(x - _ox, y))
                {
                    btn.OnClick?.Invoke();
                    Render();
                    break;
                }
            }
        }

        private void TrocarAba(ScreenTab aba, bool viaUsuario = false)
        {
            if (AbaAtual == aba) return;
            AbaAtual = aba;
            IndiceBotaoFocadoTeclado = -1;
            if (viaUsuario)
            {
                _game.RegistrarFeedbackExterno(
                    LocalityCatalog.ObterNome(aba),
                    $"Ir para {LocalityCatalog.ObterNome(aba)}",
                    $"Você está agora em {LocalityCatalog.ObterNome(aba)}. {LocalityCatalog.ObterPersonagem(aba)}.",
                    FeedbackSeverity.Info,
                    adicionarAoHistorico: false);
            }
            Render();
        }

        private void AbrirModoComando()
        {
            ModoComandoAtivo = true;
            TextoComandoAtual = "";
            _cursorHistoricoComandos = _commands.UltimosComandos.Count;
            IndiceBotaoFocadoTeclado = -1;
        }

        private void FecharModoComando()
        {
            ModoComandoAtivo = false;
            TextoComandoAtual = "";
            _cursorHistoricoComandos = -1;
        }

        private void EnviarComandoDigitado()
        {
            string cmd = TextoComandoAtual;
            FecharModoComando();
            ExecutarComando(cmd);
        }

        public void ExecutarComando(string texto)
        {
            var res = _commands.Executar(texto, AbaAtual);
            if (res.NovaAba.HasValue)
            {
                AbaAtual = res.NovaAba.Value;
                IndiceBotaoFocadoTeclado = -1;
            }
            if (!string.IsNullOrEmpty(res.FiltroHistoricoAlvo))
            {
                FiltroHistorico = res.FiltroHistoricoAlvo;
                IndiceHistoricoSelecionado = 0;
            }
            if (res.AbrirAjuda) AbrirAjuda(res.SecaoAjuda ?? HelpSection.LocalidadeAtual);
            if (res.AbrirOpcoes) AbrirOpcoes();
            if (res.AbrirGerenciadorSaves) AbrirGerenciadorSaves();
            if (res.AbrirConfirmacaoRecomecar) AbrirModalRecomecar();
            if (res.AbrirConfirmacaoSaida) ConfirmandoSaida = true;
            if (res.AlternarModoBancada)
            {
                MostrarTodosIngredientesCozinha = !MostrarTodosIngredientesCozinha;
                PaginaCozinha = 0;
                _game.RegistrarFeedbackExterno("Cozinha", "Alternar modo da bancada",
                    MostrarTodosIngredientesCozinha ? "Modo: TODOS OS INGREDIENTES ativado." : "Modo: BANCADA INTELIGENTE ativado.",
                    FeedbackSeverity.Info);
            }
        }

        private static bool EhTeclaModificadora(ConsoleKey key)
        {
            int k = (int)key;
            return k == 16 || k == 17 || k == 18 || k == 20 || k == 91 || k == 92 || k == 144 || k == 160 || k == 161 || k == 162 || k == 163 || k == 164 || k == 165;
        }

        public void HandleKeyPress(ConsoleKey key, char keyChar = '\0', bool shiftPressed = false)
        {
            if (EhTeclaModificadora(key)) return;

            // 1. Diálogo de decisão antes de encerrar
            if (ConfirmandoSaida)
            {
                if (key == ConsoleKey.D1 || key == ConsoleKey.NumPad1 || key == ConsoleKey.S) { _game.SalvarJogo(); Sair(); }
                else if (key == ConsoleKey.D2 || key == ConsoleKey.NumPad2) { _game.SalvarAutomatico("saida"); Sair(); }
                else if (key == ConsoleKey.D3 || key == ConsoleKey.NumPad3 || key == ConsoleKey.Enter) Sair();
                else if (key == ConsoleKey.Escape || key == ConsoleKey.N || key == ConsoleKey.Backspace)
                {
                    ConfirmandoSaida = false;
                    Render();
                }
                return;
            }

            // 2. Modal de Opções / Acessibilidade
            if (ExibirOpcoes)
            {
                if (key == ConsoleKey.Escape || key == ConsoleKey.Enter || key == ConsoleKey.O) { ExibirOpcoes = false; Render(); return; }
                if (key == ConsoleKey.D1 || key == ConsoleKey.NumPad1) { Cfg.AltoContraste = !Cfg.AltoContraste; _game.SalvarConfiguracoes(); Render(); return; }
                if (key == ConsoleKey.D2 || key == ConsoleKey.NumPad2) { Cfg.ModoSemantico = !Cfg.ModoSemantico; _game.SalvarConfiguracoes(); Render(); return; }
                if (key == ConsoleKey.D3 || key == ConsoleKey.NumPad3) { Cfg.DesativarEfeitosVisuais = !Cfg.DesativarEfeitosVisuais; _game.SalvarConfiguracoes(); Render(); return; }
                if (key == ConsoleKey.D4 || key == ConsoleKey.NumPad4) { Cfg.VelocidadeTexto = (TextSpeed)(((int)Cfg.VelocidadeTexto + 1) % 4); _game.SalvarConfiguracoes(); Render(); return; }
                if (key == ConsoleKey.D5 || key == ConsoleKey.NumPad5) { Cfg.SonsAtivos = !Cfg.SonsAtivos; _game.SalvarConfiguracoes(); Render(); return; }
                return;
            }

            // 3. Modal Gerenciador de Saves
            if (ExibirGerenciadorSaves)
            {
                if (!string.IsNullOrEmpty(_slotAcaoPendenteTipo))
                {
                    if (key == ConsoleKey.S || key == ConsoleKey.Enter)
                    {
                        if (_slotAcaoPendenteTipo == "SALVAR") _game.SalvarJogo(_slotAcaoPendenteId);
                        else _game.ExcluirSlotSave(_slotAcaoPendenteId);
                        _slotAcaoPendenteTipo = null;
                        _slotAcaoPendenteId = null;
                        Render();
                        return;
                    }
                    if (key == ConsoleKey.N || key == ConsoleKey.Escape)
                    {
                        _slotAcaoPendenteTipo = null;
                        _slotAcaoPendenteId = null;
                        Render();
                        return;
                    }
                    return;
                }
                if (key == ConsoleKey.Escape || key == ConsoleKey.G) { ExibirGerenciadorSaves = false; Render(); return; }
                if (key == ConsoleKey.Tab || key == ConsoleKey.RightArrow || key == ConsoleKey.DownArrow)
                {
                    if (BotoesAtivos.Count > 0) IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado + 1) % BotoesAtivos.Count;
                    Render();
                    return;
                }
                if (key == ConsoleKey.LeftArrow || key == ConsoleKey.UpArrow)
                {
                    if (BotoesAtivos.Count > 0) IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado - 1 + BotoesAtivos.Count) % BotoesAtivos.Count;
                    Render();
                    return;
                }
                if ((key == ConsoleKey.Enter || key == ConsoleKey.Spacebar) && IndiceBotaoFocadoTeclado >= 0 && IndiceBotaoFocadoTeclado < BotoesAtivos.Count)
                {
                    BotoesAtivos[IndiceBotaoFocadoTeclado].OnClick?.Invoke();
                    Render();
                    return;
                }
                return;
            }

            // 4. Diálogo de recomeçar / nova partida
            if (ConfirmandoRecomecar)
            {
                if (key == ConsoleKey.D1 || key == ConsoleKey.NumPad1)
                {
                    ConfirmandoRecomecar = false;
                    AbaAtual = ScreenTab.Cozinha;
                    SecaoAjudaAtual = HelpSection.TutorialCurto;
                    _game.IniciarNovaRun(true, _novoModoSelecionado, _novaDificuldadeSelecionada);
                    Render();
                }
                else if (key == ConsoleKey.D2 || key == ConsoleKey.NumPad2 || key == ConsoleKey.Enter)
                {
                    ConfirmandoRecomecar = false;
                    AbaAtual = ScreenTab.Cozinha;
                    _game.IniciarNovaRun(false, _novoModoSelecionado, _novaDificuldadeSelecionada);
                    Render();
                }
                else if (key == ConsoleKey.Escape || key == ConsoleKey.Backspace)
                {
                    ConfirmandoRecomecar = false;
                    Render();
                }
                else if (key == ConsoleKey.Tab)
                {
                    if (BotoesAtivos.Count > 0) IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado + 1) % BotoesAtivos.Count;
                    Render();
                }
                else if (key == ConsoleKey.Spacebar && IndiceBotaoFocadoTeclado >= 0 && IndiceBotaoFocadoTeclado < BotoesAtivos.Count)
                {
                    BotoesAtivos[IndiceBotaoFocadoTeclado].OnClick?.Invoke();
                    Render();
                }
                return;
            }

            bool fimDeJogo = _game.GameOver || _game.JogoVencido;

            // 5. Modo de digitação da Barra de Comando Natural
            if (ModoComandoAtivo)
            {
                if (key == ConsoleKey.Escape) { FecharModoComando(); Render(); return; }
                if (key == ConsoleKey.Enter) { EnviarComandoDigitado(); Render(); return; }
                if (key == ConsoleKey.Backspace)
                {
                    if (TextoComandoAtual.Length > 0) TextoComandoAtual = TextoComandoAtual[..^1];
                    Render();
                    return;
                }
                if (key == ConsoleKey.UpArrow)
                {
                    if (_commands.UltimosComandos.Count > 0)
                    {
                        _cursorHistoricoComandos = Math.Max(0, _cursorHistoricoComandos - 1);
                        TextoComandoAtual = _commands.UltimosComandos[_cursorHistoricoComandos];
                    }
                    Render();
                    return;
                }
                if (key == ConsoleKey.DownArrow)
                {
                    if (_commands.UltimosComandos.Count > 0)
                    {
                        _cursorHistoricoComandos = Math.Min(_commands.UltimosComandos.Count, _cursorHistoricoComandos + 1);
                        TextoComandoAtual = _cursorHistoricoComandos < _commands.UltimosComandos.Count
                            ? _commands.UltimosComandos[_cursorHistoricoComandos]
                            : "";
                    }
                    Render();
                    return;
                }

                if (keyChar != '\0' && !char.IsControl(keyChar))
                {
                    if (TextoComandoAtual.Length < 80) TextoComandoAtual += keyChar;
                    Render();
                    return;
                }
                return;
            }

            if (key == ConsoleKey.Escape)
            {
                if (fimDeJogo) Sair();
                ConfirmandoSaida = true;
                IndiceBotaoFocadoTeclado = -1;
                Render();
                return;
            }

            if (fimDeJogo)
            {
                if (key == ConsoleKey.Enter) { AbaAtual = ScreenTab.Cozinha; _game.IniciarNovaRun(mostrarTutorial: false); Render(); }
                else if (key == ConsoleKey.L) { _game.CarregarJogo("autosave"); Render(); }
                return;
            }

            if (_game.ExibirTutorial)
            {
                if (key == ConsoleKey.D1 || key == ConsoleKey.NumPad1) { SecaoAjudaAtual = HelpSection.LocalidadeAtual; Render(); return; }
                if (key == ConsoleKey.D2 || key == ConsoleKey.NumPad2) { SecaoAjudaAtual = HelpSection.TutorialCurto; Render(); return; }
                if (key == ConsoleKey.D3 || key == ConsoleKey.NumPad3) { SecaoAjudaAtual = HelpSection.ComandosNaturais; Render(); return; }
                if (key == ConsoleKey.Enter || key == ConsoleKey.Spacebar || key == ConsoleKey.H)
                {
                    _game.ExibirTutorial = false;
                    Render();
                }
                return;
            }

            if (_game.ResumoDiarioAtivo != null)
            {
                if (key == ConsoleKey.Enter || key == ConsoleKey.Spacebar)
                {
                    _game.FecharResumoDiario();
                    Render();
                }
                return;
            }

            if (_game.ProcessoAtivo != null)
            {
                if (key == ConsoleKey.D1 || key == ConsoleKey.NumPad1) { _game.FazerAcordoJudicial(); Render(); return; }
                if (key == ConsoleKey.D2 || key == ConsoleKey.NumPad2) { _game.IrParaJulgamento(); Render(); return; }
                if (keyChar == '/' || keyChar == ':') { AbrirModoComando(); Render(); return; }
                if (key == ConsoleKey.Tab || key == ConsoleKey.RightArrow || key == ConsoleKey.DownArrow)
                {
                    if (BotoesAtivos.Count > 0) IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado + 1) % BotoesAtivos.Count;
                    Render();
                    return;
                }
                if (key == ConsoleKey.LeftArrow || key == ConsoleKey.UpArrow)
                {
                    if (BotoesAtivos.Count > 0) IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado - 1 + BotoesAtivos.Count) % BotoesAtivos.Count;
                    Render();
                    return;
                }
                if ((key == ConsoleKey.Enter || key == ConsoleKey.Spacebar) && IndiceBotaoFocadoTeclado >= 0 && IndiceBotaoFocadoTeclado < BotoesAtivos.Count)
                {
                    BotoesAtivos[IndiceBotaoFocadoTeclado].OnClick?.Invoke();
                    Render();
                    return;
                }

                _game.RegistrarFeedbackExterno("Tribunal", $"Tecla [{key}]",
                    "Ação bloqueada durante a intimação judicial!",
                    FeedbackSeverity.Aviso,
                    sugestao: "Pressione [1] para Fazer Acordo ou [2] para Ir a Julgamento.",
                    adicionarAoHistorico: false);
                Render();
                return;
            }

            if (keyChar == '/' || keyChar == ':' || key == ConsoleKey.Divide || key == ConsoleKey.Oem2)
            {
                AbrirModoComando();
                Render();
                return;
            }

            if (AbaAtual == ScreenTab.Terminal && !_game.TerminalDesbloqueado)
            {
                if (key == ConsoleKey.Backspace && _game.InputSenhaTerminal.Length > 0)
                {
                    _game.InputSenhaTerminal = _game.InputSenhaTerminal[..^1];
                    Render();
                    return;
                }
                if (key == ConsoleKey.Enter) { _game.TentarDesbloquearTerminal(); Render(); return; }
                if (key >= ConsoleKey.A && key <= ConsoleKey.Z)
                {
                    _game.InputSenhaTerminal += char.ToLowerInvariant((char)('a' + (key - ConsoleKey.A)));
                    Render();
                    return;
                }
                if ((key >= ConsoleKey.D8 && key <= ConsoleKey.D9) || (key >= ConsoleKey.NumPad8 && key <= ConsoleKey.NumPad9))
                {
                    _game.InputSenhaTerminal += (key == ConsoleKey.D8 || key == ConsoleKey.NumPad8) ? '8' : '9';
                    Render();
                    return;
                }
            }

            if (AbaAtual == ScreenTab.Historico)
            {
                if (key == ConsoleKey.UpArrow) { IndiceHistoricoSelecionado = Math.Max(0, IndiceHistoricoSelecionado - 1); Render(); return; }
                if (key == ConsoleKey.DownArrow) { IndiceHistoricoSelecionado++; Render(); return; }
                if (key == ConsoleKey.T) { FiltroHistorico = "TODOS"; IndiceHistoricoSelecionado = 0; Render(); return; }
                if (key == ConsoleKey.V) { FiltroHistorico = "SUCESSO"; IndiceHistoricoSelecionado = 0; Render(); return; }
                if (key == ConsoleKey.E) { FiltroHistorico = "ERRO"; IndiceHistoricoSelecionado = 0; Render(); return; }
                if (key == ConsoleKey.C) { FiltroHistorico = "COMANDOS"; IndiceHistoricoSelecionado = 0; Render(); return; }
                if (key == ConsoleKey.Q) { FiltroHistorico = "CONQUISTAS"; IndiceHistoricoSelecionado = 0; Render(); return; }
            }

            switch (key)
            {
                case ConsoleKey.D1: case ConsoleKey.NumPad1: TrocarAba(ScreenTab.Cozinha, true); return;
                case ConsoleKey.D2: case ConsoleKey.NumPad2: TrocarAba(ScreenTab.Entregas, true); return;
                case ConsoleKey.D3: case ConsoleKey.NumPad3: TrocarAba(ScreenTab.LivroReceitas, true); return;
                case ConsoleKey.D4: case ConsoleKey.NumPad4: TrocarAba(ScreenTab.EstoqueCatalogo, true); return;
                case ConsoleKey.D5: case ConsoleKey.NumPad5: TrocarAba(ScreenTab.Administracao, true); return;
                case ConsoleKey.D6: case ConsoleKey.NumPad6: TrocarAba(ScreenTab.Banco, true); return;
                case ConsoleKey.D7: case ConsoleKey.NumPad7: TrocarAba(ScreenTab.Historico, true); return;
                case ConsoleKey.D8: case ConsoleKey.NumPad8: TrocarAba(ScreenTab.Terminal, true); return;

                case ConsoleKey.H: AbrirAjuda(HelpSection.LocalidadeAtual); Render(); return;
                case ConsoleKey.O: AbrirOpcoes(); Render(); return;
                case ConsoleKey.G: AbrirGerenciadorSaves(); Render(); return;
                case ConsoleKey.R: AbrirModalRecomecar(); Render(); return;
                case ConsoleKey.Z: _game.DesfazerUltimaAcao(); Render(); return;
                case ConsoleKey.S: _game.SalvarJogo(); Render(); return;
                case ConsoleKey.L: _game.CarregarJogo(); Render(); return;
                case ConsoleKey.F: _game.AvancarDia(); Render(); return;

                case ConsoleKey.PageDown:
                case ConsoleKey.PageUp:
                    int d = key == ConsoleKey.PageDown ? 1 : -1;
                    switch (AbaAtual)
                    {
                        case ScreenTab.Cozinha: PaginaCozinha = Math.Max(0, PaginaCozinha + d); break;
                        case ScreenTab.LivroReceitas: PaginaReceitas = Math.Max(0, PaginaReceitas + d); break;
                        case ScreenTab.EstoqueCatalogo: PaginaEstoque = Math.Max(0, PaginaEstoque + d); break;
                        case ScreenTab.Historico: IndiceHistoricoSelecionado = Math.Max(0, IndiceHistoricoSelecionado + d * 5); break;
                    }
                    Render();
                    return;

                case ConsoleKey.Tab:
                    if (BotoesAtivos.Count > 0)
                    {
                        int step = shiftPressed ? -1 : 1;
                        IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado + step + BotoesAtivos.Count) % BotoesAtivos.Count;
                    }
                    Render();
                    return;

                case ConsoleKey.RightArrow:
                case ConsoleKey.DownArrow:
                    if (BotoesAtivos.Count > 0)
                        IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado + 1) % BotoesAtivos.Count;
                    Render();
                    return;

                case ConsoleKey.LeftArrow:
                case ConsoleKey.UpArrow:
                    if (BotoesAtivos.Count > 0)
                        IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado - 1 + BotoesAtivos.Count) % BotoesAtivos.Count;
                    Render();
                    return;

                case ConsoleKey.Spacebar:
                    if (IndiceBotaoFocadoTeclado >= 0 && IndiceBotaoFocadoTeclado < BotoesAtivos.Count)
                    {
                        BotoesAtivos[IndiceBotaoFocadoTeclado].OnClick?.Invoke();
                        Render();
                    }
                    else
                    {
                        _game.RegistrarFeedbackExterno(LocalityCatalog.ObterNome(AbaAtual), "Tecla [Espaço]",
                            "Nenhum botão está focado no momento.",
                            FeedbackSeverity.Aviso,
                            sugestao: "Use [Tab] ou setas para focar um botão, clique com o mouse ou aperte [/] para digitar um comando.",
                            adicionarAoHistorico: false);
                        Render();
                    }
                    return;

                case ConsoleKey.Enter:
                    if (IndiceBotaoFocadoTeclado >= 0 && IndiceBotaoFocadoTeclado < BotoesAtivos.Count)
                    {
                        BotoesAtivos[IndiceBotaoFocadoTeclado].OnClick?.Invoke();
                        Render();
                    }
                    else if (AbaAtual == ScreenTab.Cozinha) { _game.EntregarPedido(); Render(); }
                    else if (AbaAtual == ScreenTab.Entregas) { _game.FinalizarConferenciaEntrega(); Render(); }
                    else { AbrirModoComando(); Render(); }
                    return;

                case ConsoleKey.E:
                    if (AbaAtual == ScreenTab.Cozinha) { _game.EntregarPedido(); Render(); return; }
                    break;
                case ConsoleKey.X:
                    if (AbaAtual == ScreenTab.Cozinha) { _game.LimparPratoAtual(); Render(); return; }
                    break;
                case ConsoleKey.B:
                    if (AbaAtual == ScreenTab.Cozinha) { _game.ComprarIngredientesFaltantesPedido(); Render(); return; }
                    break;
                case ConsoleKey.M:
                    if (AbaAtual == ScreenTab.Cozinha)
                    {
                        MostrarTodosIngredientesCozinha = !MostrarTodosIngredientesCozinha;
                        PaginaCozinha = 0;
                        _game.RegistrarFeedbackExterno("Cozinha", "Alternar modo da bancada",
                            MostrarTodosIngredientesCozinha ? "Exibindo TODOS os ingredientes da fase." : "Exibindo BANCADA INTELIGENTE do pedido.",
                            FeedbackSeverity.Info, adicionarAoHistorico: false);
                        Render();
                        return;
                    }
                    break;
                case ConsoleKey.P: _game.FalarComPersonagem("pedro"); Render(); return;
                case ConsoleKey.N: _game.FalarComPersonagem("ana"); Render(); return;
                case ConsoleKey.C: _game.FalarComPersonagem("carlos"); Render(); return;
                case ConsoleKey.A:
                    if (AbaAtual == ScreenTab.Entregas) { _game.FinalizarConferenciaEntrega(); Render(); return; }
                    break;
            }

            string atalhosSugeridos = AbaAtual switch
            {
                ScreenTab.Cozinha => "[E] Entregar, [B] Repor Faltantes, [Z] Desfazer, [X] Limpar, [P] Pedro ou [/] Comando.",
                ScreenTab.Entregas => "[A] Concluir Auditoria, [N] Ana, [Z] Desfazer ou [/] Comando.",
                ScreenTab.Administracao => "[C] Carlos, [Z] Desfazer, [O] Opções ou [/] Comando.",
                ScreenTab.Historico => "[↑/↓] Navegar, [T/V/E/C/Q] Filtrar ou [G] Slots.",
                _ => "[1-8] Locais, [/] Comando, [O] Opções, [G] Slots, [H] Ajuda ou [Tab] Foco."
            };
            _game.RegistrarFeedbackExterno(
                LocalityCatalog.ObterNome(AbaAtual),
                $"Tecla [{key}]",
                $"A tecla [{key}] não executa nenhuma ação em {LocalityCatalog.ObterNome(AbaAtual)}.",
                FeedbackSeverity.Aviso,
                sugestao: $"Use {atalhosSugeridos}",
                adicionarAoHistorico: false);
            Render();
        }
    }
}
