using System;
using System.Collections.Generic;
using System.Linq;

namespace PizzariaTycoon
{
    public class CustomerOrder
    {
        public DishRecipe Receita { get; set; }
        public List<string> OrdemExigida { get; set; } = new List<string>();
        public List<string> IngredientesExcluidos { get; set; } = new List<string>();
        public List<string> PratoMontado { get; set; } = new List<string>();
        public string DescricaoCliente { get; set; }
    }

    public class DeliveryItemRecord
    {
        public string NomeNota { get; set; }
        public string NomeFisico { get; set; }
        public string Fornecedor { get; set; }
        public string Cidade { get; set; }
        public string DataRecebimento { get; set; }
        public bool EValidoReal { get; set; }
        public bool MarcadoComoValidoPeloJogador { get; set; } = true;
        public string MotivoErro { get; set; }
    }

    public class LawsuitEvent
    {
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public double ValorCausa { get; set; }
        public double ValorAcordo => Math.Round(ValorCausa * 0.5, 2);
        public double CustoAdvogado => Math.Round(ValorCausa * 0.2, 2);
        public int ChanceDeGanhar { get; set; } // Sorteado entre 30 e 70 antes da decisão
    }

    public class GameEngine
    {
        private readonly Random _rng = new Random();

        // Variável 'diversao' (0 a 100) sorteada a cada nova run, sem uso ativo
        public int Diversao { get; private set; }

        // Estado Geral
        public int NivelAtual { get; private set; } = 1;
        public int MesAtual { get; private set; } = 1;
        public string DataHoje => $"15/{MesAtual:D2}/2026";
        public double Dinheiro { get; private set; } = 1000.0;
        public int TarefasConcluidasNoMes { get; private set; } = 0;
        public bool GameOver { get; private set; } = false;
        public string MotivoGameOver { get; private set; } = "";
        public string UltimaMensagem { get; set; } = "Bem-vindo a sua Pizzaria! Use o mouse para clicar nos botoes.";

        // Recursos e Administração
        public int Funcionarios { get; private set; } = 0;
        public int Limpeza { get; private set; } = 2;
        public int Upgrades { get; private set; } = 0;
        public double MultiplicadorSalario { get; private set; } = 1.0; // 1.0 = 100%
        public double MultiplicadorPrecoPrato { get; private set; } = 1.0; // 1.0 = 100%

        // Banco (Empréstimos)
        public double DividaTotalBanco { get; private set; } = 0.0;
        public int MesesRestantesDivida { get; private set; } = 0;
        public double ParcelaMensalBanco => MesesRestantesDivida > 0 ? Math.Round(DividaTotalBanco / MesesRestantesDivida, 2) : 0;

        // Tarefas Ativas
        public CustomerOrder PedidoAtual { get; private set; }
        public List<DeliveryItemRecord> EntregaAtual { get; private set; }
        public LawsuitEvent ProcessoAtivo { get; private set; }

        public GameEngine()
        {
            Diversao = _rng.Next(0, 101); // 0 a 100
            GerarNovoPedido();
            GerarNovaEntrega();
        }

        public LevelConfig ConfigAtual => Catalog.Levels[NivelAtual - 1];

        public List<string> ObterIngredientesDesbloqueados()
        {
            var lista = new List<string>();
            for (int n = 1; n <= NivelAtual; n++)
            {
                lista.AddRange(Catalog.IngredientesPorNivel[n]);
            }
            return lista.Distinct().ToList();
        }

        public double CalcularValorVendaPrato()
        {
            double basePrice = ConfigAtual.PrecoMedioPrato * MultiplicadorPrecoPrato;
            double bonusFuncionarios = Funcionarios * (ConfigAtual.CustoPorFuncionario * 0.15) * MultiplicadorSalario;
            double bonusUpgrade = Upgrades > 0 ? _rng.Next(5, 21) * (1 + (Upgrades * 0.1)) : 0;
            return Math.Round(basePrice + bonusFuncionarios + bonusUpgrade, 2);
        }

