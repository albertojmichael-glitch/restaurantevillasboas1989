using System;
using System.Collections.Generic;

namespace PizzariaTycoon
{
    public enum ScreenTab
    {
        Cozinha,
        Entregas,
        CatalogoOficial,
        Administracao,
        Banco
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

    public class ConsoleUI
    {
        private readonly GameEngine _game;
        public ScreenTab AbaAtual { get; private set; } = ScreenTab.Cozinha;
        public List<ClickableZone> BotoesAtivos { get; private set; } = new List<ClickableZone>();

        public ConsoleUI(GameEngine game)
        {
            _game = game;
        }

        private void AddButton(int x, int y, string label, ConsoleColor bg, ConsoleColor fg, Action action)
        {
            Console.SetCursorPosition(x, y);
            Console.BackgroundColor = bg;
            Console.ForegroundColor = fg;
            Console.Write(label);
            Console.ResetColor();

            BotoesAtivos.Add(new ClickableZone
            {
                X = x,
                Y = y,
                Width = label.Length,
                Height = 1,
                OnClick = action
            });
        }

        public void Render()
        {
            BotoesAtivos.Clear();
            Console.ResetColor();
            Console.Clear();

            if (_game.GameOver)
            {
                RenderGameOver();
                return;
            }

            RenderHeader();

            // Se houver um processo ativo no Tribunal, exibe o modal prioritário com a chance 30%-70%
            if (_game.ProcessoAtivo != null)
            {
                RenderModalTribunal();
                RenderFooter();
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
                case ScreenTab.CatalogoOficial:
                    RenderCatalogo();
                    break;
                case ScreenTab.Administracao:
                    RenderAdministracao();
                    break;
                case ScreenTab.Banco:
                    RenderBanco();
                    break;
            }

            RenderFooter();
        }

        private void RenderHeader()
        {
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.SetCursorPosition(0, 0);
            Console.Write(new string(' ', 118));
            Console.SetCursorPosition(2, 0);
            Console.Write($"PIZZARIA TYCOON | Nivel {_game.NivelAtual}: {_game.ConfigAtual.Titulo} | Data: {_game.DataHoje} | Run Fun (diversao): {_game.Diversao}");

            Console.SetCursorPosition(0, 1);
            Console.Write(new string(' ', 118));
            Console.SetCursorPosition(2, 1);
            Console.Write($"CAIXA: R$ {_game.Dinheiro:F2} | Despesa Mensal: R$ {_game.CalcularDespesaMensalTotal():F2} | Func: {_game.Funcionarios} | Limp: {_game.Limpeza} | Upg: {_game.Upgrades}");
            Console.ResetColor();

            // Botão de Virar o Mês no topo direito
            bool podeVirar = _game.TarefasConcluidasNoMes >= 10;
            ConsoleColor corVirar = podeVirar ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray;
            AddButton(88, 1, $"[ VIRAR MES ({_game.TarefasConcluidasNoMes}/10) ]", corVirar, ConsoleColor.White, () =>
            {
                _game.VirarMes();
            });
        }

        private void RenderNavigationTabs()
        {
            int y = 3;
            AddButton(2, y, " [ 1. COZINHA (PEDIDOS) ] ", AbaAtual == ScreenTab.Cozinha ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.Cozinha);
            AddButton(29, y, " [ 2. ENTREGAS (CAIXA) ] ", AbaAtual == ScreenTab.Entregas ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.Entregas);
            AddButton(55, y, " [ 3. LISTA OFICIAL ] ", AbaAtual == ScreenTab.CatalogoOficial ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.CatalogoOficial);
            AddButton(78, y, " [ 4. GERENCIA ] ", AbaAtual == ScreenTab.Administracao ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.Administracao);
            AddButton(96, y, " [ 5. BANCO ] ", AbaAtual == ScreenTab.Banco ? ConsoleColor.Cyan : ConsoleColor.DarkGray, ConsoleColor.Black, () => AbaAtual = ScreenTab.Banco);
        }

