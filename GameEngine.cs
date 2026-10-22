using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PizzariaTycoon
{
    public class CustomerOrder
    {
        public DishRecipe Receita { get; set; }
        public List<string> OrdemExigida { get; set; } = new List<string>();
        public List<string> IngredientesExcluidos { get; set; } = new List<string>();
        public List<string> PratoMontado { get; set; } = new List<string>();
        public List<string> OpcoesBancada { get; set; } = new List<string>();
        public string FalaDoCliente { get; set; }
        public bool EClienteCritico { get; set; }
        public int SorteioGorjetaBase { get; set; } // Fixado na criação para o valor nunca oscilar na tela!
    }

    public class DeliveryItemRecord
    {
        public string IngredienteReal { get; set; }
        public string NomeNota { get; set; }
        public string NomeFisico { get; set; }
        public string Fornecedor { get; set; }
        public string Cidade { get; set; }
        public string DataNota { get; set; }
        public string Lote { get; set; }
        public int Quantidade { get; set; }
        public string Temperatura { get; set; }
        public bool CaixaAberta { get; set; } = false;
        public bool EValidoReal { get; set; }
        public bool MarcadoComoValidoPeloJogador { get; set; } = true;
        public string MotivoErro { get; set; }
    }

    public class LawsuitEvent
    {
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public double ValorCausa { get; set; }
        // Ponto de equilíbrio em 55%: abaixo de 55% compensa acordo, acima de 55% compensa julgamento
        public double ValorAcordo => Math.Round(ValorCausa * 0.55, 2);  // 55% da causa
        public double CustoAdvogado => Math.Round(ValorCausa * 0.10, 2); // 10% da causa
        public int ChanceDeGanhar { get; set; }
    }

    public class SaveStateData
    {
        public int Diversao { get; set; }
        public int NivelAtual { get; set; }
        public DateTime DataAtual { get; set; }
        public int DiaSemana { get; set; }
        public int AcoesRestantesDia { get; set; }
        public double Dinheiro { get; set; }
        public double Reputacao { get; set; }
        public int Funcionarios { get; set; }
        public int Limpeza { get; set; }
        public int UpgForno { get; set; }
        public int UpgGeladeira { get; set; }
        public int UpgBalcao { get; set; }
        public int UpgLogistica { get; set; }
        public double MultiplicadorSalario { get; set; }
        public double MultiplicadorPrecoPrato { get; set; }
        public double DividaTotalBanco { get; set; }
        public int MesesRestantesDivida { get; set; }
        public int PedidosPerfeitosTotal { get; set; }
        public int RecordePontuacao { get; set; }
        public Dictionary<string, int> Estoque { get; set; }
        public List<string> PratosAtivos { get; set; }
    }

    public class GameEngine
    {
        private readonly Random _rng = new Random();
        private const string SaveFileName = "savegame.json";
        private const double LimiteFalenciaImediata = -500.0;

        public int Diversao { get; private set; }

        // Calendário Real com DateTime
        public int NivelAtual { get; private set; } = 1;
        public DateTime DataAtual { get; private set; } = new DateTime(2026, 1, 7);
        public int DiaSemana { get; private set; } = 1; // 1 a 4 por mês
        public int MesesJogados { get; private set; } = 1;
        public string DataHojeFormatada => DataAtual.ToString("dd/MM/yyyy");

        // Escassez de Ações Diárias e Mensais
        public int AcoesMaximasDia => 8 + Math.Min(6, UpgForno / 2);
        public int AcoesRestantesDia { get; private set; }
        public int AcoesRealizadasNoMes { get; private set; } = 0;
        public int TarefasConcluidasNoMes { get; private set; } = 0;
        public int LimiteAcoesNoMes => 14 + (TotalUpgrades / 10);

        // Finanças e Reputação
        public double Dinheiro { get; private set; } = 1000.0;
        public double Reputacao { get; private set; } = 3.0;
        public int PedidosPerfeitosTotal { get; private set; } = 0;
        public int RecordePontuacao { get; private set; } = 0;

        // Estado do Jogo
        public bool GameOver { get; private set; } = false;
        public bool JogoVencido { get; private set; } = false;
        public string MotivoFimDeJogo { get; private set; } = "";
        public string UltimaMensagem { get; set; } = "Bem-vindo! Use o mouse ou teclado (1-6, Tab, Setas, Enter).";
        public string EventoDoDiaDescricao { get; private set; } = "Expediente Normal";
        private string _eventoAtivoCodigo = "NORMAL";

        // Recursos e Upgrades Nomeados
        public int Funcionarios { get; private set; } = 0;
        public int Limpeza { get; private set; } = 2;
        public int UpgForno { get; private set; } = 0;
        public int UpgGeladeira { get; private set; } = 0;
        public int UpgBalcao { get; private set; } = 0;
        public int UpgLogistica { get; private set; } = 0;
        public int TotalUpgrades => UpgForno + UpgGeladeira + UpgBalcao + UpgLogistica;
        public int Upgrades => TotalUpgrades;

        public double MultiplicadorSalario { get; private set; } = 1.0;
        public double MultiplicadorPrecoPrato { get; private set; } = 1.0;

        // Estoque e Cardápio
        public Dictionary<string, int> Estoque { get; private set; } = new Dictionary<string, int>();
        public Dictionary<string, int> EstoqueContaminado { get; private set; } = new Dictionary<string, int>();
        public HashSet<string> PratosAtivosNoCardapio { get; private set; } = new HashSet<string>();
        public int CapacidadeMaximaEstoque => 15 + (UpgGeladeira * 8);

        // Banco com Juros Mensais (4% a.m.) e Limite por Reputação
        public double DividaTotalBanco { get; private set; } = 0.0;
        public int MesesRestantesDivida { get; private set; } = 0;
        public double TaxaJurosMensal => 0.04;
        public double LimiteCreditoBanco => Math.Round(Reputacao * NivelAtual * 2000.0, 2);
        public double ParcelaMensalBanco => MesesRestantesDivida > 0 ? Math.Round(DividaTotalBanco / MesesRestantesDivida, 2) : 0;

        // Tarefas Ativas
        public CustomerOrder PedidoAtual { get; private set; }
        public List<DeliveryItemRecord> EntregaAtual { get; private set; }
        public int InspecoesCaixaRestantes { get; private set; }
        public int InspecoesCaixaMaximas => Math.Min(7, 3 + (UpgLogistica / 3));
        public LawsuitEvent ProcessoAtivo { get; private set; }

        public GameEngine()
        {
            IniciarNovaRun();
        }

        public void IniciarNovaRun()
        {
            Diversao = _rng.Next(0, 101);
            NivelAtual = 1;
            DataAtual = new DateTime(2026, 1, 7);
            DiaSemana = 1;
            MesesJogados = 1;
            AcoesRealizadasNoMes = 0;
            TarefasConcluidasNoMes = 0;
            Dinheiro = 1000.0;
            Reputacao = 3.0;
            Funcionarios = 0;
            Limpeza = 2;
            UpgForno = 0;
            UpgGeladeira = 0;
            UpgBalcao = 0;
            UpgLogistica = 0;
            MultiplicadorSalario = 1.0;
            MultiplicadorPrecoPrato = 1.0;
            DividaTotalBanco = 0;
            MesesRestantesDivida = 0;
            PedidosPerfeitosTotal = 0;
            GameOver = false;
            JogoVencido = false;
            ProcessoAtivo = null;

            Estoque.Clear();
            EstoqueContaminado.Clear();
            PratosAtivosNoCardapio.Clear();

            foreach (var ing in Catalog.TodosIngredientes)
            {
                Estoque[ing.Nome] = ing.Nivel == 1 ? 6 : 0;
                EstoqueContaminado[ing.Nome] = 0;
            }

            foreach (var prato in Catalog.Pratos.Where(p => p.NivelDesbloqueio == 1))
            {
                PratosAtivosNoCardapio.Add(prato.Nome);
            }

            AcoesRestantesDia = AcoesMaximasDia;
            SortearEventoDoDia();
            GerarNovoPedido();
            GerarNovaEntrega(cobrarTaxa: false);
        }

        public LevelConfig ConfigAtual => Catalog.Levels[NivelAtual - 1];

        private void ModificarDinheiro(double delta)
        {
            Dinheiro = Math.Round(Dinheiro + delta, 2);
            if (Dinheiro < LimiteFalenciaImediata)
            {
                GameOver = true;
                MotivoFimDeJogo = $"Falência Imediata! Seu saldo (R$ {Dinheiro:F2}) ultrapassou o limite de crédito negativo (R$ {LimiteFalenciaImediata:F2}).";
                RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal());
            }
            else
            {
                VerificarCondicaoVitoria();
            }
        }

        private void RegistrarAcaoNoMes(bool sucesso)
        {
            if (GameOver || JogoVencido) return;

            AcoesRealizadasNoMes++;
            if (sucesso) TarefasConcluidasNoMes++;

            if (AcoesRealizadasNoMes >= LimiteAcoesNoMes)
            {
                DiaSemana = 4;
                AvancarDia();
                if (!GameOver && !JogoVencido)
                {
                    UltimaMensagem = "Limite de ações do mês atingido! O mês virou e as despesas mensais foram cobradas.";
                }
            }
            else
            {
                ConsumirAcao();
            }
        }

        public void TocarSom(bool sucesso)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Console.Beep(sucesso ? 880 : 240, sucesso ? 60 : 140);
                }
            }
            catch { }
        }

        private void AjustarReputacao(double delta)
        {
            Reputacao = Math.Clamp(Math.Round(Reputacao + delta, 2), 0.5, 5.0);
        }

        public int CalcularPontuacaoFinal()
        {
            int pts = (int)(Dinheiro + (NivelAtual * 2500) + (Reputacao * 1500) + (PedidosPerfeitosTotal * 120) - DividaTotalBanco);
            return Math.Max(0, pts);
        }

        // ================== ESCASSEZ, CALENDÁRIO E EVENTOS ==================
        private void ConsumirAcao()
        {
            if (GameOver || JogoVencido) return;
            AcoesRestantesDia--;
            if (AcoesRestantesDia <= 0)
            {
                AvancarDia();
            }
        }

        public void AvancarDia()
        {
            if (GameOver || JogoVencido) return;

            DiaSemana++;
            if (DiaSemana > 4)
            {
                DiaSemana = 1;
                MesesJogados++;
                var proximoMes = DataAtual.AddMonths(1);
                DataAtual = new DateTime(proximoMes.Year, proximoMes.Month, 7);
                ProcessarViradaDeMes();
            }
            else
            {
                DataAtual = DataAtual.AddDays(7);
            }

            if (!GameOver && !JogoVencido)
            {
                AcoesRestantesDia = AcoesMaximasDia;
                SortearEventoDoDia();
            }
        }

        private void SortearEventoDoDia()
        {
            int chanceEvento = 25 + (Diversao / 2);
            if (_rng.Next(100) >= chanceEvento)
            {
                _eventoAtivoCodigo = "NORMAL";
                EventoDoDiaDescricao = $"Expediente Normal (Bônus de Diversão da Run: +{Diversao / 5}% nas gorjetas)";
                return;
            }

            int tipo = _rng.Next(4);
            switch (tipo)
            {
                case 0:
                    _eventoAtivoCodigo = "RUSH";
                    AcoesRestantesDia += 3;
                    EventoDoDiaDescricao = "DIA DE RUSH! (+3 Ações extras hoje!)";
                    break;
                case 1:
                    _eventoAtivoCodigo = "CRITICO";
                    EventoDoDiaDescricao = "VISITA DO CRÍTICO! (Próximo pedido vale +0.5 Estrela e 2x Valor!)";
                    break;
                case 2:
                    _eventoAtivoCodigo = "GREVE";
                    EventoDoDiaDescricao = "GREVE DE CAMINHONEIROS! (Entregas custam R$ 80, mas pratos pagam +20%)";
                    break;
                case 3:
                    _eventoAtivoCodigo = "FESTIVAL";
                    EventoDoDiaDescricao = "FESTIVAL GASTRONÔMICO! (Gorjetas do Balcão e Diversão dobradas hoje!)";
                    break;
            }
        }

        // ================== CARDÁPIO E COZINHA DETERMINÍSTICA ==================
        public void AlternarPratoNoCardapio(string nomePrato)
        {
            if (PratosAtivosNoCardapio.Contains(nomePrato))
            {
                if (PratosAtivosNoCardapio.Count <= 2)
                {
                    UltimaMensagem = "Mantenha pelo menos 2 pratos ativos no cardápio!";
                    return;
                }
                PratosAtivosNoCardapio.Remove(nomePrato);
                UltimaMensagem = $"{nomePrato} removido do cardápio ativo.";
            }
            else
            {
                PratosAtivosNoCardapio.Add(nomePrato);
                UltimaMensagem = $"{nomePrato} ativado no cardápio!";
            }
            GerarNovoPedido();
        }

        public double CalcularValorVendaPrato()
        {
            double basePrice = ConfigAtual.PrecoMedioPrato * MultiplicadorPrecoPrato;

            // Retorno decrescente (Math.Sqrt) evita que 100 funcionários gerem R$ 14.000 por prato
            double fatorSalario = Math.Min(1.25, MultiplicadorSalario);
            double bonusFuncionarios = Math.Sqrt(Funcionarios) * (ConfigAtual.PrecoMedioPrato * 0.18) * fatorSalario;

            int sementeGorjeta = PedidoAtual != null ? PedidoAtual.SorteioGorjetaBase : 12;
            double multFestival = _eventoAtivoCodigo == "FESTIVAL" ? 2.0 : 1.0;
            double fatorDiversao = 1.0 + (Diversao / 500.0);
            double bonusUpgrade = TotalUpgrades > 0
                ? (sementeGorjeta + (UpgBalcao * 2.5) + (TotalUpgrades * 0.4)) * multFestival * fatorDiversao
                : 0;

            double multGreve = _eventoAtivoCodigo == "GREVE" ? 1.20 : 1.0;
            return Math.Round((basePrice + bonusFuncionarios + bonusUpgrade) * multGreve, 2);
        }

        public void GerarNovoPedido()
        {
            var disponiveis = Catalog.Pratos
                .Where(p => p.NivelDesbloqueio <= NivelAtual && PratosAtivosNoCardapio.Contains(p.Nome))
                .ToList();

            if (disponiveis.Count == 0)
            {
                disponiveis = Catalog.Pratos.Where(p => p.NivelDesbloqueio <= NivelAtual).ToList();
            }

            var receita = disponiveis[_rng.Next(disponiveis.Count)];
            var recheioPadrao = new List<string>(receita.IngredientesPadrao);

            int qtdSem = _rng.Next(1, 3);
            var excluidos = recheioPadrao.OrderBy(_ => _rng.Next()).Take(qtdSem).ToList();
            var recheioCliente = recheioPadrao.Where(i => !excluidos.Contains(i)).ToList();

            string instrucaoTroca = "";
            if (recheioCliente.Count >= 2 && _rng.Next(100) < 50)
            {
                int idx = _rng.Next(0, recheioCliente.Count - 1);
                string itemA = recheioCliente[idx];
                string itemB = recheioCliente[idx + 1];
                recheioCliente[idx] = itemB;
                recheioCliente[idx + 1] = itemA;
                instrucaoTroca = $" | Ordem especial: [{itemB}] logo antes de [{itemA}]!";
            }

            var ordemFinal = new List<string> { receita.IngredienteBaseTopo };
            ordemFinal.AddRange(recheioCliente);
            if (!string.IsNullOrEmpty(receita.IngredienteBaseFundo))
            {
                ordemFinal.Add(receita.IngredienteBaseFundo);
            }

            var opcoes = new HashSet<string>(ordemFinal);
            foreach (var ex in excluidos) opcoes.Add(ex);

            var desbloqueados = Catalog.TodosIngredientes
                .Where(i => i.Nivel <= NivelAtual)
                .Select(i => i.Nome)
                .OrderBy(_ => _rng.Next())
                .ToList();

            foreach (var d in desbloqueados)
            {
                if (opcoes.Count >= 14) break;
                opcoes.Add(d);
            }

            bool critico = _eventoAtivoCodigo == "CRITICO";
            string prefixoCritico = critico ? "[CRÍTICO] " : "";
            string textoSem = excluidos.Count > 0 ? $" SEM {string.Join(" e SEM ", excluidos)}" : "";

            PedidoAtual = new CustomerOrder
            {
                Receita = receita,
                OrdemExigida = ordemFinal,
                IngredientesExcluidos = excluidos,
                OpcoesBancada = opcoes.OrderBy(_ => _rng.Next()).ToList(),
                EClienteCritico = critico,
                SorteioGorjetaBase = _rng.Next(5, 21),
                FalaDoCliente = $"{prefixoCritico}\"Quero {receita.Nome} (ordem padrão){textoSem}!\"{instrucaoTroca}"
            };
        }

        public void AdicionarIngredienteAoPrato(string ingrediente)
        {
            if (PedidoAtual == null) return;

            int jaUsadosNoPrato = PedidoAtual.PratoMontado.Count(i => i == ingrediente);
            int disponivelEstoque = Estoque.ContainsKey(ingrediente) ? Estoque[ingrediente] : 0;

            if (disponivelEstoque - jaUsadosNoPrato <= 0)
            {
                TocarSom(false);
                UltimaMensagem = $"FALTA ESTOQUE de [{ingrediente}]! Clique no botão vermelho para compra emergencial ou aprove entregas.";
                return;
            }

            PedidoAtual.PratoMontado.Add(ingrediente);
            UltimaMensagem = $"Ingrediente adicionado: {ingrediente}.";
        }

        public void CompraEmergencialIngrediente(string ingrediente)
        {
            double custo = 35.0 * NivelAtual;
            if (Dinheiro < custo)
            {
                UltimaMensagem = $"Saldo insuficiente para compra emergencial (R$ {custo:F2})!";
                return;
            }
            ModificarDinheiro(-custo);
            Estoque[ingrediente] = (Estoque.ContainsKey(ingrediente) ? Estoque[ingrediente] : 0) + 3;
            UltimaMensagem = $"Compra emergencial: +3 un. de [{ingrediente}] por R$ {custo:F2}!";
        }

        public void DesfazerUltimoIngrediente()
        {
            if (PedidoAtual == null || PedidoAtual.PratoMontado.Count == 0) return;
            string removido = PedidoAtual.PratoMontado[^1];
            PedidoAtual.PratoMontado.RemoveAt(PedidoAtual.PratoMontado.Count - 1);
            UltimaMensagem = $"Desfeito: [{removido}] removido do prato.";
        }

        public void LimparPratoAtual()
        {
            PedidoAtual?.PratoMontado.Clear();
            UltimaMensagem = "Bancada limpa! Monte o prato novamente.";
        }

        public void EntregarPedido()
        {
            if (PedidoAtual == null || GameOver || JogoVencido) return;

            // Elasticidade de demanda por Reputação e por Preço alto (> 110%)
            double precoMaxTolerado = 0.85 + (Reputacao * 0.22);
            if (MultiplicadorPrecoPrato > precoMaxTolerado && _rng.Next(100) < 55)
            {
                AjustarReputacao(-0.20);
                TocarSom(false);
                UltimaMensagem = $"CLIENTE RECLAMOU DO PREÇO CARO ({MultiplicadorPrecoPrato * 100:F0}%) para sua Reputação ({Reputacao:F1}*) e desistiu! (-0.2*)";
                GerarNovoPedido();
                RegistrarAcaoNoMes(sucesso: false);
                return;
            }

            if (MultiplicadorPrecoPrato > 1.10)
            {
                int chanceDesistir = (int)((MultiplicadorPrecoPrato - 1.10) * 150);
                if (_rng.Next(100) < chanceDesistir)
                {
                    AjustarReputacao(-0.10);
                    TocarSom(false);
                    UltimaMensagem = $"Cliente achou o cardápio caro ({MultiplicadorPrecoPrato * 100:F0}%) e foi embora sem comprar!";
                    GerarNovoPedido();
                    RegistrarAcaoNoMes(sucesso: false);
                    return;
                }
            }

            bool usouIngredienteContaminado = false;
            foreach (var ing in PedidoAtual.PratoMontado)
            {
                if (Estoque.ContainsKey(ing) && Estoque[ing] > 0)
                    Estoque[ing]--;

                if (EstoqueContaminado.ContainsKey(ing) && EstoqueContaminado[ing] > 0)
                {
                    EstoqueContaminado[ing]--;
                    usouIngredienteContaminado = true;
                }
            }

            string erroDetalhado = DiagnosticarErroPrato(PedidoAtual);
            bool acertou = false;

            if (erroDetalhado == null && !usouIngredienteContaminado)
            {
                acertou = true;
                double ganho = CalcularValorVendaPrato();
                if (PedidoAtual.EClienteCritico)
                {
                    ganho = Math.Round(ganho * 2.0, 2);
                    AjustarReputacao(+0.50);
                    _eventoAtivoCodigo = "NORMAL";
                }
                else
                {
                    AjustarReputacao(+0.12 + (UpgBalcao * 0.02));
                }

                ModificarDinheiro(ganho);
                PedidosPerfeitosTotal++;
                TocarSom(true);
                UltimaMensagem = $"PEDIDO PERFEITO! +R$ {ganho:F2} | Reputação subiu para {Reputacao:F1}*!";
            }
            else if (usouIngredienteContaminado)
            {
                double multaContam = Math.Round(ConfigAtual.PrecoMedioPrato * 1.5, 2);
                ModificarDinheiro(-multaContam);
                AjustarReputacao(-0.45);
                TocarSom(false);
                UltimaMensagem = $"PRATO ESTRAGADO! Ingrediente contaminado aceito na entrega causou intoxicação (-R$ {multaContam:F2})!";
                DispararProcesso("Intoxicação Alimentar Grave!", "Cliente passou mal por ingrediente vencido/contaminado do estoque.", 600.0 * NivelAtual);
            }
            else
            {
                // Penalidade proporcional ao nível (de R$ 100 no Nv.1 até R$ 3.000 no Nv.5)
                double multaErroPrato = Math.Round(ConfigAtual.PrecoMedioPrato * 1.25, 2);
                ModificarDinheiro(-multaErroPrato);
                AjustarReputacao(PedidoAtual.EClienteCritico ? -0.60 : -0.25);
                TocarSom(false);
                UltimaMensagem = $"PEDIDO ERRADO! (-R$ {multaErroPrato:F2}): {erroDetalhado}";

                if (PedidoAtual.Receita.Nome == "Baiacu" && _rng.Next(100) < 65)
                {
                    DispararProcesso("Envenenamento por Baiacu!", "O corte incorreto liberou toxina letal no prato do cliente!", 4500.0);
                }
            }

            if (!GameOver && !JogoVencido)
            {
                TestarRiscoProcessoSanitario();
                GerarNovoPedido();
                RegistrarAcaoNoMes(sucesso: acertou);
            }
        }

        private string DiagnosticarErroPrato(CustomerOrder pedido)
        {
            var montado = pedido.PratoMontado;
            var exigido = pedido.OrdemExigida;

            foreach (var proibido in pedido.IngredientesExcluidos)
            {
                if (montado.Contains(proibido))
                    return $"Colocou ingrediente proibido pelo cliente: [{proibido}]!";
            }

            foreach (var obrigatorio in exigido)
            {
                if (!montado.Contains(obrigatorio))
                    return $"Faltou colocar o ingrediente: [{obrigatorio}]!";
            }

            if (montado.Count != exigido.Count)
                return $"Quantidade incorreta de itens (colocou {montado.Count}, o prato exigia {exigido.Count}).";

            for (int i = 0; i < exigido.Count; i++)
            {
                if (montado[i] != exigido[i])
                    return $"Ordem incorreta na {i + 1}ª posição: colocou [{montado[i]}], mas o correto era [{exigido[i]}]!";
            }

            return null;
        }

        // ================== ENTREGAS COM DATAS REAIS (DateTime) ==================
        public void GerarNovaEntrega(bool cobrarTaxa = true)
        {
            if (cobrarTaxa)
            {
                double taxa = _eventoAtivoCodigo == "GREVE" ? 80.0 : 40.0;
                ModificarDinheiro(-taxa);
                if (GameOver) return;
            }

            InspecoesCaixaRestantes = InspecoesCaixaMaximas;
            EntregaAtual = new List<DeliveryItemRecord>();

            var ingredientesNivel = Catalog.TodosIngredientes.Where(i => i.Nivel <= NivelAtual).ToList();

            for (int i = 0; i < 7; i++)
            {
                bool seraValido = _rng.Next(100) < 55;
                var info = ingredientesNivel[_rng.Next(ingredientesNivel.Count)];

                string nomeNota = info.Nome;
                string nomeFisico = info.Nome;
                string fornecedor = Catalog.FornecedoresValidos[_rng.Next(Catalog.FornecedoresValidos.Count)];
                string cidade = Catalog.CidadesValidas[_rng.Next(Catalog.CidadesValidas.Count)];
                string dataNota = DataHojeFormatada;
                string lote = $"LT-{_rng.Next(100, 999)}";
                int qtd = _rng.Next(3, 6) + (UpgLogistica / 4);
                string temp = info.ExigeRefrigeracao ? $"{_rng.Next(1, 5)}°C (Frio)" : "22°C (Amb)";
                string motivo = "OK";

                if (!seraValido)
                {
                    int tipoErro = _rng.Next(7);
                    switch (tipoErro)
                    {
                        case 0:
                            nomeFisico = Catalog.ItensEntregaIntrusos[_rng.Next(Catalog.ItensEntregaIntrusos.Count)];
                            nomeNota = nomeFisico;
                            motivo = "Item intruso";
                            break;
                        case 1:
                            nomeNota = Catalog.ErrosDigitacao.ContainsKey(info.Nome) ? Catalog.ErrosDigitacao[info.Nome] : info.Nome + "s";
                            nomeFisico = nomeNota;
                            motivo = "Erro de digitação";
                            break;
                        case 2:
                            var outro = ingredientesNivel[_rng.Next(ingredientesNivel.Count)].Nome;
                            if (outro == info.Nome) outro = "Sal";
                            nomeFisico = outro;
                            motivo = "Item físico diverge da Nota";
                            break;
                        case 3:
                            cidade = Catalog.CidadesInvalidas[_rng.Next(Catalog.CidadesInvalidas.Count)];
                            motivo = "Cidade inválida";
                            break;
                        case 4:
                            fornecedor = Catalog.FornecedoresInvalidos[_rng.Next(Catalog.FornecedoresInvalidos.Count)];
                            motivo = "Fornecedor não autorizado";
                            break;
                        case 5:
                            int sub = _rng.Next(3);
                            DateTime dataFalsa = sub switch
                            {
                                0 => DataAtual.AddDays(1),
                                1 => DataAtual.AddMonths(1),
                                _ => DataAtual.AddYears(-1)
                            };
                            dataNota = dataFalsa.ToString("dd/MM/yyyy");
                            motivo = "Data de recebimento divergente";
                            break;
                        case 6:
                            if (info.ExigeRefrigeracao)
                            {
                                temp = "19°C (Quente!)";
                                motivo = "Refrigeração violada (>5°C)";
                            }
                            else
                            {
                                lote = "FALSO-00";
                                motivo = "Código de Lote inválido";
                            }
                            break;
                    }
                }

                EntregaAtual.Add(new DeliveryItemRecord
                {
                    IngredienteReal = info.Nome,
                    NomeNota = nomeNota,
                    NomeFisico = nomeFisico,
                    Fornecedor = fornecedor,
                    Cidade = cidade,
                    DataNota = dataNota,
                    Lote = lote,
                    Quantidade = qtd,
                    Temperatura = temp,
                    CaixaAberta = false,
                    EValidoReal = seraValido,
                    MarcadoComoValidoPeloJogador = true,
                    MotivoErro = motivo
                });
            }
        }

        public void InspecionarCaixaEntrega(int index)
        {
            if (EntregaAtual == null || index < 0 || index >= EntregaAtual.Count) return;
            if (EntregaAtual[index].CaixaAberta) return;

            if (InspecoesCaixaRestantes <= 0)
            {
                TocarSom(false);
                UltimaMensagem = "Sem inspeções restantes nesta entrega! Melhore o upgrade de LOGÍSTICA.";
                return;
            }

            EntregaAtual[index].CaixaAberta = true;
            InspecoesCaixaRestantes--;
            UltimaMensagem = $"Caixa #{index + 1} aberta! Item Físico, Lote e Temperatura revelados.";
        }

        public void AlternarAprovacaoItemEntrega(int index)
        {
            if (EntregaAtual == null || index < 0 || index >= EntregaAtual.Count) return;
            EntregaAtual[index].MarcadoComoValidoPeloJogador = !EntregaAtual[index].MarcadoComoValidoPeloJogador;
        }

        public void FinalizarConferenciaEntrega()
        {
            if (EntregaAtual == null || GameOver || JogoVencido) return;

            int erros = 0;
            int itensAdicionados = 0;

            foreach (var item in EntregaAtual)
            {
                if (item.EValidoReal != item.MarcadoComoValidoPeloJogador)
                {
                    erros++;
                }

                if (item.MarcadoComoValidoPeloJogador && Estoque.ContainsKey(item.IngredienteReal))
                {
                    Estoque[item.IngredienteReal] = Math.Min(CapacidadeMaximaEstoque, Estoque[item.IngredienteReal] + item.Quantidade);
                    itensAdicionados += item.Quantidade;

                    if (!item.EValidoReal)
                    {
                        EstoqueContaminado[item.IngredienteReal] += 1;
                    }
                }
            }

            if (erros == 0)
            {
                double bonus = 70.0 + (NivelAtual * 25.0);
                ModificarDinheiro(bonus);
                TocarSom(true);
                UltimaMensagem = $"CONFERÊNCIA IMPECÁVEL! +{itensAdicionados} insumos no estoque e +R$ {bonus:F2} de bônus!";
            }
            else
            {
                // Multa proporcional ao nível
                double multaPorItem = 20.0 * NivelAtual;
                double multa = erros * multaPorItem;
                ModificarDinheiro(-multa);
                TocarSom(false);
                UltimaMensagem = $"Conferência teve {erros} erro(s) (-R$ {multa:F2}). Itens inválidos aceitos contaminaram o estoque!";
            }

            if (!GameOver && !JogoVencido)
            {
                GerarNovaEntrega(cobrarTaxa: true);
                RegistrarAcaoNoMes(sucesso: erros == 0);
            }
        }

        // ================== GERÊNCIA E COMPRAS EM LOTE ==================
        public void ContratarFuncionariosLote(int quantidade)
        {
            double custoUnit = ConfigAtual.CustoPorFuncionario;
            int maxPossivel = Math.Max(0, (int)Math.Floor(Dinheiro / custoUnit));
            int qtdReal = Math.Min(quantidade, maxPossivel);

            if (qtdReal <= 0)
            {
                UltimaMensagem = "Saldo insuficiente para contratar funcionário!";
                return;
            }

            ModificarDinheiro(-(qtdReal * custoUnit));
            Funcionarios += qtdReal;
            UltimaMensagem = $"+{qtdReal} Funcionário(s) contratado(s)! Total: {Funcionarios}.";
            VerificarSubidaDeNivel();
        }

        public void ComprarLimpezaLote(int quantidade)
        {
            double custoUnit = ConfigAtual.CustoPorLimpeza;
            int maxPossivel = Math.Max(0, (int)Math.Floor(Dinheiro / custoUnit));
            int qtdReal = Math.Min(quantidade, maxPossivel);

            if (qtdReal <= 0)
            {
                UltimaMensagem = "Saldo insuficiente para investir em limpeza!";
                return;
            }

            ModificarDinheiro(-(qtdReal * custoUnit));
            Limpeza += qtdReal;
            UltimaMensagem = $"+{qtdReal} Nível(is) de Limpeza! Total: {Limpeza}.";
            VerificarSubidaDeNivel();
        }

        public double ObterCustoUpgradeEspecifico(int nivelAtualUpg)
        {
            return Math.Round(ConfigAtual.CustoUpgradeBase * (1.0 + (nivelAtualUpg * 0.12)), 2);
        }

        public void ComprarUpgradeNomeado(string tipo, int lote)
        {
            int comprados = 0;
            for (int i = 0; i < lote; i++)
            {
                int nivelRef = tipo switch
                {
                    "FORNO" => UpgForno,
                    "GELADEIRA" => UpgGeladeira,
                    "BALCAO" => UpgBalcao,
                    _ => UpgLogistica
                };

                double custo = ObterCustoUpgradeEspecifico(nivelRef);
                if (Dinheiro < custo) break;

                ModificarDinheiro(-custo);
                comprados++;

                if (tipo == "FORNO") UpgForno++;
                else if (tipo == "GELADEIRA") UpgGeladeira++;
                else if (tipo == "BALCAO") UpgBalcao++;
                else UpgLogistica++;
            }

            if (comprados == 0)
            {
                UltimaMensagem = "Saldo insuficiente para comprar este upgrade!";
                return;
            }

            UltimaMensagem = $"Upgrade [{tipo}] melhorado +{comprados}x! (Total de Upgrades: {TotalUpgrades})";
            VerificarSubidaDeNivel();
        }

        public void AjustarSalario(double delta)
        {
            MultiplicadorSalario = Math.Clamp(Math.Round(MultiplicadorSalario + delta, 2), 0.5, 2.0);
            UltimaMensagem = $"Salários ajustados para {(MultiplicadorSalario * 100):F0}%.";

            if (delta < 0 && MultiplicadorSalario < 0.85 && Funcionarios > 0 && _rng.Next(100) < 40)
            {
                Funcionarios--;
                AjustarReputacao(-0.15);
                UltimaMensagem = "Corte salarial excessivo fez um funcionário pedir demissão!";
                if (_rng.Next(100) < 55)
                {
                    DispararProcesso("Reclamação Trabalhista!", "Ex-funcionário cobra redução salarial indevida na Justiça do Trabalho.", 650.0 * NivelAtual);
                }
            }
        }

        public void AjustarPrecoPratos(double delta)
        {
            MultiplicadorPrecoPrato = Math.Clamp(Math.Round(MultiplicadorPrecoPrato + delta, 2), 0.6, 1.8);
            UltimaMensagem = $"Preço do cardápio ajustado para {(MultiplicadorPrecoPrato * 100):F0}%.";
        }

        private void VerificarSubidaDeNivel()
        {
            while (NivelAtual < 5)
            {
                var prox = Catalog.Levels[NivelAtual];
                if (Funcionarios >= prox.MinFuncionarios && Limpeza >= prox.MinLimpeza && TotalUpgrades >= prox.MinUpgrades)
                {
                    NivelAtual++;
                    ModificarDinheiro(prox.BonusNivel);

                    foreach (var ing in Catalog.TodosIngredientes.Where(i => i.Nivel == NivelAtual))
                    {
                        Estoque[ing.Nome] = Math.Max(Estoque[ing.Nome], 5);
                    }
                    foreach (var prato in Catalog.Pratos.Where(p => p.NivelDesbloqueio == NivelAtual))
                    {
                        PratosAtivosNoCardapio.Add(prato.Nome);
                    }

                    TocarSom(true);
                    UltimaMensagem = $"SUBIU PARA O NÍVEL {NivelAtual} ({prox.Titulo})! Bônus: +R$ {prox.BonusNivel:F2}!";
                }
                else break;
            }
            VerificarCondicaoVitoria();
        }

        // ================== BANCO COM JUROS MENSAIS SOBRE SALDO DEVEDOR ==================
        public void PedirEmprestimo(double valorSolicitado)
        {
            if (DividaTotalBanco + valorSolicitado > LimiteCreditoBanco)
            {
                UltimaMensagem = $"Crédito negado! Seu limite atual (baseado em {Reputacao:F1}*) é R$ {LimiteCreditoBanco:F2}.";
                return;
            }

            ModificarDinheiro(valorSolicitado);
            DividaTotalBanco = Math.Round(DividaTotalBanco + valorSolicitado, 2); // Sem embutir 1/3 na largada!
            if (MesesRestantesDivida <= 0) MesesRestantesDivida = 12;

            UltimaMensagem = $"Empréstimo de R$ {valorSolicitado:F2} recebido! Juros de 4% ao mês sobre o saldo devedor (quitar antes economiza!).";
        }

        public void AmortizarDivida(double valor)
        {
            if (DividaTotalBanco <= 0)
            {
                UltimaMensagem = "Você não possui dívidas ativas no banco!";
                return;
            }

            double pagamento = Math.Min(valor, DividaTotalBanco);
            if (Dinheiro < pagamento)
            {
                UltimaMensagem = "Saldo em caixa insuficiente para amortizar esse valor!";
                return;
            }

            ModificarDinheiro(-pagamento);
            DividaTotalBanco = Math.Round(DividaTotalBanco - pagamento, 2);
            if (DividaTotalBanco <= 0.5)
            {
                DividaTotalBanco = 0;
                MesesRestantesDivida = 0;
                UltimaMensagem = "Dívida bancária 100% quitada! Você economizou todos os juros futuros.";
            }
            else
            {
                UltimaMensagem = $"Amortizado R$ {pagamento:F2}! Saldo devedor caiu para R$ {DividaTotalBanco:F2}.";
            }
        }

        // ================== TRIBUNAL ==================
        private void TestarRiscoProcessoSanitario()
        {
            if (ProcessoAtivo != null) return;

            int limpezaNecessaria = ConfigAtual.MinLimpeza + (Funcionarios / 2) + (TotalUpgrades / 4) + 3;
            int deficit = limpezaNecessaria - Limpeza;

            if (deficit > 0 && _rng.Next(100) < (deficit * 8))
            {
                DispararProcesso(
                    "Autuação da Vigilância Sanitária!",
                    "A sujeira acumulada pelo movimento do restaurante gerou multa.",
                    ConfigAtual.PrecoMedioPrato * 5.0
                );
            }
        }

        public void DispararProcesso(string titulo, string descricao, double valorCausa)
        {
            int bonusReputacao = (int)((Reputacao - 3.0) * 5);
            int chance = Math.Clamp(_rng.Next(30, 71) + bonusReputacao, 30, 75);

            ProcessoAtivo = new LawsuitEvent
            {
                Titulo = titulo,
                Descricao = descricao,
                ValorCausa = Math.Round(valorCausa, 2),
                ChanceDeGanhar = chance
            };
        }

        public void FazerAcordoJudicial()
        {
            if (ProcessoAtivo == null) return;
            double acordo = ProcessoAtivo.ValorAcordo;
            ProcessoAtivo = null;
            AjustarReputacao(-0.10);
            ModificarDinheiro(-acordo);
            if (!GameOver)
                UltimaMensagem = $"Acordo pago (-R$ {acordo:F2} = 55% da causa). Processo encerrado.";
        }

        public void IrParaJulgamento()
        {
            if (ProcessoAtivo == null) return;
            var proc = ProcessoAtivo;
            ProcessoAtivo = null;

            ModificarDinheiro(-proc.CustoAdvogado);
            if (GameOver) return;

            if (_rng.Next(1, 101) <= proc.ChanceDeGanhar)
            {
                AjustarReputacao(+0.10);
                TocarSom(true);
                UltimaMensagem = $"VITÓRIA NO TRIBUNAL ({proc.ChanceDeGanhar}%)! Pagou apenas os 10% do advogado (R$ {proc.CustoAdvogado:F2}).";
            }
            else
            {
                AjustarReputacao(-0.30);
                TocarSom(false);
                double condenacao = proc.ValorCausa * 1.0; // Condenação de 1.0x
                ModificarDinheiro(-condenacao);
                if (!GameOver)
                    UltimaMensagem = $"DERROTA NO TRIBUNAL ({proc.ChanceDeGanhar}%)! Condenado em 1.0x (R$ {condenacao:F2}) + advogado.";
            }
        }

        // ================== VIRADA DE MÊS E EXECUÇÃO BANCÁRIA REAL ==================
        public double CalcularDespesaMensalFixa()
        {
            double sal = Funcionarios * ConfigAtual.CustoPorFuncionario * MultiplicadorSalario;
            double limp = Limpeza * (ConfigAtual.CustoPorLimpeza * 0.20);
            return Math.Round(ConfigAtual.CustoFixoMensal + sal + limp, 2);
        }

        public void ProcessarViradaDeMes()
        {
            AcoesRealizadasNoMes = 0;
            TarefasConcluidasNoMes = 0;
            Limpeza = Math.Max(0, Limpeza - (1 + NivelAtual)); // Desgaste mensal natural da limpeza!

            double folha = CalcularDespesaMensalFixa();
            ModificarDinheiro(-folha);
            if (GameOver) return;

            if (DividaTotalBanco > 0)
            {
                // Aplica 4% de juros mensais sobre o saldo devedor atual (quitar antes evita esses juros!)
                DividaTotalBanco = Math.Round(DividaTotalBanco * (1.0 + TaxaJurosMensal), 2);
                double parcela = ParcelaMensalBanco;
                MesesRestantesDivida--;

                if (Dinheiro >= parcela)
                {
                    ModificarDinheiro(-parcela);
                    DividaTotalBanco = Math.Max(0, Math.Round(DividaTotalBanco - parcela, 2));
                    UltimaMensagem = $"Mês {MesesJogados}: Folha (-R$ {folha:F2}) e parcela do banco (-R$ {parcela:F2}) pagas em dia!";
                }
                else
                {
                    DividaTotalBanco = Math.Round(DividaTotalBanco * 1.05, 2); // Multa adicional por atraso
                    AjustarReputacao(-0.25);
                    TocarSom(false);
                    UltimaMensagem = $"CALOTE MENSAL! Caixa insuficiente para pagar a parcela de R$ {parcela:F2}. Dívida acumulou juros (Restam {MesesRestantesDivida} meses)!";
                }

                if (MesesRestantesDivida <= 0 && DividaTotalBanco > 1.0)
                {
                    GameOver = true;
                    MotivoFimDeJogo = $"Execução Bancária! O prazo de 12 meses expirou e você não quitou a dívida pendente de R$ {DividaTotalBanco:F2}.";
                    RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal());
                    return;
                }
            }
            else
            {
                UltimaMensagem = $"MÊS {MesesJogados} INICIADO! Folha salarial e custos fixos (-R$ {folha:F2}) descontados.";
            }

            VerificarCondicaoVitoria();
        }

        private void VerificarCondicaoVitoria()
        {
            if (NivelAtual >= 5 && (Dinheiro - DividaTotalBanco) >= 50000.0 && Reputacao >= 4.5)
            {
                JogoVencido = true;
                MotivoFimDeJogo = "PARABÉNS! Você construiu um Império Gastronômico Global (Nível 5, R$ 50k+ Líquidos e 4.5+ Estrelas)!";
                RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal());
            }
        }

        // ================== SALVAR E CARREGAR (JSON) ==================
        public void SalvarJogo()
        {
            try
            {
                var data = new SaveStateData
                {
                    Diversao = Diversao,
                    NivelAtual = NivelAtual,
                    DataAtual = DataAtual,
                    DiaSemana = DiaSemana,
                    AcoesRestantesDia = AcoesRestantesDia,
                    Dinheiro = Dinheiro,
                    Reputacao = Reputacao,
                    Funcionarios = Funcionarios,
                    Limpeza = Limpeza,
                    UpgForno = UpgForno,
                    UpgGeladeira = UpgGeladeira,
                    UpgBalcao = UpgBalcao,
                    UpgLogistica = UpgLogistica,
                    MultiplicadorSalario = MultiplicadorSalario,
                    MultiplicadorPrecoPrato = MultiplicadorPrecoPrato,
                    DividaTotalBanco = DividaTotalBanco,
                    MesesRestantesDivida = MesesRestantesDivida,
                    PedidosPerfeitosTotal = PedidosPerfeitosTotal,
                    RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal()),
                    Estoque = Estoque,
                    PratosAtivos = PratosAtivosNoCardapio.ToList()
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SaveFileName, json);
                TocarSom(true);
                UltimaMensagem = "Jogo salvo com sucesso em 'savegame.json'!";
            }
            catch (Exception ex)
            {
                UltimaMensagem = $"Erro ao salvar: {ex.Message}";
            }
        }

        public void CarregarJogo()
        {
            try
            {
                if (!File.Exists(SaveFileName))
                {
                    UltimaMensagem = "Nenhum arquivo 'savegame.json' encontrado!";
                    return;
                }

                string json = File.ReadAllText(SaveFileName);
                var data = JsonSerializer.Deserialize<SaveStateData>(json);

                Diversao = data.Diversao;
                NivelAtual = data.NivelAtual;
                DataAtual = data.DataAtual == default ? new DateTime(2026, 1, 7) : data.DataAtual;
                DiaSemana = data.DiaSemana <= 0 ? 1 : data.DiaSemana;
                AcoesRestantesDia = data.AcoesRestantesDia;
                Dinheiro = data.Dinheiro;
                Reputacao = data.Reputacao;
                Funcionarios = data.Funcionarios;
                Limpeza = data.Limpeza;
                UpgForno = data.UpgForno;
                UpgGeladeira = data.UpgGeladeira;
                UpgBalcao = data.UpgBalcao;
                UpgLogistica = data.UpgLogistica;
                MultiplicadorSalario = data.MultiplicadorSalario;
                MultiplicadorPrecoPrato = data.MultiplicadorPrecoPrato;
                DividaTotalBanco = data.DividaTotalBanco;
                MesesRestantesDivida = data.MesesRestantesDivida;
                PedidosPerfeitosTotal = data.PedidosPerfeitosTotal;
                RecordePontuacao = data.RecordePontuacao;
                Estoque = data.Estoque ?? new Dictionary<string, int>();
                PratosAtivosNoCardapio = new HashSet<string>(data.PratosAtivos ?? new List<string>());

                GameOver = false;
                JogoVencido = false;
                GerarNovoPedido();
                GerarNovaEntrega(cobrarTaxa: false);
                TocarSom(true);
                UltimaMensagem = "Progresso carregado com sucesso!";
            }
            catch (Exception ex)
            {
                UltimaMensagem = $"Erro ao carregar save: {ex.Message}";
            }
        }
    }
}