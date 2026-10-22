using System;
using System.Collections.Generic;
using System.Linq;

namespace PizzariaTycoon
{
    public enum ScreenTab
    {
        Cozinha = 1,
        Entregas = 2,
        LivroReceitas = 3,
        EstoqueCatalogo = 4,
        Administracao = 5,
        Banco = 6
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
        public const int ScreenWidth = 120;
        public const int ScreenHeight = 30;

        private readonly GameEngine _game;
        private readonly ScreenCell[,] _backBuffer = new ScreenCell[ScreenWidth, ScreenHeight];
        private readonly ScreenCell[,] _frontBuffer = new ScreenCell[ScreenWidth, ScreenHeight];
        private bool _forceFullRedraw = true;

        public ScreenTab AbaAtual { get; private set; } = ScreenTab.Cozinha;
        public List<ClickableZone> BotoesAtivos { get; private set; } = new List<ClickableZone>();
        public int HoverX { get; private set; } = -1;
        public int HoverY { get; private set; } = -1;
        public int IndiceBotaoFocadoTeclado { get; private set; } = -1;

        // Paginação e Modos para nunca estourar a tela no Nível 5 (Corrige Bug 1)
        public bool MostrarTodosIngredientesCozinha { get; private set; } = false;
        public int PaginaCozinha { get; private set; } = 0;
        public int PaginaEstoque { get; private set; } = 0;
        public int PaginaReceitas { get; private set; } = 0;

        public ConsoleUI(GameEngine game)
        {
            _game = game;
            ClearBackBuffer();
        }

        private void ClearBackBuffer()
        {
            for (int y = 0; y < ScreenHeight; y++)
            {
                for (int x = 0; x < ScreenWidth; x++)
                {
                    _backBuffer[x, y] = new ScreenCell { Char = ' ', Fg = ConsoleColor.Gray, Bg = ConsoleColor.Black };
                }
            }
        }

        // Escreve no BackBuffer com checagem estrita de limites (Nunca lança ArgumentOutOfRangeException)
        private void SafeWrite(int x, int y, string text, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black)
        {
            if (string.IsNullOrEmpty(text) || y < 0 || y >= ScreenHeight || x >= ScreenWidth) return;

            for (int i = 0; i < text.Length; i++)
            {
                int cx = x + i;
                if (cx < 0) continue;
                if (cx >= ScreenWidth) break;

                _backBuffer[cx, y] = new ScreenCell { Char = text[i], Fg = fg, Bg = bg };
            }
        }

        private void AddButton(int x, int y, string label, ConsoleColor bg, ConsoleColor fg, Action action)
        {
            if (y < 0 || y >= ScreenHeight || x < 0 || x >= ScreenWidth) return;

            int btnIndex = BotoesAtivos.Count;
            bool isMouseHover = HoverX >= x && HoverX < (x + label.Length) && HoverY == y;
            bool isKeyFocus = IndiceBotaoFocadoTeclado == btnIndex;

            ConsoleColor finalBg = (isMouseHover || isKeyFocus) ? ConsoleColor.Yellow : bg;
            ConsoleColor finalFg = (isMouseHover || isKeyFocus) ? ConsoleColor.Black : fg;

            SafeWrite(x, y, label, finalFg, finalBg);

            BotoesAtivos.Add(new ClickableZone
            {
                X = x,
                Y = y,
                Width = label.Length,
                Height = 1,
                OnClick = action
            });
        }