        private void RenderCozinha()
        {
            var pedido = _game.PedidoAtual;
            Console.SetCursorPosition(2, 5);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"PEDIDO DO CLIENTE (Valor estimado: R$ {_game.CalcularValorVendaPrato():F2}):");
            Console.ResetColor();

            Console.SetCursorPosition(2, 6);
            Console.Write(pedido.DescricaoCliente);

            Console.SetCursorPosition(2, 8);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("SEU PRATO ATUAL: ");
            Console.ResetColor();
            Console.Write(pedido.PratoMontado.Count > 0 ? string.Join(" -> ", pedido.PratoMontado) : "(Vazio - clique nos ingredientes abaixo na ordem exata)");

            AddButton(2, 10, " [ ENTREGAR PEDIDO ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.EntregarPedido());
            AddButton(25, 10, " [ LIMPAR BANCADA ] ", ConsoleColor.DarkRed, ConsoleColor.White, () => _game.LimparPratoAtual());

            Console.SetCursorPosition(2, 12);
            Console.Write("CLIQUE NOS INGREDIENTES DISPONIVEIS:");

            var ingredientes = _game.ObterIngredientesDesbloqueados();
            int col = 2;
            int row = 14;

            foreach (var ing in ingredientes)
            {
                string btnText = $"[{ing}]";
                if (col + btnText.Length > 115)
                {
                    col = 2;
                    row += 2;
                }

                string ingCopy = ing;
                AddButton(col, row, btnText, ConsoleColor.DarkCyan, ConsoleColor.White, () =>
                {
                    _game.AdicionarIngredienteAoPrato(ingCopy);
                });

                col += btnText.Length + 2;
            }
        }

        private void RenderEntregas()
        {
            Console.SetCursorPosition(2, 5);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"INSPECAO DE ENTREGA (7 Itens) - Data Correta: {_game.DataHoje} | Cidades Validas: Sao Paulo, Curitiba, Rio de Janeiro");
            Console.ResetColor();

            Console.SetCursorPosition(2, 6);
            Console.Write("Fornecedores Autorizados: AgroFoods Brasil, Distribuidora Sul, Laticinios Real, Horta Fresca SA");

            Console.SetCursorPosition(2, 8);
            Console.Write("ID | NOTA FISCAL            | ITEM FISICO NA CAIXA   | FORNECEDOR              | CIDADE          | DATA       | STATUS");
            Console.SetCursorPosition(2, 9);
            Console.Write(new string('-', 114));

            for (int i = 0; i < _game.EntregaAtual.Count; i++)
            {
                var item = _game.EntregaAtual[i];
                int y = 10 + (i * 2);

                Console.SetCursorPosition(2, y);
                Console.Write($"{i + 1,2} | {item.NomeNota,-22} | {item.NomeFisico,-22} | {item.Fornecedor,-23} | {item.Cidade,-15} | {item.DataRecebimento,-10} | ");

                int idx = i;
                if (item.MarcadoComoValidoPeloJogador)
                {
                    AddButton(103, y, "[ APROVADO ]", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AlternarAprovacaoItemEntrega(idx));
                }
                else
                {
                    AddButton(103, y, "[ REJEITADO ]", ConsoleColor.DarkRed, ConsoleColor.White, () => _game.AlternarAprovacaoItemEntrega(idx));
                }
            }

            AddButton(2, 25, " [ FINALIZAR CONFERENCIA DA CAIXA ] ", ConsoleColor.Blue, ConsoleColor.White, () =>
            {
                _game.FinalizarConferenciaEntrega();
            });
        }