        public double CalcularDespesaMensalTotal()
        {
            double salFunc = Funcionarios * ConfigAtual.CustoPorFuncionario * MultiplicadorSalario;
            double custoLimp = Limpeza * (ConfigAtual.CustoPorLimpeza * 0.25);
            return Math.Round(ConfigAtual.CustoFixoMensal + salFunc + custoLimp + ParcelaMensalBanco, 2);
        }

        // ================== COZINHA (PEDIDOS) ==================
        public void GerarNovoPedido()
        {
            var pratosDisponiveis = Catalog.Pratos.Where(p => p.NivelDesbloqueio <= NivelAtual).ToList();
            var receita = pratosDisponiveis[_rng.Next(pratosDisponiveis.Count)];

            var variaveisEmbaralhadas = receita.IngredientesVariaveis
                .OrderBy(_ => _rng.Next())
                .ToList();

            // Escolhe 1 ou 2 ingredientes para o cliente pedir "SEM"
            int qtdSem = _rng.Next(1, 3);
            var excluidos = variaveisEmbaralhadas.Take(qtdSem).ToList();
            var escolhidosCliente = variaveisEmbaralhadas.Skip(qtdSem).Take(4).ToList();

            var ordemFinal = new List<string> { receita.IngredienteBaseTopo };
            ordemFinal.AddRange(escolhidosCliente);
            if (!string.IsNullOrEmpty(receita.IngredienteBaseFundo))
            {
                ordemFinal.Add(receita.IngredienteBaseFundo);
            }

            string textoSem = excluidos.Count > 0 ? $" SEM {string.Join(" e SEM ", excluidos)}" : "";
            string desc = $"Quero {receita.Nome}: [{string.Join(" -> ", ordemFinal)}]{textoSem}!";

            PedidoAtual = new CustomerOrder
            {
                Receita = receita,
                OrdemExigida = ordemFinal,
                IngredientesExcluidos = excluidos,
                DescricaoCliente = desc
            };
        }

        public void AdicionarIngredienteAoPrato(string ingrediente)
        {
            if (PedidoAtual == null) return;
            PedidoAtual.PratoMontado.Add(ingrediente);
            UltimaMensagem = $"Ingrediente adicionado: {ingrediente}";
        }

        public void LimparPratoAtual()
        {
            PedidoAtual?.PratoMontado.Clear();
            UltimaMensagem = "Bancada limpa! Monte o prato novamente.";
        }

        public void EntregarPedido()
        {
            if (PedidoAtual == null) return;

            bool acertouOrdem = PedidoAtual.PratoMontado.SequenceEqual(PedidoAtual.OrdemExigida);

            if (acertouOrdem)
            {
                double ganho = CalcularValorVendaPrato();
                Dinheiro += ganho;
                TarefasConcluidasNoMes++;
                UltimaMensagem = $"PEDIDO PERFEITO! Voce ganhou R$ {ganho:F2}.";
            }
            else
            {
                Dinheiro -= 100.0;
                TarefasConcluidasNoMes++;
                UltimaMensagem = "PEDIDO ERRado! Ordem ou ingredientes incorretos (-R$ 100,00).";

                // Risco especial do Baiacu no Nível 5
                if (PedidoAtual.Receita.Nome == "Baiacu" && _rng.Next(100) < 60)
                {
                    DispararProcesso(
                        "Intoxicacao por Baiacu!",
                        "Voce errou o preparo do Baiacu e o cliente foi hospitalizado as pressas!",
                        4500.0
                    );
                }
            }

            TestarRiscoProcessoSanitario();
            GerarNovoPedido();
        }

