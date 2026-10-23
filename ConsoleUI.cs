using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PizzariaTycoon
{
    public enum ScreenTab
    {
        Cozinha = 1,
        Entregas = 2,
        LivroReceitas = 3,
        EstoqueCatalogo = 4,
        Administracao = 5,
        Banco = 6,
        Terminal = 7
    }

    public class ClickableZone
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
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
        public const int MaxLayoutWidth = 150;   // largura lógica máxima; telas maiores ganham margens

        public int ScreenWidth { get; private set; } = 148;
        public int ScreenHeight { get; private set; } = 36;

        private int _physW = 148;   // largura real do console
        private int _ox = 0;        // deslocamento horizontal para centralizar o layout

        private readonly GameEngine _game;
        private readonly ScreenCell[,] _backBuffer = new ScreenCell[MaxBufferWidth, MaxBufferHeight];
        private readonly ScreenCell[,] _frontBuffer = new ScreenCell[MaxBufferWidth, MaxBufferHeight];
        private bool _forceFullRedraw = true;

        public ScreenTab AbaAtual { get; private set; } = ScreenTab.Cozinha;
        public List<ClickableZone> BotoesAtivos { get; private set; } = new List<ClickableZone>();
        public int HoverX { get; private set; } = -1;
        public int HoverY { get; private set; } = -1;
        public int IndiceBotaoFocadoTeclado { get; private set; } = -1;

        public bool MostrarTodosIngredientesCozinha { get; private set; } = false;
        public bool ConfirmandoSaida { get; private set; } = false;
        public int PaginaCozinha { get; private set; } = 0;
        public int PaginaEstoque { get; private set; } = 0;
        public int PaginaReceitas { get; private set; } = 0;

        // ------------------------------------------------------------------
        //  Tipos e dados auxiliares
        // ------------------------------------------------------------------
        private struct Seg
        {
            public string T;
            public ConsoleColor C;
            public Seg(string t, ConsoleColor c) { T = t; C = c; }
        }

        private sealed record DevBtn(string Text, ConsoleColor Bg, Action Act);

        private static readonly Dictionary<string, IngredientInfo> IngPorNome =
            Catalog.TodosIngredientes.GroupBy(i => i.Nome).ToDictionary(g => g.Key, g => g.First());

        private static readonly string[] TabNomes =
            { "COZINHA", "ENTREGAS", "RECEITAS", "ESTOQUE", "GERÊNCIA", "BANCO", "TERMINAL" };

        private int ContentBottom => ScreenHeight - 4;

        public ConsoleUI(GameEngine game)
        {
            _game = game;
            SyncScreenDimensions();
            ClearBackBuffer();
        }

        // ------------------------------------------------------------------
        //  Infra de desenho
        // ------------------------------------------------------------------
        private bool SyncScreenDimensions()
        {
            try
            {
                int pw = Math.Clamp(Math.Min(Console.WindowWidth, Console.BufferWidth), 118, MaxBufferWidth - 1);
                int h = Math.Clamp(Math.Min(Console.WindowHeight, Console.BufferHeight), 30, MaxBufferHeight - 1);
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

            for (int i = 0; i < text.Length; i++)
            {
                int cx = x + i;
                if (cx < 0) continue;
                if (cx >= ScreenWidth) break;
                _backBuffer[cx + _ox, y] = new ScreenCell { Char = text[i], Fg = fg, Bg = bg };
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
            if (w == 1) return "…";
            return s.Substring(0, w - 1) + "…";
        }

        private static string Money(double v) => "R$ " + v.ToString("N2");

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

            char tl = dbl ? '╔' : '┌', tr = dbl ? '╗' : '┐';
            char bl = dbl ? '╚' : '└', br = dbl ? '╝' : '┘';
            char hz = dbl ? '═' : '─', vt = dbl ? '║' : '│';

            string blank = new string(' ', w);
            for (int r = 0; r < h; r++) Write(x, y + r, blank);

            Write(x, y, tl + new string(hz, w - 2) + tr, border);
            Write(x, y + h - 1, bl + new string(hz, w - 2) + br, border);
            for (int r = 1; r < h - 1; r++)
            {
                Write(x, y + r, vt.ToString(), border);
                Write(x + w - 1, y + r, vt.ToString(), border);
            }

            if (!string.IsNullOrEmpty(title))
                Write(x + 2, y, " " + title + " ", Brighten(border));
        }

        private void DrawBar(int x, int y, int w, double ratio, ConsoleColor filled, ConsoleColor empty = ConsoleColor.DarkGray)
        {
            if (double.IsNaN(ratio)) ratio = 0;
            ratio = Math.Clamp(ratio, 0, 1);
            int f = (int)Math.Round(ratio * w);
            if (f > 0) Write(x, y, new string('█', f), filled);
            if (w - f > 0) Write(x + f, y, new string('░', w - f), empty);
        }

        private void AddButton(int x, int y, string label, ConsoleColor bg, Action action)
        {
            if (y < 0 || y >= ScreenHeight || x < 0 || x >= ScreenWidth) return;

            int btnIndex = BotoesAtivos.Count;
            int hx = HoverX - _ox;
            bool isMouseHover = hx >= x && hx < (x + label.Length) && HoverY == y;
            bool isKeyFocus = IndiceBotaoFocadoTeclado == btnIndex;

            ConsoleColor finalBg = bg;
            if (isMouseHover) finalBg = Brighten(bg);
            if (isKeyFocus) finalBg = ConsoleColor.Yellow;

            ConsoleColor finalFg = IsLight(finalBg) ? ConsoleColor.Black : ConsoleColor.White;

            Write(x, y, label, finalFg, finalBg);

            BotoesAtivos.Add(new ClickableZone
            {
                X = x,
                Y = y,
                Width = label.Length,
                Height = 1,
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
            int maxW = Math.Min(_physW, Console.BufferWidth);
            int maxH = Math.Min(ScreenHeight, Console.BufferHeight);

            ConsoleColor curFg = (ConsoleColor)(-1);
            ConsoleColor curBg = (ConsoleColor)(-1);

            for (int y = 0; y < maxH; y++)
            {
                for (int x = 0; x < maxW; x++)
                {
                    // Evita escrever na última célula (provoca rolagem do console)
                    if (y == maxH - 1 && x == maxW - 1) continue;

                    var next = _backBuffer[x, y];
                    var prev = _frontBuffer[x, y];

                    if (!_forceFullRedraw && next.Char == prev.Char && next.Fg == prev.Fg && next.Bg == prev.Bg)
                        continue;

                    try
                    {
                        // Posiciona o cursor a cada célula: robusto contra
                        // diferenças de largura de caracteres entre terminais.
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
        }

        // ------------------------------------------------------------------
        //  Render principal
        // ------------------------------------------------------------------
        public void Render()
        {
            SyncScreenDimensions();
            BotoesAtivos.Clear();
            ClearBackBuffer();

            if (_game.GameOver || _game.JogoVencido)
            {
                RenderEndScreen();
            }
            else
            {
                bool modalAtivo = _game.ExibirTutorial || _game.ProcessoAtivo != null || _game.ResumoDiarioAtivo != null;
                RenderHeader(!modalAtivo && !ConfirmandoSaida);

                if (_game.ExibirTutorial)
                {
                    RenderModalTutorial();
                }
                else if (_game.ProcessoAtivo != null)
                {
                    RenderModalTribunal();
                }
                else if (_game.ResumoDiarioAtivo != null)
                {
                    RenderModalResumoDiario();
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
                        case ScreenTab.Terminal: RenderTerminalDev(); break;
                    }
                }

                RenderFooter();
            }

            if (ConfirmandoSaida) RenderModalSaida();

            if (IndiceBotaoFocadoTeclado >= BotoesAtivos.Count)
                IndiceBotaoFocadoTeclado = BotoesAtivos.Count - 1;

            FlushBufferToConsole();
        }

        // ------------------------------------------------------------------
        //  Cabeçalho, abas e rodapé
        // ------------------------------------------------------------------
        private int StatChip(int x, int y, string label, string value, ConsoleColor valueColor)
        {
            Write(x, y, label + " ", ConsoleColor.DarkGray);
            x += label.Length + 1;
            Write(x, y, value, valueColor);
            x += value.Length;
            Write(x, y, " │ ", ConsoleColor.DarkGray);
            return x + 3;
        }

        private void RenderHeader(bool interativo)
        {
            int W = ScreenWidth;

            // Linha 0 - barra de título
            Write(0, 0, new string(' ', W), ConsoleColor.White, ConsoleColor.DarkBlue);
            Write(2, 0, "♦ PIZZARIA TYCOON", ConsoleColor.Yellow, ConsoleColor.DarkBlue);
            Write(21, 0, $"Nv.{_game.NivelAtual} · {_game.ConfigAtual.Titulo}", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            string data = $"{_game.DataHojeFormatada}  ·  Dia {_game.DiaSemana}/4";
            Write(W - data.Length - 2, 0, data, ConsoleColor.White, ConsoleColor.DarkBlue);

            // Linha 1 - indicadores
            int x = 2;
            x = StatChip(x, 1, "CAIXA", Money(_game.Dinheiro), _game.Dinheiro >= 0 ? ConsoleColor.Green : ConsoleColor.Red);

            Write(x, 1, "REP ", ConsoleColor.DarkGray);
            x += 4;
            ConsoleColor corRep = _game.Reputacao >= 4 ? ConsoleColor.Green : (_game.Reputacao >= 2.5 ? ConsoleColor.Yellow : ConsoleColor.Red);
            DrawBar(x, 1, 5, _game.Reputacao / 5.0, corRep);
            x += 6;
            Write(x, 1, $"{_game.Reputacao:F1}", corRep);
            x += $"{_game.Reputacao:F1}".Length;
            Write(x, 1, " │ ", ConsoleColor.DarkGray);
            x += 3;

            Write(x, 1, "AÇÕES ", ConsoleColor.DarkGray);
            x += 6;
            int maxAc = Math.Max(1, _game.AcoesMaximasDia);
            int resta = Math.Clamp(_game.AcoesRestantesDia, 0, maxAc);
            Write(x, 1, new string('●', resta), resta <= 1 ? ConsoleColor.Red : ConsoleColor.Yellow);
            Write(x + resta, 1, new string('○', maxAc - resta), ConsoleColor.DarkGray);
            x += maxAc;
            Write(x, 1, " │ ", ConsoleColor.DarkGray);
            x += 3;

            x = StatChip(x, 1, "FUNC", _game.Funcionarios.ToString(), ConsoleColor.White);
            int ideal = _game.ObterLimpezaIdeal();
            x = StatChip(x, 1, "LIMP", $"{_game.Limpeza}/{ideal}", _game.Limpeza >= ideal ? ConsoleColor.Green : ConsoleColor.Yellow);
            x = StatChip(x, 1, "UPG", _game.TotalUpgrades.ToString(), ConsoleColor.White);
            x = StatChip(x, 1, "FOLHA/MÊS", Money(_game.CalcularDespesaMensalFixa()), ConsoleColor.Gray);
            if (_game.DividaTotalBanco > 0)
                x = StatChip(x, 1, "DÍVIDA", Money(_game.DividaTotalBanco), ConsoleColor.Red);
            if (x + 8 < W)
                Write(x, 1, $"DIVERSÃO {_game.Diversao}", ConsoleColor.Magenta);

            // Linha 2 - botões à direita, evento à esquerda
            var botoes = new List<(string label, ConsoleColor bg, Action act)>
            {
                (" ? AJUDA ", ConsoleColor.DarkCyan, () => _game.ExibirTutorial = true),
                (" SALVAR ", ConsoleColor.DarkGreen, () => _game.SalvarJogo()),
                (" CARREGAR ", ConsoleColor.DarkBlue, () => _game.CarregarJogo()),
                (" ENCERRAR DIA ►► ", ConsoleColor.DarkYellow, () => _game.AvancarDia())
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

            Write(2, 2, "EVENTO ► ", ConsoleColor.DarkCyan);
            Write(11, 2, Fit(_game.EventoDoDiaDescricao, Math.Max(10, primeiroBotaoX - 13)), ConsoleColor.Cyan);

            // Linha 3 - meta atual
            Write(0, 3, new string('─', W), ConsoleColor.DarkGray);
            Write(2, 3, " META ► " + Fit(_game.ObterTextoProximaMeta(), W - 16) + " ", ConsoleColor.Cyan);
        }

        private void RenderNavigationTabs()
        {
            int x = 1;
            int y = 4;
            int ativoX = 0, ativoW = 0;

            for (int i = 0; i < TabNomes.Length; i++)
            {
                var tab = (ScreenTab)(i + 1);
                bool ativo = AbaAtual == tab;
                string label = $" {i + 1} {TabNomes[i]} ";
                ConsoleColor bg = ativo
                    ? (tab == ScreenTab.Terminal ? ConsoleColor.Magenta : ConsoleColor.Cyan)
                    : ConsoleColor.DarkGray;

                var alvo = tab;
                AddButton(x, y, label, bg, () =>
                {
                    AbaAtual = alvo;
                    IndiceBotaoFocadoTeclado = -1;
                });

                if (ativo) { ativoX = x; ativoW = label.Length; }
                x += label.Length + 1;
            }

            Write(0, 5, new string('─', ScreenWidth), ConsoleColor.DarkGray);
            if (ativoW > 0)
                Write(ativoX, 5, new string('═', ativoW), AbaAtual == ScreenTab.Terminal ? ConsoleColor.Magenta : ConsoleColor.Cyan);
        }

        private static (ConsoleColor bg, string icon) ToneOf(string msg)
        {
            string m = msg ?? "";
            string[] ruins =
            {
                "insuficiente", "negado", "FALTA", "ERRADO", "ESTRAGADO", "Erro", "incorreta", "DERROTA",
                "ATRASO", "RECLAMOU", "Sem inspe", "Nenhum", "Mantenha", "desistiu", "erro(s)", "demissão",
                "embora", "Você não"
            };
            string[] bons =
            {
                "PERFEITO", "IMPECÁVEL", "sucesso", "VITÓRIA", "SUBIU", "quitada", "contratado",
                "Nível(is)", "DESBLOQUEADO", "em dia", "INICIADO", "melhorado", "recebido", "ativado"
            };

            foreach (var r in ruins)
                if (m.Contains(r, StringComparison.Ordinal)) return (ConsoleColor.DarkRed, "▲");
            foreach (var b in bons)
                if (m.Contains(b, StringComparison.Ordinal)) return (ConsoleColor.DarkGreen, "●");
            return (ConsoleColor.DarkBlue, "►");
        }

        private void RenderFooter()
        {
            int W = ScreenWidth;
            int fy = ScreenHeight - 3;

            var (bg, icon) = ToneOf(_game.UltimaMensagem);
            Write(0, fy, new string(' ', W), ConsoleColor.White, bg);
            Write(0, fy + 1, new string(' ', W), ConsoleColor.White, bg);

            var linhas = Wrap(_game.UltimaMensagem, W - 8, 2);
            for (int i = 0; i < linhas.Count; i++)
                Write(4, fy + i, linhas[i], ConsoleColor.White, bg);
            Write(2, fy, icon, ConsoleColor.Yellow, bg);

            // Atalhos
            var dicas = new List<(string k, string d)>();
            dicas.Add(("1-7", "Abas"));
            dicas.Add(("Tab/←↑↓→", "Foco"));
            dicas.Add(("Espaço", "Ativar"));
            if (AbaAtual == ScreenTab.Cozinha)
            {
                dicas.Add(("Enter", "Entregar"));
                dicas.Add(("Z", "Desfazer"));
            }
            dicas.Add(("PgUp/PgDn", "Páginas"));
            dicas.Add(("H", "Ajuda"));
            dicas.Add(("S", "Salvar"));
            dicas.Add(("L", "Carregar"));
            dicas.Add(("Esc", "Sair"));

            int x = 2;
            int y = ScreenHeight - 1;
            foreach (var d in dicas)
            {
                int need = d.k.Length + 1 + d.d.Length + 3;
                if (x + need >= W - 1) break;
                Write(x, y, d.k, ConsoleColor.Yellow);
                Write(x + d.k.Length + 1, y, d.d, ConsoleColor.DarkGray);
                x += need;
            }
        }

        // ------------------------------------------------------------------
        //  Modais
        // ------------------------------------------------------------------
        private void DrawModalFrame(int w, int h, string title, ConsoleColor cor, out int x, out int y)
        {
            x = (ScreenWidth - w) / 2;
            y = Math.Max(6, 6 + (ContentBottom - 6 + 1 - h) / 2);
            DrawPanel(x, y, w, h, "", cor, dbl: true);
            if (!string.IsNullOrEmpty(title))
            {
                string t = " " + title + " ";
                Write(x + (w - t.Length) / 2, y, t, Brighten(cor));
            }
        }

        private void RenderModalTutorial()
        {
            int w = Math.Min(112, ScreenWidth - 6);
            int inner = w - 10;

            var L = new List<(string t, ConsoleColor c)>();
            void Sec(string t, ConsoleColor c)
            {
                if (L.Count > 0) L.Add(("", ConsoleColor.Gray));
                L.Add((t, c));
            }
            void Item(string t, ConsoleColor c)
            {
                var ws = Wrap(t, inner - 2);
                for (int i = 0; i < ws.Count; i++) L.Add(((i == 0 ? "• " : "  ") + ws[i], c));
            }

            Sec("1 · MONTE O PRATO NA COZINHA  (aba 1)", ConsoleColor.Green);
            Item("Cada prato tem uma ORDEM PADRÃO: veja o Guia do pedido ou a aba 3. Clique nos ingredientes seguindo essa ordem.", ConsoleColor.White);
            Item("Respeite as exceções: se o cliente pedir 'SEM Cebola', pule a Cebola e mantenha a ordem do resto. Algumas ordens especiais trocam dois ingredientes de lugar.", ConsoleColor.Gray);

            Sec("2 · REPONHA O ESTOQUE AUDITANDO ENTREGAS  (aba 2)", ConsoleColor.Cyan);
            Item("Cozinhar gasta ingredientes. Aceite as caixas válidas para abastecer a câmara fria.", ConsoleColor.White);
            Item("Recuse caixas com cidade errada, fornecedor desconhecido, data divergente ou erro de digitação.", ConsoleColor.Gray);
            Item("As inspeções são limitadas: abra só as caixas suspeitas e confira Item Físico, Lote (LT-XXX) e Temperatura (frios ≤ 5°C).", ConsoleColor.Gray);

            Sec("3 · CRESÇA COM CALMA  (aba 5 · Gerência)", ConsoleColor.Magenta);
            Item("Não contrate funcionários demais no Nível 1: a folha salarial pesa. Acompanhe as barras de meta do nível.", ConsoleColor.White);
            Item("Acabaram as ações? Use ENCERRAR DIA. A folha do mês é cobrada ao fim do Dia 4.", ConsoleColor.White);

            Sec("DICAS", ConsoleColor.Yellow);
            Item("Ctrl + roda do mouse aumenta ou diminui o zoom do terminal.", ConsoleColor.Gray);
            Item("Tab e setas movem o foco entre botões; Espaço ou Enter ativam. PgUp/PgDn trocam de página.", ConsoleColor.Gray);
            Item("A aba 7 (TERMINAL) é um modo de desenvolvedor protegido por senha.", ConsoleColor.Gray);

            int h = L.Count + 7;
            DrawModalFrame(w, h, "GUIA RÁPIDO DO PROPRIETÁRIO", ConsoleColor.Cyan, out int x, out int y);

            int cy = y + 2;
            foreach (var (t, c) in L)
            {
                if (t.Length > 0 && !t.StartsWith("•") && !t.StartsWith("  "))
                    Write(x + 4, cy, t, c);
                else
                    Write(x + 6, cy, t, c);
                cy++;
            }

            string btn = " ENTENDI! COMEÇAR A JOGAR (Enter) ";
            AddButton(x + (w - btn.Length) / 2, y + h - 3, btn, ConsoleColor.DarkGreen, () => _game.ExibirTutorial = false);
        }

        private void RenderModalResumoDiario()
        {
            var r = _game.ResumoDiarioAtivo;
            int w = Math.Min(104, ScreenWidth - 6);
            int inner = w - 8;

            var mes = string.IsNullOrEmpty(r.ResumoViradaMes) ? null : Wrap(r.ResumoViradaMes, inner - 2, 3);
            var risco = Wrap(r.MaiorRiscoAtual ?? "", inner - 3, 3);
            var meta = Wrap(r.ProximaMeta ?? "", inner - 3, 3);

            int blocks = (mes != null ? mes.Count + 2 : 0) + (risco.Count + 2) + (meta.Count + 2);
            int h = 10 + blocks;

            DrawModalFrame(w, h, $"FECHAMENTO DO EXPEDIENTE · DIA {r.DiaConcluido}/4 · MÊS {r.MesRef}", ConsoleColor.Yellow, out int x, out int y);

            double saldoDia = Math.Round(r.ReceitaDia - r.DespesaDia, 2);
            string sinalSaldo = saldoDia >= 0 ? "+" : "-";
            int colW = (w - 6) / 3;

            Write(x + 3, y + 3, "GANHOS", ConsoleColor.DarkGray);
            Write(x + 3, y + 4, "+" + Money(r.ReceitaDia), ConsoleColor.Green);
            Write(x + 3 + colW, y + 3, "GASTOS / MULTAS", ConsoleColor.DarkGray);
            Write(x + 3 + colW, y + 4, "-" + Money(r.DespesaDia), ConsoleColor.Red);
            Write(x + 3 + colW * 2, y + 3, "BALANÇO DO DIA", ConsoleColor.DarkGray);
            Write(x + 3 + colW * 2, y + 4, sinalSaldo + Money(Math.Abs(saldoDia)), saldoDia >= 0 ? ConsoleColor.Green : ConsoleColor.Red);

            string sinalRep = r.VariacaoReputacao >= 0 ? "▲ +" : "▼ ";
            Write(x + 3, y + 6, "REPUTAÇÃO", ConsoleColor.DarkGray);
            Write(x + 13, y + 6, $"{sinalRep}{r.VariacaoReputacao:F2}", r.VariacaoReputacao >= 0 ? ConsoleColor.Cyan : ConsoleColor.Red);
            DrawBar(x + 25, y + 6, 12, _game.Reputacao / 5.0, ConsoleColor.Cyan);
            Write(x + 38, y + 6, $"{_game.Reputacao:F1} / 5", ConsoleColor.Gray);

            Write(x + 3, y + 7, "FOLHA PREVISTA", ConsoleColor.DarkGray);
            Write(x + 18, y + 7, "-" + Money(_game.CalcularDespesaMensalFixa()) + "  (cobrada ao fim do Dia 4)", ConsoleColor.Gray);

            int cy = y + 9;
            if (mes != null)
            {
                Write(x + 3, cy++, "FECHAMENTO DO MÊS", ConsoleColor.Magenta);
                foreach (var l in mes) Write(x + 5, cy++, l, ConsoleColor.White);
                cy++;
            }

            Write(x + 3, cy++, "MAIOR RISCO ATUAL", ConsoleColor.Red);
            foreach (var l in risco) Write(x + 5, cy++, "→ " + l, ConsoleColor.White);
            cy++;

            Write(x + 3, cy++, "PRÓXIMA META", ConsoleColor.Green);
            foreach (var l in meta) Write(x + 5, cy++, "→ " + l, ConsoleColor.White);

            string btn = " INICIAR PRÓXIMO DIA (Enter) ";
            AddButton(x + (w - btn.Length) / 2, y + h - 2, btn, ConsoleColor.DarkGreen, () => _game.FecharResumoDiario());
        }

        private void RenderModalTribunal()
        {
            var proc = _game.ProcessoAtivo;
            int w = Math.Min(110, ScreenWidth - 6);
            int h = 18;

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
            DrawBar(x + 22, y + 8, 24, proc.ChanceDeGanhar / 100.0, corChance);
            Write(x + 47, y + 8, $"{proc.ChanceDeGanhar}%", corChance);
            Write(x + 53, y + 8, "(acima de 55% compensa ir a julgamento)", ConsoleColor.DarkGray);

            Write(x + 3, y + 10, "1 · ACORDO EXTRAJUDICIAL", ConsoleColor.Yellow);
            Write(x + 5, y + 11, $"Paga {Money(proc.ValorAcordo)} (55% da causa) e encerra o processo.", ConsoleColor.Gray);
            Write(x + 3, y + 12, "2 · IR A JULGAMENTO", ConsoleColor.Green);
            Write(x + 5, y + 13, $"Advogado custa 10% ({Money(proc.CustoAdvogado)}). Se vencer, não paga condenação.", ConsoleColor.Gray);

            string b1 = $" FAZER ACORDO ({Money(proc.ValorAcordo)}) ";
            string b2 = $" IR A JULGAMENTO ({proc.ChanceDeGanhar}%) ";
            int bx = x + (w - (b1.Length + 3 + b2.Length)) / 2;
            AddButton(bx, y + h - 3, b1, ConsoleColor.DarkYellow, () => _game.FazerAcordoJudicial());
            AddButton(bx + b1.Length + 3, y + h - 3, b2, ConsoleColor.DarkGreen, () => _game.IrParaJulgamento());
        }

        private void RenderModalSaida()
        {
            // Escurece o fundo e deixa apenas o diálogo interativo
            for (int yy = 0; yy < ScreenHeight; yy++)
                for (int xx = 0; xx < _physW; xx++)
                {
                    var c = _backBuffer[xx, yy];
                    c.Fg = ConsoleColor.DarkGray;
                    if (c.Bg != ConsoleColor.Black) c.Bg = ConsoleColor.Black;
                    _backBuffer[xx, yy] = c;
                }
            BotoesAtivos.Clear();

            int w = 58, h = 9;
            DrawModalFrame(w, h, "SAIR DO JOGO?", ConsoleColor.Yellow, out int x, out int y);

            Write(x + (w - 36) / 2, y + 3, "O progresso não salvo será perdido.", ConsoleColor.White);

            string b1 = " SALVAR E SAIR (S) ";
            string b2 = " SAIR (Enter) ";
            string b3 = " VOLTAR (Esc) ";
            int total = b1.Length + b2.Length + b3.Length + 4;
            int bx = x + (w - total) / 2;
            int by = y + h - 3;

            AddButton(bx, by, b1, ConsoleColor.DarkGreen, () => { _game.SalvarJogo(); Sair(); });
            AddButton(bx + b1.Length + 2, by, b2, ConsoleColor.DarkRed, Sair);
            AddButton(bx + b1.Length + b2.Length + 4, by, b3, ConsoleColor.DarkGray, () => ConfirmandoSaida = false);
        }

        private static void Sair()
        {
            Console.ResetColor();
            Console.Clear();
            Console.CursorVisible = true;
            Environment.Exit(0);
        }

        // ------------------------------------------------------------------
        //  ABA 1 · COZINHA
        // ------------------------------------------------------------------
        private void RenderCozinha()
        {
            var pedido = _game.PedidoAtual;
            int W = ScreenWidth;
            int inner = W - 8;

            // ---- Pedido do cliente ----
            DrawPanel(1, 6, W - 2, 7, "PEDIDO DO CLIENTE", ConsoleColor.Yellow);

            int tx = W - 3;
            if (pedido.EClienteCritico)
            {
                string t = " CLIENTE CRÍTICO ";
                tx -= t.Length;
                Write(tx, 6, t, ConsoleColor.White, ConsoleColor.DarkRed);
                tx -= 1;
            }
            if (pedido.EPedidoSazonalBonus)
            {
                string t = " BÔNUS SAZONAL +35% ";
                tx -= t.Length;
                Write(tx, 6, t, ConsoleColor.Black, ConsoleColor.Green);
            }

            var fala = Wrap(pedido.FalaDoCliente, inner, 2);
            for (int i = 0; i < fala.Count; i++)
                Write(3, 7 + i, fala[i], ConsoleColor.White);

            Write(3, 9, "Valor", ConsoleColor.DarkGray);
            Write(9, 9, Money(_game.CalcularValorVendaPrato()), ConsoleColor.Green);
            Write(9 + Money(_game.CalcularValorVendaPrato()).Length + 1, 9, "· gasta 1 ação", ConsoleColor.DarkGray);
            if (pedido.IngredientesExcluidos.Count > 0)
            {
                int sx = 9 + Money(_game.CalcularValorVendaPrato()).Length + 18;
                Write(sx, 9, "SEM: " + string.Join(", ", pedido.IngredientesExcluidos), ConsoleColor.Red);
            }

            var guia = new List<Seg>();
            void AddGuia(string nome)
            {
                bool sem = pedido.IngredientesExcluidos.Contains(nome);
                guia.Add(new Seg(sem ? nome + " (sem)" : nome, sem ? ConsoleColor.DarkGray : IngFg(nome)));
            }
            AddGuia(pedido.Receita.IngredienteBaseTopo);
            foreach (var ing in pedido.Receita.IngredientesPadrao) AddGuia(ing);
            if (!string.IsNullOrEmpty(pedido.Receita.IngredienteBaseFundo)) AddGuia(pedido.Receita.IngredienteBaseFundo);

            string rotuloGuia = "GUIA ";
            Write(3, 10, rotuloGuia, ConsoleColor.DarkCyan);
            DrawFlow(3 + rotuloGuia.Length, 10, Flow(guia, inner - rotuloGuia.Length, " → ", ConsoleColor.DarkGray), 2);

            // ---- Bancada ----
            DrawPanel(1, 13, W - 2, 5, $"NA BANCADA · {pedido.PratoMontado.Count} ingrediente(s)", ConsoleColor.Green);
            if (pedido.PratoMontado.Count == 0)
            {
                Write(3, 14, "Clique nos ingredientes abaixo seguindo a ordem do cliente.", ConsoleColor.DarkGray);
            }
            else
            {
                var plate = new List<Seg>();
                for (int i = 0; i < pedido.PratoMontado.Count; i++)
                    plate.Add(new Seg($"{i + 1}.{pedido.PratoMontado[i]}", IngFg(pedido.PratoMontado[i])));
                DrawFlow(3, 14, Flow(plate, inner, " → ", ConsoleColor.DarkGray), 2);
            }

            int bx = 3;
            string b1 = " ► ENTREGAR PRATO [Enter] ";
            AddButton(bx, 16, b1, ConsoleColor.DarkGreen, () => _game.EntregarPedido());
            bx += b1.Length + 2;
            string b2 = " ◄ DESFAZER [Z] ";
            AddButton(bx, 16, b2, ConsoleColor.DarkYellow, () => _game.DesfazerUltimoIngrediente());
            bx += b2.Length + 2;
            string b3 = " LIMPAR TUDO ";
            AddButton(bx, 16, b3, ConsoleColor.DarkRed, () => _game.LimparPratoAtual());

            string modo = MostrarTodosIngredientesCozinha ? " MODO: TODOS OS INGREDIENTES " : " MODO: BANCADA INTELIGENTE ";
            AddButton(W - 3 - modo.Length, 16, modo, ConsoleColor.DarkMagenta, () =>
            {
                MostrarTodosIngredientesCozinha = !MostrarTodosIngredientesCozinha;
                PaginaCozinha = 0;
            });

            // ---- Ingredientes ----
            int py = 18;
            int ph = ContentBottom - py;
            List<string> lista = MostrarTodosIngredientesCozinha
                ? Catalog.TodosIngredientes.Where(i => i.Nivel <= _game.NivelAtual).Select(i => i.Nome).ToList()
                : pedido.OpcoesBancada;

            int rows = Math.Max(1, ph - 2);
            int nameW = lista.Count == 0 ? 8 : Math.Min(22, Math.Max(8, lista.Max(n => n.Length)));
            int btnW = nameW + 10;
            int cols = Math.Max(1, (W - 4 + 1) / (btnW + 1));
            int perPage = cols * rows;
            int totalPag = Math.Max(1, (int)Math.Ceiling(lista.Count / (double)perPage));
            PaginaCozinha = Math.Clamp(PaginaCozinha, 0, totalPag - 1);

            DrawPanel(1, py, W - 2, ph, $"INGREDIENTES · página {PaginaCozinha + 1}/{totalPag}", ConsoleColor.Cyan);
            if (totalPag > 1)
            {
                string nx = " PRÓX ► ";
                string pv = " ◄ ANT ";
                AddButton(W - 3 - nx.Length, py, nx, ConsoleColor.DarkGray, () => PaginaCozinha = Math.Min(totalPag - 1, PaginaCozinha + 1));
                AddButton(W - 3 - nx.Length - 1 - pv.Length, py, pv, ConsoleColor.DarkGray, () => PaginaCozinha = Math.Max(0, PaginaCozinha - 1));
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

                string qtxt;
                if (qtd > 0) qtxt = $"x{qtd}" + (qtd <= 2 ? "!" : "") + (contam > 0 ? "▲" : "");
                else qtxt = "+COMPRA";

                string label = " " + Fit(nome, nameW).PadRight(nameW) + " " + qtxt.PadRight(7) + " ";
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

            // ---- Legenda ----
            int lx = 3;
            int ly = ContentBottom;
            Write(lx, ly, "CATEGORIAS", ConsoleColor.DarkGray);
            lx += 12;
            var cats = new (string nome, IngredientCategory cat)[]
            {
                ("Base", IngredientCategory.Base), ("Proteína", IngredientCategory.Proteina),
                ("Laticínio", IngredientCategory.Laticinio), ("Vegetal", IngredientCategory.Vegetal),
                ("Molho/Tempero", IngredientCategory.MolhoTempero), ("Fruta/Doce", IngredientCategory.DoceFruta)
            };
            foreach (var (nome, cat) in cats)
            {
                Write(lx, ly, "■ ", Brighten(Catalog.ObterCorCategoria(cat)));
                Write(lx + 2, ly, nome, ConsoleColor.Gray);
                lx += nome.Length + 5;
            }
            Write(lx + 2, ly, "▲ contaminado   ! estoque baixo", ConsoleColor.DarkGray);
        }

        // ------------------------------------------------------------------
        //  ABA 2 · ENTREGAS
        // ------------------------------------------------------------------
        private void RenderEntregas()
        {
            int W = ScreenWidth;
            int n = _game.EntregaAtual.Count;
            int yBtn = 10 + n * 2;
            int h = Math.Min(yBtn - 4, ContentBottom - 5);

            DrawPanel(1, 6, W - 2, h, "AUDITORIA DE ENTREGA", ConsoleColor.Yellow);

            Write(3, 7, "Hoje", ConsoleColor.DarkGray);
            Write(8, 7, _game.DataHojeFormatada, ConsoleColor.White);
            Write(20, 7, "Cidades válidas", ConsoleColor.DarkGray);
            Write(36, 7, "São Paulo, Curitiba, Rio de Janeiro", ConsoleColor.White);
            Write(74, 7, "Inspeções", ConsoleColor.DarkGray);
            int max = _game.InspecoesCaixaMaximas;
            int rest = Math.Clamp(_game.InspecoesCaixaRestantes, 0, max);
            Write(84, 7, new string('●', rest), rest == 0 ? ConsoleColor.Red : ConsoleColor.Cyan);
            Write(84 + rest, 7, new string('○', max - rest), ConsoleColor.DarkGray);

            Write(3, 8, "Abra caixas suspeitas para ver Item Físico, Lote e Temperatura (frios ≤ 5°C). Aceitar abastece a cozinha; aceitar lixo contamina!", ConsoleColor.DarkGray);

            int extra = Math.Max(0, W - 4 - 110);
            int nw = 22 + Math.Min(extra / 2, 4);
            int fw = 20 + Math.Min(extra - extra / 2, 4);

            int cIdx = 3;
            int cNota = cIdx + 2 + 3;
            int cQtd = cNota + nw + 3;
            int cForn = cQtd + 3 + 3;
            int cCid = cForn + fw + 3;
            int cData = cCid + 14 + 3;
            int cCaixa = cData + 10 + 3;
            int cDec = cCaixa + 11 + 1;

            // Cabeçalho da tabela
            Write(2, 9, new string(' ', W - 4), ConsoleColor.White, ConsoleColor.DarkBlue);
            Write(cIdx, 9, "#", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cNota, 9, "NOTA FISCAL", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cQtd, 9, "QTD", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cForn, 9, "FORNECEDOR", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cCid, 9, "CIDADE", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cData, 9, "DATA", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cCaixa, 9, "CAIXA", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            Write(cDec, 9, "DECISÃO", ConsoleColor.Cyan, ConsoleColor.DarkBlue);

            for (int i = 0; i < n; i++)
            {
                var item = _game.EntregaAtual[i];
                int y = 10 + i * 2;
                int idx = i;

                foreach (int sx in new[] { cNota - 2, cQtd - 2, cForn - 2, cCid - 2, cData - 2, cCaixa - 2 })
                    Write(sx, y, "│", ConsoleColor.DarkGray);

                Write(cIdx, y, (i + 1).ToString(), ConsoleColor.Yellow);
                Write(cNota, y, Fit(item.NomeNota, nw), ConsoleColor.White);
                Write(cQtd, y, $"+{item.Quantidade}", ConsoleColor.Green);
                Write(cForn, y, Fit(item.Fornecedor, fw), ConsoleColor.White);
                Write(cCid, y, Fit(item.Cidade, 14), ConsoleColor.White);
                Write(cData, y, Fit(item.DataNota, 10), ConsoleColor.White);

                if (!item.CaixaAberta)
                    AddButton(cCaixa, y, $" ABRIR ({_game.InspecoesCaixaRestantes}) ", ConsoleColor.DarkCyan, () => _game.InspecionarCaixaEntrega(idx));
                else
                    Write(cCaixa, y, " aberta   ", ConsoleColor.Cyan);

                if (item.MarcadoComoValidoPeloJogador)
                    AddButton(cDec, y, " ACEITAR ", ConsoleColor.DarkGreen, () => _game.AlternarAprovacaoItemEntrega(idx));
                else
                    AddButton(cDec, y, " RECUSAR ", ConsoleColor.DarkRed, () => _game.AlternarAprovacaoItemEntrega(idx));

                // Linha de detalhe da caixa
                Write(cNota, y + 1, "└─", ConsoleColor.DarkGray);
                if (item.CaixaAberta)
                {
                    int dx = cNota + 3;
                    Write(dx, y + 1, "Físico:", ConsoleColor.DarkGray);
                    Write(dx + 8, y + 1, item.NomeFisico, ConsoleColor.Cyan);
                    dx += 8 + item.NomeFisico.Length + 3;
                    Write(dx, y + 1, "Lote:", ConsoleColor.DarkGray);
                    Write(dx + 6, y + 1, item.Lote, ConsoleColor.Cyan);
                    dx += 6 + item.Lote.Length + 3;
                    Write(dx, y + 1, "Temp:", ConsoleColor.DarkGray);
                    Write(dx + 6, y + 1, item.Temperatura, ConsoleColor.Cyan);
                }
                else
                {
                    Write(cNota + 3, y + 1, "caixa fechada · abra para conferir", ConsoleColor.DarkGray);
                }
            }

            AddButton(3, yBtn, " CONCLUIR AUDITORIA E GUARDAR APROVADOS NO ESTOQUE (1 ação) ", ConsoleColor.DarkBlue, () => _game.FinalizarConferenciaEntrega());
        }

        // ------------------------------------------------------------------
        //  ABA 3 · RECEITAS
        // ------------------------------------------------------------------
        private void RenderLivroReceitas()
        {
            int W = ScreenWidth;
            var pratos = Catalog.Pratos.Where(p => p.NivelDesbloqueio <= _game.NivelAtual).ToList();

            int top = 7;
            int bottom = ContentBottom - 2;
            int avail = Math.Max(3, bottom - top + 1);
            int flowW = W - 4 - 11;

            var cards = new List<(DishRecipe p, List<List<Seg>> lines)>();
            foreach (var p in pratos)
            {
                var segs = new List<Seg> { new Seg(p.IngredienteBaseTopo, IngFg(p.IngredienteBaseTopo)) };
                foreach (var ing in p.IngredientesPadrao) segs.Add(new Seg(ing, IngFg(ing)));
                if (!string.IsNullOrEmpty(p.IngredienteBaseFundo)) segs.Add(new Seg(p.IngredienteBaseFundo, IngFg(p.IngredienteBaseFundo)));
                cards.Add((p, Flow(segs, flowW, " → ", ConsoleColor.DarkGray)));
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

            DrawPanel(1, 6, W - 2, ContentBottom - 5, $"LIVRO DE RECEITAS & CARDÁPIO · página {PaginaReceitas + 1}/{totalPag}", ConsoleColor.Yellow);
            if (totalPag > 1)
            {
                string nx = " PRÓX ► ";
                string pv = " ◄ ANT ";
                AddButton(W - 3 - nx.Length, 6, nx, ConsoleColor.DarkGray, () => PaginaReceitas = Math.Min(totalPag - 1, PaginaReceitas + 1));
                AddButton(W - 3 - nx.Length - 1 - pv.Length, 6, pv, ConsoleColor.DarkGray, () => PaginaReceitas = Math.Max(0, PaginaReceitas - 1));
            }

            int row = top;
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
                Write(nx2, row, $"Nv.{p.NivelDesbloqueio}", ConsoleColor.DarkGray);
                nx2 += 6;

                if (p.EstacaoFavorita == "INVERNO") Write(nx2, row, "INVERNO", ConsoleColor.Cyan);
                else if (p.EstacaoFavorita == "VERAO") Write(nx2, row, "VERÃO", ConsoleColor.Yellow);
                else if (p.EstacaoFavorita == "EVENTO") Write(nx2, row, "EVENTO", ConsoleColor.Magenta);
                row++;

                int used = DrawFlow(14, row, lines, 2);
                row += used;
            }

            Write(3, ContentBottom - 1, "Clique em ATIVO / INATIVO para ligar ou desligar o prato no cardápio (mínimo de 2 ativos).", ConsoleColor.DarkGray);
        }

        // ------------------------------------------------------------------
        //  ABA 4 · ESTOQUE
        // ------------------------------------------------------------------
        private void RenderEstoqueCatalogo()
        {
            int W = ScreenWidth;
            var disponiveis = Catalog.TodosIngredientes.Where(i => i.Nivel <= _game.NivelAtual).ToList();
            int cap = _game.CapacidadeMaximaEstoque;

            int gridTop = 10;
            int gridBottom = ContentBottom - 2;
            int rows = Math.Max(1, gridBottom - gridTop + 1);
            int cellW = 36;
            int cols = Math.Max(1, (W - 4 + 1) / (cellW + 1));
            int perPage = cols * rows;
            int totalPag = Math.Max(1, (int)Math.Ceiling(disponiveis.Count / (double)perPage));
            PaginaEstoque = Math.Clamp(PaginaEstoque, 0, totalPag - 1);

            DrawPanel(1, 6, W - 2, ContentBottom - 5, $"ESTOQUE · CÂMARA FRIA & DESPENSA · página {PaginaEstoque + 1}/{totalPag}", ConsoleColor.Yellow);
            if (totalPag > 1)
            {
                string nx = " PRÓX ► ";
                string pv = " ◄ ANT ";
                AddButton(W - 3 - nx.Length, 6, nx, ConsoleColor.DarkGray, () => PaginaEstoque = Math.Min(totalPag - 1, PaginaEstoque + 1));
                AddButton(W - 3 - nx.Length - 1 - pv.Length, 6, pv, ConsoleColor.DarkGray, () => PaginaEstoque = Math.Max(0, PaginaEstoque - 1));
            }

            int totalUn = 0, zerados = 0, contaminados = 0;
            foreach (var ing in disponiveis)
            {
                int q = _game.Estoque.ContainsKey(ing.Nome) ? _game.Estoque[ing.Nome] : 0;
                int c = _game.EstoqueContaminado.ContainsKey(ing.Nome) ? _game.EstoqueContaminado[ing.Nome] : 0;
                totalUn += q;
                if (q == 0) zerados++;
                if (c > 0) contaminados++;
            }

            double ocup = cap * disponiveis.Count > 0 ? (double)totalUn / (cap * disponiveis.Count) : 0;
            Write(3, 7, "OCUPAÇÃO", ConsoleColor.DarkGray);
            DrawBar(12, 7, 30, ocup, ConsoleColor.Cyan);
            Write(43, 7, $"{totalUn}/{cap * disponiveis.Count} un.  ({ocup * 100:F0}%)", ConsoleColor.Gray);
            Write(70, 7, "Capacidade por insumo:", ConsoleColor.DarkGray);
            Write(93, 7, $"{cap} un.", ConsoleColor.White);

            Write(3, 8, $"Esgotados: ", ConsoleColor.DarkGray);
            Write(14, 8, zerados.ToString(), zerados > 0 ? ConsoleColor.Red : ConsoleColor.Green);
            Write(20, 8, "Contaminados: ", ConsoleColor.DarkGray);
            Write(34, 8, contaminados.ToString(), contaminados > 0 ? ConsoleColor.Magenta : ConsoleColor.Green);
            Write(44, 8, "Barras: vermelho < 25% · amarelo < 60% · verde ≥ 60%", ConsoleColor.DarkGray);

            var pagina = disponiveis.Skip(PaginaEstoque * perPage).Take(perPage).ToList();
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

                if (contam > 0) Write(cx, cy, "▲", ConsoleColor.Magenta);
                Write(cx + 1, cy, Fit(ing.Nome, 21).PadRight(21), corNome);
                DrawBar(cx + 23, cy, 6, ratio, corBar);
                Write(cx + 30, cy, $"{qtd}/{cap}", qtd == 0 ? ConsoleColor.Red : ConsoleColor.Gray);
            }

            Write(3, ContentBottom - 1, "Fornecedores autorizados: AgroFoods Brasil · Distribuidora Sul · Laticínios Real · Horta Fresca SA", ConsoleColor.Cyan);
        }

        // ------------------------------------------------------------------
        //  ABA 5 · GERÊNCIA
        // ------------------------------------------------------------------
        private void ProgressRow(int y, string label, int valor, int alvo, string sufixo)
        {
            Write(3, y, label.PadRight(16), ConsoleColor.Gray);
            double r = alvo > 0 ? (double)valor / alvo : 1;
            ConsoleColor col = valor >= alvo ? ConsoleColor.Green : (r >= 0.6 ? ConsoleColor.Yellow : ConsoleColor.Red);
            DrawBar(20, y, 26, r, col);
            Write(48, y, $"{valor}/{alvo}".PadRight(10), col);
            Write(58, y, sufixo, ConsoleColor.DarkGray);
        }

        private void RenderAdministracao()
        {
            int W = ScreenWidth;
            var cfg = _game.ConfigAtual;
            var next = _game.NivelAtual < 5 ? Catalog.Levels[_game.NivelAtual] : null;

            // ---- Painel de metas ----
            DrawPanel(1, 6, W - 2, 8, next != null ? $"PROGRESSO PARA O NÍVEL {next.Nivel} · {next.Titulo}" : "NÍVEL MÁXIMO · META FINAL", ConsoleColor.Green);
            Write(3, 7, Fit(_game.ObterTextoProximaMeta(), W - 8), ConsoleColor.Yellow);
            Write(3, 8, "Risco: " + Fit(_game.DiagnosticarMaiorRiscoAtual(), W - 14), ConsoleColor.Cyan);

            if (next != null)
            {
                ProgressRow(9, "Funcionários", _game.Funcionarios, next.MinFuncionarios, $"mín. p/ Nv.{next.Nivel} · recomendado Nv.{cfg.Nivel}: {cfg.RecFuncionarios}");
                ProgressRow(10, "Limpeza", _game.Limpeza, next.MinLimpeza, $"mín. p/ Nv.{next.Nivel} · recomendado Nv.{cfg.Nivel}: {cfg.RecLimpeza}");
                ProgressRow(11, "Upgrades", _game.TotalUpgrades, next.MinUpgrades, $"mín. p/ Nv.{next.Nivel} · recomendado Nv.{cfg.Nivel}: {cfg.RecUpgrades}");
            }
            else
            {
                ProgressRow(9, "Funcionários", _game.Funcionarios, cfg.RecFuncionarios, "recomendado do nível");
                ProgressRow(10, "Limpeza", _game.Limpeza, cfg.RecLimpeza, "recomendado do nível");
                ProgressRow(11, "Upgrades", _game.TotalUpgrades, cfg.RecUpgrades, "recomendado do nível");
            }
            ProgressRow(12, "Limpeza ideal", _game.Limpeza, Math.Max(1, _game.ObterLimpezaIdeal()), "abaixo do ideal gera multas");

            int bx = 70;

            // ---- Equipe e higiene ----
            DrawPanel(1, 14, W - 2, 4, "EQUIPE & HIGIENE", ConsoleColor.Cyan);
            Write(3, 15, "Funcionários", ConsoleColor.Gray);
            Write(17, 15, _game.Funcionarios.ToString(), ConsoleColor.White);
            Write(24, 15, $"{Money(cfg.CustoPorFuncionario)}/un", _game.Dinheiro >= cfg.CustoPorFuncionario ? ConsoleColor.Gray : ConsoleColor.Red);
            Write(42, 15, "+valor por prato", ConsoleColor.DarkGray);
            AddButton(bx, 15, " +1 ", ConsoleColor.DarkGreen, () => _game.ContratarFuncionariosLote(1));
            AddButton(bx + 5, 15, " +10 ", ConsoleColor.DarkGreen, () => _game.ContratarFuncionariosLote(10));
            AddButton(bx + 11, 15, " +MAX ", ConsoleColor.DarkGreen, () => _game.ContratarFuncionariosLote(999));

            Write(3, 16, "Limpeza", ConsoleColor.Gray);
            Write(17, 16, $"{_game.Limpeza}", ConsoleColor.White);
            Write(24, 16, $"{Money(cfg.CustoPorLimpeza)}/un", _game.Dinheiro >= cfg.CustoPorLimpeza ? ConsoleColor.Gray : ConsoleColor.Red);
            Write(42, 16, "evita multas", ConsoleColor.DarkGray);
            AddButton(bx, 16, " +1 ", ConsoleColor.DarkCyan, () => _game.ComprarLimpezaLote(1));
            AddButton(bx + 5, 16, " +10 ", ConsoleColor.DarkCyan, () => _game.ComprarLimpezaLote(10));
            AddButton(bx + 11, 16, " +MAX ", ConsoleColor.DarkCyan, () => _game.ComprarLimpezaLote(999));

            // ---- Upgrades ----
            DrawPanel(1, 18, W - 2, 6, "UPGRADES DO RESTAURANTE", ConsoleColor.Magenta);
            RenderUpgradeRow(19, "FORNO", "Forno Turbo", _game.UpgForno, $"+ações diárias ({_game.AcoesMaximasDia}/dia)", bx);
            RenderUpgradeRow(20, "GELADEIRA", "Câmara Fria", _game.UpgGeladeira, $"capacidade {_game.CapacidadeMaximaEstoque} un./insumo", bx);
            RenderUpgradeRow(21, "BALCAO", "Balcão VIP", _game.UpgBalcao, "+gorjeta e reputação", bx);
            RenderUpgradeRow(22, "LOGISTICA", "Logística", _game.UpgLogistica, $"{_game.InspecoesCaixaMaximas} inspeções por entrega", bx);

            // ---- Salários e preços ----
            DrawPanel(1, 24, W - 2, 3, "SALÁRIOS & PREÇOS", ConsoleColor.Yellow);
            Write(3, 25, "Salários", ConsoleColor.Gray);
            Write(12, 25, $"{_game.MultiplicadorSalario * 100:F0}%", ConsoleColor.White);
            AddButton(19, 25, " -10% ", ConsoleColor.DarkRed, () => _game.AjustarSalario(-0.10));
            AddButton(26, 25, " +10% ", ConsoleColor.DarkGreen, () => _game.AjustarSalario(0.10));

            Write(42, 25, "Preço do cardápio", ConsoleColor.Gray);
            Write(60, 25, $"{_game.MultiplicadorPrecoPrato * 100:F0}%", ConsoleColor.White);
            AddButton(67, 25, " -10% ", ConsoleColor.DarkYellow, () => _game.AjustarPrecoPratos(-0.10));
            AddButton(74, 25, " +10% ", ConsoleColor.DarkGreen, () => _game.AjustarPrecoPratos(0.10));
        }

        private void RenderUpgradeRow(int y, string codigo, string nome, int nivelAtual, string efeito, int bx)
        {
            double custo = _game.ObterCustoUpgradeEspecifico(nivelAtual);
            Write(3, y, nome.PadRight(14), ConsoleColor.White);
            Write(18, y, $"Nv.{nivelAtual}".PadRight(6), ConsoleColor.Yellow);
            Write(25, y, Fit(efeito, 30), ConsoleColor.DarkGray);

            string c = Money(custo);
            Write(bx - 2 - c.Length, y, c, _game.Dinheiro >= custo ? ConsoleColor.Gray : ConsoleColor.Red);

            AddButton(bx, y, " +1 ", ConsoleColor.DarkMagenta, () => _game.ComprarUpgradeNomeado(codigo, 1));
            AddButton(bx + 5, y, " +5 ", ConsoleColor.DarkMagenta, () => _game.ComprarUpgradeNomeado(codigo, 5));
            AddButton(bx + 10, y, " +MAX ", ConsoleColor.DarkMagenta, () => _game.ComprarUpgradeNomeado(codigo, 999));
        }

        // ------------------------------------------------------------------
        //  ABA 6 · BANCO
        // ------------------------------------------------------------------
        private void RenderBanco()
        {
            int W = ScreenWidth;

            DrawPanel(1, 6, W - 2, 8, "BANCO EMPRESARIAL", ConsoleColor.Yellow);

            double limite = _game.LimiteCreditoBanco;
            double divida = _game.DividaTotalBanco;
            double uso = limite > 0 ? divida / limite : (divida > 0 ? 1 : 0);

            Write(3, 7, "Limite de crédito", ConsoleColor.DarkGray);
            Write(22, 7, Money(limite), ConsoleColor.Green);
            Write(44, 7, $"(reputação {_game.Reputacao:F1} × nível {_game.NivelAtual})", ConsoleColor.DarkGray);

            Write(3, 9, "Dívida total", ConsoleColor.DarkGray);
            Write(22, 9, Money(divida), divida > 0 ? ConsoleColor.Red : ConsoleColor.Green);
            DrawBar(44, 9, 30, uso, uso > 0.8 ? ConsoleColor.Red : (uso > 0.4 ? ConsoleColor.Yellow : ConsoleColor.Green));
            Write(75, 9, $"{uso * 100:F0}% do limite", ConsoleColor.Gray);

            Write(3, 10, "Prazo restante", ConsoleColor.DarkGray);
            Write(22, 10, $"{_game.MesesRestantesDivida} meses", ConsoleColor.White);
            Write(3, 11, "Parcela mensal", ConsoleColor.DarkGray);
            Write(22, 11, Money(_game.ParcelaMensalBanco), ConsoleColor.White);

            Write(3, 12, "Juros de 3% ao mês sobre o saldo devedor. Quitar antes economiza juros!", ConsoleColor.Cyan);

            DrawPanel(1, 14, W - 2, 4, "TOMAR EMPRÉSTIMO", ConsoleColor.Cyan);
            AddButton(3, 15, " + R$ 1.500 ", ConsoleColor.DarkCyan, () => _game.PedirEmprestimo(1500));
            AddButton(17, 15, " + R$ 4.500 ", ConsoleColor.DarkCyan, () => _game.PedirEmprestimo(4500));
            AddButton(31, 15, " + R$ 15.000 ", ConsoleColor.DarkCyan, () => _game.PedirEmprestimo(15000));

            DrawPanel(1, 18, W - 2, 4, "AMORTIZAÇÃO ANTECIPADA · reduz a dívida na hora e evita juros", ConsoleColor.Green);
            AddButton(3, 19, " PAGAR R$ 500 ", ConsoleColor.DarkGreen, () => _game.AmortizarDivida(500));
            AddButton(20, 19, " PAGAR R$ 2.000 ", ConsoleColor.DarkGreen, () => _game.AmortizarDivida(2000));
            AddButton(39, 19, $" QUITAR TUDO ({Money(divida)}) ", ConsoleColor.Green, () => _game.AmortizarDivida(_game.DividaTotalBanco));
        }

        // ------------------------------------------------------------------
        //  ABA 7 · TERMINAL DEV (senha: xit89)
        // ------------------------------------------------------------------
        private void DevRow(int y, string label, params DevBtn[] btns)
        {
            Write(3, y, label, ConsoleColor.Yellow);
            int x = 36;
            foreach (var b in btns)
            {
                AddButton(x, y, b.Text, b.Bg, b.Act);
                x += b.Text.Length + 1;
            }
        }

        private void RenderTerminalDev()
        {
            int W = ScreenWidth;

            if (!_game.TerminalDesbloqueado)
            {
                DrawPanel(1, 6, W - 2, 12, "TERMINAL DE DESENVOLVEDOR · ACESSO RESTRITO", ConsoleColor.Magenta);
                Write(3, 8, "Este terminal exige autenticação de desenvolvedor.", ConsoleColor.Yellow);
                Write(3, 9, "Digite a senha no teclado e aperte Enter, ou clique nas teclas abaixo.", ConsoleColor.Gray);

                string mascara = new string('•', _game.InputSenhaTerminal.Length);
                Write(3, 11, "SENHA", ConsoleColor.DarkGray);
                Write(10, 11, "[ " + mascara.PadRight(14) + " ]", ConsoleColor.Cyan);

                string[] teclas = { "x", "i", "t", "8", "9" };
                int kx = 3;
                foreach (var t in teclas)
                {
                    string tt = t;
                    AddButton(kx, 13, $"  {t}  ", ConsoleColor.DarkGray, () => _game.InputSenhaTerminal += tt);
                    kx += 6;
                }

                AddButton(3, 15, " AUTENTICAR (Enter) ", ConsoleColor.DarkGreen, () => _game.TentarDesbloquearTerminal());
                AddButton(25, 15, " LIMPAR ", ConsoleColor.DarkRed, () => _game.InputSenhaTerminal = "");
                return;
            }

            DrawPanel(1, 6, W - 2, 21, "TERMINAL DE DESENVOLVEDOR · AUTENTICADO", ConsoleColor.Magenta);
            Write(3, 7, "Altere qualquer variável do jogo em tempo real.", ConsoleColor.Green);

            DevRow(9, $"1. CAIXA ({Money(_game.Dinheiro)})",
                new DevBtn(" +1.000 ", ConsoleColor.DarkGreen, () => _game.DevAlterarDinheiro(1000)),
                new DevBtn(" +10.000 ", ConsoleColor.DarkGreen, () => _game.DevAlterarDinheiro(10000)),
                new DevBtn(" +100.000 ", ConsoleColor.DarkGreen, () => _game.DevAlterarDinheiro(100000)),
                new DevBtn(" -1.000 ", ConsoleColor.DarkRed, () => _game.DevAlterarDinheiro(-1000)),
                new DevBtn(" -10.000 ", ConsoleColor.DarkRed, () => _game.DevAlterarDinheiro(-10000)),
                new DevBtn(" ZERAR ", ConsoleColor.Red, () => _game.DevDefinirDinheiro(0)));

            DevRow(11, $"2. FUNCIONÁRIOS ({_game.Funcionarios})",
                new DevBtn(" +1 ", ConsoleColor.DarkGreen, () => _game.DevAlterarFuncionarios(1)),
                new DevBtn(" +10 ", ConsoleColor.DarkGreen, () => _game.DevAlterarFuncionarios(10)),
                new DevBtn(" +50 ", ConsoleColor.DarkGreen, () => _game.DevAlterarFuncionarios(50)),
                new DevBtn(" -1 ", ConsoleColor.DarkRed, () => _game.DevAlterarFuncionarios(-1)),
                new DevBtn(" -10 ", ConsoleColor.DarkRed, () => _game.DevAlterarFuncionarios(-10)));

            DevRow(13, $"3. LIMPEZA ({_game.Limpeza})",
                new DevBtn(" +1 ", ConsoleColor.DarkCyan, () => _game.DevAlterarLimpeza(1)),
                new DevBtn(" +10 ", ConsoleColor.DarkCyan, () => _game.DevAlterarLimpeza(10)),
                new DevBtn(" +50 ", ConsoleColor.DarkCyan, () => _game.DevAlterarLimpeza(50)),
                new DevBtn(" -1 ", ConsoleColor.DarkRed, () => _game.DevAlterarLimpeza(-1)),
                new DevBtn(" -10 ", ConsoleColor.DarkRed, () => _game.DevAlterarLimpeza(-10)));

            DevRow(15, $"4. UPGRADES ({_game.TotalUpgrades})",
                new DevBtn(" +1 TODOS ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("TODOS", 1)),
                new DevBtn(" +10 TODOS ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("TODOS", 10)),
                new DevBtn(" -1 TODOS ", ConsoleColor.DarkRed, () => _game.DevAlterarUpgrades("TODOS", -1)),
                new DevBtn(" -10 TODOS ", ConsoleColor.DarkRed, () => _game.DevAlterarUpgrades("TODOS", -10)));

            DevRow(17, "   individuais",
                new DevBtn($" +Forno ({_game.UpgForno}) ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("FORNO", 1)),
                new DevBtn($" +Gelad. ({_game.UpgGeladeira}) ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("GELADEIRA", 1)),
                new DevBtn($" +Balcão ({_game.UpgBalcao}) ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("BALCAO", 1)),
                new DevBtn($" +Logíst. ({_game.UpgLogistica}) ", ConsoleColor.DarkMagenta, () => _game.DevAlterarUpgrades("LOGISTICA", 1)));

            DevRow(19, $"5. REP ({_game.Reputacao:F1}) & NÍVEL ({_game.NivelAtual})",
                new DevBtn(" +0.5 Rep ", ConsoleColor.DarkGreen, () => _game.DevAlterarReputacao(0.5)),
                new DevBtn(" -0.5 Rep ", ConsoleColor.DarkRed, () => _game.DevAlterarReputacao(-0.5)),
                new DevBtn(" +1 NÍVEL ", ConsoleColor.DarkBlue, () => _game.DevAlterarNivel(1)),
                new DevBtn(" -1 NÍVEL ", ConsoleColor.DarkRed, () => _game.DevAlterarNivel(-1)));

            DevRow(21, "6. UTILITÁRIOS",
                new DevBtn(" ENCHER ESTOQUE ", ConsoleColor.DarkCyan, () => _game.DevEncherEstoqueTotal()),
                new DevBtn(" ZERAR DÍVIDA ", ConsoleColor.DarkGreen, () => _game.DevPerdoarDivida()),
                new DevBtn(" SORTEAR EVENTO ", ConsoleColor.DarkYellow, () => _game.SortearEventoDoDia()));

            AddButton(3, 24, " BLOQUEAR TERMINAL (LOGOUT) ", ConsoleColor.DarkRed, () => _game.BloquearTerminal());
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

            Write(x + 5, y + 9, $"Nível {_game.NivelAtual}  ·  Reputação {_game.Reputacao:F1}  ·  Pratos perfeitos {_game.PedidosPerfeitosTotal}  ·  Diversão {_game.Diversao}", ConsoleColor.Gray);

            string b1 = " JOGAR DE NOVO (Enter) ";
            string b2 = " CARREGAR SAVE (L) ";
            string b3 = " SAIR (Esc) ";
            int bx = x + 5;
            int by = y + h - 3;
            AddButton(bx, by, b1, ConsoleColor.DarkGreen, () => _game.IniciarNovaRun(mostrarTutorial: false));
            AddButton(bx + b1.Length + 2, by, b2, ConsoleColor.DarkCyan, () => _game.CarregarJogo());
            AddButton(bx + b1.Length + b2.Length + 4, by, b3, ConsoleColor.DarkRed, Sair);
        }

        // ------------------------------------------------------------------
        //  Entrada
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

        /// <summary>Chamado quando o console é redimensionado.</summary>
        public void HandleResize()
        {
            _forceFullRedraw = true;
            Render();
        }

        public void HandleMouseClick(int x, int y)
        {
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

        private void TrocarAba(ScreenTab aba)
        {
            AbaAtual = aba;
            IndiceBotaoFocadoTeclado = -1;
            Render();
        }

        public void HandleKeyPress(ConsoleKey key)
        {
            // Diálogo de saída
            if (ConfirmandoSaida)
            {
                if (key == ConsoleKey.Enter || key == ConsoleKey.Y) Sair();
                else if (key == ConsoleKey.S) { _game.SalvarJogo(); Sair(); }
                else if (key == ConsoleKey.Escape || key == ConsoleKey.N || key == ConsoleKey.Backspace)
                {
                    ConfirmandoSaida = false;
                    Render();
                }
                return;
            }

            bool fimDeJogo = _game.GameOver || _game.JogoVencido;

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
                if (key == ConsoleKey.Enter) { _game.IniciarNovaRun(mostrarTutorial: false); Render(); }
                else if (key == ConsoleKey.L) { _game.CarregarJogo(); Render(); }
                return;
            }

            if (_game.ExibirTutorial)
            {
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

            // Terminal bloqueado: digitar a senha direto no teclado
            if (AbaAtual == ScreenTab.Terminal && !_game.TerminalDesbloqueado && _game.ProcessoAtivo == null)
            {
                if (key == ConsoleKey.Backspace && _game.InputSenhaTerminal.Length > 0)
                {
                    _game.InputSenhaTerminal = _game.InputSenhaTerminal[..^1];
                    Render();
                    return;
                }
                if (key == ConsoleKey.Enter)
                {
                    _game.TentarDesbloquearTerminal();
                    Render();
                    return;
                }
                if (key >= ConsoleKey.A && key <= ConsoleKey.Z)
                {
                    char c = char.ToLowerInvariant((char)('a' + (key - ConsoleKey.A)));
                    _game.InputSenhaTerminal += c;
                    Render();
                    return;
                }
                if ((key >= ConsoleKey.D8 && key <= ConsoleKey.D9) || (key >= ConsoleKey.NumPad8 && key <= ConsoleKey.NumPad9))
                {
                    char d = (key == ConsoleKey.D8 || key == ConsoleKey.NumPad8) ? '8' : '9';
                    _game.InputSenhaTerminal += d;
                    Render();
                    return;
                }
            }

            if (key == ConsoleKey.H)
            {
                _game.ExibirTutorial = true;
                Render();
                return;
            }

            // Paginação
            if (key == ConsoleKey.PageDown || key == ConsoleKey.PageUp)
            {
                int d = key == ConsoleKey.PageDown ? 1 : -1;
                switch (AbaAtual)
                {
                    case ScreenTab.Cozinha: PaginaCozinha = Math.Max(0, PaginaCozinha + d); break;
                    case ScreenTab.LivroReceitas: PaginaReceitas = Math.Max(0, PaginaReceitas + d); break;
                    case ScreenTab.EstoqueCatalogo: PaginaEstoque = Math.Max(0, PaginaEstoque + d); break;
                }
                Render();
                return;
            }

            if (key == ConsoleKey.Tab || key == ConsoleKey.RightArrow || key == ConsoleKey.DownArrow)
            {
                if (BotoesAtivos.Count > 0)
                    IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado + 1) % BotoesAtivos.Count;
                Render();
                return;
            }

            if (key == ConsoleKey.LeftArrow || key == ConsoleKey.UpArrow)
            {
                if (BotoesAtivos.Count > 0)
                    IndiceBotaoFocadoTeclado = (IndiceBotaoFocadoTeclado - 1 + BotoesAtivos.Count) % BotoesAtivos.Count;
                Render();
                return;
            }

            if (key == ConsoleKey.Spacebar)
            {
                if (IndiceBotaoFocadoTeclado >= 0 && IndiceBotaoFocadoTeclado < BotoesAtivos.Count)
                {
                    BotoesAtivos[IndiceBotaoFocadoTeclado].OnClick?.Invoke();
                    Render();
                }
                return;
            }

            if (_game.ProcessoAtivo != null) return;

            switch (key)
            {
                case ConsoleKey.D1: case ConsoleKey.NumPad1: TrocarAba(ScreenTab.Cozinha); break;
                case ConsoleKey.D2: case ConsoleKey.NumPad2: TrocarAba(ScreenTab.Entregas); break;
                case ConsoleKey.D3: case ConsoleKey.NumPad3: TrocarAba(ScreenTab.LivroReceitas); break;
                case ConsoleKey.D4: case ConsoleKey.NumPad4: TrocarAba(ScreenTab.EstoqueCatalogo); break;
                case ConsoleKey.D5: case ConsoleKey.NumPad5: TrocarAba(ScreenTab.Administracao); break;
                case ConsoleKey.D6: case ConsoleKey.NumPad6: TrocarAba(ScreenTab.Banco); break;
                case ConsoleKey.D7: case ConsoleKey.NumPad7: TrocarAba(ScreenTab.Terminal); break;
                case ConsoleKey.Z:
                    if (AbaAtual == ScreenTab.Cozinha) { _game.DesfazerUltimoIngrediente(); Render(); }
                    break;
                case ConsoleKey.Enter:
                    if (IndiceBotaoFocadoTeclado >= 0 && IndiceBotaoFocadoTeclado < BotoesAtivos.Count)
                    {
                        BotoesAtivos[IndiceBotaoFocadoTeclado].OnClick?.Invoke();
                        Render();
                    }
                    else if (AbaAtual == ScreenTab.Cozinha)
                    {
                        _game.EntregarPedido();
                        Render();
                    }
                    break;
                case ConsoleKey.S:
                    _game.SalvarJogo(); Render(); break;
                case ConsoleKey.L:
                    _game.CarregarJogo(); Render(); break;
            }
        }
    }
}