        private void RenderCatalogo()
        {
            Console.SetCursorPosition(2, 5);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("LISTA OFICIAL DE ITENS DE ENTREGA (32 Itens Validos - Cuidado com erros de digitacao e itens intrusos!):");
            Console.ResetColor();

            int col = 2;
            int row = 7;
            foreach (var item in Catalog.ItensEntregaValidos)
            {
                Console.SetCursorPosition(col, row);
                Console.Write($"- {item}");
                row++;
                if (row > 22)
                {
                    row = 7;
                    col += 30;
                }
            }
        }

        private void RenderAdministracao()
        {
            var cfg = _game.ConfigAtual;
            Console.SetCursorPosition(2, 5);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"PAINEL DE GERENCIAMENTO - NIVEL {_game.NivelAtual} ({cfg.Titulo})");
            Console.ResetColor();

            // Requisitos do próximo nível
            if (_game.NivelAtual < 5)
            {
                var prox = Catalog.Levels[_game.NivelAtual];
                Console.SetCursorPosition(2, 7);
                Console.Write($"Requisitos p/ Nivel {_game.NivelAtual + 1} ({prox.Titulo} - Bonus R$ {prox.BonusNivel:F2}): " +
                              $"Func: {_game.Funcionarios}/{prox.MinFuncionarios} | Limpeza: {_game.Limpeza}/{prox.MinLimpeza} | Upgrades: {_game.Upgrades}/{prox.MinUpgrades}");
            }
            else
            {
                Console.SetCursorPosition(2, 7);
                Console.Write("Voce atingiu o NIVEL MAXIMO (Restaurante Elite Global)! Expansao ilimitada liberada.");
            }