        // Double-Buffer: Atualiza apenas os caracteres que mudaram, zerando a piscada de tela (Corrige Bug 8)
        private void FlushBufferToConsole()
        {
            int maxW = Math.Min(ScreenWidth, Console.BufferWidth);
            int maxH = Math.Min(ScreenHeight, Console.BufferHeight);

            for (int y = 0; y < maxH; y++)
            {
                for (int x = 0; x < maxW; x++)
                {
                    // Evita escrever no último canto inferior direito para o terminal não rolar a linha
                    if (y == maxH - 1 && x == maxW - 1) continue;

                    var next = _backBuffer[x, y];
                    var prev = _frontBuffer[x, y];

                    if (_forceFullRedraw || next.Char != prev.Char || next.Fg != prev.Fg || next.Bg != prev.Bg)
                    {
                        try
                        {
                            Console.SetCursorPosition(x, y);
                            Console.ForegroundColor = next.Fg;
                            Console.BackgroundColor = next.Bg;
                            Console.Write(next.Char);
                            _frontBuffer[x, y] = next;
                        }
                        catch { }
                    }
                }
            }
            Console.ResetColor();
            _forceFullRedraw = false;
        }

        public void Render()
        {
            BotoesAtivos.Clear();
            ClearBackBuffer();

            if (_game.GameOver || _game.JogoVencido)
            {
                RenderEndScreen();
                FlushBufferToConsole();
                return;
            }

            RenderHeader();

            if (_game.ProcessoAtivo != null)
            {
                RenderModalTribunal();
                RenderFooter();
                FlushBufferToConsole();
                return;
            }

            RenderNavigationTabs();

            switch (AbaAtual)
            {
                case ScreenTab.Cozinha:
                    RenderCozinha();
                    break;
                case ScreenTab.Entregas:
                    RenderEntregas();
                    break;
                case ScreenTab.LivroReceitas:
                    RenderLivroReceitas();
                    break;
                case ScreenTab.EstoqueCatalogo:
                    RenderEstoqueCatalogo();
                    break;
                case ScreenTab.Administracao:
                    RenderAdministracao();
                    break;
                case ScreenTab.Banco:
                    RenderBanco();
                    break;
            }

            if (IndiceBotaoFocadoTeclado >= BotoesAtivos.Count)
                IndiceBotaoFocadoTeclado = BotoesAtivos.Count - 1;

            RenderFooter();
            FlushBufferToConsole();
        }

        private void RenderHeader()
        {
            SafeWrite(0, 0, new string(' ', ScreenWidth), ConsoleColor.White, ConsoleColor.DarkBlue);
            SafeWrite(2, 0, $"PIZZARIA TYCOON | Nv.{_game.NivelAtual} ({_game.ConfigAtual.Titulo}) | Reputação: {_game.Reputacao:F1}* | Data: {_game.DataHojeFormatada} (Sem.{_game.DiaSemana}/4) | Ações: {_game.AcoesRestantesDia}/{_game.AcoesMaximasDia}", ConsoleColor.White, ConsoleColor.DarkBlue);

            SafeWrite(0, 1, new string(' ', ScreenWidth), ConsoleColor.White, ConsoleColor.DarkBlue);
            SafeWrite(2, 1, $"CAIXA: R$ {_game.Dinheiro:F2} | Folha/Mês: R$ {_game.CalcularDespesaMensalFixa():F2} | Func:{_game.Funcionarios} Limp:{_game.Limpeza} Upg:{_game.TotalUpgrades} | Fun:{_game.Diversao}", ConsoleColor.White, ConsoleColor.DarkBlue);

            AddButton(86, 1, "[ SALVAR (S) ]", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.SalvarJogo());
            AddButton(102, 1, "[ LOAD (L) ]", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.CarregarJogo());

            SafeWrite(2, 2, $"EVENTO: {_game.EventoDoDiaDescricao}", ConsoleColor.Cyan, ConsoleColor.Black);
            AddButton(94, 2, "[ PULAR DIA >> ]", ConsoleColor.DarkGray, ConsoleColor.White, () => _game.AvancarDia());
        }