        // ================== ESTOQUE (ENTREGAS) ==================
        public void GerarNovaEntrega()
        {
            // Recebimento desconta R$ 40
            Dinheiro -= 40.0;
            EntregaAtual = new List<DeliveryItemRecord>();

            for (int i = 0; i < 7; i++)
            {
                bool seraValido = _rng.Next(100) < 55;
                string itemBase = Catalog.ItensEntregaValidos[_rng.Next(Catalog.ItensEntregaValidos.Count)];
                string nomeNota = itemBase;
                string nomeFisico = itemBase;
                string fornecedor = Catalog.FornecedoresValidos[_rng.Next(Catalog.FornecedoresValidos.Count)];
                string cidade = Catalog.CidadesValidas[_rng.Next(Catalog.CidadesValidas.Count)];
                string data = DataHoje;
                string motivo = "OK";

                if (!seraValido)
                {
                    int tipoErro = _rng.Next(6);
                    switch (tipoErro)
                    {
                        case 0: // Item intruso inexistente
                            nomeFisico = Catalog.ItensEntregaIntrusos[_rng.Next(Catalog.ItensEntregaIntrusos.Count)];
                            nomeNota = nomeFisico;
                            motivo = "Item intruso (nao pertence ao catalogo)";
                            break;
                        case 1: // Erro de digitação
                            if (Catalog.ErrosDigitacao.ContainsKey(itemBase))
                                nomeFisico = Catalog.ErrosDigitacao[itemBase];
                            else
                                nomeFisico = itemBase + "x";
                            nomeNota = nomeFisico;
                            motivo = "Erro de digitacao no nome";
                            break;
                        case 2: // Divergência entre Nota e Caixa
                            string outro = Catalog.ItensEntregaValidos[_rng.Next(Catalog.ItensEntregaValidos.Count)];
                            if (outro == itemBase) outro = "Agua";
                            nomeFisico = outro;
                            motivo = "Item fisico diverge da Nota";
                            break;
                        case 3: // Cidade inválida
                            cidade = Catalog.CidadesInvalidas[_rng.Next(Catalog.CidadesInvalidas.Count)];
                            motivo = "Cidade invalida";
                            break;
                        case 4: // Fornecedor inválido
                            fornecedor = Catalog.FornecedoresInvalidos[_rng.Next(Catalog.FornecedoresInvalidos.Count)];
                            motivo = "Fornecedor nao autorizado";
                            break;
                        case 5: // Data inválida
                            data = $"01/01/2021";
                            motivo = "Data de recebimento incorreta";
                            break;
                    }
                }

                EntregaAtual.Add(new DeliveryItemRecord
                {
                    NomeNota = nomeNota,
                    NomeFisico = nomeFisico,
                    Fornecedor = fornecedor,
                    Cidade = cidade,
                    DataRecebimento = data,
                    EValidoReal = seraValido,
                    MarcadoComoValidoPeloJogador = true,
                    MotivoErro = motivo
                });
            }
        }

        public void AlternarAprovacaoItemEntrega(int index)
        {
            if (EntregaAtual == null || index < 0 || index >= EntregaAtual.Count) return;
            EntregaAtual[index].MarcadoComoValidoPeloJogador = !EntregaAtual[index].MarcadoComoValidoPeloJogador;
        }

        public void FinalizarConferenciaEntrega()
        {
            if (EntregaAtual == null) return;

            int erros = 0;
            foreach (var item in EntregaAtual)
            {
                if (item.EValidoReal != item.MarcadoComoValidoPeloJogador)
                {
                    erros++;
                }
            }

            double multa = erros * 20.0;
            Dinheiro -= multa;
            TarefasConcluidasNoMes++;

            if (erros == 0)
            {
                double bonusAuditoria = 60.0 + (NivelAtual * 20.0);
                Dinheiro += bonusAuditoria;
                UltimaMensagem = $"CONFERENCIA IMPECAVEL! 0 erros (+R$ {bonusAuditoria:F2} de reembolso/bonus).";
            }
            else
            {
                UltimaMensagem = $"Conferencia concluida com {erros} erro(s). Multa: -R$ {multa:F2}.";
            }

            GerarNovaEntrega();
        }

        // ================== ADMINISTRAÇÃO & RECURSOS ==================
        public void ContratarFuncionario()
        {
            double custoContratacao = ConfigAtual.CustoPorFuncionario;
            if (Dinheiro < custoContratacao)
            {
                UltimaMensagem = "Saldo insuficiente para contratar funcionario!";
                return;
            }
            Dinheiro -= custoContratacao;
            Funcionarios++;
            UltimaMensagem = $"Funcionario contratado! Total: {Funcionarios}.";
            VerificarSubidaDeNivel();
        }

