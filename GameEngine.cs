using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
        public bool EInfluenciador { get; set; }
        public bool EPedidoSazonalBonus { get; set; }
        public int SorteioGorjetaBase { get; set; }
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
        public double ValorAcordo => Math.Round(ValorCausa * 0.55, 2);
        public double CustoAdvogado => Math.Round(ValorCausa * 0.10, 2);
        public int ChanceDeGanhar { get; set; }
    }

    public class DailySummaryReport
    {
        public int DiaConcluido { get; set; }
        public int MesRef { get; set; }
        public double ReceitaDia { get; set; }
        public double DespesaDia { get; set; }
        public double VariacaoReputacao { get; set; }
        public double BonusEficiencia { get; set; }
        public string MaiorRiscoAtual { get; set; }
        public string ProximaMeta { get; set; }
        public string ResumoViradaMes { get; set; }
    }

    public class SaveStateData
    {
        public int FormatVersion { get; set; } = PersistenceService.CurrentFormatVersion;
        public int Diversao { get; set; }
        public int NivelAtual { get; set; }
        public DateTime DataAtual { get; set; }
        public int DiaSemana { get; set; }
        public int MesesJogados { get; set; }
        public int DiasDecorridosTotais { get; set; }
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
        public int SequenciaPerfeita { get; set; }
        public int MelhorSequenciaPerfeita { get; set; }
        public int RecordePontuacao { get; set; }
        public DifficultyMode Dificuldade { get; set; } = DifficultyMode.Normal;
        public GameMode ModoJogo { get; set; } = GameMode.Carreira;
        public Dictionary<string, int> Estoque { get; set; }
        public Dictionary<string, int> EstoqueContaminado { get; set; }
        public List<string> PratosAtivos { get; set; }
        public List<string> ConquistasDesbloqueadas { get; set; }
        public NarrativeState Narrativa { get; set; }
    }

    public class GameEngine
    {
        private Random _rng;
        private readonly int? _seedFixa;
        private const double LimiteFalenciaImediata = -500.0;
        private const int MaxHistoricoEventos = 120;
        public const int PrazoCampanhaDias = 12;
        public const string SenhaDesenvolvedor = "xit89";
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public PersistenceService Persistence { get; }
        public AccessibilitySettings Configuracoes { get; private set; }
        public string SlotAtivo { get; set; } = "slot1";
        public DateTime? UltimoAutosaveLocal { get; private set; }

        public DifficultyMode Dificuldade { get; private set; } = DifficultyMode.Normal;
        public GameMode ModoJogo { get; private set; } = GameMode.Carreira;

        public int Diversao { get; private set; }

        // Modo Desenvolvedor (Aba Terminal)
        public bool TerminalDesbloqueado { get; private set; } = false;
        public string InputSenhaTerminal { get; set; } = "";

        // Modais de Tutorial e Resumo Diário
        public bool ExibirTutorial { get; set; } = true;
        public DailySummaryReport ResumoDiarioAtivo { get; private set; } = null;

        private double _receitaAcumuladaDia = 0;
        private double _despesaAcumuladaDia = 0;
        private double _reputacaoInicioDia = 3.2;
        private int _errosNoDiaAtual = 0;
        private int _tarefasNoDiaAtual = 0;
        private int _contadorDialogos = 0;

        // Calendário e Horário do Turno
        public int NivelAtual { get; private set; } = 1;
        public DateTime DataAtual { get; private set; } = new DateTime(2026, 1, 7);
        public int DiaSemana { get; private set; } = 1;
        public int MesesJogados { get; private set; } = 1;
        public int DiasDecorridosTotais { get; private set; } = 1;
        public string DataHojeFormatada => DataAtual.ToString("dd/MM/yyyy");

        public string HorarioTurnoFormatado
        {
            get
            {
                int max = Math.Max(1, AcoesMaximasDia);
                int usadas = Math.Clamp(max - AcoesRestantesDia, 0, max);
                int minutos = 18 * 60 + (int)Math.Round((double)usadas / max * 330); // 18:00 até 23:30
                int h = (minutos / 60) % 24;
                int m = (minutos % 60) >= 30 ? 30 : 0;
                return $"{h:00}h{m:00}";
            }
        }

        // Escassez de Ações (Energia do Turno) ajustada por Dificuldade
        public int AcoesMaximasDia
        {
            get
            {
                int bonusDif = Dificuldade == DifficultyMode.Facil ? 2 : (Dificuldade == DifficultyMode.Dificil ? -1 : 0);
                return Math.Max(6, 8 + bonusDif + Math.Min(6, UpgForno / 2));
            }
        }
        public int AcoesRestantesDia { get; private set; }
        public int AcoesRealizadasNoMes { get; private set; } = 0;
        public int TarefasConcluidasNoMes { get; private set; } = 0;
        public int LimiteAcoesNoMes => 16 + (TotalUpgrades / 8);

        // Finanças, Eficiência e Reputação
        public double Dinheiro { get; private set; } = 1100.0;
        public double Reputacao { get; private set; } = 3.2;
        public int PedidosPerfeitosTotal { get; private set; } = 0;
        public int SequenciaPerfeita { get; private set; } = 0;
        public int MelhorSequenciaPerfeita { get; private set; } = 0;
        public int RecordePontuacao { get; private set; } = 0;

        public double BonusEficienciaCombo => SequenciaPerfeita >= 10 ? 0.20 : (SequenciaPerfeita >= 5 ? 0.10 : (SequenciaPerfeita >= 3 ? 0.05 : 0.0));
        private double FatorMultaDificuldade => Dificuldade == DifficultyMode.Facil ? 0.70 : (Dificuldade == DifficultyMode.Dificil ? 1.25 : 1.0);
        private double FatorCustoFixoDificuldade => Dificuldade == DifficultyMode.Facil ? 0.85 : (Dificuldade == DifficultyMode.Dificil ? 1.20 : 1.0);

        // Eventos, Narrativa e Conquistas
        public bool GameOver { get; private set; } = false;
        public bool JogoVencido { get; private set; } = false;
        public string MotivoFimDeJogo { get; private set; } = "";
        public string UltimaMensagem { get; set; } = "Bem-vindo! Escolha uma opção numerada, clique ou digite um comando.";
        public string EventoDoDiaDescricao { get; private set; } = "Expediente Normal";
        private string _eventoAtivoCodigo = "NORMAL";
        public string IngredienteEmFaltaOuCaro { get; private set; } = "";

        public NarrativeState Narrativa { get; private set; } = new NarrativeState();
        public HashSet<string> ConquistasDesbloqueadas { get; private set; } = new HashSet<string>();

        // Sistema Estruturado de Feedback, Histórico e Desfazer
        public ActionFeedback FeedbackAtual { get; private set; } = new ActionFeedback();
        public List<HistoryEntry> HistoricoEventos { get; private set; } = new List<HistoryEntry>();
        private int _contadorEventos = 0;
        private readonly Stack<UndoEntry> _pilhaDesfazer = new Stack<UndoEntry>();
        public string UltimoMotivoBloqueioDesfazer { get; private set; } = "Nenhuma ação simples foi executada ainda.";

        public bool PodeDesfazer => _pilhaDesfazer.Count > 0 && !GameOver && !JogoVencido && ProcessoAtivo == null;
        public string DescricaoProximoDesfazer => _pilhaDesfazer.Count > 0 ? _pilhaDesfazer.Peek().Descricao : "";

        // Recursos e Upgrades Nomeados
        public int Funcionarios { get; private set; } = 0;
        public int Limpeza { get; private set; } = 4;
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

        // Banco
        public double DividaTotalBanco { get; private set; } = 0.0;
        public int MesesRestantesDivida { get; private set; } = 0;
        public double TaxaJurosMensal => Dificuldade == DifficultyMode.Facil ? 0.025 : (Dificuldade == DifficultyMode.Dificil ? 0.04 : 0.03);
        public double LimiteCreditoBanco => Math.Round(Reputacao * NivelAtual * 2200.0, 2);
        public double ParcelaMensalBanco => MesesRestantesDivida > 0 ? Math.Round(DividaTotalBanco / MesesRestantesDivida, 2) : 0;

        // Tarefas Ativas
        public CustomerOrder PedidoAtual { get; private set; }
        public List<DeliveryItemRecord> EntregaAtual { get; private set; }
        public int InspecoesCaixaRestantes { get; private set; }
        public int InspecoesCaixaMaximas => Math.Min(7, 3 + (Dificuldade == DifficultyMode.Facil ? 1 : 0) + (UpgLogistica / 3));
        public LawsuitEvent ProcessoAtivo { get; private set; }

        public GameEngine(int? seed = null, PersistenceService persistence = null)
        {
            _seedFixa = seed;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            Persistence = persistence ?? new PersistenceService();
            Configuracoes = Persistence.CarregarConfiguracoes();
            IniciarNovaRun(mostrarTutorial: true, modo: GameMode.Carreira, dificuldade: DifficultyMode.Normal);
        }

        private static string FmtMoney(double valor) => "R$ " + valor.ToString("N2", PtBr);

        public void SalvarConfiguracoes()
        {
            Persistence.SalvarConfiguracoes(Configuracoes);
        }

        public StateSnapshot CapturarSnapshot()
        {
            int invTotal = 0;
            int contamTotal = 0;
            foreach (var ing in Catalog.TodosIngredientes.Where(i => i.Nivel <= NivelAtual))
            {
                if (Estoque.TryGetValue(ing.Nome, out int q)) invTotal += q;
                if (EstoqueContaminado.TryGetValue(ing.Nome, out int c)) contamTotal += c;
            }

            return new StateSnapshot
            {
                Dinheiro = Dinheiro,
                Reputacao = Reputacao,
                Energia = AcoesRestantesDia,
                Funcionarios = Funcionarios,
                Limpeza = Limpeza,
                TotalUpgrades = TotalUpgrades,
                DividaTotalBanco = DividaTotalBanco,
                NivelAtual = NivelAtual,
                InventarioTotal = invTotal,
                EstoqueContaminadoTotal = contamTotal,
                DiaSemana = DiaSemana,
                MesesJogados = MesesJogados,
                PratosAtivos = PratosAtivosNoCardapio.Count,
                MultiplicadorSalario = MultiplicadorSalario,
                MultiplicadorPrecoPrato = MultiplicadorPrecoPrato,
                MoralEquipe = Narrativa?.MoralEquipe ?? 60,
                ConquistasCount = ConquistasDesbloqueadas.Count
            };
        }

        private void RegistrarFeedback(
            string localidade,
            string acao,
            string resposta,
            FeedbackSeverity severidade,
            StateSnapshot antes = null,
            string sugestao = "",
            string correcaoOrtografica = "",
            bool ehComandoTexto = false,
            bool adicionarAoHistorico = true)
        {
            var deltas = antes != null
                ? StateSnapshot.CalcularDeltas(antes, CapturarSnapshot())
                : new List<StateDeltaItem>();

            UltimaMensagem = string.IsNullOrWhiteSpace(sugestao)
                ? resposta
                : $"{resposta} Sugestão: {sugestao}";

            FeedbackAtual = new ActionFeedback
            {
                Localidade = localidade,
                Acao = acao,
                Resposta = resposta,
                Sugestao = sugestao ?? "",
                CorrecaoOrtografica = correcaoOrtografica ?? "",
                Deltas = deltas,
                Severidade = severidade
            };

            if (adicionarAoHistorico)
            {
                _contadorEventos++;
                HistoricoEventos.Add(new HistoryEntry
                {
                    Numero = _contadorEventos,
                    DataFormatada = DataHojeFormatada,
                    DiaSemana = DiaSemana,
                    Mes = MesesJogados,
                    Localidade = localidade,
                    Acao = acao,
                    Resposta = resposta,
                    Sugestao = sugestao ?? "",
                    ResumoDeltas = FeedbackAtual.ResumoDeltas,
                    Severidade = severidade,
                    EhComandoTexto = ehComandoTexto
                });

                if (HistoricoEventos.Count > MaxHistoricoEventos)
                    HistoricoEventos.RemoveAt(0);
            }
        }

        public void RegistrarFeedbackExterno(
            string localidade,
            string acao,
            string resposta,
            FeedbackSeverity severidade,
            string sugestao = "",
            string correcaoOrtografica = "",
            bool ehComandoTexto = false,
            bool adicionarAoHistorico = true)
        {
            RegistrarFeedback(localidade, acao, resposta, severidade, null, sugestao, correcaoOrtografica, ehComandoTexto, adicionarAoHistorico);
        }

        public void AnexarNotaCorrecaoAoFeedbackAtual(string correcao)
        {
            if (FeedbackAtual != null && !string.IsNullOrWhiteSpace(correcao))
            {
                FeedbackAtual.CorrecaoOrtografica = correcao;
            }
        }

        private void EmpilharDesfazer(string descricao, string localidade, Action desfazer)
        {
            _pilhaDesfazer.Push(new UndoEntry
            {
                Descricao = descricao,
                Localidade = localidade,
                ExecutarDesfazer = desfazer
            });
            UltimoMotivoBloqueioDesfazer = "";
        }

        private void InvalidarDesfazer(string motivo)
        {
            _pilhaDesfazer.Clear();
            UltimoMotivoBloqueioDesfazer = motivo;
        }

        public void IniciarNovaRun(bool mostrarTutorial = false, GameMode? modo = null, DifficultyMode? dificuldade = null)
        {
            if (_seedFixa.HasValue) _rng = new Random(_seedFixa.Value);
            if (modo.HasValue) ModoJogo = modo.Value;
            if (dificuldade.HasValue) Dificuldade = dificuldade.Value;

            Diversao = _rng.Next(0, 101);
            ExibirTutorial = mostrarTutorial;
            ResumoDiarioAtivo = null;
            NivelAtual = 1;
            DataAtual = new DateTime(2026, 1, 7);
            DiaSemana = 1;
            MesesJogados = 1;
            DiasDecorridosTotais = 1;
            AcoesRealizadasNoMes = 0;
            TarefasConcluidasNoMes = 0;

            Dinheiro = ModoJogo == GameMode.GestaoLivre
                ? 5000.0
                : (Dificuldade == DifficultyMode.Facil ? 1500.0 : (Dificuldade == DifficultyMode.Dificil ? 850.0 : 1100.0));

            Reputacao = Dificuldade == DifficultyMode.Facil ? 3.5 : 3.2;
            Funcionarios = 0;
            Limpeza = Dificuldade == DifficultyMode.Facil ? 6 : 4;
            UpgForno = 0;
            UpgGeladeira = 0;
            UpgBalcao = 0;
            UpgLogistica = 0;
            MultiplicadorSalario = 1.0;
            MultiplicadorPrecoPrato = 1.0;
            DividaTotalBanco = 0;
            MesesRestantesDivida = 0;
            PedidosPerfeitosTotal = 0;
            SequenciaPerfeita = 0;
            MelhorSequenciaPerfeita = 0;
            GameOver = false;
            JogoVencido = false;
            MotivoFimDeJogo = "";
            ProcessoAtivo = null;

            _receitaAcumuladaDia = 0;
            _despesaAcumuladaDia = 0;
            _reputacaoInicioDia = Reputacao;
            _errosNoDiaAtual = 0;
            _tarefasNoDiaAtual = 0;
            _contadorDialogos = 0;

            Narrativa = new NarrativeState
            {
                MoralEquipe = Dificuldade == DifficultyMode.Facil ? 70 : 60,
                HistoricoSanitario = 65,
                ConfiancaFornecedores = 60
            };
            Narrativa.RegistrarCronica($"Abertura do restaurante no modo {LocalityCatalog.ObterNomeModo(ModoJogo)} ({LocalityCatalog.ObterNomeDificuldade(Dificuldade)}).");
            ConquistasDesbloqueadas.Clear();

            Estoque.Clear();
            EstoqueContaminado.Clear();
            PratosAtivosNoCardapio.Clear();
            HistoricoEventos.Clear();
            _contadorEventos = 0;
            InvalidarDesfazer("Partida recém-iniciada — ainda não há ações para desfazer.");

            int estInicial = Dificuldade == DifficultyMode.Facil ? 10 : (Dificuldade == DifficultyMode.Dificil ? 6 : 8);
            foreach (var ing in Catalog.TodosIngredientes)
            {
                Estoque[ing.Nome] = ing.Nivel == 1 ? estInicial : 0;
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

            RegistrarFeedback(
                "Cozinha",
                $"Nova partida ({LocalityCatalog.ObterNomeModo(ModoJogo)} · {LocalityCatalog.ObterNomeDificuldade(Dificuldade)})",
                $"Portas abertas às 18h00 na Fase 1/5 ({ConfigAtual.Titulo})! O primeiro cliente pediu {PedidoAtual.Receita.Nome}.",
                FeedbackSeverity.Info,
                sugestao: $"Monte {PedidoAtual.Receita.Nome} começando por [{PedidoAtual.OrdemExigida[0]}] ou digite \"falar com Pedro\".");
        }

        public LevelConfig ConfigAtual => Catalog.Levels[NivelAtual - 1];

        public void FecharResumoDiario()
        {
            if (ResumoDiarioAtivo == null) return;
            ResumoDiarioAtivo = null;
            RegistrarFeedback(
                "Restaurante",
                $"Abrir expediente do Dia {DiaSemana}/4 (Mês {MesesJogados})",
                $"Turno aberto às 18h00 com {AcoesRestantesDia}/{AcoesMaximasDia} de Energia. Evento: {EventoDoDiaDescricao}",
                FeedbackSeverity.Info);
        }

        private void ModificarDinheiro(double delta)
        {
            if (delta > 0) _receitaAcumuladaDia += delta;
            else if (delta < 0) _despesaAcumuladaDia += Math.Abs(delta);

            Dinheiro = Math.Round(Dinheiro + delta, 2);
            if (ModoJogo != GameMode.GestaoLivre && Dinheiro < LimiteFalenciaImediata)
            {
                GameOver = true;
                MotivoFimDeJogo = $"Falência Imediata! Seu saldo ({FmtMoney(Dinheiro)}) ultrapassou o limite de crédito negativo ({FmtMoney(LimiteFalenciaImediata)}).";
                RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal());
            }
            else
            {
                VerificarCondicaoVitoria();
            }
        }

        public int ObterLimpezaIdeal()
        {
            return ConfigAtual.MinLimpeza + (Funcionarios / 3) + (TotalUpgrades / 5) + 3;
        }

        public InventorySummary ObterResumoInventario()
        {
            var disponiveis = Catalog.TodosIngredientes.Where(i => i.Nivel <= NivelAtual).ToList();
            int cap = CapacidadeMaximaEstoque;
            var resumo = new InventorySummary
            {
                CapacidadeTotal = disponiveis.Count * cap
            };

            foreach (var ing in disponiveis)
            {
                int q = Estoque.TryGetValue(ing.Nome, out int val) ? val : 0;
                int c = EstoqueContaminado.TryGetValue(ing.Nome, out int cval) ? cval : 0;
                resumo.TotalUnidades += q;
                if (q == 0)
                {
                    resumo.Esgotados++;
                    if (resumo.ItensCriticos.Count < 4) resumo.ItensCriticos.Add(ing.Nome);
                }
                else if (q <= 2)
                {
                    resumo.Baixos++;
                    if (resumo.ItensCriticos.Count < 4) resumo.ItensCriticos.Add($"{ing.Nome}({q})");
                }
                if (c > 0) resumo.Contaminados++;
            }

            return resumo;
        }

        public string DiagnosticarMaiorRiscoAtual()
        {
            if (ProcessoAtivo != null)
                return $"Resolva a intimação judicial ({ProcessoAtivo.Titulo}) antes de retomar o salão.";

            if (ModoJogo == GameMode.CampanhaDias && DiasDecorridosTotais >= PrazoCampanhaDias - 2)
                return $"Restam {Math.Max(0, PrazoCampanhaDias - DiasDecorridosTotais + 1)} dia(s) na Campanha! Foque nos requisitos de vitória.";

            if (Dinheiro < 200 && ModoJogo != GameMode.GestaoLivre)
                return $"Reforce o caixa ({FmtMoney(Dinheiro)}) entregando pratos perfeitos antes de novos gastos.";

            if (Narrativa.MoralEquipe <= 35 && Funcionarios > 0)
                return $"Moral da equipe crítica ({Narrativa.MoralEquipe}/100)! Ajuste salários em Gerência [5] para evitar demissões.";

            if (Funcionarios > ConfigAtual.RecFuncionarios + 6)
                return $"Reduza contratações: {Funcionarios}/{ConfigAtual.RecFuncionarios} recomendados estão pesando na folha.";

            int limpIdeal = ObterLimpezaIdeal();
            if (Limpeza < limpIdeal)
                return $"Eleve a limpeza ({Limpeza}/{limpIdeal}) em Gerência [5] para evitar multa na Blitz Sanitária.";

            var inv = ObterResumoInventario();
            if (inv.Contaminados > 0)
                return $"Atenção: {inv.Contaminados} insumo(s) contaminado(s) no estoque! Audite as Docas [2] com rigor.";

            var baixos = Catalog.TodosIngredientes
                .Where(i => i.Nivel <= NivelAtual && Estoque.ContainsKey(i.Nome) && Estoque[i.Nome] <= 1)
                .Select(i => i.Nome)
                .Take(3)
                .ToList();

            if (baixos.Count > 0)
                return $"Reponha nas Docas [2]: estoque quase zerado de {string.Join(", ", baixos)}.";

            if (DividaTotalBanco > 0 && Dinheiro < ParcelaMensalBanco)
                return $"Separe {FmtMoney(ParcelaMensalBanco)} no caixa para pagar a parcela bancária em dia.";

            return "Operação equilibrada! Mantenha a sequência perfeita para multiplicar seus lucros.";
        }

        public string ObterTextoProximaMeta()
        {
            if (ModoJogo == GameMode.CampanhaDias)
            {
                int diasRest = Math.Max(0, PrazoCampanhaDias - DiasDecorridosTotais + 1);
                double liq = Math.Round(Dinheiro - DividaTotalBanco, 2);
                double faltaDin = Math.Max(0, 8000.0 - liq);
                double faltaRep = Math.Max(0, Math.Round(4.0 - Reputacao, 1));
                return $"Campanha ({diasRest}d restantes): alcance Fase 3/5 (atual {NivelAtual}), +{FmtMoney(faltaDin)} líq. e +{faltaRep:F1}* Rep.";
            }

            if (ModoJogo == GameMode.GestaoLivre)
            {
                if (NivelAtual < 5)
                {
                    var prox = Catalog.Levels[NivelAtual];
                    int fF = Math.Max(0, prox.MinFuncionarios - Funcionarios);
                    int lF = Math.Max(0, prox.MinLimpeza - Limpeza);
                    int uF = Math.Max(0, prox.MinUpgrades - TotalUpgrades);
                    return $"Gestão Livre · Desbloqueie Fase {NivelAtual + 1}/5 ({prox.Titulo}): +{fF} Func, +{lF} Limp, +{uF} Upg.";
                }
                return $"Gestão Livre · Fase máxima alcançada! Explore receitas e complete {ConquistasDesbloqueadas.Count}/{Catalog.Conquistas.Count} conquistas.";
            }

            if (NivelAtual < 5)
            {
                var prox = Catalog.Levels[NivelAtual];
                int fFalta = Math.Max(0, prox.MinFuncionarios - Funcionarios);
                int lFalta = Math.Max(0, prox.MinLimpeza - Limpeza);
                int uFalta = Math.Max(0, prox.MinUpgrades - TotalUpgrades);
                return $"Expanda para Fase {NivelAtual + 1}/5 ({prox.Titulo}): contrate {fFalta} Func, +{lFalta} Limp e +{uFalta} Upg.";
            }
            double faltaDinheiro = Math.Max(0, 50000.0 - (Dinheiro - DividaTotalBanco));
            double faltaRepGlobal = Math.Max(0, Math.Round(4.5 - Reputacao, 1));
            return $"Conquiste a Vitória Global: junte +{FmtMoney(faltaDinheiro)} líquidos e +{faltaRepGlobal:F1}* de Reputação.";
        }

        private void RegistrarAcaoNoMes(bool sucesso)
        {
            if (GameOver || JogoVencido) return;

            _tarefasNoDiaAtual++;
            if (!sucesso) _errosNoDiaAtual++;

            AcoesRealizadasNoMes++;
            if (sucesso) TarefasConcluidasNoMes++;

            if (AcoesRealizadasNoMes >= LimiteAcoesNoMes)
            {
                DiaSemana = 4;
                AvancarDiaInterno();
            }
            else
            {
                ConsumirAcao();
            }
        }

        public void TocarSom(bool sucesso, bool especial = false)
        {
            if (Configuracoes != null && !Configuracoes.SonsAtivos) return;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    if (especial)
                    {
                        Console.Beep(784, 55);
                        Console.Beep(1046, 80);
                    }
                    else
                    {
                        Console.Beep(sucesso ? 880 : 240, sucesso ? 50 : 110);
                    }
                }
            }
            catch { }
        }

        private void AjustarReputacao(double delta)
        {
            Reputacao = Math.Clamp(Math.Round(Reputacao + delta, 2), 0.5, 5.0);
        }

        private void AjustarMoral(int delta)
        {
            Narrativa.MoralEquipe = Math.Clamp(Narrativa.MoralEquipe + delta, 0, 100);
        }

        private void AjustarSanitario(int delta)
        {
            Narrativa.HistoricoSanitario = Math.Clamp(Narrativa.HistoricoSanitario + delta, 0, 100);
        }

        private void AjustarConfiancaFornecedores(int delta)
        {
            Narrativa.ConfiancaFornecedores = Math.Clamp(Narrativa.ConfiancaFornecedores + delta, 0, 100);
        }

        public int CalcularPontuacaoFinal()
        {
            int bonusConquistas = Catalog.Conquistas
                .Where(c => ConquistasDesbloqueadas.Contains(c.Id))
                .Sum(c => c.BonusPontos);

            int pts = (int)(Dinheiro + (NivelAtual * 2500) + (Reputacao * 1500) + (PedidosPerfeitosTotal * 120) + (MelhorSequenciaPerfeita * 80) + bonusConquistas - DividaTotalBanco);
            return Math.Max(0, pts);
        }

        // ================== CONQUISTAS ==================
        private List<string> VerificarConquistas(double lucroLiquidoDia = 0)
        {
            var novas = new List<string>();
            void Desbloquear(string id, bool condicao)
            {
                if (!condicao || ConquistasDesbloqueadas.Contains(id)) return;
                var def = Catalog.Conquistas.FirstOrDefault(c => c.Id == id);
                if (def == null) return;
                ConquistasDesbloqueadas.Add(id);
                novas.Add(def.Titulo);
                Narrativa.RegistrarCronica($"Conquista desbloqueada: {def.Titulo} (+{def.BonusPontos} pts).");
            }

            Desbloquear("PRIMEIRO_PRATO", PedidosPerfeitosTotal >= 1);
            Desbloquear("COMBO_5", SequenciaPerfeita >= 5 || MelhorSequenciaPerfeita >= 5);
            Desbloquear("AUDITOR_IMPLACAVEL", Narrativa.AuditoriasImpecaveis >= 3);
            Desbloquear("SEM_DIVIDAS", Narrativa.EmprestimosQuitados >= 1);
            Desbloquear("HIGIENE_OURO", Narrativa.BlitzAprovadas >= 1);
            Desbloquear("LIDER_QUERIDO", Narrativa.MoralEquipe >= 80 && Funcionarios >= 5);
            Desbloquear("EXPANSAO_N3", NivelAtual >= 3);
            Desbloquear("ELITE_GLOBAL", NivelAtual >= 5);
            Desbloquear("SOBREVIVENTE_TRIBUNAL", Narrativa.VitoriasTribunal >= 1);
            Desbloquear("EFICIENCIA_MESTRE", lucroLiquidoDia >= 500.0 && _errosNoDiaAtual == 0 && _tarefasNoDiaAtual >= 2);

            if (novas.Count > 0)
            {
                TocarSom(true, especial: true);
                SalvarAutomatico("conquista");
            }

            return novas;
        }

        // ================== DESFAZER UNIFICADO ==================
        public void DesfazerUltimaAcao()
        {
            if (GameOver || JogoVencido)
            {
                RegistrarFeedback("Restaurante", "Desfazer", "Não é possível desfazer após o encerramento da partida.", FeedbackSeverity.Erro,
                    sugestao: "Pressione [Enter] para iniciar nova partida ou [G] para carregar outro slot.");
                return;
            }

            if (ProcessoAtivo != null)
            {
                RegistrarFeedback("Tribunal", "Desfazer", "Não é possível desfazer enquanto houver uma intimação judicial aberta.", FeedbackSeverity.Erro,
                    sugestao: "Escolha [1] Fazer Acordo ou [2] Ir a Julgamento.");
                return;
            }

            if (_pilhaDesfazer.Count == 0)
            {
                string motivo = string.IsNullOrWhiteSpace(UltimoMotivoBloqueioDesfazer)
                    ? "Não há ações simples recentes para desfazer."
                    : UltimoMotivoBloqueioDesfazer;
                TocarSom(false);
                RegistrarFeedback("Restaurante", "Desfazer", motivo, FeedbackSeverity.Aviso,
                    sugestao: "Ações simples reversíveis: ingredientes na bancada, limpar bancada, decisões de caixas, cardápio e ajustes de preço/salário.");
                return;
            }

            var antes = CapturarSnapshot();
            var entrada = _pilhaDesfazer.Pop();
            entrada.ExecutarDesfazer?.Invoke();
            TocarSom(true);
            RegistrarFeedback(
                entrada.Localidade,
                $"Desfazer: {entrada.Descricao}",
                $"Ação revertida com sucesso: {entrada.Descricao}.",
                FeedbackSeverity.Info,
                antes);
        }

        // ================== DIÁLOGOS NARRATIVOS COERENTES E VARIADOS ==================
        public void FalarComPersonagem(string nomeNormalizado)
        {
            if (ProcessoAtivo != null)
            {
                RegistrarFeedback("Tribunal", $"Falar com {nomeNormalizado}",
                    "O juiz exige sua decisão antes de conversar com a equipe!",
                    FeedbackSeverity.Aviso,
                    sugestao: "Resolva a intimação com \"fazer acordo\" [1] ou \"ir a julgamento\" [2].");
                return;
            }

            _contadorDialogos++;

            if (nomeNormalizado == "pedro" || nomeNormalizado == "cozinheiro" || nomeNormalizado == "subchefe" || nomeNormalizado == "chef")
            {
                if (PedidoAtual == null)
                {
                    RegistrarFeedback("Cozinha", "Falar com Pedro Almeida", "Pedro Almeida (Subchefe): \"Estamos aguardando o próximo cliente no balcão.\"", FeedbackSeverity.Info);
                    return;
                }

                string tomMoral = Narrativa.MoralEquipe >= 75
                    ? "A cozinha está voando hoje!"
                    : (Narrativa.MoralEquipe <= 35 ? "O clima aqui está pesado com os cortes, chefe..." : $"Turno das {HorarioTurnoFormatado} em andamento.");

                int proximoIdx = PedidoAtual.PratoMontado.Count;
                string dicaOrdem;
                if (proximoIdx < PedidoAtual.OrdemExigida.Count)
                {
                    string proxIng = PedidoAtual.OrdemExigida[proximoIdx];
                    int est = Estoque.TryGetValue(proxIng, out int q) ? q : 0;
                    dicaOrdem = est > 0
                        ? $"Coloque agora [{proxIng}] (temos {est} un.)."
                        : $"ALERTA: [{proxIng}] zerou! Use \"comprar faltantes\" ou vá às Docas [2].";
                }
                else if (DiagnosticarErroPrato(PedidoAtual) == null)
                {
                    dicaOrdem = SequenciaPerfeita >= 2
                        ? $"Prato impecável! Entregue com [E] para manter seu combo de {SequenciaPerfeita + 1}x!"
                        : "Prato perfeito na bancada! Pode servir agora com [E] ou \"entregar prato\".";
                }
                else
                {
                    dicaOrdem = $"Ajuste antes de servir: {DiagnosticarErroPrato(PedidoAtual)}";
                }

                string abertura = (_contadorDialogos % 3) switch
                {
                    0 => $"{tomMoral} Sobre {PedidoAtual.Receita.Nome}:",
                    1 => $"Na escuta, chefe ({HorarioTurnoFormatado}). Para {PedidoAtual.Receita.Nome}:",
                    _ => $"Conferi a comanda de {PedidoAtual.Receita.Nome} ({tomMoral}):"
                };

                RegistrarFeedback("Cozinha", "Falar com Pedro Almeida (Subchefe)",
                    $"Pedro Almeida: \"{abertura} {dicaOrdem}\"",
                    FeedbackSeverity.Info);
                return;
            }

            if (nomeNormalizado == "ana" || nomeNormalizado == "conferente" || nomeNormalizado == "fornecedor" || nomeNormalizado == "docas")
            {
                var inv = ObterResumoInventario();
                int aceitas = EntregaAtual?.Count(c => c.MarcadoComoValidoPeloJogador) ?? 0;
                string statusForn = Narrativa.ConfiancaFornecedores >= 75
                    ? "Os fornecedores confiam na gente e estão mandando +1 un. bônus por caixa!"
                    : (Narrativa.ConfiancaFornecedores <= 40 ? "Tivemos atritos nas docas — redobre a atenção com notas falsas." : "Relação normal com os distribuidores.");

                string alertaEstoque = inv.Esgotados > 0
                    ? $"Itens zerados: {string.Join(", ", inv.ItensCriticos)}."
                    : "Nenhum ingrediente zerado no momento.";

                RegistrarFeedback("Docas (Entregas)", "Falar com Ana Beatriz (Conferente)",
                    $"Ana Beatriz: \"Restam {InspecoesCaixaRestantes}/{InspecoesCaixaMaximas} inspeções e {aceitas}/7 caixas marcadas. {statusForn} {alertaEstoque}\"",
                    FeedbackSeverity.Info,
                    sugestao: "Cidades válidas: São Paulo, Curitiba e Rio de Janeiro. Frios devem estar ≤ 5°C.");
                return;
            }

            if (nomeNormalizado == "carlos" || nomeNormalizado == "gerente" || nomeNormalizado == "contador" || nomeNormalizado == "caixa")
            {
                double folha = CalcularDespesaMensalFixa();
                string risco = DiagnosticarMaiorRiscoAtual();
                string saudeSanit = Narrativa.HistoricoSanitario >= 80 ? "Selo sanitário forte" : (Narrativa.HistoricoSanitario <= 40 ? "Vigilância de olho em nós" : "Higiene regular");
                RegistrarFeedback("Escritório (Gerência)", "Falar com Carlos Eduardo (Gerente)",
                    $"Carlos Eduardo: \"Saldo {FmtMoney(Dinheiro)}, folha prevista {FmtMoney(folha)}/mês, Moral {Narrativa.MoralEquipe}/100 ({saudeSanit}). {risco}\"",
                    FeedbackSeverity.Info,
                    sugestao: ObterTextoProximaMeta());
                return;
            }

            if (nomeNormalizado == "cliente" || nomeNormalizado == "balcao")
            {
                if (PedidoAtual == null)
                {
                    RegistrarFeedback("Cozinha", "Falar com Cliente", "Nenhum cliente aguardando no balcão.", FeedbackSeverity.Info);
                    return;
                }

                string humor = Reputacao >= 4.2
                    ? "Ouvi falar muito bem deste restaurante! "
                    : (Narrativa.HistoricoSanitario <= 40 ? "Espero que esteja tudo fresquinho hoje... " : "");

                RegistrarFeedback("Cozinha", "Falar com Cliente",
                    $"Cliente: \"{humor}{PedidoAtual.FalaDoCliente}\" (Valor estimado: {FmtMoney(CalcularValorVendaPrato())})",
                    FeedbackSeverity.Info,
                    sugestao: "Monte na ordem do Guia e entregue com [E] ou \"entregar prato\".");
                return;
            }

            RegistrarFeedback("Restaurante", $"Falar com {nomeNormalizado}",
                $"Não há ninguém chamado \"{nomeNormalizado}\" na equipe agora.",
                FeedbackSeverity.Aviso,
                sugestao: "Converse com: Pedro (Cozinha), Ana (Docas), Carlos (Gerência) ou Cliente.");
        }

        // ================== CALENDÁRIO, TURNO E 10 EVENTOS ==================
        private void ConsumirAcao()
        {
            if (GameOver || JogoVencido) return;
            AcoesRestantesDia--;
            if (AcoesRestantesDia <= 0)
            {
                AvancarDiaInterno();
            }
        }

        public void AvancarDia()
        {
            if (GameOver || JogoVencido) return;
            var antes = CapturarSnapshot();
            InvalidarDesfazer("Encerrar o dia avança o calendário e não pode ser desfeito.");
            AvancarDiaInterno();
            RegistrarFeedback(
                "Restaurante",
                "Encerrar turno do dia",
                $"Expediente encerrado! Avançou para Dia {DiaSemana}/4 · Mês {MesesJogados} ({DataHojeFormatada}). Autosave atualizado.",
                FeedbackSeverity.Info,
                antes);
        }

        private void AvancarDiaInterno()
        {
            if (GameOver || JogoVencido) return;

            int diaQueAcabou = DiaSemana;
            int mesRef = MesesJogados;
            double recDia = Math.Round(_receitaAcumuladaDia, 2);
            double despDia = Math.Round(_despesaAcumuladaDia, 2);
            double lucroDia = Math.Round(recDia - despDia, 2);

            // Bônus de Eficiência Diária (recompensa decisões eficientes!)
            double bonusEficiencia = 0;
            if (lucroDia > 0 && _errosNoDiaAtual == 0 && _tarefasNoDiaAtual >= 2)
            {
                bonusEficiencia = Math.Round(60.0 * NivelAtual, 2);
                ModificarDinheiro(bonusEficiencia);
                AjustarReputacao(+0.10);
                AjustarMoral(+3);
                recDia = Math.Round(recDia + bonusEficiencia, 2);
            }

            VerificarConquistas(lucroLiquidoDia: lucroDia);

            double varRep = Math.Round(Reputacao - _reputacaoInicioDia, 2);
            string msgViradaMes = null;

            DiasDecorridosTotais++;
            DiaSemana++;
            if (DiaSemana > 4)
            {
                DiaSemana = 1;
                MesesJogados++;
                var proximoMes = DataAtual.AddMonths(1);
                DataAtual = new DateTime(proximoMes.Year, proximoMes.Month, 7);
                ProcessarViradaDeMes();
                msgViradaMes = UltimaMensagem;
            }
            else
            {
                DataAtual = DataAtual.AddDays(7);
            }

            // Verifica encerramento do prazo no modo Campanha de 12 Dias
            if (!GameOver && !JogoVencido && ModoJogo == GameMode.CampanhaDias && DiasDecorridosTotais > PrazoCampanhaDias)
            {
                VerificarCondicaoVitoria();
                if (!JogoVencido)
                {
                    GameOver = true;
                    MotivoFimDeJogo = $"Fim dos {PrazoCampanhaDias} dias da Campanha! Você terminou na Fase {NivelAtual}/5 com {FmtMoney(Dinheiro - DividaTotalBanco)} líquidos e {Reputacao:F1}* de Reputação.";
                    RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal());
                }
            }

            if (!GameOver && !JogoVencido)
            {
                ResumoDiarioAtivo = new DailySummaryReport
                {
                    DiaConcluido = diaQueAcabou,
                    MesRef = mesRef,
                    ReceitaDia = recDia,
                    DespesaDia = despDia,
                    VariacaoReputacao = varRep,
                    BonusEficiencia = bonusEficiencia,
                    MaiorRiscoAtual = DiagnosticarMaiorRiscoAtual(),
                    ProximaMeta = ObterTextoProximaMeta(),
                    ResumoViradaMes = msgViradaMes
                };

                _receitaAcumuladaDia = 0;
                _despesaAcumuladaDia = 0;
                _reputacaoInicioDia = Reputacao;
                _errosNoDiaAtual = 0;
                _tarefasNoDiaAtual = 0;

                AcoesRestantesDia = AcoesMaximasDia;
                SortearEventoDoDia();
                GerarNovoPedido();
                SalvarAutomatico("fim-do-dia");
            }
        }

        public void SortearEventoDoDia()
        {
            IngredienteEmFaltaOuCaro = "";
            int chanceEvento = 38 + (Diversao / 2);

            if (_rng.Next(100) >= chanceEvento)
            {
                _eventoAtivoCodigo = "NORMAL";
                EventoDoDiaDescricao = $"Expediente Normal às 18h (Bônus Diversão: +{Diversao / 5}% gorjetas)";
                return;
            }

            var ingsNivel = Catalog.TodosIngredientes.Where(i => i.Nivel <= NivelAtual).ToList();
            var ingSorteado = ingsNivel[_rng.Next(ingsNivel.Count)].Nome;

            int tipo = _rng.Next(10);
            switch (tipo)
            {
                case 0:
                    _eventoAtivoCodigo = "ESCASSEZ_FORNECEDOR";
                    IngredienteEmFaltaOuCaro = ingSorteado;
                    EventoDoDiaDescricao = $"ESCASSEZ DE FORNECEDOR! [{ingSorteado}] não vem nas cargas hoje!";
                    break;

                case 1:
                    _eventoAtivoCodigo = "INFLACAO_INSUMO";
                    IngredienteEmFaltaOuCaro = ingSorteado;
                    EventoDoDiaDescricao = $"DISPARO DE PREÇO! Compra emergencial de [{ingSorteado}] custa 3x hoje (pratos pagam +15%)!";
                    break;

                case 2:
                    _eventoAtivoCodigo = "PRAGA";
                    int perdaLimp = Math.Min(Limpeza, 2 + NivelAtual);
                    Limpeza -= perdaLimp;
                    AjustarSanitario(-8);
                    if (Estoque.ContainsKey(ingSorteado))
                        Estoque[ingSorteado] = Math.Max(0, Estoque[ingSorteado] - 2);
                    EventoDoDiaDescricao = $"PROBLEMA DE PRAGAS! Limpeza -{perdaLimp} e -2 un. de [{ingSorteado}]!";
                    break;

                case 3:
                    _eventoAtivoCodigo = "BLITZ_SANITARIA";
                    int ideal = ObterLimpezaIdeal();
                    if (Limpeza >= ideal)
                    {
                        AjustarReputacao(+0.25);
                        AjustarSanitario(+15);
                        Narrativa.BlitzAprovadas++;
                        Narrativa.RegistrarCronica($"Aprovado com louvor na Blitz Sanitária ({Limpeza}/{ideal}).");
                        VerificarConquistas();
                        EventoDoDiaDescricao = $"BLITZ SANITÁRIA APROVADA ({Limpeza}/{ideal})! +0,25* Reputação e Selo Sanitário fortalecido!";
                    }
                    else
                    {
                        AjustarSanitario(-15);
                        EventoDoDiaDescricao = $"BLITZ SANITÁRIA! Higiene abaixo do ideal ({Limpeza}/{ideal})!";
                        DispararProcesso("Blitz da Vigilância Sanitária!", "Inspeção surpresa flagrou falta de higiene adequada no turno.", ConfigAtual.PrecoMedioPrato * 4.5);
                    }
                    break;

                case 4:
                    _eventoAtivoCodigo = "CLIMA_INVERNO";
                    EventoDoDiaDescricao = "FRENTE FRIA (INVERNO)! Pratos quentes (Macarrão, Lasanha, Yakisoba, Risoto) rendem +35%!";
                    break;

                case 5:
                    _eventoAtivoCodigo = "CLIMA_VERAO";
                    EventoDoDiaDescricao = "ONDA DE CALOR (VERÃO)! Pratos leves (Salada, Sanduíche, Sushi, Açaí) rendem +35%!";
                    break;

                case 6:
                    _eventoAtivoCodigo = "MENU_EVENTO";
                    AcoesRestantesDia += 2;
                    EventoDoDiaDescricao = "NOITE DO CHEF! Pratos Especiais/Gourmet dão +40% de bônus e +2 de Energia hoje!";
                    break;

                case 7:
                    _eventoAtivoCodigo = "CRITICO";
                    EventoDoDiaDescricao = "VISITA DA CRÍTICA DONA HELENA! Próximo pedido vale +0,5* e 2x Valor!";
                    break;

                case 8:
                    _eventoAtivoCodigo = "FESTIVAL_LOCAL";
                    AjustarMoral(+5);
                    EventoDoDiaDescricao = "FESTIVAL GASTRONÔMICO NO BAIRRO! Todos os pratos pagam +25% e Moral da Equipe +5!";
                    break;

                case 9:
                    _eventoAtivoCodigo = "INFLUENCIADOR";
                    EventoDoDiaDescricao = "INFLUENCIADOR NO SALÃO! Próximo pedido perfeito dá +0,35* e +50% valor (erro penaliza em dobro)!";
                    break;
            }
        }

        // ================== CARDÁPIO E COZINHA ==================
        public void AlternarPratoNoCardapio(string nomePrato)
        {
            var antes = CapturarSnapshot();
            if (PratosAtivosNoCardapio.Contains(nomePrato))
            {
                if (PratosAtivosNoCardapio.Count <= 2)
                {
                    TocarSom(false);
                    RegistrarFeedback("Cardápio & Receitas", $"Desativar {nomePrato}",
                        $"Não foi possível desativar [{nomePrato}]: mantenha no mínimo 2 pratos ativos no cardápio.",
                        FeedbackSeverity.Erro,
                        sugestao: "Ative outro prato do cardápio antes de remover este.");
                    return;
                }
                PratosAtivosNoCardapio.Remove(nomePrato);
                var pedidoAnterior = PedidoAtual;
                GerarNovoPedido();
                EmpilharDesfazer($"Desativar {nomePrato} no cardápio", "Cardápio & Receitas", () =>
                {
                    PratosAtivosNoCardapio.Add(nomePrato);
                    if (pedidoAnterior != null) PedidoAtual = pedidoAnterior;
                });
                RegistrarFeedback("Cardápio & Receitas", $"Desativar {nomePrato}",
                    $"[{nomePrato}] removido do cardápio ativo ({PratosAtivosNoCardapio.Count} pratos ativos).",
                    FeedbackSeverity.Info, antes);
            }
            else
            {
                var receita = Catalog.Pratos.FirstOrDefault(p => p.Nome == nomePrato);
                if (receita == null || receita.NivelDesbloqueio > NivelAtual)
                {
                    TocarSom(false);
                    RegistrarFeedback("Cardápio & Receitas", $"Ativar {nomePrato}",
                        $"Prato [{nomePrato}] ainda está bloqueado para a Fase {NivelAtual}/5.",
                        FeedbackSeverity.Erro,
                        sugestao: $"Evolua até a Fase {receita?.NivelDesbloqueio ?? NivelAtual + 1} em Gerência [5] para desbloqueá-lo.");
                    return;
                }

                PratosAtivosNoCardapio.Add(nomePrato);
                var pedidoAnterior = PedidoAtual;
                GerarNovoPedido();
                EmpilharDesfazer($"Ativar {nomePrato} no cardápio", "Cardápio & Receitas", () =>
                {
                    PratosAtivosNoCardapio.Remove(nomePrato);
                    if (pedidoAnterior != null) PedidoAtual = pedidoAnterior;
                });
                RegistrarFeedback("Cardápio & Receitas", $"Ativar {nomePrato}",
                    $"[{nomePrato}] ativado no cardápio! ({PratosAtivosNoCardapio.Count} pratos ativos).",
                    FeedbackSeverity.Sucesso, antes);
            }
        }

        public double CalcularValorVendaPrato()
        {
            double basePrice = ConfigAtual.PrecoMedioPrato * MultiplicadorPrecoPrato;

            int funcEfetivos = Math.Min(Funcionarios, (int)(ConfigAtual.RecFuncionarios * 1.5) + 4);
            double fatorSalario = Math.Min(1.25, MultiplicadorSalario);
            double bonusFuncionarios = Math.Sqrt(funcEfetivos) * (ConfigAtual.PrecoMedioPrato * 0.18) * fatorSalario;

            int sementeGorjeta = PedidoAtual != null ? PedidoAtual.SorteioGorjetaBase : 12;
            double fatorMoral = Narrativa.MoralEquipe >= 75 ? 1.08 : (Narrativa.MoralEquipe <= 35 ? 0.95 : 1.0);
            double fatorDiversao = (1.0 + (Diversao / 500.0)) * fatorMoral;
            double bonusUpgrade = TotalUpgrades > 0
                ? (sementeGorjeta + (UpgBalcao * 3.0) + (TotalUpgrades * 0.5)) * fatorDiversao
                : 0;

            double multSazonal = 1.0;
            if (PedidoAtual != null && PedidoAtual.EPedidoSazonalBonus)
                multSazonal = 1.35;
            else if (_eventoAtivoCodigo == "FESTIVAL_LOCAL")
                multSazonal = 1.25;
            else if (_eventoAtivoCodigo == "INFLACAO_INSUMO")
                multSazonal = 1.15;

            double multEficiencia = 1.0 + BonusEficienciaCombo;
            return Math.Round((basePrice + bonusFuncionarios + bonusUpgrade) * multSazonal * multEficiencia, 2);
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
            bool podeInverter = !(NivelAtual == 1 && PedidosPerfeitosTotal < 3);
            if (podeInverter && recheioCliente.Count >= 2 && _rng.Next(100) < 45)
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

            bool bonusSazonal = (_eventoAtivoCodigo == "CLIMA_INVERNO" && receita.EstacaoFavorita == "INVERNO") ||
                                (_eventoAtivoCodigo == "CLIMA_VERAO" && receita.EstacaoFavorita == "VERAO") ||
                                (_eventoAtivoCodigo == "MENU_EVENTO" && (receita.EPratoDeEvento || receita.EstacaoFavorita == "EVENTO"));

            bool critico = _eventoAtivoCodigo == "CRITICO";
            bool influenciador = _eventoAtivoCodigo == "INFLUENCIADOR";
            string prefixo = critico ? "[CRÍTICA DONA HELENA] "
                : (influenciador ? "[INFLUENCIADOR] "
                : (bonusSazonal ? "[BÔNUS SAZONAL +35%] " : ""));
            string textoSem = excluidos.Count > 0 ? $" SEM {string.Join(" e SEM ", excluidos)}" : "";

            PedidoAtual = new CustomerOrder
            {
                Receita = receita,
                OrdemExigida = ordemFinal,
                IngredientesExcluidos = excluidos,
                OpcoesBancada = opcoes.OrderBy(_ => _rng.Next()).ToList(),
                EClienteCritico = critico,
                EInfluenciador = influenciador,
                EPedidoSazonalBonus = bonusSazonal,
                SorteioGorjetaBase = _rng.Next(5, 21),
                FalaDoCliente = $"{prefixo}\"Quero {receita.Nome}{textoSem}!\"{instrucaoTroca}"
            };
        }

        public double ObterCustoCompraEmergencial(string ingrediente)
        {
            double multiplicadorEvento = (_eventoAtivoCodigo == "INFLACAO_INSUMO" && ingrediente == IngredienteEmFaltaOuCaro) ? 3.0 : 1.0;
            return Math.Round(28.0 * NivelAtual * multiplicadorEvento, 2);
        }

        public void ComprarIngredientesFaltantesPedido()
        {
            if (PedidoAtual == null) return;
            var faltantes = PedidoAtual.OrdemExigida
                .Distinct()
                .Where(ing => !Estoque.TryGetValue(ing, out int q) || q <= 0)
                .ToList();

            if (faltantes.Count == 0)
            {
                RegistrarFeedback("Cozinha", "Comprar faltantes do pedido",
                    $"Todos os ingredientes de {PedidoAtual.Receita.Nome} já estão em estoque!",
                    FeedbackSeverity.Info,
                    sugestao: $"Continue montando o prato começando por [{PedidoAtual.OrdemExigida[0]}].");
                return;
            }

            var antes = CapturarSnapshot();
            int comprados = 0;
            double gastoTotal = 0;
            foreach (var ing in faltantes)
            {
                double custo = ObterCustoCompraEmergencial(ing);
                if (Dinheiro < custo && ModoJogo != GameMode.GestaoLivre) break;
                ModificarDinheiro(-custo);
                Estoque[ing] = (Estoque.ContainsKey(ing) ? Estoque[ing] : 0) + 4;
                gastoTotal += custo;
                comprados++;
            }

            if (comprados == 0)
            {
                TocarSom(false);
                RegistrarFeedback("Cozinha", "Comprar faltantes do pedido",
                    $"Saldo insuficiente ({FmtMoney(Dinheiro)}) para reposição emergencial dos itens faltantes.",
                    FeedbackSeverity.Erro,
                    sugestao: "Aprove entregas gratuitas nas Docas [2] ou solicite crédito no Banco [6].");
                return;
            }

            InvalidarDesfazer("Compra emergencial em lote movimentou caixa e estoque.");
            TocarSom(true);
            RegistrarFeedback("Cozinha", $"Repor {comprados} item(ns) faltante(s)",
                $"Reposição rápida concluída: +{comprados * 4} un. distribuídas em {comprados} insumo(s) por {FmtMoney(gastoTotal)}.",
                FeedbackSeverity.Sucesso, antes);
        }

        public void AdicionarIngredienteAoPrato(string ingrediente)
        {
            if (PedidoAtual == null) return;

            int jaUsadosNoPrato = PedidoAtual.PratoMontado.Count(i => i == ingrediente);
            int disponivelEstoque = Estoque.ContainsKey(ingrediente) ? Estoque[ingrediente] : 0;

            if (disponivelEstoque - jaUsadosNoPrato <= 0)
            {
                double custo = ObterCustoCompraEmergencial(ingrediente);
                TocarSom(false);
                RegistrarFeedback("Cozinha", $"Adicionar {ingrediente}",
                    $"Não foi possível adicionar [{ingrediente}]: estoque esgotado ({disponivelEstoque} un. disponíveis).",
                    FeedbackSeverity.Erro,
                    sugestao: $"Clique em +BUY (emergencial por {FmtMoney(custo)}), digite \"comprar faltantes\" ou audite entregas nas Docas [2].");
                return;
            }

            PedidoAtual.PratoMontado.Add(ingrediente);
            var pedidoRef = PedidoAtual;
            EmpilharDesfazer($"Adicionar {ingrediente} na bancada", "Cozinha", () =>
            {
                if (PedidoAtual == pedidoRef && PedidoAtual.PratoMontado.Count > 0)
                    PedidoAtual.PratoMontado.RemoveAt(PedidoAtual.PratoMontado.Count - 1);
            });

            string proximoHint = PedidoAtual.PratoMontado.Count < PedidoAtual.OrdemExigida.Count
                ? $"Faltam {PedidoAtual.OrdemExigida.Count - PedidoAtual.PratoMontado.Count} item(ns) para finalizar."
                : "Prato pronto! Pressione [E] ou Enter para servir.";

            RegistrarFeedback("Cozinha", $"Adicionar {ingrediente}",
                $"[{ingrediente}] colocado na bancada ({PedidoAtual.PratoMontado.Count}/{PedidoAtual.OrdemExigida.Count}). {proximoHint}",
                FeedbackSeverity.Info,
                adicionarAoHistorico: false);
        }

        public void CompraEmergencialIngrediente(string ingrediente)
        {
            var antes = CapturarSnapshot();
            double custo = ObterCustoCompraEmergencial(ingrediente);

            if (Dinheiro < custo && ModoJogo != GameMode.GestaoLivre)
            {
                TocarSom(false);
                RegistrarFeedback("Cozinha", $"Compra emergencial ({ingrediente})",
                    $"Compra falhou: saldo ({FmtMoney(Dinheiro)}) é menor que {FmtMoney(custo)} para +4 un. de [{ingrediente}].",
                    FeedbackSeverity.Erro,
                    sugestao: "Aprove entregas nas Docas [2] ou solicite crédito no Banco [6].");
                return;
            }

            InvalidarDesfazer("Compra emergencial movimentou caixa e estoque de forma definitiva.");
            ModificarDinheiro(-custo);
            Estoque[ingrediente] = (Estoque.ContainsKey(ingrediente) ? Estoque[ingrediente] : 0) + 4;
            TocarSom(true);
            RegistrarFeedback("Cozinha", $"Compra emergencial ({ingrediente})",
                $"Comprou +4 un. de [{ingrediente}] por {FmtMoney(custo)}. Estoque atual: {Estoque[ingrediente]} un.",
                FeedbackSeverity.Sucesso, antes);
        }

        public void DesfazerUltimoIngrediente()
        {
            if (PedidoAtual == null || PedidoAtual.PratoMontado.Count == 0)
            {
                RegistrarFeedback("Cozinha", "Desfazer ingrediente",
                    "A bancada já está vazia — não há ingrediente para remover.",
                    FeedbackSeverity.Aviso,
                    sugestao: $"Comece adicionando [{PedidoAtual?.OrdemExigida.FirstOrDefault() ?? "a base do prato"}].",
                    adicionarAoHistorico: false);
                return;
            }
            DesfazerUltimaAcao();
        }

        public void LimparPratoAtual()
        {
            if (PedidoAtual == null || PedidoAtual.PratoMontado.Count == 0)
            {
                RegistrarFeedback("Cozinha", "Limpar bancada",
                    "A bancada já está limpa.",
                    FeedbackSeverity.Aviso,
                    sugestao: $"Adicione [{PedidoAtual?.OrdemExigida.FirstOrDefault() ?? "o ingrediente base"}] para iniciar o prato.",
                    adicionarAoHistorico: false);
                return;
            }

            var copiaAnterior = new List<string>(PedidoAtual.PratoMontado);
            var pedidoRef = PedidoAtual;
            PedidoAtual.PratoMontado.Clear();

            EmpilharDesfazer($"Limpar bancada ({copiaAnterior.Count} itens)", "Cozinha", () =>
            {
                if (PedidoAtual == pedidoRef)
                {
                    PedidoAtual.PratoMontado.Clear();
                    PedidoAtual.PratoMontado.AddRange(copiaAnterior);
                }
            });

            RegistrarFeedback("Cozinha", "Limpar bancada",
                $"Bancada limpa ({copiaAnterior.Count} ingrediente(s) devolvido(s) à mesa).",
                FeedbackSeverity.Info,
                sugestao: "Pressione [Z] se quiser restaurar o prato anterior.",
                adicionarAoHistorico: false);
        }

        public void EntregarPedido()
        {
            if (PedidoAtual == null || GameOver || JogoVencido) return;

            if (PedidoAtual.PratoMontado.Count == 0)
            {
                string primeiro = PedidoAtual.OrdemExigida.FirstOrDefault() ?? "a base do prato";
                TocarSom(false);
                RegistrarFeedback("Cozinha", $"Entregar {PedidoAtual.Receita.Nome}",
                    "Não é possível entregar um prato vazio! Nenhuma energia foi gasta.",
                    FeedbackSeverity.Erro,
                    sugestao: $"Monte o pedido seguindo o Guia começando por [{primeiro}].");
                return;
            }

            var antes = CapturarSnapshot();
            string nomePrato = PedidoAtual.Receita.Nome;
            InvalidarDesfazer("Entregar pedido consome energia e registra resultado definitivo.");

            double toleranciaDif = Dificuldade == DifficultyMode.Dificil ? -0.08 : (Dificuldade == DifficultyMode.Facil ? 0.10 : 0.0);
            double precoMaxTolerado = 0.85 + toleranciaDif + (Reputacao * 0.22);
            if (MultiplicadorPrecoPrato > precoMaxTolerado && _rng.Next(100) < 55)
            {
                SequenciaPerfeita = 0;
                AjustarReputacao(-0.20);
                TocarSom(false);
                GerarNovoPedido();
                RegistrarAcaoNoMes(sucesso: false);
                RegistrarFeedback("Cozinha", $"Entregar {nomePrato}",
                    $"Cliente reclamou do preço caro ({MultiplicadorPrecoPrato * 100:F0}%) para sua Reputação ({Reputacao:F1}*) e desistiu!",
                    FeedbackSeverity.Erro, antes,
                    sugestao: "Reduza o preço do cardápio em Gerência [5] ou eleve sua Reputação.");
                return;
            }

            if (MultiplicadorPrecoPrato > 1.15)
            {
                int chanceDesistir = (int)((MultiplicadorPrecoPrato - 1.15) * 140);
                if (_rng.Next(100) < chanceDesistir)
                {
                    SequenciaPerfeita = 0;
                    AjustarReputacao(-0.10);
                    TocarSom(false);
                    GerarNovoPedido();
                    RegistrarAcaoNoMes(sucesso: false);
                    RegistrarFeedback("Cozinha", $"Entregar {nomePrato}",
                        $"Cliente achou o cardápio caro ({MultiplicadorPrecoPrato * 100:F0}%) e foi embora sem comprar!",
                        FeedbackSeverity.Erro, antes,
                        sugestao: "Ajuste o preço para 100%-110% em Gerência [5] para evitar desistências.");
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
            string respostaFinal;
            string sugestaoFinal = "";
            FeedbackSeverity sev;

            if (erroDetalhado == null && !usouIngredienteContaminado)
            {
                acertou = true;
                SequenciaPerfeita++;
                MelhorSequenciaPerfeita = Math.Max(MelhorSequenciaPerfeita, SequenciaPerfeita);

                double ganho = CalcularValorVendaPrato();
                if (PedidoAtual.EClienteCritico)
                {
                    ganho = Math.Round(ganho * 2.0, 2);
                    AjustarReputacao(+0.50);
                    Narrativa.RegistrarCronica($"Dona Helena (Crítica) elogiou {nomePrato} publicamente!");
                    _eventoAtivoCodigo = "NORMAL";
                }
                else if (PedidoAtual.EInfluenciador)
                {
                    ganho = Math.Round(ganho * 1.5, 2);
                    AjustarReputacao(+0.35);
                    Narrativa.RegistrarCronica($"Influenciador viralizou seu prato {nomePrato} nas redes!");
                    _eventoAtivoCodigo = "NORMAL";
                }
                else
                {
                    AjustarReputacao(+0.12 + (UpgBalcao * 0.02));
                }

                if (SequenciaPerfeita % 3 == 0) AjustarMoral(+2);

                ModificarDinheiro(ganho);
                PedidosPerfeitosTotal++;
                var novasConq = VerificarConquistas();
                TocarSom(true);

                string comboTxt = SequenciaPerfeita >= 3 ? $" · Combo {SequenciaPerfeita}x (+{BonusEficienciaCombo * 100:F0}%)" : "";
                string conqTxt = novasConq.Count > 0 ? $" · 🏆 Conquista: {string.Join(", ", novasConq)}!" : "";
                respostaFinal = $"PEDIDO PERFEITO ({nomePrato})! +{FmtMoney(ganho)}{comboTxt}{conqTxt}";
                sev = FeedbackSeverity.Sucesso;
            }
            else if (usouIngredienteContaminado)
            {
                SequenciaPerfeita = 0;
                double multaContam = Math.Round(ConfigAtual.PrecoMedioPrato * 1.2 * FatorMultaDificuldade, 2);
                ModificarDinheiro(-multaContam);
                AjustarReputacao(-0.35);
                AjustarSanitario(-25);
                Narrativa.RegistrarCronica($"Intoxicação alimentar registrada com {nomePrato} (estoque contaminado).");
                TocarSom(false);
                respostaFinal = $"PRATO ESTRAGADO! Ingrediente contaminado causou intoxicação (-{FmtMoney(multaContam)})!";
                sugestaoFinal = "Recuse caixas suspeitas nas Docas [2] e confira temperatura e lote.";
                sev = FeedbackSeverity.Erro;
                DispararProcesso("Intoxicação Alimentar Grave!", "Cliente passou mal por ingrediente vencido/contaminado do estoque.", 450.0 * NivelAtual);
            }
            else
            {
                SequenciaPerfeita = 0;
                double fatorMulta = (NivelAtual == 1 ? 0.65 : (NivelAtual == 2 ? 0.85 : 1.15)) * FatorMultaDificuldade;
                double multaErroPrato = Math.Round(ConfigAtual.PrecoMedioPrato * fatorMulta, 2);
                ModificarDinheiro(-multaErroPrato);
                double penRep = PedidoAtual.EClienteCritico ? -0.45 : (PedidoAtual.EInfluenciador ? -0.36 : -0.18);
                AjustarReputacao(penRep);
                if (PedidoAtual.EInfluenciador) _eventoAtivoCodigo = "NORMAL";
                TocarSom(false);
                respostaFinal = $"PEDIDO ERRADO ({nomePrato})! {erroDetalhado}";
                sugestaoFinal = "Siga a ordem do Guia ou digite \"falar com Pedro\" antes de entregar.";
                sev = FeedbackSeverity.Erro;

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

            RegistrarFeedback("Cozinha", $"Entregar {nomePrato}", respostaFinal, sev, antes, sugestaoFinal);
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

        // ================== ENTREGAS (DOCAS) ==================
        public void GerarNovaEntrega(bool cobrarTaxa = true)
        {
            if (cobrarTaxa)
            {
                double taxa = Math.Round(35.0 * FatorCustoFixoDificuldade, 2);
                ModificarDinheiro(-taxa);
                if (GameOver) return;
            }

            InspecoesCaixaRestantes = InspecoesCaixaMaximas;
            EntregaAtual = new List<DeliveryItemRecord>();

            var ingredientesNivel = Catalog.TodosIngredientes
                .Where(i => i.Nivel <= NivelAtual && (_eventoAtivoCodigo != "ESCASSEZ_FORNECEDOR" || i.Nome != IngredienteEmFaltaOuCaro))
                .ToList();

            int bonusConfianca = Narrativa.ConfiancaFornecedores >= 75 ? 1 : 0;

            for (int i = 0; i < 7; i++)
            {
                bool seraValido = _rng.Next(100) < 60;
                var info = ingredientesNivel[_rng.Next(ingredientesNivel.Count)];

                string nomeNota = info.Nome;
                string nomeFisico = info.Nome;
                string fornecedor = Catalog.FornecedoresValidos[_rng.Next(Catalog.FornecedoresValidos.Count)];
                string cidade = Catalog.CidadesValidas[_rng.Next(Catalog.CidadesValidas.Count)];
                string dataNota = DataHojeFormatada;
                string lote = $"LT-{_rng.Next(100, 999)}";
                int redEscassez = _eventoAtivoCodigo == "ESCASSEZ_FORNECEDOR" ? 1 : 0;
                int qtd = Math.Max(2, _rng.Next(3, 6) + (UpgLogistica / 3) + bonusConfianca - redEscassez);
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
            if (EntregaAtual == null || index < 0 || index >= EntregaAtual.Count)
            {
                RegistrarFeedback("Docas (Entregas)", $"Abrir caixa #{index + 1}",
                    $"Caixa #{index + 1} não existe nesta carga.",
                    FeedbackSeverity.Erro,
                    sugestao: $"Informe um número entre 1 e {EntregaAtual?.Count ?? 7} (ex.: \"abrir caixa 1\").");
                return;
            }

            if (EntregaAtual[index].CaixaAberta)
            {
                var cx = EntregaAtual[index];
                RegistrarFeedback("Docas (Entregas)", $"Abrir caixa #{index + 1}",
                    $"Caixa #{index + 1} já está aberta: Físico [{cx.NomeFisico}], Lote [{cx.Lote}], Temp [{cx.Temperatura}].",
                    FeedbackSeverity.Aviso,
                    sugestao: $"Use \"aceitar caixa {index + 1}\" ou \"recusar caixa {index + 1}\" para definir sua decisão.");
                return;
            }

            if (InspecoesCaixaRestantes <= 0)
            {
                TocarSom(false);
                RegistrarFeedback("Docas (Entregas)", $"Abrir caixa #{index + 1}",
                    "Não foi possível abrir a caixa: 0 inspeções restantes nesta carga!",
                    FeedbackSeverity.Erro,
                    sugestao: "Decida pelas informações da nota (fornecedor/cidade/data) ou melhore Logística em Gerência [5].");
                return;
            }

            InvalidarDesfazer("Abrir caixa revela conteúdo físico oculto e não pode ser desfeito.");
            EntregaAtual[index].CaixaAberta = true;
            InspecoesCaixaRestantes--;
            var item = EntregaAtual[index];
            TocarSom(true);
            RegistrarFeedback("Docas (Entregas)", $"Inspecionar Caixa #{index + 1}",
                $"Caixa #{index + 1} aberta! Físico: [{item.NomeFisico}] · Lote: [{item.Lote}] · Temp: [{item.Temperatura}]. Restam {InspecoesCaixaRestantes} inspeção(ões).",
                FeedbackSeverity.Info);
        }

        public void AlternarAprovacaoItemEntrega(int index, bool? definirValor = null)
        {
            if (EntregaAtual == null || index < 0 || index >= EntregaAtual.Count)
            {
                RegistrarFeedback("Docas (Entregas)", $"Decidir caixa #{index + 1}",
                    $"Caixa #{index + 1} não encontrada.",
                    FeedbackSeverity.Erro,
                    sugestao: "Escolha uma caixa de 1 a 7.");
                return;
            }

            var item = EntregaAtual[index];
            bool valorAnterior = item.MarcadoComoValidoPeloJogador;
            bool novoValor = definirValor ?? !valorAnterior;
            if (novoValor == valorAnterior)
            {
                RegistrarFeedback("Docas (Entregas)", $"Caixa #{index + 1}",
                    $"Caixa #{index + 1} ({item.NomeNota}) já estava marcada como {(novoValor ? "ACEITAR" : "RECUSAR")}.",
                    FeedbackSeverity.Aviso,
                    adicionarAoHistorico: false);
                return;
            }

            item.MarcadoComoValidoPeloJogador = novoValor;
            var cargaRef = EntregaAtual;
            EmpilharDesfazer($"Marcar Caixa #{index + 1} como {(novoValor ? "ACEITAR" : "RECUSAR")}", "Docas (Entregas)", () =>
            {
                if (EntregaAtual == cargaRef && index < EntregaAtual.Count)
                    EntregaAtual[index].MarcadoComoValidoPeloJogador = valorAnterior;
            });

            RegistrarFeedback("Docas (Entregas)", $"Decidir Caixa #{index + 1}",
                $"Caixa #{index + 1} ({item.NomeNota}) marcada como {(novoValor ? "ACEITAR" : "RECUSAR")}.",
                FeedbackSeverity.Info,
                adicionarAoHistorico: false);
        }

        public void FinalizarConferenciaEntrega()
        {
            if (EntregaAtual == null || GameOver || JogoVencido) return;

            var antes = CapturarSnapshot();
            InvalidarDesfazer("Concluir auditoria consome energia e atualiza o estoque definitivamente.");

            int erros = 0;
            int itensAdicionados = 0;
            var detalhesErros = new List<string>();

            for (int i = 0; i < EntregaAtual.Count; i++)
            {
                var item = EntregaAtual[i];
                if (item.EValidoReal != item.MarcadoComoValidoPeloJogador)
                {
                    erros++;
                    if (detalhesErros.Count < 2)
                    {
                        string acaoErrada = item.MarcadoComoValidoPeloJogador ? $"aceitou #{i + 1} ({item.MotivoErro})" : $"recusou #{i + 1} (era válida)";
                        detalhesErros.Add(acaoErrada);
                    }
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

            string resposta;
            string sugestao = "";
            FeedbackSeverity sev;

            if (erros == 0)
            {
                double bonus = 80.0 + (NivelAtual * 25.0);
                ModificarDinheiro(bonus);
                Narrativa.AuditoriasImpecaveis++;
                AjustarConfiancaFornecedores(+8);
                AjustarSanitario(+5);
                var novas = VerificarConquistas();
                TocarSom(true);
                string conqTxt = novas.Count > 0 ? $" · 🏆 Conquista: {string.Join(", ", novas)}!" : "";
                resposta = $"CONFERÊNCIA IMPECÁVEL! +{itensAdicionados} un. no estoque e +{FmtMoney(bonus)} de bônus!{conqTxt}";
                sev = FeedbackSeverity.Sucesso;
            }
            else
            {
                double multaPorItem = Math.Round(18.0 * NivelAtual * FatorMultaDificuldade, 2);
                double multa = Math.Round(erros * multaPorItem, 2);
                ModificarDinheiro(-multa);
                AjustarConfiancaFornecedores(-10);
                TocarSom(false);
                string resumo = detalhesErros.Count > 0 ? $" Ex.: {string.Join("; ", detalhesErros)}." : "";
                resposta = $"Auditoria com {erros} erro(s) (-{FmtMoney(multa)}).{resumo}";
                sugestao = "Confira cidade, fornecedor, data de hoje e abra caixas suspeitas antes de concluir.";
                sev = FeedbackSeverity.Erro;
            }

            if (!GameOver && !JogoVencido)
            {
                GerarNovaEntrega(cobrarTaxa: true);
                RegistrarAcaoNoMes(sucesso: erros == 0);
            }

            RegistrarFeedback("Docas (Entregas)", "Concluir auditoria de entrega", resposta, sev, antes, sugestao);
        }

        // ================== GERÊNCIA E COMPRAS EM LOTE ==================
        public void ContratarFuncionariosLote(int quantidade)
        {
            var antes = CapturarSnapshot();
            double custoUnit = ConfigAtual.CustoPorFuncionario;
            int maxPossivel = ModoJogo == GameMode.GestaoLivre ? Math.Min(quantidade, 50) : Math.Max(0, (int)Math.Floor(Dinheiro / custoUnit));
            int qtdReal = Math.Min(quantidade, maxPossivel);

            if (qtdReal <= 0)
            {
                TocarSom(false);
                RegistrarFeedback("Escritório (Gerência)", "Contratar funcionário",
                    $"Não foi possível contratar: cada funcionário custa {FmtMoney(custoUnit)} e seu caixa atual é {FmtMoney(Dinheiro)}.",
                    FeedbackSeverity.Erro,
                    sugestao: "Entregue pedidos na Cozinha [1] ou solicite empréstimo no Banco [6].");
                return;
            }

            InvalidarDesfazer("Contratação altera folha e pode disparar evolução de fase.");
            ModificarDinheiro(-(qtdReal * custoUnit));
            Funcionarios += qtdReal;
            TocarSom(true);

            bool subiu = VerificarSubidaDeNivel();
            VerificarConquistas();

            string avisoFolha = Funcionarios > ConfigAtual.RecFuncionarios
                ? $"Atenção: acima do recomendado ({ConfigAtual.RecFuncionarios}) o ganho por prato tem retorno reduzido."
                : "";

            string resp = subiu
                ? UltimaMensagem
                : $"+{qtdReal} Funcionário(s) contratado(s)! Total: {Funcionarios} (Recomendado Fase {NivelAtual}: {ConfigAtual.RecFuncionarios}).";

            RegistrarFeedback("Escritório (Gerência)", $"Contratar +{qtdReal} funcionário(s)", resp, FeedbackSeverity.Sucesso, antes, avisoFolha);
        }

        public void ComprarLimpezaLote(int quantidade)
        {
            var antes = CapturarSnapshot();
            double custoUnit = ConfigAtual.CustoPorLimpeza;
            int maxPossivel = ModoJogo == GameMode.GestaoLivre ? Math.Min(quantidade, 50) : Math.Max(0, (int)Math.Floor(Dinheiro / custoUnit));
            int qtdReal = Math.Min(quantidade, maxPossivel);

            if (qtdReal <= 0)
            {
                TocarSom(false);
                RegistrarFeedback("Escritório (Gerência)", "Investir em limpeza",
                    $"Não foi possível comprar limpeza: cada nível custa {FmtMoney(custoUnit)} e seu caixa é {FmtMoney(Dinheiro)}.",
                    FeedbackSeverity.Erro,
                    sugestao: "Entregue pedidos na Cozinha [1] para levantar caixa.");
                return;
            }

            InvalidarDesfazer("Compra de limpeza altera caixa e pode evoluir a fase.");
            ModificarDinheiro(-(qtdReal * custoUnit));
            Limpeza += qtdReal;
            AjustarSanitario(Math.Min(10, qtdReal * 2));
            TocarSom(true);

            bool subiu = VerificarSubidaDeNivel();
            string resp = subiu
                ? UltimaMensagem
                : $"+{qtdReal} Nível(is) de Limpeza! Total: {Limpeza} (Ideal atual: {ObterLimpezaIdeal()}).";

            RegistrarFeedback("Escritório (Gerência)", $"Comprar +{qtdReal} limpeza", resp, FeedbackSeverity.Sucesso, antes);
        }

        public double ObterCustoUpgradeEspecifico(int nivelAtualUpg)
        {
            return Math.Round(ConfigAtual.CustoUpgradeBase * (1.0 + (nivelAtualUpg * 0.10)), 2);
        }

        public int ObterNivelUpgrade(string tipo) => tipo switch
        {
            "FORNO" => UpgForno,
            "GELADEIRA" => UpgGeladeira,
            "BALCAO" => UpgBalcao,
            _ => UpgLogistica
        };

        public void ComprarUpgradeNomeado(string tipo, int lote)
        {
            var antes = CapturarSnapshot();
            double custoInicial = ObterCustoUpgradeEspecifico(ObterNivelUpgrade(tipo));

            int comprados = 0;
            for (int i = 0; i < Math.Min(lote, 50); i++)
            {
                int nivelRef = ObterNivelUpgrade(tipo);
                double custo = ObterCustoUpgradeEspecifico(nivelRef);
                if (Dinheiro < custo && ModoJogo != GameMode.GestaoLivre) break;

                ModificarDinheiro(-custo);
                comprados++;

                if (tipo == "FORNO") UpgForno++;
                else if (tipo == "GELADEIRA") UpgGeladeira++;
                else if (tipo == "BALCAO") UpgBalcao++;
                else UpgLogistica++;
            }

            if (comprados == 0)
            {
                TocarSom(false);
                RegistrarFeedback("Escritório (Gerência)", $"Upgrade {tipo}",
                    $"Não foi possível melhorar [{tipo}]: custa {FmtMoney(custoInicial)} e você tem {FmtMoney(Dinheiro)}.",
                    FeedbackSeverity.Erro,
                    sugestao: "Fature mais pedidos na Cozinha [1] ou use crédito no Banco [6].");
                return;
            }

            InvalidarDesfazer("Compra de upgrade é permanente e pode evoluir a fase.");
            TocarSom(true);
            bool subiu = VerificarSubidaDeNivel();
            string resp = subiu
                ? UltimaMensagem
                : $"Upgrade [{tipo}] melhorado +{comprados}x! Total de Upgrades: {TotalUpgrades}.";

            RegistrarFeedback("Escritório (Gerência)", $"Upgrade {tipo} (+{comprados})", resp, FeedbackSeverity.Sucesso, antes);
        }

        public void AjustarSalario(double delta)
        {
            var antes = CapturarSnapshot();
            double anterior = MultiplicadorSalario;
            int moralAnterior = Narrativa.MoralEquipe;
            double novo = Math.Clamp(Math.Round(MultiplicadorSalario + delta, 2), 0.5, 2.0);

            if (Math.Abs(novo - anterior) < 0.001)
            {
                RegistrarFeedback("Escritório (Gerência)", "Ajustar salários",
                    $"Salários já estão no limite ({anterior * 100:F0}%).",
                    FeedbackSeverity.Aviso,
                    sugestao: delta > 0 ? "O máximo é 200%. Você pode reduzir com -10%." : "O mínimo é 50%. Você pode aumentar com +10%.");
                return;
            }

            MultiplicadorSalario = novo;

            if (delta > 0 && MultiplicadorSalario >= 1.10)
            {
                AjustarMoral(+5);
                VerificarConquistas();
            }
            else if (delta < 0 && MultiplicadorSalario < 0.90)
            {
                AjustarMoral(-6);
            }

            if (delta < 0 && MultiplicadorSalario < 0.85 && Funcionarios > 0 && _rng.Next(100) < 40)
            {
                InvalidarDesfazer("Corte salarial provocou demissão — ação não pode ser desfeita.");
                Funcionarios--;
                AjustarReputacao(-0.15);
                AjustarMoral(-12);
                Narrativa.RegistrarCronica($"Corte salarial para {MultiplicadorSalario * 100:F0}% gerou pedido de demissão.");
                TocarSom(false);
                if (_rng.Next(100) < 55)
                {
                    DispararProcesso("Reclamação Trabalhista!", "Ex-funcionário cobra redução salarial indevida na Justiça do Trabalho.", 550.0 * NivelAtual);
                }
                RegistrarFeedback("Escritório (Gerência)", "Reduzir salários",
                    $"Corte salarial para {MultiplicadorSalario * 100:F0}% fez um funcionário pedir demissão e derrubou a Moral da Equipe!",
                    FeedbackSeverity.Erro, antes,
                    sugestao: "Evite reduzir salários abaixo de 85% para proteger a equipe e evitar processos.");
                return;
            }

            EmpilharDesfazer($"Ajustar salários para {MultiplicadorSalario * 100:F0}%", "Escritório (Gerência)", () =>
            {
                MultiplicadorSalario = anterior;
                Narrativa.MoralEquipe = moralAnterior;
            });

            RegistrarFeedback("Escritório (Gerência)", $"Ajustar salários ({(delta > 0 ? "+" : "")}{delta * 100:F0}%)",
                $"Salários ajustados para {MultiplicadorSalario * 100:F0}% (Moral: {Narrativa.MoralEquipe}/100 · Folha: {FmtMoney(CalcularDespesaMensalFixa())}/mês).",
                FeedbackSeverity.Info, antes);
        }

        public void AjustarPrecoPratos(double delta)
        {
            var antes = CapturarSnapshot();
            double anterior = MultiplicadorPrecoPrato;
            double novo = Math.Clamp(Math.Round(MultiplicadorPrecoPrato + delta, 2), 0.6, 1.8);

            if (Math.Abs(novo - anterior) < 0.001)
            {
                RegistrarFeedback("Escritório (Gerência)", "Ajustar preço do cardápio",
                    $"Preços já estão no limite ({anterior * 100:F0}%).",
                    FeedbackSeverity.Aviso,
                    sugestao: delta > 0 ? "O teto é 180%." : "O piso é 60%.");
                return;
            }

            MultiplicadorPrecoPrato = novo;
            EmpilharDesfazer($"Ajustar preço do cardápio para {MultiplicadorPrecoPrato * 100:F0}%", "Escritório (Gerência)", () =>
            {
                MultiplicadorPrecoPrato = anterior;
            });

            string aviso = MultiplicadorPrecoPrato > 1.15
                ? "Atenção: preços acima de 115% podem fazer clientes desistirem!"
                : "";

            RegistrarFeedback("Escritório (Gerência)", $"Ajustar preços ({(delta > 0 ? "+" : "")}{delta * 100:F0}%)",
                $"Preço do cardápio ajustado para {MultiplicadorPrecoPrato * 100:F0}% (valor do pedido atual: {FmtMoney(CalcularValorVendaPrato())}).",
                MultiplicadorPrecoPrato > 1.15 ? FeedbackSeverity.Aviso : FeedbackSeverity.Info,
                antes, aviso);
        }

        private bool VerificarSubidaDeNivel()
        {
            bool subiu = false;
            while (NivelAtual < 5)
            {
                var prox = Catalog.Levels[NivelAtual];
                if (Funcionarios >= prox.MinFuncionarios && Limpeza >= prox.MinLimpeza && TotalUpgrades >= prox.MinUpgrades)
                {
                    NivelAtual++;
                    subiu = true;
                    ModificarDinheiro(prox.BonusNivel);
                    AjustarMoral(+8);
                    Narrativa.RegistrarCronica($"Restaurante promovido à Fase {NivelAtual}/5 ({prox.Titulo})!");

                    foreach (var ing in Catalog.TodosIngredientes.Where(i => i.Nivel == NivelAtual))
                    {
                        Estoque[ing.Nome] = Math.Max(Estoque[ing.Nome], 6);
                    }
                    foreach (var prato in Catalog.Pratos.Where(p => p.NivelDesbloqueio == NivelAtual))
                    {
                        PratosAtivosNoCardapio.Add(prato.Nome);
                    }

                    TocarSom(true, especial: true);
                    UltimaMensagem = $"SUBIU PARA A FASE {NivelAtual}/5 ({prox.Titulo})! Bônus: +{FmtMoney(prox.BonusNivel)} e novos pratos desbloqueados!";
                }
                else break;
            }

            if (subiu)
            {
                VerificarConquistas();
                SalvarAutomatico($"fase-{NivelAtual}");
            }

            VerificarCondicaoVitoria();
            return subiu;
        }

        // ================== MODO DESENVOLVEDOR (TERMINAL: SENHA xit89) ==================
        public void TentarDesbloquearTerminal()
        {
            if (string.Equals(InputSenhaTerminal?.Trim(), SenhaDesenvolvedor, StringComparison.OrdinalIgnoreCase))
            {
                TerminalDesbloqueado = true;
                TocarSom(true);
                RegistrarFeedback("Terminal Dev", "Autenticar Terminal",
                    "MODO DESENVOLVEDOR DESBLOQUEADO (xit89)! Controle total liberado.",
                    FeedbackSeverity.Sucesso);
            }
            else
            {
                TocarSom(false);
                string tent = InputSenhaTerminal;
                InputSenhaTerminal = "";
                RegistrarFeedback("Terminal Dev", "Autenticar Terminal",
                    $"Senha incorreta ('{tent}')! Acesso negado ao Terminal Dev.",
                    FeedbackSeverity.Erro,
                    sugestao: "Se não for desenvolvedor, volte para Cozinha [1] ou Gerência [5].");
            }
        }

        public void BloquearTerminal()
        {
            TerminalDesbloqueado = false;
            InputSenhaTerminal = "";
            RegistrarFeedback("Terminal Dev", "Bloquear Terminal",
                "Terminal de Desenvolvedor bloqueado com segurança.",
                FeedbackSeverity.Info);
        }

        public void DevAlterarDinheiro(double delta)
        {
            var antes = CapturarSnapshot();
            Dinheiro = Math.Round(Dinheiro + delta, 2);
            RegistrarFeedback("Terminal Dev", $"[DEV] Caixa ({delta:+0;-0})",
                $"[DEV] Caixa alterado em {FmtMoney(delta)}. Saldo atual: {FmtMoney(Dinheiro)}.",
                FeedbackSeverity.Info, antes);
        }

        public void DevDefinirDinheiro(double novoValor)
        {
            var antes = CapturarSnapshot();
            Dinheiro = Math.Round(novoValor, 2);
            RegistrarFeedback("Terminal Dev", "[DEV] Definir caixa",
                $"[DEV] Caixa definido para {FmtMoney(Dinheiro)}.",
                FeedbackSeverity.Info, antes);
        }

        public void DevAlterarFuncionarios(int delta)
        {
            var antes = CapturarSnapshot();
            Funcionarios = Math.Max(0, Funcionarios + delta);
            VerificarSubidaDeNivel();
            RegistrarFeedback("Terminal Dev", $"[DEV] Funcionários ({delta:+0;-0})",
                $"[DEV] Funcionários ajustados para {Funcionarios}.",
                FeedbackSeverity.Info, antes);
        }

        public void DevAlterarLimpeza(int delta)
        {
            var antes = CapturarSnapshot();
            Limpeza = Math.Max(0, Limpeza + delta);
            VerificarSubidaDeNivel();
            RegistrarFeedback("Terminal Dev", $"[DEV] Limpeza ({delta:+0;-0})",
                $"[DEV] Limpeza ajustada para {Limpeza}.",
                FeedbackSeverity.Info, antes);
        }

        public void DevAlterarUpgrades(string tipo, int delta)
        {
            var antes = CapturarSnapshot();
            if (tipo == "TODOS")
            {
                UpgForno = Math.Max(0, UpgForno + delta);
                UpgGeladeira = Math.Max(0, UpgGeladeira + delta);
                UpgBalcao = Math.Max(0, UpgBalcao + delta);
                UpgLogistica = Math.Max(0, UpgLogistica + delta);
            }
            else if (tipo == "FORNO") UpgForno = Math.Max(0, UpgForno + delta);
            else if (tipo == "GELADEIRA") UpgGeladeira = Math.Max(0, UpgGeladeira + delta);
            else if (tipo == "BALCAO") UpgBalcao = Math.Max(0, UpgBalcao + delta);
            else if (tipo == "LOGISTICA") UpgLogistica = Math.Max(0, UpgLogistica + delta);

            VerificarSubidaDeNivel();
            RegistrarFeedback("Terminal Dev", $"[DEV] Upgrades {tipo}",
                $"[DEV] Upgrades [{tipo}] alterados em {delta}. Total: {TotalUpgrades}.",
                FeedbackSeverity.Info, antes);
        }

        public void DevAlterarReputacao(double delta)
        {
            var antes = CapturarSnapshot();
            AjustarReputacao(delta);
            RegistrarFeedback("Terminal Dev", "[DEV] Reputação",
                $"[DEV] Reputação ajustada para {Reputacao:F1}*.",
                FeedbackSeverity.Info, antes);
        }

        public void DevAlterarNivel(int delta)
        {
            var antes = CapturarSnapshot();
            NivelAtual = Math.Clamp(NivelAtual + delta, 1, 5);
            foreach (var ing in Catalog.TodosIngredientes.Where(i => i.Nivel <= NivelAtual))
                Estoque[ing.Nome] = Math.Max(Estoque[ing.Nome], 8);
            foreach (var prato in Catalog.Pratos.Where(p => p.NivelDesbloqueio <= NivelAtual))
                PratosAtivosNoCardapio.Add(prato.Nome);

            GerarNovoPedido();
            VerificarConquistas();
            RegistrarFeedback("Terminal Dev", "[DEV] Alterar Fase",
                $"[DEV] Fase forçada para {NivelAtual}/5 ({ConfigAtual.Titulo}).",
                FeedbackSeverity.Info, antes);
        }

        public void DevEncherEstoqueTotal()
        {
            var antes = CapturarSnapshot();
            foreach (var ing in Catalog.TodosIngredientes)
            {
                Estoque[ing.Nome] = CapacidadeMaximaEstoque;
                EstoqueContaminado[ing.Nome] = 0;
            }
            RegistrarFeedback("Terminal Dev", "[DEV] Encher estoque",
                $"[DEV] Todos os 96 ingredientes abastecidos no máximo ({CapacidadeMaximaEstoque} un.) e descontaminados!",
                FeedbackSeverity.Sucesso, antes);
        }

        public void DevPerdoarDivida()
        {
            var antes = CapturarSnapshot();
            DividaTotalBanco = 0;
            MesesRestantesDivida = 0;
            RegistrarFeedback("Terminal Dev", "[DEV] Zerar dívida",
                "[DEV] Dívida bancária zerada!",
                FeedbackSeverity.Sucesso, antes);
        }

        // ================== BANCO ==================
        public void PedirEmprestimo(double valorSolicitado)
        {
            var antes = CapturarSnapshot();
            double disponivel = Math.Max(0, Math.Round(LimiteCreditoBanco - DividaTotalBanco, 2));

            if (double.IsNaN(valorSolicitado) || double.IsInfinity(valorSolicitado) || valorSolicitado <= 0 || valorSolicitado > 1_000_000)
            {
                RegistrarFeedback("Banco Empresarial", "Solicitar empréstimo",
                    "Valor de empréstimo inválido ou fora dos limites.",
                    FeedbackSeverity.Erro,
                    sugestao: "Escolha um dos valores disponíveis (+R$ 1.500, +R$ 4.500 ou +R$ 15.000).");
                return;
            }

            if (DividaTotalBanco + valorSolicitado > LimiteCreditoBanco)
            {
                TocarSom(false);
                RegistrarFeedback("Banco Empresarial", $"Empréstimo de {FmtMoney(valorSolicitado)}",
                    $"Crédito negado! Seu crédito livre atual é {FmtMoney(disponivel)} (limite total {FmtMoney(LimiteCreditoBanco)}).",
                    FeedbackSeverity.Erro,
                    sugestao: disponivel >= 1500
                        ? "Tente um valor menor (+R$ 1.500) ou amortize parte da dívida."
                        : "Aumente sua Reputação e Fase para ampliar o limite de crédito.");
                return;
            }

            InvalidarDesfazer("Empréstimo bancário é contrato financeiro definitivo.");
            ModificarDinheiro(valorSolicitado);
            DividaTotalBanco = Math.Round(DividaTotalBanco + valorSolicitado, 2);
            if (MesesRestantesDivida <= 0) MesesRestantesDivida = 12;
            TocarSom(true);

            RegistrarFeedback("Banco Empresarial", $"Tomar empréstimo ({FmtMoney(valorSolicitado)})",
                $"Empréstimo de {FmtMoney(valorSolicitado)} creditado! Juros de {TaxaJurosMensal * 100:F1}% a.m. ({MesesRestantesDivida} meses).",
                FeedbackSeverity.Sucesso, antes,
                sugestao: "Amortize antes do fim do mês para economizar nos juros.");
        }

        public void AmortizarDivida(double valor)
        {
            var antes = CapturarSnapshot();
            if (DividaTotalBanco <= 0)
            {
                TocarSom(false);
                RegistrarFeedback("Banco Empresarial", "Amortizar dívida",
                    "Você não possui dívidas ativas no banco!",
                    FeedbackSeverity.Aviso,
                    sugestao: "Seu crédito está limpo. Se precisar de capital, use as opções de empréstimo.");
                return;
            }

            if (double.IsNaN(valor) || double.IsInfinity(valor) || valor <= 0)
            {
                RegistrarFeedback("Banco Empresarial", "Amortizar dívida",
                    "Valor de amortização inválido.",
                    FeedbackSeverity.Erro,
                    sugestao: "Use PAGAR R$ 500, PAGAR R$ 2.000 ou QUITAR TUDO.");
                return;
            }

            double pagamento = Math.Min(valor, DividaTotalBanco);
            if (Dinheiro < pagamento && ModoJogo != GameMode.GestaoLivre)
            {
                TocarSom(false);
                RegistrarFeedback("Banco Empresarial", $"Amortizar {FmtMoney(pagamento)}",
                    $"Saldo insuficiente ({FmtMoney(Dinheiro)}) para pagar {FmtMoney(pagamento)}.",
                    FeedbackSeverity.Erro,
                    sugestao: Dinheiro >= 500
                        ? "Tente amortizar R$ 500,00 ou entregue mais pedidos na Cozinha [1]."
                        : "Entregue pedidos na Cozinha [1] para acumular caixa antes de amortizar.");
                return;
            }

            InvalidarDesfazer("Pagamento de dívida bancária é definitivo.");
            ModificarDinheiro(-pagamento);
            DividaTotalBanco = Math.Round(DividaTotalBanco - pagamento, 2);
            TocarSom(true);

            if (DividaTotalBanco <= 0.5)
            {
                DividaTotalBanco = 0;
                MesesRestantesDivida = 0;
                Narrativa.EmprestimosQuitados++;
                Narrativa.RegistrarCronica("Quitou integralmente a dívida bancária do restaurante!");
                var novas = VerificarConquistas();
                string conqTxt = novas.Count > 0 ? $" · 🏆 Conquista: {string.Join(", ", novas)}!" : "";
                RegistrarFeedback("Banco Empresarial", "Quitar dívida",
                    $"Dívida bancária 100% quitada! Você economizou todos os juros futuros.{conqTxt}",
                    FeedbackSeverity.Sucesso, antes);
            }
            else
            {
                RegistrarFeedback("Banco Empresarial", $"Amortizar {FmtMoney(pagamento)}",
                    $"Amortizado {FmtMoney(pagamento)}! Saldo devedor caiu para {FmtMoney(DividaTotalBanco)}.",
                    FeedbackSeverity.Sucesso, antes);
            }
        }

        // ================== TRIBUNAL ==================
        private void TestarRiscoProcessoSanitario()
        {
            if (ProcessoAtivo != null) return;

            int limpezaNecessaria = ObterLimpezaIdeal();
            int deficit = limpezaNecessaria - Limpeza;

            int chance = deficit * 7;
            if (Narrativa.HistoricoSanitario >= 80) chance = (int)(chance * 0.65);

            if (deficit > 0 && _rng.Next(100) < chance)
            {
                DispararProcesso(
                    "Autuação da Vigilância Sanitária!",
                    "A sujeira acumulada pelo movimento do restaurante gerou multa.",
                    ConfigAtual.PrecoMedioPrato * 4.5
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
                ValorCausa = Math.Round(valorCausa * FatorMultaDificuldade, 2),
                ChanceDeGanhar = chance
            };
        }

        public void FazerAcordoJudicial()
        {
            if (ProcessoAtivo == null) return;
            var antes = CapturarSnapshot();
            string titulo = ProcessoAtivo.Titulo;
            double acordo = ProcessoAtivo.ValorAcordo;
            ProcessoAtivo = null;
            InvalidarDesfazer("Acordo judicial é definitivo.");

            AjustarReputacao(-0.10);
            ModificarDinheiro(-acordo);
            if (!GameOver)
            {
                SalvarAutomatico("acordo-judicial");
                RegistrarFeedback("Tribunal", $"Acordo judicial ({titulo})",
                    $"Acordo pago (-{FmtMoney(acordo)} = 55% da causa). Processo encerrado.",
                    FeedbackSeverity.Aviso, antes);
            }
        }

        public void IrParaJulgamento()
        {
            if (ProcessoAtivo == null) return;
            var antes = CapturarSnapshot();
            var proc = ProcessoAtivo;
            ProcessoAtivo = null;
            InvalidarDesfazer("Sentença judicial é definitiva.");

            ModificarDinheiro(-proc.CustoAdvogado);
            if (GameOver) return;

            if (_rng.Next(1, 101) <= proc.ChanceDeGanhar)
            {
                AjustarReputacao(+0.10);
                Narrativa.VitoriasTribunal++;
                Narrativa.RegistrarCronica($"Venceu o processo '{proc.Titulo}' no Tribunal!");
                var novas = VerificarConquistas();
                TocarSom(true);
                SalvarAutomatico("vitoria-tribunal");
                string conqTxt = novas.Count > 0 ? $" · 🏆 Conquista: {string.Join(", ", novas)}!" : "";
                RegistrarFeedback("Tribunal", $"Julgamento ({proc.Titulo})",
                    $"VITÓRIA NO TRIBUNAL ({proc.ChanceDeGanhar}%)! Pagou apenas honorários ({FmtMoney(proc.CustoAdvogado)}).{conqTxt}",
                    FeedbackSeverity.Sucesso, antes);
            }
            else
            {
                AjustarReputacao(-0.25);
                TocarSom(false);
                double condenacao = proc.ValorCausa;
                ModificarDinheiro(-condenacao);
                if (!GameOver)
                {
                    SalvarAutomatico("derrota-tribunal");
                    RegistrarFeedback("Tribunal", $"Julgamento ({proc.Titulo})",
                        $"DERROTA NO TRIBUNAL ({proc.ChanceDeGanhar}%)! Condenado em {FmtMoney(condenacao)} + advogado.",
                        FeedbackSeverity.Erro, antes,
                        sugestao: "Mantenha a limpeza acima do ideal e recuse cargas suspeitas para evitar processos.");
                }
            }
        }

        // ================== VIRADA DE MÊS ==================
        public double CalcularDespesaMensalFixa()
        {
            double sal = Funcionarios * ConfigAtual.CustoPorFuncionario * MultiplicadorSalario;
            double limp = Limpeza * (ConfigAtual.CustoPorLimpeza * 0.18);
            return Math.Round((ConfigAtual.CustoFixoMensal + sal + limp) * FatorCustoFixoDificuldade, 2);
        }

        public void ProcessarViradaDeMes()
        {
            AcoesRealizadasNoMes = 0;
            TarefasConcluidasNoMes = 0;
            Limpeza = Math.Max(0, Limpeza - (1 + NivelAtual));

            double folha = CalcularDespesaMensalFixa();
            ModificarDinheiro(-folha);
            if (GameOver) return;

            if (DividaTotalBanco > 0)
            {
                DividaTotalBanco = Math.Round(DividaTotalBanco * (1.0 + TaxaJurosMensal), 2);
                double parcela = ParcelaMensalBanco;
                MesesRestantesDivida--;

                if (Dinheiro >= parcela)
                {
                    ModificarDinheiro(-parcela);
                    DividaTotalBanco = Math.Max(0, Math.Round(DividaTotalBanco - parcela, 2));
                    UltimaMensagem = $"Mês {MesesJogados}: Folha (-{FmtMoney(folha)}) e parcela do banco (-{FmtMoney(parcela)}) pagas em dia!";
                }
                else
                {
                    DividaTotalBanco = Math.Round(DividaTotalBanco * 1.04, 2);
                    AjustarReputacao(-0.20);
                    TocarSom(false);
                    UltimaMensagem = $"ATRASO BANCÁRIO! Caixa insuficiente para a parcela de {FmtMoney(parcela)}. Dívida acumulou juros (Restam {MesesRestantesDivida} meses)!";
                }

                if (ModoJogo != GameMode.GestaoLivre && MesesRestantesDivida <= 0 && DividaTotalBanco > 1.0)
                {
                    GameOver = true;
                    MotivoFimDeJogo = $"Execução Bancária! O prazo de 12 meses expirou e você não quitou a dívida pendente de {FmtMoney(DividaTotalBanco)}.";
                    RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal());
                    return;
                }
            }
            else
            {
                UltimaMensagem = $"MÊS {MesesJogados} INICIADO! Folha salarial e custos fixos (-{FmtMoney(folha)}) descontados.";
            }

            VerificarCondicaoVitoria();
        }

        private void VerificarCondicaoVitoria()
        {
            if (ModoJogo == GameMode.GestaoLivre) return;

            if (ModoJogo == GameMode.CampanhaDias)
            {
                if (NivelAtual >= 3 && (Dinheiro - DividaTotalBanco) >= 8000.0 && Reputacao >= 4.0)
                {
                    JogoVencido = true;
                    MotivoFimDeJogo = $"CAMPANHA VENCIDA NO DIA {DiasDecorridosTotais}/{PrazoCampanhaDias}! Você atingiu Fase {NivelAtual}/5, {FmtMoney(Dinheiro - DividaTotalBanco)} líquidos e {Reputacao:F1}*!";
                    RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal());
                }
                return;
            }

            if (NivelAtual >= 5 && (Dinheiro - DividaTotalBanco) >= 50000.0 && Reputacao >= 4.5)
            {
                JogoVencido = true;
                MotivoFimDeJogo = "PARABÉNS! Você construiu um Império Gastronômico Global (Fase 5/5, R$ 50k+ Líquidos e 4,5+ Estrelas)!";
                RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal());
            }
        }

        // ================== GUIA CONTEXTUAL ACIONÁVEL ==================
        public ContextualGuidance ObterGuiaContextual(ScreenTab aba)
        {
            var inv = ObterResumoInventario();
            var guia = new ContextualGuidance
            {
                LocalidadeNome = LocalityCatalog.ObterNome(aba),
                PersonagemLocal = LocalityCatalog.ObterPersonagem(aba)
            };

            // Objetivos Ativos em frases acionáveis
            guia.ObjetivosAtivos.Add(ObterTextoProximaMeta());
            if (SequenciaPerfeita >= 2)
                guia.ObjetivosAtivos.Add($"Mantenha o combo de {SequenciaPerfeita}x (+{BonusEficienciaCombo * 100:F0}% lucro).");
            else if (NivelAtual < 5)
                guia.ObjetivosAtivos.Add($"Garanta +{FmtMoney(Catalog.Levels[NivelAtual].BonusNivel)} ao alcançar a Fase {NivelAtual + 1}.");
            if (DividaTotalBanco > 0)
                guia.ObjetivosAtivos.Add($"Amortize {FmtMoney(DividaTotalBanco)} ({MesesRestantesDivida}m restantes).");

            // Próximos Passos Acionáveis
            if (ProcessoAtivo != null)
            {
                guia.ProximosPassos.Add("1. Decida no Tribunal: [1] Acordo ou [2] Julgamento.");
            }
            else
            {
                if (PedidoAtual != null)
                {
                    if (PedidoAtual.PratoMontado.Count == 0)
                        guia.ProximosPassos.Add($"1. Sirva às {HorarioTurnoFormatado}: monte {PedidoAtual.Receita.Nome} começando por {PedidoAtual.OrdemExigida[0]}.");
                    else if (DiagnosticarErroPrato(PedidoAtual) == null)
                        guia.ProximosPassos.Add($"1. Entregue {PedidoAtual.Receita.Nome} com [E] (+{FmtMoney(CalcularValorVendaPrato())}).");
                    else
                        guia.ProximosPassos.Add($"1. Finalize {PedidoAtual.Receita.Nome} na bancada ({PedidoAtual.PratoMontado.Count}/{PedidoAtual.OrdemExigida.Count}).");
                }

                if (inv.Esgotados > 0)
                    guia.ProximosPassos.Add($"2. Vá às Docas [2] e reponha {inv.Esgotados} insumo(s) zerado(s).");
                else if (Limpeza < ObterLimpezaIdeal())
                    guia.ProximosPassos.Add($"2. Eleve a limpeza em Gerência [5] ({Limpeza}/{ObterLimpezaIdeal()}).");
                else if (AcoesRestantesDia <= 1)
                    guia.ProximosPassos.Add("2. Feche o expediente com [F] após sua última ação.");
                else
                    guia.ProximosPassos.Add($"2. Invista em Gerência [5] rumo à Fase {Math.Min(5, NivelAtual + 1)}/5.");

                if (DiaSemana == 4 && Dinheiro < CalcularDespesaMensalFixa())
                    guia.ProximosPassos.Add($"3. Reserve {FmtMoney(CalcularDespesaMensalFixa())} p/ a folha do Dia 4.");
                else
                    guia.ProximosPassos.Add("3. Pressione [/] para comandos ou [O] para Opções/Som.");
            }

            void AddAcao(string tecla, string titulo, bool disp, string motivo = "")
            {
                var item = new ActionAvailability { Tecla = tecla, Titulo = titulo, Disponivel = disp, MotivoBloqueio = motivo };
                if (disp) guia.AcoesDisponiveis.Add(item);
                else guia.AcoesBloqueadas.Add(item);
            }

            if (ProcessoAtivo != null)
            {
                guia.SubtituloLocalidade = "Audiência de Emergência";
                guia.SituacaoAtual = $"Processo aberto: {ProcessoAtivo.Titulo} (causa {FmtMoney(ProcessoAtivo.ValorCausa)}).";
                AddAcao("1", $"Acordo ({FmtMoney(ProcessoAtivo.ValorAcordo)})", true);
                AddAcao("2", $"Julgar ({ProcessoAtivo.ChanceDeGanhar}%)", true);
                AddAcao("1-8", "Trocar localidade", false, "Resolva o tribunal primeiro");
                return guia;
            }

            switch (aba)
            {
                case ScreenTab.Cozinha:
                    guia.SubtituloLocalidade = $"Turno {HorarioTurnoFormatado}";
                    int faltam = PedidoAtual != null ? Math.Max(0, PedidoAtual.OrdemExigida.Count - PedidoAtual.PratoMontado.Count) : 0;
                    guia.SituacaoAtual = PedidoAtual != null
                        ? $"Pedido: {PedidoAtual.Receita.Nome} ({PedidoAtual.PratoMontado.Count}/{PedidoAtual.OrdemExigida.Count}, falta {faltam}) · Combo {SequenciaPerfeita}x."
                        : "Bancada pronta.";
                    AddAcao("E", "Entregar prato (-1 Energia)", PedidoAtual != null && PedidoAtual.PratoMontado.Count > 0, "Bancada vazia");
                    AddAcao("Z", "Desfazer última ação", PodeDesfazer, "Nada p/ desfazer");
                    AddAcao("X", "Limpar bancada", PedidoAtual != null && PedidoAtual.PratoMontado.Count > 0, "Bancada já limpa");
                    AddAcao("P", "Falar com Pedro Almeida", true);
                    break;

                case ScreenTab.Entregas:
                    guia.SubtituloLocalidade = "Auditoria de Cargas";
                    int abertas = EntregaAtual?.Count(c => c.CaixaAberta) ?? 0;
                    int aceitas = EntregaAtual?.Count(c => c.MarcadoComoValidoPeloJogador) ?? 0;
                    guia.SituacaoAtual = $"Carga: {abertas} aberta(s), {aceitas} aceita(s), {InspecoesCaixaRestantes} insp. · Confiança {Narrativa.ConfiancaFornecedores}/100.";
                    AddAcao("1-7", "Abrir/Alternar caixas", InspecoesCaixaRestantes > 0, "0 inspeções restantes");
                    AddAcao("A", "Concluir auditoria (-1 Energia)", true);
                    AddAcao("N", "Falar com Ana Beatriz", true);
                    break;

                case ScreenTab.LivroReceitas:
                    guia.SubtituloLocalidade = "Gestão de Cardápio";
                    guia.SituacaoAtual = $"{PratosAtivosNoCardapio.Count} prato(s) ativo(s) na Fase {NivelAtual}/5.";
                    AddAcao("Clique", "Ativar / Desativar prato", true);
                    AddAcao("Z", "Desfazer alteração", PodeDesfazer, "Nada p/ desfazer");
                    AddAcao("-", "Desativar abaixo de 2", PratosAtivosNoCardapio.Count > 2, "Mínimo de 2 pratos ativos");
                    break;

                case ScreenTab.EstoqueCatalogo:
                    guia.SubtituloLocalidade = "Câmara Fria & Despensa";
                    guia.SituacaoAtual = $"{inv.TotalUnidades}/{inv.CapacidadeTotal} un. · {inv.Esgotados} zerado(s) · {inv.Contaminados} contam. · Selo {Narrativa.HistoricoSanitario}/100.";
                    AddAcao("PgUp/Dn", "Trocar página", true);
                    AddAcao("2", "Ir às Docas repor estoque", true);
                    AddAcao("5", "Melhorar Câmara Fria", Dinheiro >= ObterCustoUpgradeEspecifico(UpgGeladeira) || ModoJogo == GameMode.GestaoLivre, $"Falta {FmtMoney(Math.Max(0, ObterCustoUpgradeEspecifico(UpgGeladeira) - Dinheiro))}");
                    break;

                case ScreenTab.Administracao:
                    guia.SubtituloLocalidade = "Equipe, Higiene & Upgrades";
                    guia.SituacaoAtual = $"Moral {Narrativa.MoralEquipe}/100 · Folha {FmtMoney(CalcularDespesaMensalFixa())}/mês · Dificuldade: {LocalityCatalog.ObterNomeDificuldade(Dificuldade)}.";
                    AddAcao("+Func", $"Contratar ({FmtMoney(ConfigAtual.CustoPorFuncionario)})", Dinheiro >= ConfigAtual.CustoPorFuncionario || ModoJogo == GameMode.GestaoLivre, "Saldo insuficiente");
                    AddAcao("+Limp", $"Limpeza ({FmtMoney(ConfigAtual.CustoPorLimpeza)})", Dinheiro >= ConfigAtual.CustoPorLimpeza || ModoJogo == GameMode.GestaoLivre, "Saldo insuficiente");
                    AddAcao("C", "Falar com Carlos Eduardo", true);
                    AddAcao("Z", "Desfazer preço/salário", PodeDesfazer, "Nada reversível");
                    break;

                case ScreenTab.Banco:
                    guia.SubtituloLocalidade = "Caixa & Linha de Crédito";
                    double livre = Math.Max(0, Math.Round(LimiteCreditoBanco - DividaTotalBanco, 2));
                    guia.SituacaoAtual = $"Crédito livre: {FmtMoney(livre)} · Dívida: {FmtMoney(DividaTotalBanco)} · Juros {TaxaJurosMensal * 100:F1}% a.m.";
                    AddAcao("+1.5k", "Empréstimo R$ 1.500", livre >= 1500, "Acima do limite");
                    AddAcao("Pagar", "Amortizar R$ 500", DividaTotalBanco > 0 && (Dinheiro >= Math.Min(500, DividaTotalBanco) || ModoJogo == GameMode.GestaoLivre), DividaTotalBanco <= 0 ? "Sem dívida ativa" : "Saldo insuficiente");
                    AddAcao("Quitar", "Quitar dívida total", DividaTotalBanco > 0 && (Dinheiro >= DividaTotalBanco || ModoJogo == GameMode.GestaoLivre), DividaTotalBanco <= 0 ? "Sem dívida ativa" : "Saldo insuficiente");
                    break;

                case ScreenTab.Historico:
                    guia.SubtituloLocalidade = "Diário, Conquistas & Comandos";
                    guia.SituacaoAtual = $"{HistoricoEventos.Count} evento(s) · Conquistas: {ConquistasDesbloqueadas.Count}/{Catalog.Conquistas.Count} · Crônicas: {Narrativa.Cronicas.Count}.";
                    AddAcao("↑/↓", "Navegar acontecimentos", HistoricoEventos.Count > 0, "Histórico vazio");
                    AddAcao("T/V/E/C/Q", "Filtrar eventos/conquistas", true);
                    AddAcao("G", "Gerenciar slots de save", true);
                    break;

                case ScreenTab.Terminal:
                    guia.SubtituloLocalidade = "Console Técnico";
                    guia.SituacaoAtual = TerminalDesbloqueado ? "Modo Desenvolvedor autenticado." : "Acesso bloqueado por senha.";
                    AddAcao("Enter", TerminalDesbloqueado ? "Executar ajuste dev" : "Autenticar senha", true);
                    AddAcao("Dev", "Ajustes em tempo real", TerminalDesbloqueado, "Requer senha de dev");
                    break;
            }

            return guia;
        }

        // ================== PERSISTÊNCIA (MULTI-SLOTS, AUTOSAVE, RECUPERAÇÃO) ==================
        public SaveStateData ExportarSaveData()
        {
            return new SaveStateData
            {
                FormatVersion = PersistenceService.CurrentFormatVersion,
                Diversao = Diversao,
                NivelAtual = NivelAtual,
                DataAtual = DataAtual,
                DiaSemana = DiaSemana,
                MesesJogados = MesesJogados,
                DiasDecorridosTotais = DiasDecorridosTotais,
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
                SequenciaPerfeita = SequenciaPerfeita,
                MelhorSequenciaPerfeita = MelhorSequenciaPerfeita,
                RecordePontuacao = Math.Max(RecordePontuacao, CalcularPontuacaoFinal()),
                Dificuldade = Dificuldade,
                ModoJogo = ModoJogo,
                Estoque = new Dictionary<string, int>(Estoque),
                EstoqueContaminado = new Dictionary<string, int>(EstoqueContaminado),
                PratosAtivos = PratosAtivosNoCardapio.ToList(),
                ConquistasDesbloqueadas = ConquistasDesbloqueadas.ToList(),
                Narrativa = new NarrativeState
                {
                    MoralEquipe = Narrativa.MoralEquipe,
                    HistoricoSanitario = Narrativa.HistoricoSanitario,
                    ConfiancaFornecedores = Narrativa.ConfiancaFornecedores,
                    AuditoriasImpecaveis = Narrativa.AuditoriasImpecaveis,
                    VitoriasTribunal = Narrativa.VitoriasTribunal,
                    EmprestimosQuitados = Narrativa.EmprestimosQuitados,
                    BlitzAprovadas = Narrativa.BlitzAprovadas,
                    Cronicas = new List<string>(Narrativa.Cronicas)
                }
            };
        }

        public bool SalvarAutomatico(string motivo = "")
        {
            var res = Persistence.Salvar("autosave", ExportarSaveData());
            if (res.Sucesso) UltimoAutosaveLocal = DateTime.Now;
            return res.Sucesso;
        }

        public void SalvarJogo(string slotId = null)
        {
            string alvo = PersistenceService.NormalizarSlotId(slotId ?? SlotAtivo);
            if (alvo != "autosave") SlotAtivo = alvo;

            var res = Persistence.Salvar(alvo, ExportarSaveData());
            if (res.Sucesso)
            {
                TocarSom(true);
                RegistrarFeedback("Sistema", $"Salvar em {PersistenceService.ObterTituloSlot(alvo)}",
                    res.Mensagem, FeedbackSeverity.Sucesso);
            }
            else
            {
                TocarSom(false);
                RegistrarFeedback("Sistema", $"Salvar em {PersistenceService.ObterTituloSlot(alvo)}",
                    res.Mensagem, FeedbackSeverity.Erro, sugestao: res.Sugestao);
            }
        }

        public void CarregarJogo(string slotId = null)
        {
            string alvo = PersistenceService.NormalizarSlotId(slotId ?? SlotAtivo);
            var antes = CapturarSnapshot();
            var res = Persistence.Carregar(alvo);

            if (!res.Sucesso || res.Data == null)
            {
                TocarSom(false);
                RegistrarFeedback("Sistema", $"Carregar {PersistenceService.ObterTituloSlot(alvo)}",
                    res.Mensagem, FeedbackSeverity.Erro, sugestao: res.Sugestao);
                return;
            }

            if (alvo != "autosave") SlotAtivo = alvo;
            AplicarSaveData(res.Data);
            TocarSom(true);

            var sev = res.RecuperadoDeBackup ? FeedbackSeverity.Aviso : FeedbackSeverity.Sucesso;
            RegistrarFeedback("Sistema", $"Carregar {PersistenceService.ObterTituloSlot(alvo)}",
                res.Mensagem, sev, antes, res.Sugestao);
        }

        public void ExcluirSlotSave(string slotId)
        {
            string alvo = PersistenceService.NormalizarSlotId(slotId);
            bool ok = Persistence.ExcluirSlot(alvo, out string msg);
            RegistrarFeedback("Sistema", $"Excluir {PersistenceService.ObterTituloSlot(alvo)}",
                msg, ok ? FeedbackSeverity.Info : FeedbackSeverity.Aviso);
        }

        public List<SaveSlotSummary> ListarSlotsSave() => Persistence.ListarSlots();

        private void AplicarSaveData(SaveStateData data)
        {
            Diversao = data.Diversao;
            NivelAtual = Math.Clamp(data.NivelAtual, 1, 5);
            DataAtual = data.DataAtual == default ? new DateTime(2026, 1, 7) : data.DataAtual;
            DiaSemana = data.DiaSemana <= 0 ? 1 : data.DiaSemana;
            MesesJogados = data.MesesJogados <= 0 ? 1 : data.MesesJogados;
            DiasDecorridosTotais = data.DiasDecorridosTotais <= 0 ? ((MesesJogados - 1) * 4 + DiaSemana) : data.DiasDecorridosTotais;
            AcoesRestantesDia = Math.Clamp(data.AcoesRestantesDia, 1, Math.Max(6, AcoesMaximasDia + 4));
            Dinheiro = data.Dinheiro;
            Reputacao = Math.Clamp(data.Reputacao, 0.5, 5.0);
            Funcionarios = Math.Max(0, data.Funcionarios);
            Limpeza = Math.Max(0, data.Limpeza);
            UpgForno = Math.Max(0, data.UpgForno);
            UpgGeladeira = Math.Max(0, data.UpgGeladeira);
            UpgBalcao = Math.Max(0, data.UpgBalcao);
            UpgLogistica = Math.Max(0, data.UpgLogistica);
            MultiplicadorSalario = data.MultiplicadorSalario <= 0 ? 1.0 : data.MultiplicadorSalario;
            MultiplicadorPrecoPrato = data.MultiplicadorPrecoPrato <= 0 ? 1.0 : data.MultiplicadorPrecoPrato;
            DividaTotalBanco = Math.Max(0, data.DividaTotalBanco);
            MesesRestantesDivida = Math.Max(0, data.MesesRestantesDivida);
            PedidosPerfeitosTotal = Math.Max(0, data.PedidosPerfeitosTotal);
            SequenciaPerfeita = Math.Max(0, data.SequenciaPerfeita);
            MelhorSequenciaPerfeita = Math.Max(SequenciaPerfeita, data.MelhorSequenciaPerfeita);
            RecordePontuacao = Math.Max(RecordePontuacao, data.RecordePontuacao);
            Dificuldade = data.Dificuldade == 0 ? DifficultyMode.Normal : data.Dificuldade;
            ModoJogo = data.ModoJogo == 0 ? GameMode.Carreira : data.ModoJogo;

            Estoque = data.Estoque != null ? new Dictionary<string, int>(data.Estoque) : new Dictionary<string, int>();
            EstoqueContaminado = data.EstoqueContaminado != null ? new Dictionary<string, int>(data.EstoqueContaminado) : new Dictionary<string, int>();
            foreach (var ing in Catalog.TodosIngredientes)
            {
                if (!Estoque.ContainsKey(ing.Nome)) Estoque[ing.Nome] = 0;
                if (!EstoqueContaminado.ContainsKey(ing.Nome)) EstoqueContaminado[ing.Nome] = 0;
            }

            PratosAtivosNoCardapio = new HashSet<string>(data.PratosAtivos ?? new List<string>());
            if (PratosAtivosNoCardapio.Count < 2)
            {
                foreach (var p in Catalog.Pratos.Where(p => p.NivelDesbloqueio <= NivelAtual).Take(2))
                    PratosAtivosNoCardapio.Add(p.Nome);
            }

            ConquistasDesbloqueadas = new HashSet<string>(data.ConquistasDesbloqueadas ?? new List<string>());
            Narrativa = data.Narrativa ?? new NarrativeState();
            Narrativa.Cronicas ??= new List<string>();

            ExibirTutorial = false;
            ResumoDiarioAtivo = null;
            GameOver = false;
            JogoVencido = false;
            ProcessoAtivo = null;
            InvalidarDesfazer("Partida carregada — pilha de desfazer reiniciada.");
            GerarNovoPedido();
            GerarNovaEntrega(cobrarTaxa: false);
        }
    }
}