            // Botões de compra
            AddButton(2, 10, $" [ + CONTRATAR FUNCIONARIO (R$ {cfg.CustoPorFuncionario:F2}) ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.ContratarFuncionario());
            AddButton(52, 10, $" [ + MELHORAR LIMPEZA (R$ {cfg.CustoPorLimpeza:F2}) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.ComprarLimpeza());
            AddButton(2, 12, $" [ + COMPRAR UPGRADE DO RESTAURANTE (R$ {cfg.CustoUpgrade:F2}) ] ", ConsoleColor.DarkMagenta, ConsoleColor.White, () => _game.ComprarUpgrade());

            // Salários
            Console.SetCursorPosition(2, 15);
            Console.Write($"Nivel Salarial Atual: {(_game.MultiplicadorSalario * 100):F0}% (Custo mensal por func: R$ {(cfg.CustoPorFuncionario * _game.MultiplicadorSalario):F2})");
            AddButton(2, 16, " [ - REDUZIR SALARIOS (-10%) ] ", ConsoleColor.DarkRed, ConsoleColor.White, () => _game.AjustarSalario(-0.10));
            AddButton(36, 16, " [ + AUMENTAR SALARIOS (+10%) ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AjustarSalario(0.10));

            // Preço dos Pratos
            Console.SetCursorPosition(2, 19);
            Console.Write($"Preco dos Pratos: {(_game.MultiplicadorPrecoPrato * 100):F0}% (Valor de venda medio atual: R$ {_game.CalcularValorVendaPrato():F2})");
            AddButton(2, 20, " [ - BAIXAR PRECO DOS PRATOS (-10%) ] ", ConsoleColor.DarkYellow, ConsoleColor.Black, () => _game.AjustarPrecoPratos(-0.10));
            AddButton(42, 20, " [ + SUBIR PRECO DOS PRATOS (+10%) ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AjustarPrecoPratos(0.10));
        }

        private void RenderBanco()
        {
            Console.SetCursorPosition(2, 5);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("BANCO CENTRAL DO RESTAURANTE (Prazo: 12 Meses | Juros Fixos: +1/3 do valor solicitado)");
            Console.ResetColor();

            Console.SetCursorPosition(2, 7);
            Console.Write($"Divida Total Ativa: R$ {_game.DividaTotalBanco:F2} | Meses Restantes: {_game.MesesRestantesDivida} | Parcela Mensal: R$ {_game.ParcelaMensalBanco:F2}");

            AddButton(2, 10, " [ PEDIR R$ 1.500 (Paga R$ 2.000) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.PedirEmprestimo(1500));
            AddButton(40, 10, " [ PEDIR R$ 4.500 (Paga R$ 6.000) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.PedirEmprestimo(4500));
            AddButton(2, 12, " [ PEDIR R$ 12.000 (Paga R$ 16.000) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.PedirEmprestimo(12000));
            AddButton(42, 12, " [ PEDIR R$ 30.000 (Paga R$ 40.000) ] ", ConsoleColor.DarkCyan, ConsoleColor.White, () => _game.PedirEmprestimo(30000));

            AddButton(2, 15, $" [ QUITAR DIVIDA TOTAL AGORA (R$ {_game.DividaTotalBanco:F2}) ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () => _game.AmortizarDividaTotal());
        }

        private void RenderModalTribunal()
        {
            var proc = _game.ProcessoAtivo;
            Console.SetCursorPosition(10, 6);
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" ==================== INTIMACAO JUDICIAL - TRIBUNAL ==================== ");
            Console.ResetColor();

            Console.SetCursorPosition(10, 8);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"PROCESSO: {proc.Titulo}");
            Console.ResetColor();

            Console.SetCursorPosition(10, 9);
            Console.Write(proc.Descricao);

            Console.SetCursorPosition(10, 11);
            Console.Write($"Valor Original da Causa: R$ {proc.ValorCausa:F2}");

            Console.SetCursorPosition(10, 13);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($">>> CHANCE DE GANHAR NO JULGAMENTO (calculada pelos advogados): {proc.ChanceDeGanhar}% <<<");
            Console.ResetColor();

            Console.SetCursorPosition(10, 15);
            Console.Write($"Opcao 1 (Acordo): Paga R$ {proc.ValorAcordo:F2} garantido e encerra o caso sem riscos.");
            Console.SetCursorPosition(10, 16);
            Console.Write($"Opcao 2 (Julgamento): Custa R$ {proc.CustoAdvogado:F2} de advogado. Se vencer ({proc.ChanceDeGanhar}%), nao paga a multa!");

            AddButton(10, 19, $" [ FAZER ACORDO (Pagar R$ {proc.ValorAcordo:F2}) ] ", ConsoleColor.DarkYellow, ConsoleColor.Black, () =>
            {
                _game.FazerAcordoJudicial();
            });

            AddButton(52, 19, $" [ IR A JULGAMENTO ({proc.ChanceDeGanhar}% de Vitoria) ] ", ConsoleColor.DarkGreen, ConsoleColor.White, () =>
            {
                _game.IrParaJulgamento();
            });
        }

        private void RenderFooter()
        {
            Console.SetCursorPosition(0, 27);
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write(new string(' ', 118));
            Console.SetCursorPosition(2, 27);
            Console.Write($"STATUS: {_game.UltimaMensagem}");
            Console.ResetColor();
        }

        private void RenderGameOver()
        {
            Console.SetCursorPosition(20, 10);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("==================== FIM DE JOGO (GAME OVER) ====================");
            Console.ResetColor();

            Console.SetCursorPosition(20, 12);
            Console.Write(_game.MotivoGameOver);

            Console.SetCursorPosition(20, 14);
            Console.Write($"Nivel Alcancado: {_game.NivelAtual} | Meses Sobrevividos: {_game.MesAtual} | Variavel Diversao da Run: {_game.Diversao}");

            AddButton(20, 17, " [ FECHAR JOGO ] ", ConsoleColor.Red, ConsoleColor.White, () => Environment.Exit(0));
        }

        public void HandleMouseClick(int x, int y)
        {
            foreach (var btn in BotoesAtivos)
            {
                if (btn.Contains(x, y))
                {
                    btn.OnClick?.Invoke();
                    Render();
                    break;
                }
            }
        }
    }
}