        public void ComprarLimpeza()
        {
            double custo = ConfigAtual.CustoPorLimpeza;
            if (Dinheiro < custo)
            {
                UltimaMensagem = "Saldo insuficiente para investir em limpeza!";
                return;
            }
            Dinheiro -= custo;
            Limpeza++;
            UltimaMensagem = $"Limpeza melhorada! Nivel de limpeza: {Limpeza}.";
            VerificarSubidaDeNivel();
        }

        public void ComprarUpgrade()
        {
            double custo = ConfigAtual.CustoUpgrade;
            if (Dinheiro < custo)
            {
                UltimaMensagem = "Saldo insuficiente para comprar melhoria!";
                return;
            }
            Dinheiro -= custo;
            Upgrades++;
            UltimaMensagem = $"Upgrade comprado! Total de melhorias: {Upgrades}.";
            VerificarSubidaDeNivel();
        }

        public void AjustarSalario(double delta)
        {
            MultiplicadorSalario = Math.Clamp(Math.Round(MultiplicadorSalario + delta, 2), 0.5, 2.0);
            UltimaMensagem = $"Salarios ajustados para {(MultiplicadorSalario * 100):F0}% do valor base.";

            if (delta < 0 && MultiplicadorSalario < 0.85 && Funcionarios > 0)
            {
                if (_rng.Next(100) < 40)
                {
                    Funcionarios--;
                    UltimaMensagem = "Corte salarial excessivo! Um funcionario pediu demissao.";

                    if (_rng.Next(100) < 50)
                    {
                        DispararProcesso(
                            "Processo Trabalhista!",
                            "O ex-funcionario acionou a Justica do Trabalho por reducao salarial abusiva.",
                            600.0 * NivelAtual
                        );
                    }
                }
            }
        }

        public void AjustarPrecoPratos(double delta)
        {
            MultiplicadorPrecoPrato = Math.Clamp(Math.Round(MultiplicadorPrecoPrato + delta, 2), 0.6, 1.8);
            UltimaMensagem = $"Preco do cardapio ajustado para {(MultiplicadorPrecoPrato * 100):F0}% da media.";
        }

        private void VerificarSubidaDeNivel()
        {
            if (NivelAtual >= 5) return;

            var proxConfig = Catalog.Levels[NivelAtual]; // Próximo nível (índice NivelAtual)
            if (Funcionarios >= proxConfig.MinFuncionarios &&
                Limpeza >= proxConfig.MinLimpeza &&
                Upgrades >= proxConfig.MinUpgrades)
            {
                NivelAtual++;
                Dinheiro += proxConfig.BonusNivel;
                UltimaMensagem = $"PARABENS! Voce subiu para o NIVEL {NivelAtual} ({proxConfig.Titulo}) e ganhou R$ {proxConfig.BonusNivel:F2}!";
            }
        }

        // ================== BANCO (EMPRÉSTIMOS) ==================
        public void PedirEmprestimo(double valorSolicitado)
        {
            if (DividaTotalBanco > 0)
            {
                UltimaMensagem = "Quite seu emprestimo atual antes de pedir outro!";
                return;
            }

            double dividaComJuros = Math.Round(valorSolicitado + (valorSolicitado / 3.0), 2);
            Dinheiro += valorSolicitado;
            DividaTotalBanco = dividaComJuros;
            MesesRestantesDivida = 12;
            UltimaMensagem = $"Emprestimo de R$ {valorSolicitado:F2} aprovado! Total a pagar em 12 meses: R$ {dividaComJuros:F2}.";
        }

        public void AmortizarDividaTotal()
        {
            if (DividaTotalBanco <= 0)
            {
                UltimaMensagem = "Voce nao possui dividas ativas no banco.";
                return;
            }
            if (Dinheiro < DividaTotalBanco)
            {
                UltimaMensagem = "Saldo insuficiente para quitar a divida inteira agora!";
                return;
            }

            Dinheiro -= DividaTotalBanco;
            DividaTotalBanco = 0;
            MesesRestantesDivida = 0;
            UltimaMensagem = "Divida bancaria 100% quitada!";
        }

