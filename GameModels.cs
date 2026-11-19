using System;
using System.Collections.Generic;
using System.Globalization;
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
        Banco = 6,
        Historico = 7,
        Terminal = 8
    }

    public enum FeedbackSeverity
    {
        Info,
        Sucesso,
        Aviso,
        Erro
    }

    public enum HelpSection
    {
        LocalidadeAtual = 1,
        TutorialCurto = 2,
        ComandosNaturais = 3
    }

    public enum DifficultyMode
    {
        Facil = 1,
        Normal = 2,
        Dificil = 3
    }

    public enum GameMode
    {
        Carreira = 1,
        CampanhaDias = 2,
        GestaoLivre = 3
    }

    public enum TextSpeed
    {
        Instantaneo = 0,
        Rapido = 1,
        Normal = 2,
        Lento = 3
    }

    public class AccessibilitySettings
    {
        public bool AltoContraste { get; set; } = false;
        public bool ModoSemantico { get; set; } = false;
        public bool DesativarEfeitosVisuais { get; set; } = false;
        public TextSpeed VelocidadeTexto { get; set; } = TextSpeed.Rapido;
        public bool SonsAtivos { get; set; } = true;

        public string ObterResumo()
        {
            string hc = AltoContraste ? "ON" : "OFF";
            string sem = ModoSemantico ? "ON" : "OFF";
            string fx = DesativarEfeitosVisuais ? "OFF" : "ON";
            string som = SonsAtivos ? "ON" : "OFF";
            return $"Contraste:{hc} | Semântico:{sem} | Efeitos:{fx} | Texto:{VelocidadeTexto} | Som:{som}";
        }
    }

    public class NarrativeState
    {
        public int MoralEquipe { get; set; } = 60;           // 0..100
        public int HistoricoSanitario { get; set; } = 65;    // 0..100
        public int ConfiancaFornecedores { get; set; } = 60; // 0..100
        public int AuditoriasImpecaveis { get; set; } = 0;
        public int VitoriasTribunal { get; set; } = 0;
        public int EmprestimosQuitados { get; set; } = 0;
        public int BlitzAprovadas { get; set; } = 0;
        public List<string> Cronicas { get; set; } = new List<string>();

        public void RegistrarCronica(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            Cronicas.Add(texto);
            if (Cronicas.Count > 30) Cronicas.RemoveAt(0);
        }
    }

    public class AchievementDefinition
    {
        public string Id { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public int BonusPontos { get; set; }
    }

    public class SaveSlotSummary
    {
        public string SlotId { get; set; }
        public string Titulo { get; set; }
        public bool Existe { get; set; }
        public bool Corrompido { get; set; }
        public bool TemBackup { get; set; }
        public int FormatVersion { get; set; }
        public string Resumo { get; set; }
        public DateTime? SavedAtLocal { get; set; }
    }

    public class SaveFileEnvelope
    {
        public int FormatVersion { get; set; } = 2;
        public string SlotId { get; set; } = "slot1";
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
        public string Checksum { get; set; } = "";
        public SaveStateData Data { get; set; }
    }

    public class PersistenceOperationResult
    {
        public bool Sucesso { get; set; }
        public bool RecuperadoDeBackup { get; set; }
        public bool MigradoDeVersaoAntiga { get; set; }
        public string Mensagem { get; set; } = "";
        public string Sugestao { get; set; } = "";
        public SaveStateData Data { get; set; }
    }

    public class StateDeltaItem
    {
        public string Texto { get; set; }
        public int Sinal { get; set; } // +1 bom, -1 ruim, 0 neutro

        public StateDeltaItem(string texto, int sinal)
        {
            Texto = texto;
            Sinal = sinal;
        }
    }

    public class StateSnapshot
    {
        public double Dinheiro { get; set; }
        public double Reputacao { get; set; }
        public int Energia { get; set; }
        public int Funcionarios { get; set; }
        public int Limpeza { get; set; }
        public int TotalUpgrades { get; set; }
        public double DividaTotalBanco { get; set; }
        public int NivelAtual { get; set; }
        public int InventarioTotal { get; set; }
        public int EstoqueContaminadoTotal { get; set; }
        public int DiaSemana { get; set; }
        public int MesesJogados { get; set; }
        public int PratosAtivos { get; set; }
        public double MultiplicadorSalario { get; set; }
        public double MultiplicadorPrecoPrato { get; set; }
        public int MoralEquipe { get; set; }
        public int ConquistasCount { get; set; }

        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public static List<StateDeltaItem> CalcularDeltas(StateSnapshot antes, StateSnapshot depois)
        {
            var deltas = new List<StateDeltaItem>();
            if (antes == null || depois == null) return deltas;

            double dDin = Math.Round(depois.Dinheiro - antes.Dinheiro, 2);
            if (Math.Abs(dDin) >= 0.01)
            {
                string sinal = dDin > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem(
                    $"Dinheiro {sinal}R$ {Math.Abs(dDin).ToString("N2", PtBr)}",
                    dDin > 0 ? 1 : -1));
            }

            double dRep = Math.Round(depois.Reputacao - antes.Reputacao, 2);
            if (Math.Abs(dRep) >= 0.01)
            {
                string sinal = dRep > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem(
                    $"Reputação {sinal}{Math.Abs(dRep).ToString("0.##", PtBr)}*",
                    dRep > 0 ? 1 : -1));
            }

            int dEnergia = depois.Energia - antes.Energia;
            if (dEnergia != 0)
            {
                string sinal = dEnergia > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Energia {sinal}{Math.Abs(dEnergia)}", dEnergia > 0 ? 1 : 0));
            }

            int dInv = depois.InventarioTotal - antes.InventarioTotal;
            if (dInv != 0)
            {
                string sinal = dInv > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Inventário {sinal}{Math.Abs(dInv)} un", dInv > 0 ? 1 : 0));
            }

            int dContam = depois.EstoqueContaminadoTotal - antes.EstoqueContaminadoTotal;
            if (dContam != 0)
            {
                string sinal = dContam > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Contaminados {sinal}{Math.Abs(dContam)}", dContam > 0 ? -1 : 1));
            }

            int dFunc = depois.Funcionarios - antes.Funcionarios;
            if (dFunc != 0)
            {
                string sinal = dFunc > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Funcionários {sinal}{Math.Abs(dFunc)}", dFunc > 0 ? 1 : -1));
            }

            int dLimp = depois.Limpeza - antes.Limpeza;
            if (dLimp != 0)
            {
                string sinal = dLimp > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Limpeza {sinal}{Math.Abs(dLimp)}", dLimp > 0 ? 1 : -1));
            }

            int dUpg = depois.TotalUpgrades - antes.TotalUpgrades;
            if (dUpg != 0)
            {
                string sinal = dUpg > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Upgrades {sinal}{Math.Abs(dUpg)}", dUpg > 0 ? 1 : -1));
            }

            double dDiv = Math.Round(depois.DividaTotalBanco - antes.DividaTotalBanco, 2);
            if (Math.Abs(dDiv) >= 0.01)
            {
                string sinal = dDiv > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem(
                    $"Dívida {sinal}R$ {Math.Abs(dDiv).ToString("N2", PtBr)}",
                    dDiv > 0 ? -1 : 1));
            }

            int dMoral = depois.MoralEquipe - antes.MoralEquipe;
            if (dMoral != 0)
            {
                string sinal = dMoral > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Moral {sinal}{Math.Abs(dMoral)}", dMoral > 0 ? 1 : -1));
            }

            int dConq = depois.ConquistasCount - antes.ConquistasCount;
            if (dConq > 0)
            {
                deltas.Add(new StateDeltaItem($"Conquista +{dConq}", 1));
            }

            int dNivel = depois.NivelAtual - antes.NivelAtual;
            if (dNivel != 0)
            {
                string sinal = dNivel > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Fase {sinal}{Math.Abs(dNivel)} (Nv.{depois.NivelAtual})", dNivel > 0 ? 1 : -1));
            }

            if (depois.DiaSemana != antes.DiaSemana || depois.MesesJogados != antes.MesesJogados)
            {
                deltas.Add(new StateDeltaItem($"Tempo: Dia {depois.DiaSemana}/4 · Mês {depois.MesesJogados}", 0));
            }

            int dPratos = depois.PratosAtivos - antes.PratosAtivos;
            if (dPratos != 0)
            {
                string sinal = dPratos > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Cardápio {sinal}{Math.Abs(dPratos)} prato(s)", 0));
            }

            double dSal = Math.Round((depois.MultiplicadorSalario - antes.MultiplicadorSalario) * 100);
            if (Math.Abs(dSal) >= 1)
            {
                string sinal = dSal > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Salários {sinal}{Math.Abs(dSal):F0}%", 0));
            }

            double dPre = Math.Round((depois.MultiplicadorPrecoPrato - antes.MultiplicadorPrecoPrato) * 100);
            if (Math.Abs(dPre) >= 1)
            {
                string sinal = dPre > 0 ? "+" : "-";
                deltas.Add(new StateDeltaItem($"Preços {sinal}{Math.Abs(dPre):F0}%", 0));
            }

            return deltas;
        }
    }

    public class ActionFeedback
    {
        public string Acao { get; set; } = "Início da partida";
        public string Resposta { get; set; } = "Bem-vindo ao restaurante! Siga o painel de próximos passos ou digite um comando.";
        public string Sugestao { get; set; } = "";
        public string CorrecaoOrtografica { get; set; } = "";
        public List<StateDeltaItem> Deltas { get; set; } = new List<StateDeltaItem>();
        public FeedbackSeverity Severidade { get; set; } = FeedbackSeverity.Info;
        public string Localidade { get; set; } = "Cozinha";

        public string ResumoDeltas =>
            Deltas != null && Deltas.Count > 0
                ? string.Join(" | ", Deltas.Select(d => d.Texto))
                : "Sem alterações de estado";
    }

    public class HistoryEntry
    {
        public int Numero { get; set; }
        public string DataFormatada { get; set; }
        public int DiaSemana { get; set; }
        public int Mes { get; set; }
        public string Localidade { get; set; }
        public string Acao { get; set; }
        public string Resposta { get; set; }
        public string Sugestao { get; set; }
        public string ResumoDeltas { get; set; }
        public FeedbackSeverity Severidade { get; set; }
        public bool EhComandoTexto { get; set; }
    }

    public class UndoEntry
    {
        public string Descricao { get; set; }
        public string Localidade { get; set; }
        public Action ExecutarDesfazer { get; set; }
    }

    public class InventorySummary
    {
        public int TotalUnidades { get; set; }
        public int CapacidadeTotal { get; set; }
        public int Esgotados { get; set; }
        public int Baixos { get; set; }
        public int Contaminados { get; set; }
        public List<string> ItensCriticos { get; set; } = new List<string>();
    }

    public class ActionAvailability
    {
        public string Tecla { get; set; }
        public string Titulo { get; set; }
        public bool Disponivel { get; set; }
        public string MotivoBloqueio { get; set; }
    }

    public class ContextualGuidance
    {
        public string LocalidadeNome { get; set; }
        public string SubtituloLocalidade { get; set; }
        public string PersonagemLocal { get; set; }
        public string SituacaoAtual { get; set; }
        public List<string> ObjetivosAtivos { get; set; } = new List<string>();
        public List<string> ProximosPassos { get; set; } = new List<string>();
        public List<ActionAvailability> AcoesDisponiveis { get; set; } = new List<ActionAvailability>();
        public List<ActionAvailability> AcoesBloqueadas { get; set; } = new List<ActionAvailability>();
    }

    public class CommandExecutionResult
    {
        public bool Sucesso { get; set; }
        public ScreenTab? NovaAba { get; set; }
        public bool AbrirAjuda { get; set; }
        public HelpSection? SecaoAjuda { get; set; }
        public bool AbrirConfirmacaoRecomecar { get; set; }
        public bool AbrirConfirmacaoSaida { get; set; }
        public bool AbrirOpcoes { get; set; }
        public bool AbrirGerenciadorSaves { get; set; }
        public bool AlternarModoBancada { get; set; }
        public string FiltroHistoricoAlvo { get; set; }
    }

    public static class LocalityCatalog
    {
        public static string ObterNome(ScreenTab tab) => tab switch
        {
            ScreenTab.Cozinha => "Cozinha",
            ScreenTab.Entregas => "Docas (Entregas)",
            ScreenTab.LivroReceitas => "Cardápio & Receitas",
            ScreenTab.EstoqueCatalogo => "Despensa & Estoque",
            ScreenTab.Administracao => "Escritório (Gerência)",
            ScreenTab.Banco => "Banco Empresarial",
            ScreenTab.Historico => "Histórico & Conquistas",
            ScreenTab.Terminal => "Terminal Dev",
            _ => "Restaurante"
        };

        public static string ObterNomeCurto(ScreenTab tab) => tab switch
        {
            ScreenTab.Cozinha => "COZINHA",
            ScreenTab.Entregas => "DOCAS",
            ScreenTab.LivroReceitas => "CARDÁPIO",
            ScreenTab.EstoqueCatalogo => "ESTOQUE",
            ScreenTab.Administracao => "GERÊNCIA",
            ScreenTab.Banco => "BANCO",
            ScreenTab.Historico => "HISTÓRICO",
            ScreenTab.Terminal => "TERMINAL",
            _ => "LOCAL"
        };

        public static string ObterPersonagem(ScreenTab tab) => tab switch
        {
            ScreenTab.Cozinha => "Pedro Almeida (Subchefe) e Cliente",
            ScreenTab.Entregas => "Ana Beatriz (Conferente das Docas)",
            ScreenTab.LivroReceitas => "Pedro Almeida (Subchefe · Cardápio)",
            ScreenTab.EstoqueCatalogo => "Ana Beatriz (Câmara Fria)",
            ScreenTab.Administracao => "Carlos Eduardo (Gerente Financeiro)",
            ScreenTab.Banco => "Carlos Eduardo (Caixa & Crédito)",
            ScreenTab.Historico => "Diário, Conquistas & Comandos",
            ScreenTab.Terminal => "Console Técnico Restrito",
            _ => "Equipe"
        };

        public static string ObterNomeDificuldade(DifficultyMode dif) => dif switch
        {
            DifficultyMode.Facil => "Fácil",
            DifficultyMode.Dificil => "Difícil",
            _ => "Normal"
        };

        public static string ObterNomeModo(GameMode modo) => modo switch
        {
            GameMode.CampanhaDias => "Campanha (12 Dias)",
            GameMode.GestaoLivre => "Gestão Livre",
            _ => "Carreira"
        };
    }
}