        private void RenderNavigationTabs()
        {
            int y = 4;
            AddButton(2, y, " [1. COZINHA] ", AbaAtual == ScreenTab.Cozinha ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.Cozinha);
            AddButton(18, y, " [2. ENTREGAS] ", AbaAtual == ScreenTab.Entregas ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.Entregas);
            AddButton(35, y, " [3. RECEITAS & MENU] ", AbaAtual == ScreenTab.LivroReceitas ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.LivroReceitas);
            AddButton(59, y, " [4. ESTOQUE] ", AbaAtual == ScreenTab.EstoqueCatalogo ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.EstoqueCatalogo);
            AddButton(75, y, " [5. GERÊNCIA] ", AbaAtual == ScreenTab.Administracao ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.Administracao);
            AddButton(92, y, " [6. BANCO] ", AbaAtual == ScreenTab.Banco ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.Banco);
        }

        private void RenderCozinha()
        {
            var pedido = _game.PedidoAtual;
            SafeWrite(2, 6, $"PEDIDO DO CLIENTE (Valor Fixado: R$ {_game.CalcularValorVendaPrato():F2} | Gasta 1 Ação):", ConsoleColor.Yellow);
            SafeWrite(2, 7, pedido.FalaDoCliente, ConsoleColor.White);

            SafeWrite(2, 9, "PRATO NA BANCADA: ", ConsoleColor.Green);
            string montadoTxt = pedido.PratoMontado.Count > 0 ? string.Join(" -> ", pedido.PratoMontado) : "(Vazio - consulte a aba 3. RECEITAS para ver a ordem padrão!)";
            SafeWrite(20, 9, montadoTxt, ConsoleColor.White);

            AddButton(2, 11, " [ ENTREGAR PRATO (ENTER) ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.EntregarPedido());
            AddButton(32, 11, " [ DESFAZER ÚLTIMO (Z) ] ", ConsoleColor.DarkYellow, ConsoleColor.Black, () => _game.DesfazerUltimoIngrediente());
            AddButton(59, 11, " [ LIMPAR TUDO ] ", ConsoleColor.DarkRed, ConsoleColor.White, () => _game.LimparPratoAtual());

            string modoLabel = MostrarTodosIngredientesCozinha ? "[ MODO: TODOS PAGINADOS ]" : "[ MODO: BANCADA INTELIGENTE (14) ]";
            AddButton(78, 11, modoLabel, ConsoleColor.DarkMagenta, ConsoleColor.White, () =>
            {
                MostrarTodosIngredientesCozinha = !MostrarTodosIngredientesCozinha;
                PaginaCozinha = 0;
            });

            List<string> listaCompleta = MostrarTodosIngredientesCozinha
                ? Catalog.TodosIngredientes.Where(i => i.Nivel <= _game.NivelAtual).Select(i => i.Nome).ToList()
                : pedido.OpcoesBancada;

            int itensPorPagina = 16;
            int totalPaginas = Math.Max(1, (int)Math.Ceiling(listaCompleta.Count / (double)itensPorPagina));
            PaginaCozinha = Math.Clamp(PaginaCozinha, 0, totalPaginas - 1);

            SafeWrite(2, 13, $"INGREDIENTES DISPONÍVEIS (Página {PaginaCozinha + 1}/{totalPaginas}):", ConsoleColor.Gray);
            if (totalPaginas > 1)
            {
                AddButton(48, 13, "[ < Pág. Anterior ]", ConsoleColor.DarkGray, ConsoleColor.White, () => PaginaCozinha = Math.Max(0, PaginaCozinha - 1));
                AddButton(69, 13, "[ Próx. Pág. > ]", ConsoleColor.DarkGray, ConsoleColor.White, () => PaginaCozinha = Math.Min(totalPaginas - 1, PaginaCozinha + 1));
            }

            var itensPagina = listaCompleta.Skip(PaginaCozinha * itensPorPagina).Take(itensPorPagina).ToList();
            int col = 2;
            int row = 15;

            foreach (var ingNome in itensPagina)
            {
                var info = Catalog.TodosIngredientes.FirstOrDefault(i => i.Nome == ingNome);
                var corCat = info != null ? Catalog.ObterCorCategoria(info.Categoria) : ConsoleColor.DarkCyan;
                int qtdEstoque = _game.Estoque.ContainsKey(ingNome) ? _game.Estoque[ingNome] : 0;

                string btnText = qtdEstoque > 0 ? $" [{ingNome} ({qtdEstoque})] " : $" [{ingNome} (0) +COMPRAR] ";
                if (col + btnText.Length > 116)
                {
                    col = 2;
                    row += 2;
                }

                // Garante que nunca passa da linha 23
                if (row > 23) break;

                string copy = ingNome;
                if (qtdEstoque > 0)
                    AddButton(col, row, btnText, corCat, ConsoleColor.White, () => _game.AdicionarIngredienteAoPrato(copy));
                else
                    AddButton(col, row, btnText, ConsoleColor.DarkRed, ConsoleColor.White, () => _game.CompraEmergencialIngrediente(copy));

                col += btnText.Length + 2;
            }

            SafeWrite(2, 25, "Cores: [Amarelo=Base] [Vermelho=Proteína] [Ciano=Laticínio] [Verde=Vegetal] [Magenta=Molho] [Azul=Fruta/Doce]", ConsoleColor.DarkGray);
        }

        private void RenderEntregas()
        {
            SafeWrite(2, 6, $"AUDITORIA DE ENTREGA | Data Hoje: {_game.DataHojeFormatada} | Cidades: São Paulo, Curitiba, Rio de Janeiro | Inspeções: {_game.InspecoesCaixaRestantes}", ConsoleColor.Yellow);
            SafeWrite(2, 7, "Regra: Abra caixas suspeitas para conferir Item Físico, Lote (LT-XXX) e Temperatura (Frios <= 5°C).", ConsoleColor.Gray);

            SafeWrite(2, 9, "# | NOTA FISCAL          | QTD | FORNECEDOR           | CIDADE          | DATA NOTA  | INTERIOR DA CAIXA (LOTE/TEMP)      | DECISÃO");
            SafeWrite(2, 10, new string('-', 115));

            for (int i = 0; i < _game.EntregaAtual.Count; i++)
            {
                var item = _game.EntregaAtual[i];
                int y = 11 + (i * 2);

                SafeWrite(2, y, $"{i + 1} | {item.NomeNota,-20} | +{item.Quantidade}  | {item.Fornecedor,-20} | {item.Cidade,-15} | {item.DataNota,-10} | ");

                int idx = i;
                if (!item.CaixaAberta)
                {
                    AddButton(73, y, $" [ ABRIR CAIXA ({_game.InspecoesCaixaRestantes}) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.InspecionarCaixaEntrega(idx));
                }
                else
                {
                    string interior = $"{item.NomeFisico} | {item.Lote} | {item.Temperatura}";
                    SafeWrite(73, y, $"{interior,-33}", ConsoleColor.Cyan);
                }

                if (item.MarcadoComoValidoPeloJogador)
                    AddButton(107, y, "[ACEITAR]", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AlternarAprovacaoItemEntrega(idx));
                else
                    AddButton(107, y, "[RECUSAR]", ConsoleColor.DarkRed, ConsoleColor.White, () => _game.AlternarAprovacaoItemEntrega(idx));
            }

            AddButton(2, 25, " [ CONCLUIR AUDITORIA E GUARDAR APROVADOS NO ESTOQUE (1 Ação) ] ", ConsoleColor.Blue, ConsoleColor.White, () => _game.FinalizarConferenciaEntrega());
        }

        private void RenderLivroReceitas()
        {
            var pratos = Catalog.Pratos.Where(p => p.NivelDesbloqueio <= _game.NivelAtual).ToList();
            int porPagina = 14;
            int totalPag = Math.Max(1, (int)Math.Ceiling(pratos.Count / (double)porPagina));
            PaginaReceitas = Math.Clamp(PaginaReceitas, 0, totalPag - 1);

            SafeWrite(2, 6, $"LIVRO DE RECEITAS & CARDÁPIO ATIVO (Pág. {PaginaReceitas + 1}/{totalPag}) - Clique para ativar/desativar pratos:", ConsoleColor.Yellow);
            if (totalPag > 1)
            {
                AddButton(84, 6, "[ < Pág. Ant ]", ConsoleColor.DarkGray, ConsoleColor.White, () => PaginaReceitas = Math.Max(0, PaginaReceitas - 1));
                AddButton(100, 6, "[ Próx. Pág > ]", ConsoleColor.DarkGray, ConsoleColor.White, () => PaginaReceitas = Math.Min(totalPag - 1, PaginaReceitas + 1));
            }

            int row = 8;
            foreach (var p in pratos.Skip(PaginaReceitas * porPagina).Take(porPagina))
            {
                bool ativo = _game.PratosAtivosNoCardapio.Contains(p.Nome);
                string nomeCopy = p.Nome;

                AddButton(2, row, ativo ? "[ ATIVO ]" : "[INATIVO]", ativo ? ConsoleColor.DarkGreen : ConsoleColor.DarkRed, ConsoleColor.White, () => _game.AlternarPratoNoCardapio(nomeCopy));

                var ordemCompleta = new List<string> { p.IngredienteBaseTopo };
                ordemCompleta.AddRange(p.IngredientesPadrao);
                if (!string.IsNullOrEmpty(p.IngredienteBaseFundo)) ordemCompleta.Add(p.IngredienteBaseFundo);

                SafeWrite(13, row, $"{p.Nome,-18}: {string.Join(" -> ", ordemCompleta)}", ConsoleColor.White);
                row++;
            }
        }

        private void RenderEstoqueCatalogo()
        {
            var disponiveis = Catalog.TodosIngredientes.Where(i => i.Nivel <= _game.NivelAtual).ToList();
            int porPagina = 48; // 3 colunas x 16 linhas por página (Comporta 96 itens em 2 páginas sem estourar!)
            int totalPag = Math.Max(1, (int)Math.Ceiling(disponiveis.Count / (double)porPagina));
            PaginaEstoque = Math.Clamp(PaginaEstoque, 0, totalPag - 1);

            SafeWrite(2, 6, $"ESTOQUE DA CÂMARA FRIA / DESPENSA (Capacidade Máx: {_game.CapacidadeMaximaEstoque} un. | Pág. {PaginaEstoque + 1}/{totalPag}):", ConsoleColor.Yellow);
            if (totalPag > 1)
            {
                AddButton(84, 6, "[ < Pág. Ant ]", ConsoleColor.DarkGray, ConsoleColor.White, () => PaginaEstoque = Math.Max(0, PaginaEstoque - 1));
                AddButton(100, 6, "[ Próx. Pág > ]", ConsoleColor.DarkGray, ConsoleColor.White, () => PaginaEstoque = Math.Min(totalPag - 1, PaginaEstoque + 1));
            }

            int col = 2;
            int row = 8;

            foreach (var ing in disponiveis.Skip(PaginaEstoque * porPagina).Take(porPagina))
            {
                int qtd = _game.Estoque.ContainsKey(ing.Nome) ? _game.Estoque[ing.Nome] : 0;
                int contam = _game.EstoqueContaminado.ContainsKey(ing.Nome) ? _game.EstoqueContaminado[ing.Nome] : 0;
                string alerta = contam > 0 ? " [CONTAM!]" : "";
                ConsoleColor cor = qtd == 0 ? ConsoleColor.Red : (contam > 0 ? ConsoleColor.Magenta : ConsoleColor.Gray);

                SafeWrite(col, row, $"{ing.Nome,-23}: {qtd,2}{alerta}", cor);
                row++;
                if (row >= 24)
                {
                    row = 8;
                    col += 38;
                }
            }

            SafeWrite(2, 25, "Fornecedores Autorizados: AgroFoods Brasil, Distribuidora Sul, Laticínios Real, Horta Fresca SA", ConsoleColor.Cyan);
        }

        private void RenderAdministracao()
        {
            var cfg = _game.ConfigAtual;
            SafeWrite(2, 6, "GERÊNCIA & UPGRADES NOMEADOS | Meta Vitória: Nível 5 + R$ 50.000 Líquidos + 4.5 Estrelas", ConsoleColor.Yellow);

            if (_game.NivelAtual < 5)
            {
                var prox = Catalog.Levels[_game.NivelAtual];
                SafeWrite(2, 7, $"Próximo Nível ({prox.Titulo}): Func {_game.Funcionarios}/{prox.MinFuncionarios} | Limpeza {_game.Limpeza}/{prox.MinLimpeza} | Upgrades Totais {_game.TotalUpgrades}/{prox.MinUpgrades}", ConsoleColor.Cyan);
            }

            SafeWrite(2, 9, $"Funcionários ({_game.Funcionarios}) - Custo: R$ {cfg.CustoPorFuncionario:F0}/un:");
            AddButton(48, 9, " [ +1 ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.ContratarFuncionariosLote(1));
            AddButton(58, 9, " [ +10 ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.ContratarFuncionariosLote(10));
            AddButton(69, 9, " [ +MÁX ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.ContratarFuncionariosLote(999));

            SafeWrite(2, 11, $"Limpeza ({_game.Limpeza}) - Custo: R$ {cfg.CustoPorLimpeza:F0}/un:");
            AddButton(48, 11, " [ +1 ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.ComprarLimpezaLote(1));
            AddButton(58, 11, " [ +10 ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.ComprarLimpezaLote(10));
            AddButton(69, 11, " [ +MÁX ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.ComprarLimpezaLote(999));

            SafeWrite(2, 13, "UPGRADES ESPECIALIZADOS DO RESTAURANTE:", ConsoleColor.Yellow);

            RenderUpgradeRow(14, "FORNO", $"1. Forno Turbo (Nv.{_game.UpgForno}) [+Ações Diárias]", _game.UpgForno);
            RenderUpgradeRow(16, "GELADEIRA", $"2. Câmara Fria (Nv.{_game.UpgGeladeira}) [Cap. Estoque: {_game.CapacidadeMaximaEstoque}]", _game.UpgGeladeira);
            RenderUpgradeRow(18, "BALCAO", $"3. Balcão VIP  (Nv.{_game.UpgBalcao}) [+Gorjeta e Reputação]", _game.UpgBalcao);
            RenderUpgradeRow(20, "LOGISTICA", $"4. Logística   (Nv.{_game.UpgLogistica}) [Abre {_game.InspecoesCaixaMaximas} caixas/entrega]", _game.UpgLogistica);

            SafeWrite(2, 23, $"Salários ({_game.MultiplicadorSalario * 100:F0}%):");
            AddButton(22, 23, " [-10%] ", ConsoleColor.DarkRed, ConsoleColor.White, () => _game.AjustarSalario(-0.10));
            AddButton(32, 23, " [+10%] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AjustarSalario(0.10));

            SafeWrite(48, 23, $"Preço Cardápio ({_game.MultiplicadorPrecoPrato * 100:F0}%):");
            AddButton(74, 23, " [-10%] ", ConsoleColor.DarkYellow, ConsoleColor.Black, () => _game.AjustarPrecoPratos(-0.10));
            AddButton(84, 23, " [+10%] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AjustarPrecoPratos(0.10));
        }

        private void RenderUpgradeRow(int y, string codigo, string descricao, int nivelAtual)
        {
            double custo = _game.ObterCustoUpgradeEspecifico(nivelAtual);
            SafeWrite(2, y, $"{descricao,-48} (R$ {custo:F0})");
            AddButton(62, y, " [ +1 ] ", ConsoleColor.DarkMagenta, ConsoleColor.White, () => _game.ComprarUpgradeNomeado(codigo, 1));
            AddButton(72, y, " [ +5 ] ", ConsoleColor.DarkMagenta, ConsoleColor.White, () => _game.ComprarUpgradeNomeado(codigo, 5));
            AddButton(82, y, " [ +MÁX ] ", ConsoleColor.DarkMagenta, ConsoleColor.White, () => _game.ComprarUpgradeNomeado(codigo, 999));
        }

        private void RenderBanco()
        {
            SafeWrite(2, 6, $"BANCO EMPRESARIAL | Limite de Crédito (por Reputação {_game.Reputacao:F1}*): R$ {_game.LimiteCreditoBanco:F2}", ConsoleColor.Yellow);
            SafeWrite(2, 8, $"Dívida Total: R$ {_game.DividaTotalBanco:F2} | Prazo Restante: {_game.MesesRestantesDivida} meses | Parcela Mensal: R$ {_game.ParcelaMensalBanco:F2}");
            SafeWrite(2, 9, "Atenção: Se não houver saldo positivo na virada do mês, a parcela NÃO é paga, soma +5% de mora e leva à execução no mês 12!", ConsoleColor.Red);

            AddButton(2, 11, " [ + TOMAR R$ 1.500 (Paga R$ 2.000) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.PedirEmprestimo(1500));
            AddButton(42, 11, " [ + TOMAR R$ 4.500 (Paga R$ 6.000) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.PedirEmprestimo(4500));
            AddButton(2, 13, " [ + TOMAR R$ 15.000 (Paga R$ 20.000) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.PedirEmprestimo(15000));

            SafeWrite(2, 16, "AMORTIZAÇÃO ANTECIPADA (Garante a quitação antes de vencer os 12 meses):", ConsoleColor.Green);
            AddButton(2, 18, " [ PAGAR R$ 500 ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AmortizarDivida(500));
            AddButton(23, 18, " [ PAGAR R$ 2.000 ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AmortizarDivida(2000));
            AddButton(46, 18, $" [ QUITAR TUDO (R$ {_game.DividaTotalBanco:F2}) ] ", ConsoleColor.Green, ConsoleColor.Black, () => _game.AmortizarDivida(_game.DividaTotalBanco));
        }

        private void RenderModalTribunal()
        {
            var proc = _game.ProcessoAtivo;
            SafeWrite(8, 6, " ==================== INTIMAÇÃO JUDICIAL - TRIBUNAL ==================== ", ConsoleColor.White, ConsoleColor.DarkRed);
            SafeWrite(8, 8, $"PROCESSO: {proc.Titulo}", ConsoleColor.Red);
            SafeWrite(8, 9, proc.Descricao, ConsoleColor.White);
            SafeWrite(8, 11, $"Valor da Causa (1.0x): R$ {proc.ValorCausa:F2}", ConsoleColor.Yellow);
            SafeWrite(8, 13, $">>> CHANCE DE VITÓRIA NO JULGAMENTO: {proc.ChanceDeGanhar}% (Acima de 65% compensa ir a julgamento!) <<<", ConsoleColor.Cyan);

            SafeWrite(8, 15, $"1. Acordo Extrajudicial (45%): Paga R$ {proc.ValorAcordo:F2} e encerra o processo.");
            SafeWrite(8, 16, $"2. Ir a Julgamento: Advogado custa 10% (R$ {proc.CustoAdvogado:F2}). Se vencer, paga R$ 0 de condenação!");

            AddButton(8, 19, $" [ FAZER ACORDO (R$ {proc.ValorAcordo:F2}) ] ", ConsoleColor.DarkYellow, ConsoleColor.Black, () => _game.FazerAcordoJudicial());
            AddButton(48, 19, $" [ IR A JULGAMENTO ({proc.ChanceDeGanhar}% Chance) ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.IrParaJulgamento());
        }

        private void RenderFooter()
        {
            SafeWrite(0, 27, new string(' ', ScreenWidth), ConsoleColor.Black, ConsoleColor.DarkGray);
            SafeWrite(2, 27, $"STATUS: {_game.UltimaMensagem}", ConsoleColor.Black, ConsoleColor.DarkGray);
            SafeWrite(2, 28, "Teclado: [1-6] Abas | [Tab/Setas] Navegar Botões | [Espaço/Enter] Clicar | [Z] Desfazer | [S/L] Save/Load | [ESC] Sair", ConsoleColor.DarkGray);
        }

        private void RenderEndScreen()
        {
            ConsoleColor corTitulo = _game.JogoVencido ? ConsoleColor.Green : ConsoleColor.Red;
            string titulo = _game.JogoVencido ? "=== VITÓRIA! IMPÉRIO GASTRONÔMICO CONCLUÍDO ===" : "=== FIM DE JOGO (GAME OVER) ===";

            SafeWrite(15, 8, titulo, corTitulo);
            SafeWrite(15, 10, _game.MotivoFimDeJogo, ConsoleColor.White);
            SafeWrite(15, 12, $"Pontuação Final: {_game.CalcularPontuacaoFinal()} pts | Recorde: {_game.RecordePontuacao} pts", ConsoleColor.Yellow);
            SafeWrite(15, 13, $"Nível: {_game.NivelAtual} | Reputação: {_game.Reputacao:F1}* | Pratos Perfeitos: {_game.PedidosPerfeitosTotal} | Diversão Run: {_game.Diversao}");

            AddButton(15, 16, " [ JOGAR DE NOVO (NOVA RUN) ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () =>
            {
                _game.IniciarNovaRun();
                Render();
            });

            AddButton(49, 16, " [ CARREGAR SAVE ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () =>
            {
                _game.CarregarJogo();
                Render();
            });

            AddButton(71, 16, " [ SAIR (ESC) ] ", ConsoleColor.DarkRed, ConsoleColor.White, () => Environment.Exit(0));
        }

        public void HandleMouseMove(int x, int y)
        {
            if (x == HoverX && y == HoverY) return;
            HoverX = x;
            HoverY = y;
            Render();
        }

        public void HandleMouseClick(int x, int y)
        {
            foreach (var btn in BotoesAtivos.ToList())
            {
                if (btn.Contains(x, y))
                {
                    btn.OnClick?.Invoke();
                    Render();
                    break;
                }
            }
        }

        public void HandleKeyPress(ConsoleKey key)
        {
            if (key == ConsoleKey.Escape)
            {
                Console.ResetColor();
                Console.Clear();
                Console.CursorVisible = true;
                Environment.Exit(0);
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
                case ConsoleKey.D1: case ConsoleKey.NumPad1: AbaAtual = ScreenTab.Cozinha; IndiceBotaoFocadoTeclado = -1; Render(); break;
                case ConsoleKey.D2: case ConsoleKey.NumPad2: AbaAtual = ScreenTab.Entregas; IndiceBotaoFocadoTeclado = -1; Render(); break;
                case ConsoleKey.D3: case ConsoleKey.NumPad3: AbaAtual = ScreenTab.LivroReceitas; IndiceBotaoFocadoTeclado = -1; Render(); break;
                case ConsoleKey.D4: case ConsoleKey.NumPad4: AbaAtual = ScreenTab.EstoqueCatalogo; IndiceBotaoFocadoTeclado = -1; Render(); break;
                case ConsoleKey.D5: case ConsoleKey.NumPad5: AbaAtual = ScreenTab.Administracao; IndiceBotaoFocadoTeclado = -1; Render(); break;
                case ConsoleKey.D6: case ConsoleKey.NumPad6: AbaAtual = ScreenTab.Banco; IndiceBotaoFocadoTeclado = -1; Render(); break;
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