        // ================== TRIBUNAL (PROCESSOS) ==================
        private void TestarRiscoProcessoSanitario()
        {
            if (ProcessoAtivo != null) return;

            int limpezaRecomendada = ConfigAtual.MinLimpeza + 3;
            if (Limpeza < limpezaRecomendada && _rng.Next(100) < 20)
            {
                DispararProcesso(
                    "Autuacao da Vigilancia Sanitaria!",
                    "Fiscais encontraram sujeira acumulada na cozinha durante o expediente.",
                    450.0 * NivelAtual
                );
            }
        }

        public void DispararProcesso(string titulo, string descricao, double valorCausa)
        {
            // Sorteia randomicamente entre 30% e 70% ANTES de o jogador decidir
            int chanceSorteada = _rng.Next(30, 71);

            ProcessoAtivo = new LawsuitEvent
            {
                Titulo = titulo,
                Descricao = descricao,
                ValorCausa = Math.Round(valorCausa, 2),
                ChanceDeGanhar = chanceSorteada
            };
        }

        public void FazerAcordoJudicial()
        {
            if (ProcessoAtivo == null) return;
            double valor = ProcessoAtivo.ValorAcordo;
            Dinheiro -= valor;
            UltimaMensagem = $"Acordo judicial firmado! Voce pagou R$ {valor:F2} e encerrou o processo.";
            ProcessoAtivo = null;
        }

        public void IrParaJulgamento()
        {
            if (ProcessoAtivo == null) return;

            Dinheiro -= ProcessoAtivo.CustoAdvogado;
            int rolagem = _rng.Next(1, 101); // 1 a 100

            if (rolagem <= ProcessoAtivo.ChanceDeGanhar)
            {
                UltimaMensagem = $"VITORIA NO TRIBUNAL! (Chance era {ProcessoAtivo.ChanceDeGanhar}%). Voce pagou apenas o advogado (R$ {ProcessoAtivo.CustoAdvogado:F2}).";
            }
            else
            {
                double condenacao = ProcessoAtivo.ValorCausa * 1.5;
                Dinheiro -= condenacao;
                UltimaMensagem = $"DERROTA NO TRIBUNAL! (Chance era {ProcessoAtivo.ChanceDeGanhar}%). Condenado em R$ {condenacao:F2} + advogado!";
            }

            ProcessoAtivo = null;
        }

        // ================== VIRADA DE MÊS ==================
        public void VirarMes()
        {
            if (TarefasConcluidasNoMes < 10)
            {
                UltimaMensagem = $"Conclua pelo menos 10 entregas/pedidos antes de virar o mes! (Atual: {TarefasConcluidasNoMes}/10)";
                return;
            }

            double despesasSemBanco = ConfigAtual.CustoFixoMensal
                + (Funcionarios * ConfigAtual.CustoPorFuncionario * MultiplicadorSalario)
                + (Limpeza * (ConfigAtual.CustoPorLimpeza * 0.25));

            Dinheiro -= Math.Round(despesasSemBanco, 2);

            if (DividaTotalBanco > 0)
            {
                double parcela = ParcelaMensalBanco;
                Dinheiro -= parcela;
                DividaTotalBanco = Math.Max(0, Math.Round(DividaTotalBanco - parcela, 2));
                MesesRestantesDivida--;

                if (MesesRestantesDivida <= 0 && DividaTotalBanco > 1.0)
                {
                    GameOver = true;
                    MotivoGameOver = "Prazo de 12 meses do emprestimo expirou sem quitacao!";
                    return;
                }
            }

            if (Dinheiro < -1500.0)
            {
                GameOver = true;
                MotivoGameOver = "Falencia decretada! Seu saldo negativo ultrapassou o limite de credito.";
                return;
            }

            MesAtual++;
            TarefasConcluidasNoMes = 0;
            UltimaMensagem = $"Mes {MesAtual} iniciado! Custos mensais e parcelas foram descontados.";
        }
    }
}