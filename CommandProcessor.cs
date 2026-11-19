using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PizzariaTycoon
{
    public class CommandProcessor
    {
        private readonly GameEngine _game;
        private readonly List<string> _ultimosComandos = new List<string>();
        private const int MaxHistoricoComandos = 40;

        public IReadOnlyList<string> UltimosComandos => _ultimosComandos;

        private static readonly Dictionary<string, string> ErrosComuns = new Dictionary<string, string>
        {
            { "cosinha", "cozinha" },
            { "coznha", "cozinha" },
            { "cozina", "cozinha" },
            { "caxa", "caixa" },
            { "caixaa", "caixa" },
            { "quejo", "queijo" },
            { "qeijo", "queijo" },
            { "peperone", "pepperoni" },
            { "peperoni", "pepperoni" },
            { "hamburge", "hamburguer" },
            { "hamburuer", "hamburguer" },
            { "xburger", "hamburguer" },
            { "fucionario", "funcionario" },
            { "funcionarios", "funcionario" },
            { "func", "funcionario" },
            { "limpesa", "limpeza" },
            { "linpeza", "limpeza" },
            { "emprestmo", "emprestimo" },
            { "imprestimo", "emprestimo" },
            { "desfaser", "desfazer" },
            { "disfazer", "desfazer" },
            { "undo", "desfazer" },
            { "recomeca", "recomecar" },
            { "recomeça", "recomecar" },
            { "reiniciar", "recomecar" },
            { "resetar", "recomecar" },
            { "salvr", "salvar" },
            { "salva", "salvar" },
            { "caregar", "carregar" },
            { "carrega", "carregar" },
            { "pedor", "pedro" },
            { "pedru", "pedro" },
            { "carlo", "carlos" },
            { "fallar", "falar" },
            { "flaar", "falar" },
            { "fala", "falar" },
            { "conversa", "conversar" },
            { "abri", "abrir" },
            { "abrr", "abrir" },
            { "entregra", "entrega" },
            { "entegas", "entregas" },
            { "entregar", "entregar" },
            { "estorque", "estoque" },
            { "estoqe", "estoque" },
            { "reseita", "receita" },
            { "reseitas", "receitas" },
            { "cardapo", "cardapio" },
            { "gerensia", "gerencia" },
            { "escrirorio", "escritorio" },
            { "baco", "banco" },
            { "hitorico", "historico" },
            { "historco", "historico" },
            { "terminl", "terminal" },
            { "ajudaa", "ajuda" },
            { "help", "ajuda" },
            { "tutoral", "tutorial" },
            { "acodo", "acordo" },
            { "julgameto", "julgamento" },
            { "adcionar", "adicionar" },
            { "adiconar", "adicionar" },
            { "colcar", "colocar" },
            { "conprar", "comprar" },
            { "melhora", "melhorar" },
            { "aumenta", "aumentar" },
            { "diminui", "diminuir" },
            { "redusir", "reduzir" },
            { "quitar", "quitar" },
            { "pagr", "pagar" },
            { "encerar", "encerrar" },
            { "avancar", "avancar" },
            { "avançar", "avancar" },
            { "prto", "prato" },
            { "bancda", "bancada" }
        };

        private static readonly string[] VocabularioBase =
        {
            "falar", "conversar", "perguntar", "pedro", "ana", "carlos", "cliente", "equipe",
            "abrir", "ir", "ver", "usar", "cozinha", "docas", "entregas", "entrega", "receitas",
            "receita", "cardapio", "estoque", "despensa", "inventario", "gerencia", "escritorio",
            "administracao", "banco", "caixa", "historico", "comandos", "terminal", "dev",
            "adicionar", "colocar", "por", "comprar", "entregar", "servir", "limpar", "desfazer",
            "bancada", "prato", "pedido", "inspecionar", "aceitar", "aprovar", "recusar", "rejeitar",
            "concluir", "finalizar", "auditoria", "ativar", "desativar", "alternar", "contratar",
            "funcionario", "limpeza", "melhorar", "upgrade", "forno", "geladeira", "balcao",
            "logistica", "salario", "preco", "aumentar", "subir", "reduzir", "diminuir", "baixar",
            "emprestimo", "pedir", "pagar", "amortizar", "quitar", "divida", "acordo", "julgamento",
            "julgar", "ajuda", "tutorial", "status", "objetivos", "metas", "encerrar", "avancar",
            "proximo", "dia", "salvar", "carregar", "recomecar", "novo", "sair", "modo",
            "opcoes", "acessibilidade", "contraste", "alto", "semantico", "efeitos", "som",
            "sons", "velocidade", "texto", "lento", "rapido", "normal", "instantaneo",
            "conquistas", "trofeus", "slots", "slot", "partidas", "autosave", "faltantes", "repor"
        };

        private static readonly string[] ComandosExemplo =
        {
            "falar com Pedro",
            "falar com Ana",
            "falar com Carlos",
            "falar com cliente",
            "abrir cozinha",
            "abrir docas",
            "abrir receitas",
            "abrir estoque",
            "abrir gerencia",
            "abrir banco",
            "abrir historico",
            "usar caixa",
            "abrir caixa 1",
            "aceitar caixa 1",
            "recusar caixa 1",
            "concluir entrega",
            "adicionar queijo",
            "comprar queijo",
            "entregar prato",
            "limpar bancada",
            "contratar funcionario",
            "comprar limpeza",
            "melhorar forno",
            "pedir emprestimo 1500",
            "pagar divida 500",
            "quitar divida",
            "desfazer",
            "status",
            "objetivos",
            "encerrar dia",
            "ajuda",
            "tutorial",
            "ultimos comandos",
            "salvar",
            "salvar slot 2",
            "carregar autosave",
            "partidas",
            "opcoes",
            "alto contraste",
            "som off",
            "conquistas",
            "comprar faltantes",
            "recomecar"
        };

        public CommandProcessor(GameEngine game)
        {
            _game = game;
        }

        public void RegistrarComandoNoHistorico(string comandoBruto)
        {
            string limpo = (comandoBruto ?? "").Trim();
            if (string.IsNullOrEmpty(limpo)) return;

            if (_ultimosComandos.Count == 0 || !string.Equals(_ultimosComandos[^1], limpo, StringComparison.OrdinalIgnoreCase))
            {
                _ultimosComandos.Add(limpo);
                if (_ultimosComandos.Count > MaxHistoricoComandos)
                    _ultimosComandos.RemoveAt(0);
            }
        }

        public CommandExecutionResult Executar(string entradaBruta, ScreenTab abaAtual)
        {
            var res = new CommandExecutionResult();
            string bruto = (entradaBruta ?? "").Trim();

            if (string.IsNullOrWhiteSpace(bruto))
            {
                _game.RegistrarFeedbackExterno(
                    LocalityCatalog.ObterNome(abaAtual),
                    "Comando vazio",
                    "Nenhum comando foi digitado.",
                    FeedbackSeverity.Aviso,
                    sugestao: "Experimente digitar \"falar com Pedro\", \"abrir cozinha\", \"usar caixa\" ou \"ajuda\".",
                    adicionarAoHistorico: false);
                return res;
            }

            if (bruto.Length > 100)
            {
                _game.RegistrarFeedbackExterno(
                    LocalityCatalog.ObterNome(abaAtual),
                    "Comando muito longo",
                    "O texto excede o limite de 100 caracteres e foi ignorado com segurança.",
                    FeedbackSeverity.Aviso,
                    sugestao: "Use comandos curtos e objetivos, como \"abrir cozinha\" ou \"falar com Pedro\".",
                    adicionarAoHistorico: false);
                return res;
            }

            RegistrarComandoNoHistorico(bruto);

            // Normalização e correção ortográfica token por token (limitado a 16 tokens por segurança)
            string norm = NormalizarTexto(bruto);
            var correcoes = new List<string>();
            var tokensRaw = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(16).ToArray();
            var tokens = new List<string>();

            foreach (var tk in tokensRaw)
            {
                string corrigido = CorrigirToken(tk, out bool mudou);
                tokens.Add(corrigido);
                if (mudou) correcoes.Add($"\"{tk}\" → \"{corrigido}\"");
            }

            string frase = string.Join(" ", tokens);
            string notaCorrecao = correcoes.Count > 0 ? "Corrigido: " + string.Join(", ", correcoes) : "";

            void AplicarCorrecao()
            {
                if (!string.IsNullOrEmpty(notaCorrecao))
                    _game.AnexarNotaCorrecaoAoFeedbackAtual(notaCorrecao);
            }

            // 1. Se houver processo judicial aberto, prioriza comandos do tribunal e bloqueia o resto explicando
            if (_game.ProcessoAtivo != null)
            {
                if (frase == "1" || tokens.Contains("acordo"))
                {
                    _game.FazerAcordoJudicial();
                    AplicarCorrecao();
                    res.Sucesso = true;
                    return res;
                }
                if (frase == "2" || tokens.Contains("julgamento") || tokens.Contains("julgar"))
                {
                    _game.IrParaJulgamento();
                    AplicarCorrecao();
                    res.Sucesso = true;
                    return res;
                }
                if (tokens.Contains("ajuda"))
                {
                    res.AbrirAjuda = true;
                    res.SecaoAjuda = HelpSection.LocalidadeAtual;
                    res.Sucesso = true;
                    return res;
                }

                _game.RegistrarFeedbackExterno(
                    "Tribunal",
                    $"Comando: \"{bruto}\"",
                    "Ação bloqueada: existe uma intimação judicial pendente que precisa ser decidida primeiro!",
                    FeedbackSeverity.Erro,
                    sugestao: "Digite \"fazer acordo\" (ou [1]) ou \"ir a julgamento\" (ou [2]).",
                    correcaoOrtografica: notaCorrecao,
                    ehComandoTexto: true);
                return res;
            }

            // 2. Atalhos numéricos diretos 1-8 para localidades
            if (tokens.Count == 1 && int.TryParse(tokens[0], out int numAba) && numAba >= 1 && numAba <= 8)
            {
                var destino = (ScreenTab)numAba;
                res.NovaAba = destino;
                res.Sucesso = true;
                _game.RegistrarFeedbackExterno(
                    LocalityCatalog.ObterNome(destino),
                    $"Ir para [{numAba}] {LocalityCatalog.ObterNome(destino)}",
                    $"Você entrou em {LocalityCatalog.ObterNome(destino)}. {LocalityCatalog.ObterPersonagem(destino)} está por perto.",
                    FeedbackSeverity.Info,
                    correcaoOrtografica: notaCorrecao,
                    ehComandoTexto: true);
                return res;
            }

            // 3. Senha do terminal dev
            if (frase.Contains("xit89"))
            {
                _game.InputSenhaTerminal = GameEngine.SenhaDesenvolvedor;
                _game.TentarDesbloquearTerminal();
                res.NovaAba = ScreenTab.Terminal;
                res.Sucesso = true;
                return res;
            }

            // 4. Comandos de sistema, ajuda, tutorial, status, desfazer, recomeçar, sair
            if (frase == "ajuda" || frase == "como jogar" || tokens.Contains("ajuda"))
            {
                res.AbrirAjuda = true;
                res.SecaoAjuda = tokens.Contains("comandos") ? HelpSection.ComandosNaturais : HelpSection.LocalidadeAtual;
                res.Sucesso = true;
                _game.RegistrarFeedbackExterno(LocalityCatalog.ObterNome(abaAtual), "Abrir ajuda contextual",
                    $"Ajuda contextual aberta para {LocalityCatalog.ObterNome(abaAtual)}.",
                    FeedbackSeverity.Info, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                return res;
            }

            if (tokens.Contains("tutorial"))
            {
                res.AbrirAjuda = true;
                res.SecaoAjuda = HelpSection.TutorialCurto;
                res.Sucesso = true;
                _game.RegistrarFeedbackExterno(LocalityCatalog.ObterNome(abaAtual), "Abrir tutorial curto",
                    "Tutorial rápido de 3 passos aberto.",
                    FeedbackSeverity.Info, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                return res;
            }

            if (frase.Contains("ultimos comandos") || frase == "comandos")
            {
                res.NovaAba = ScreenTab.Historico;
                res.Sucesso = true;
                string resumo = _ultimosComandos.Count > 0
                    ? string.Join(" · ", _ultimosComandos.TakeLast(5))
                    : "Nenhum comando anterior.";
                _game.RegistrarFeedbackExterno("Histórico & Comandos", "Consultar últimos comandos",
                    $"Últimos comandos: {resumo}",
                    FeedbackSeverity.Info,
                    sugestao: "Use ↑/↓ na barra de comando ou navegue na aba [7] Histórico.",
                    correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                return res;
            }

            if (tokens.Contains("desfazer"))
            {
                _game.DesfazerUltimaAcao();
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("salvar"))
            {
                int? slotNum = ExtrairNumero(tokens);
                string slotAlvo = tokens.Contains("autosave") ? "autosave" : (slotNum.HasValue && slotNum >= 1 && slotNum <= 3 ? $"slot{slotNum}" : null);
                _game.SalvarJogo(slotAlvo);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("carregar"))
            {
                int? slotNum = ExtrairNumero(tokens);
                string slotAlvo = tokens.Contains("autosave") ? "autosave" : (slotNum.HasValue && slotNum >= 1 && slotNum <= 3 ? $"slot{slotNum}" : null);
                _game.CarregarJogo(slotAlvo);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("slots") || tokens.Contains("partidas") || frase.Contains("gerenciar save"))
            {
                res.AbrirGerenciadorSaves = true;
                res.Sucesso = true;
                _game.RegistrarFeedbackExterno("Sistema", "Abrir gerenciador de partidas",
                    "Gerenciador de slots (Autosave + Slots 1 a 3) aberto.",
                    FeedbackSeverity.Info, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                return res;
            }

            if (tokens.Contains("opcoes") || tokens.Contains("acessibilidade"))
            {
                res.AbrirOpcoes = true;
                res.Sucesso = true;
                _game.RegistrarFeedbackExterno("Sistema", "Abrir acessibilidade e áudio",
                    $"Opções abertas: {_game.Configuracoes.ObterResumo()}",
                    FeedbackSeverity.Info, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                return res;
            }

            if (tokens.Contains("contraste"))
            {
                _game.Configuracoes.AltoContraste = !_game.Configuracoes.AltoContraste;
                _game.SalvarConfiguracoes();
                _game.RegistrarFeedbackExterno("Acessibilidade", "Alternar alto contraste",
                    $"Modo Alto Contraste {(_game.Configuracoes.AltoContraste ? "ATIVADO" : "DESATIVADO")}.",
                    FeedbackSeverity.Sucesso, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("semantico"))
            {
                _game.Configuracoes.ModoSemantico = !_game.Configuracoes.ModoSemantico;
                _game.SalvarConfiguracoes();
                _game.RegistrarFeedbackExterno("Acessibilidade", "Alternar modo semântico",
                    $"Modo Semântico de terminal {(_game.Configuracoes.ModoSemantico ? "ATIVADO (ASCII acessível)" : "DESATIVADO (gráfico)")}.",
                    FeedbackSeverity.Sucesso, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("efeitos"))
            {
                _game.Configuracoes.DesativarEfeitosVisuais = !_game.Configuracoes.DesativarEfeitosVisuais;
                _game.SalvarConfiguracoes();
                _game.RegistrarFeedbackExterno("Acessibilidade", "Alternar efeitos visuais",
                    $"Efeitos visuais {(_game.Configuracoes.DesativarEfeitosVisuais ? "DESATIVADOS" : "ATIVADOS")}.",
                    FeedbackSeverity.Sucesso, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("som") || tokens.Contains("sons"))
            {
                bool desligar = frase.Contains("off") || frase.Contains("deslig") || frase.Contains("mudo") || frase.Contains("sem");
                bool ligar = frase.Contains("on") || frase.Contains("lig");
                _game.Configuracoes.SonsAtivos = ligar ? true : (desligar ? false : !_game.Configuracoes.SonsAtivos);
                _game.SalvarConfiguracoes();
                _game.RegistrarFeedbackExterno("Áudio", "Configurar som",
                    $"Sons opcionais {(_game.Configuracoes.SonsAtivos ? "ATIVADOS" : "DESATIVADOS")}.",
                    FeedbackSeverity.Sucesso, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("velocidade") || (tokens.Contains("texto") && (tokens.Contains("lento") || tokens.Contains("rapido") || tokens.Contains("normal") || tokens.Contains("instantaneo"))))
            {
                if (tokens.Contains("instantaneo")) _game.Configuracoes.VelocidadeTexto = TextSpeed.Instantaneo;
                else if (tokens.Contains("rapido")) _game.Configuracoes.VelocidadeTexto = TextSpeed.Rapido;
                else if (tokens.Contains("normal")) _game.Configuracoes.VelocidadeTexto = TextSpeed.Normal;
                else if (tokens.Contains("lento")) _game.Configuracoes.VelocidadeTexto = TextSpeed.Lento;
                else _game.Configuracoes.VelocidadeTexto = (TextSpeed)(((int)_game.Configuracoes.VelocidadeTexto + 1) % 4);
                _game.SalvarConfiguracoes();
                _game.RegistrarFeedbackExterno("Acessibilidade", "Ajustar velocidade do texto",
                    $"Velocidade do texto definida para: {_game.Configuracoes.VelocidadeTexto}.",
                    FeedbackSeverity.Sucesso, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("conquistas") || tokens.Contains("trofeus"))
            {
                res.NovaAba = ScreenTab.Historico;
                res.FiltroHistoricoAlvo = "CONQUISTAS";
                res.Sucesso = true;
                _game.RegistrarFeedbackExterno("Histórico & Conquistas", "Ver conquistas",
                    $"Conquistas desbloqueadas: {_game.ConquistasDesbloqueadas.Count}/{Catalog.Conquistas.Count}.",
                    FeedbackSeverity.Info, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                return res;
            }

            if (tokens.Contains("faltantes") || (tokens.Contains("repor") && (tokens.Contains("pedido") || tokens.Contains("prato") || tokens.Contains("faltantes"))))
            {
                res.NovaAba = ScreenTab.Cozinha;
                _game.ComprarIngredientesFaltantesPedido();
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("recomecar") || frase == "novo jogo")
            {
                res.AbrirConfirmacaoRecomecar = true;
                res.Sucesso = true;
                _game.RegistrarFeedbackExterno(LocalityCatalog.ObterNome(abaAtual), "Recomeçar partida",
                    "Confirme se deseja iniciar uma nova partida do zero.",
                    FeedbackSeverity.Aviso, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                return res;
            }

            if (frase == "sair" || frase == "fechar jogo")
            {
                res.AbrirConfirmacaoSaida = true;
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("encerrar") || tokens.Contains("avancar") || frase == "proximo dia" || frase == "fechar dia")
            {
                _game.AvancarDia();
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("status") || tokens.Contains("objetivos") || tokens.Contains("metas"))
            {
                var inv = _game.ObterResumoInventario();
                _game.RegistrarFeedbackExterno(
                    LocalityCatalog.ObterNome(abaAtual),
                    "Consultar status e objetivos",
                    $"Fase {_game.NivelAtual}/5 · Caixa R$ {_game.Dinheiro:N2} · Rep {_game.Reputacao:F1}* · Energia {_game.AcoesRestantesDia}/{_game.AcoesMaximasDia} · Inv {inv.TotalUnidades}/{inv.CapacidadeTotal} un.",
                    FeedbackSeverity.Info,
                    sugestao: _game.ObterTextoProximaMeta(),
                    correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                res.Sucesso = true;
                return res;
            }

            // 5. Falar com personagens (ex: "falar com Pedro", "conversar com Ana", "perguntar ao Carlos")
            if (tokens.Contains("falar") || tokens.Contains("conversar") || tokens.Contains("perguntar") ||
                frase == "pedro" || frase == "ana" || frase == "carlos" || frase == "cliente")
            {
                string alvo = ExtrairPersonagem(tokens);
                if (string.IsNullOrEmpty(alvo))
                {
                    _game.RegistrarFeedbackExterno(
                        LocalityCatalog.ObterNome(abaAtual),
                        $"Comando: \"{bruto}\"",
                        "Com quem você deseja falar?",
                        FeedbackSeverity.Aviso,
                        sugestao: "Tente \"falar com Pedro\" (Cozinha), \"falar com Ana\" (Docas), \"falar com Carlos\" (Gerência) ou \"falar com cliente\".",
                        correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                    return res;
                }

                if (alvo == "pedro" || alvo == "cliente") res.NovaAba = ScreenTab.Cozinha;
                else if (alvo == "ana") res.NovaAba = ScreenTab.Entregas;
                else if (alvo == "carlos") res.NovaAba = ScreenTab.Administracao;

                _game.FalarComPersonagem(alvo);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            // 6. Interações com Caixa (entrega ou caixa financeiro: "usar caixa", "abrir caixa 1", "aceitar caixa 2", "recusar caixa 3")
            int? numeroExtraido = ExtrairNumero(tokens);

            if (tokens.Contains("caixa") && (tokens.Contains("abrir") || tokens.Contains("inspecionar") || tokens.Contains("usar") || tokens.Contains("ver")))
            {
                if (numeroExtraido.HasValue)
                {
                    res.NovaAba = ScreenTab.Entregas;
                    _game.InspecionarCaixaEntrega(numeroExtraido.Value - 1);
                    AplicarCorrecao();
                    res.Sucesso = true;
                    return res;
                }
                else if (abaAtual == ScreenTab.Entregas)
                {
                    // Abre a primeira caixa ainda fechada nas Docas!
                    int idxFechada = _game.EntregaAtual?.FindIndex(c => !c.CaixaAberta) ?? -1;
                    if (idxFechada >= 0)
                    {
                        _game.InspecionarCaixaEntrega(idxFechada);
                        AplicarCorrecao();
                        res.Sucesso = true;
                        return res;
                    }
                    _game.RegistrarFeedbackExterno("Docas (Entregas)", "Abrir caixa",
                        "Todas as 7 caixas desta entrega já estão abertas.",
                        FeedbackSeverity.Aviso,
                        sugestao: "Marque ACEITAR/RECUSAR e digite \"concluir entrega\".",
                        correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                    return res;
                }
                else
                {
                    // "usar caixa" fora das docas -> abre o Caixa/Banco e informa situação financeira
                    res.NovaAba = ScreenTab.Banco;
                    double folha = _game.CalcularDespesaMensalFixa();
                    _game.RegistrarFeedbackExterno(
                        "Banco Empresarial",
                        "Usar caixa financeiro",
                        $"Caixa aberto! Saldo: R$ {_game.Dinheiro:N2} · Dívida: R$ {_game.DividaTotalBanco:N2} · Folha prevista: R$ {folha:N2}/mês.",
                        FeedbackSeverity.Info,
                        sugestao: "Aqui você pode tomar empréstimo (ex.: \"pedir emprestimo 1500\") ou pagar dívidas (\"quitar divida\"). Nas Docas [2], \"abrir caixa 1\" inspeciona entregas.",
                        correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                    res.Sucesso = true;
                    return res;
                }
            }

            if ((tokens.Contains("aceitar") || tokens.Contains("aprovar")) && numeroExtraido.HasValue)
            {
                res.NovaAba = ScreenTab.Entregas;
                _game.AlternarAprovacaoItemEntrega(numeroExtraido.Value - 1, definirValor: true);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if ((tokens.Contains("recusar") || tokens.Contains("rejeitar")) && numeroExtraido.HasValue)
            {
                res.NovaAba = ScreenTab.Entregas;
                _game.AlternarAprovacaoItemEntrega(numeroExtraido.Value - 1, definirValor: false);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if ((tokens.Contains("concluir") || tokens.Contains("finalizar")) && (tokens.Contains("entrega") || tokens.Contains("entregas") || tokens.Contains("auditoria") || abaAtual == ScreenTab.Entregas))
            {
                res.NovaAba = ScreenTab.Entregas;
                _game.FinalizarConferenciaEntrega();
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            // 7. Ações da Cozinha (entregar, limpar bancada, modo bancada, comprar emergencial, adicionar ingrediente)
            if (tokens.Contains("entregar") || tokens.Contains("servir"))
            {
                res.NovaAba = ScreenTab.Cozinha;
                _game.EntregarPedido();
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("limpar") && (tokens.Contains("bancada") || tokens.Contains("prato") || abaAtual == ScreenTab.Cozinha && !tokens.Contains("cozinha")))
            {
                res.NovaAba = ScreenTab.Cozinha;
                _game.LimparPratoAtual();
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("modo") && (tokens.Contains("bancada") || tokens.Contains("todos") || tokens.Contains("ingredientes")))
            {
                res.NovaAba = ScreenTab.Cozinha;
                res.AlternarModoBancada = true;
                res.Sucesso = true;
                return res;
            }

            // 8. Ações de Gerência (contratar, limpeza, upgrades, salários, preços)
            if (tokens.Contains("contratar") || (tokens.Contains("funcionario") && (tokens.Contains("comprar") || tokens.Contains("mais") || numeroExtraido.HasValue)))
            {
                int qtd = numeroExtraido ?? 1;
                res.NovaAba = ScreenTab.Administracao;
                _game.ContratarFuncionariosLote(qtd);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("limpeza") || (tokens.Contains("limpar") && tokens.Contains("cozinha")))
            {
                int qtd = numeroExtraido ?? 1;
                res.NovaAba = ScreenTab.Administracao;
                _game.ComprarLimpezaLote(qtd);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("forno") || tokens.Contains("geladeira") || tokens.Contains("balcao") || tokens.Contains("logistica") ||
                ((tokens.Contains("melhorar") || tokens.Contains("upgrade")) && (tokens.Contains("forno") || tokens.Contains("geladeira") || tokens.Contains("balcao") || tokens.Contains("logistica"))))
            {
                string tipo = tokens.Contains("forno") ? "FORNO"
                    : tokens.Contains("geladeira") ? "GELADEIRA"
                    : tokens.Contains("balcao") ? "BALCAO"
                    : "LOGISTICA";
                int lote = numeroExtraido ?? 1;
                res.NovaAba = ScreenTab.Administracao;
                _game.ComprarUpgradeNomeado(tipo, lote);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("salario"))
            {
                bool diminuir = tokens.Contains("reduzir") || tokens.Contains("diminuir") || tokens.Contains("baixar");
                res.NovaAba = ScreenTab.Administracao;
                _game.AjustarSalario(diminuir ? -0.10 : 0.10);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("preco"))
            {
                bool diminuir = tokens.Contains("reduzir") || tokens.Contains("diminuir") || tokens.Contains("baixar");
                res.NovaAba = ScreenTab.Administracao;
                _game.AjustarPrecoPratos(diminuir ? -0.10 : 0.10);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            // 9. Ações do Banco (empréstimo, amortizar, quitar)
            if (tokens.Contains("emprestimo") || (tokens.Contains("pedir") && numeroExtraido.HasValue))
            {
                double valor = numeroExtraido ?? 1500;
                res.NovaAba = ScreenTab.Banco;
                _game.PedirEmprestimo(valor);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("quitar"))
            {
                res.NovaAba = ScreenTab.Banco;
                _game.AmortizarDivida(_game.DividaTotalBanco);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            if (tokens.Contains("amortizar") || (tokens.Contains("pagar") && (tokens.Contains("divida") || tokens.Contains("banco") || numeroExtraido.HasValue)))
            {
                double valor = numeroExtraido ?? 500;
                res.NovaAba = ScreenTab.Banco;
                _game.AmortizarDivida(valor);
                AplicarCorrecao();
                res.Sucesso = true;
                return res;
            }

            // 10. Ativar / Desativar prato no Cardápio
            if (tokens.Contains("ativar") || tokens.Contains("desativar") || tokens.Contains("alternar"))
            {
                string alvoPrato = RemoverVerbosNavegacao(tokens);
                var prato = BuscarPratoProximo(alvoPrato);
                if (prato != null)
                {
                    res.NovaAba = ScreenTab.LivroReceitas;
                    bool jaAtivo = _game.PratosAtivosNoCardapio.Contains(prato.Nome);
                    if (tokens.Contains("ativar") && jaAtivo)
                    {
                        _game.RegistrarFeedbackExterno("Cardápio & Receitas", $"Ativar {prato.Nome}",
                            $"[{prato.Nome}] já está ativo no cardápio.",
                            FeedbackSeverity.Aviso, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                        res.Sucesso = true;
                        return res;
                    }
                    if (tokens.Contains("desativar") && !jaAtivo)
                    {
                        _game.RegistrarFeedbackExterno("Cardápio & Receitas", $"Desativar {prato.Nome}",
                            $"[{prato.Nome}] já está inativo no cardápio.",
                            FeedbackSeverity.Aviso, correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                        res.Sucesso = true;
                        return res;
                    }
                    _game.AlternarPratoNoCardapio(prato.Nome);
                    AplicarCorrecao();
                    res.Sucesso = true;
                    return res;
                }
            }

            // 11. Adicionar / Comprar ingrediente na Cozinha
            if (tokens.Contains("adicionar") || tokens.Contains("colocar") || tokens.Contains("por") || tokens.Contains("comprar"))
            {
                string alvoIng = RemoverVerbosNavegacao(tokens);
                var ing = BuscarIngredienteProximo(alvoIng);
                if (ing != null)
                {
                    res.NovaAba = ScreenTab.Cozinha;
                    if (tokens.Contains("comprar"))
                        _game.CompraEmergencialIngrediente(ing.Nome);
                    else
                        _game.AdicionarIngredienteAoPrato(ing.Nome);

                    AplicarCorrecao();
                    res.Sucesso = true;
                    return res;
                }
                else
                {
                    _game.RegistrarFeedbackExterno(
                        "Cozinha",
                        $"Comando: \"{bruto}\"",
                        $"Não encontrei o ingrediente \"{alvoIng}\" nos insumos desbloqueados da Fase {_game.NivelAtual}/5.",
                        FeedbackSeverity.Erro,
                        sugestao: _game.PedidoAtual != null
                            ? $"No pedido atual ({_game.PedidoAtual.Receita.Nome}), tente: \"adicionar {_game.PedidoAtual.OrdemExigida[0]}\"."
                            : "Veja os ingredientes disponíveis na aba [4] Estoque.",
                        correcaoOrtografica: notaCorrecao, ehComandoTexto: true);
                    return res;
                }
            }

            // 12. Navegação natural entre localidades (ex: "abrir cozinha", "ir para docas", "estoque", "banco")
            ScreenTab? tabDestino = DetectarLocalidade(tokens);
            if (tabDestino.HasValue)
            {
                res.NovaAba = tabDestino.Value;
                res.Sucesso = true;
                string nomeLoc = LocalityCatalog.ObterNome(tabDestino.Value);
                _game.RegistrarFeedbackExterno(
                    nomeLoc,
                    $"Abrir {nomeLoc}",
                    $"Você foi para {nomeLoc}. {LocalityCatalog.ObterPersonagem(tabDestino.Value)}.",
                    FeedbackSeverity.Info,
                    correcaoOrtografica: notaCorrecao,
                    ehComandoTexto: true);
                return res;
            }

            // 13. Caso o jogador digite apenas o nome de um ingrediente na Cozinha (atalho inteligente)
            if (abaAtual == ScreenTab.Cozinha)
            {
                var ingDireto = BuscarIngredienteProximo(frase);
                if (ingDireto != null)
                {
                    _game.AdicionarIngredienteAoPrato(ingDireto.Nome);
                    AplicarCorrecao();
                    res.Sucesso = true;
                    return res;
                }
            }

            // 14. Comando não reconhecido -> explicar motivo e sugerir alternativas próximas!
            var sugestoes = SugerirComandosProximos(frase, abaAtual);
            _game.RegistrarFeedbackExterno(
                LocalityCatalog.ObterNome(abaAtual),
                $"Comando: \"{bruto}\"",
                $"Não entendi o comando \"{bruto}\": nenhuma ação correspondente encontrada.",
                FeedbackSeverity.Erro,
                sugestao: $"Tente: {string.Join(", ", sugestoes.Select(s => $"\"{s}\""))}.",
                correcaoOrtografica: notaCorrecao,
                ehComandoTexto: true);
            return res;
        }

        private ScreenTab? DetectarLocalidade(List<string> tokens)
        {
            if (tokens.Contains("cozinha")) return ScreenTab.Cozinha;
            if (tokens.Contains("docas") || tokens.Contains("entregas") || tokens.Contains("entrega")) return ScreenTab.Entregas;
            if (tokens.Contains("receitas") || tokens.Contains("receita") || tokens.Contains("cardapio")) return ScreenTab.LivroReceitas;
            if (tokens.Contains("estoque") || tokens.Contains("despensa") || tokens.Contains("inventario")) return ScreenTab.EstoqueCatalogo;
            if (tokens.Contains("gerencia") || tokens.Contains("escritorio") || tokens.Contains("administracao")) return ScreenTab.Administracao;
            if (tokens.Contains("banco")) return ScreenTab.Banco;
            if (tokens.Contains("historico")) return ScreenTab.Historico;
            if (tokens.Contains("terminal") || tokens.Contains("dev")) return ScreenTab.Terminal;
            return null;
        }

        private string ExtrairPersonagem(List<string> tokens)
        {
            string[] stopwords = { "falar", "conversar", "perguntar", "com", "ao", "a", "o", "para", "pro", "pra", "sobre", "no", "na" };
            var candidatos = tokens.Where(t => !stopwords.Contains(t)).ToList();
            if (candidatos.Count == 0) return "";

            string primeiro = candidatos[0];
            string[] conhecidos = { "pedro", "ana", "carlos", "cliente", "cozinheiro", "subchefe", "chef", "conferente", "gerente", "contador", "equipe" };
            foreach (var c in conhecidos)
            {
                if (primeiro == c || DistanciaLevenshtein(primeiro, c) <= 1)
                    return c == "equipe" ? "pedro" : c;
            }
            return primeiro;
        }

        private int? ExtrairNumero(List<string> tokens)
        {
            foreach (var t in tokens)
            {
                string limpo = t.Replace(".", "").Replace(",", "").Replace("r$", "").Replace("#", "");
                if (int.TryParse(limpo, out int v) && v > 0) return v;
            }
            return null;
        }

        private string RemoverVerbosNavegacao(List<string> tokens)
        {
            string[] verbos = { "adicionar", "colocar", "por", "comprar", "ativar", "desativar", "alternar", "um", "uma", "o", "a", "de", "no", "na", "prato", "bancada", "cardapio", "emergencial" };
            return string.Join(" ", tokens.Where(t => !verbos.Contains(t)));
        }

        private IngredientInfo BuscarIngredienteProximo(string busca)
        {
            if (string.IsNullOrWhiteSpace(busca)) return null;
            string b = NormalizarTexto(busca);

            var disponiveis = Catalog.TodosIngredientes.Where(i => i.Nivel <= _game.NivelAtual).ToList();

            // 1. Exato
            var exato = disponiveis.FirstOrDefault(i => NormalizarTexto(i.Nome) == b);
            if (exato != null) return exato;

            // 2. Contém (prioriza match mais curto)
            var contem = disponiveis
                .Where(i => NormalizarTexto(i.Nome).Contains(b) || b.Contains(NormalizarTexto(i.Nome)))
                .OrderBy(i => Math.Abs(NormalizarTexto(i.Nome).Length - b.Length))
                .FirstOrDefault();
            if (contem != null && b.Length >= 3) return contem;

            // 3. Levenshtein
            IngredientInfo melhor = null;
            int melhorDist = int.MaxValue;
            foreach (var ing in disponiveis)
            {
                string n = NormalizarTexto(ing.Nome);
                int d = DistanciaLevenshtein(b, n);
                int limite = n.Length >= 7 ? 2 : 1;
                if (d <= limite && d < melhorDist)
                {
                    melhorDist = d;
                    melhor = ing;
                }
            }
            return melhor;
        }

        private DishRecipe BuscarPratoProximo(string busca)
        {
            if (string.IsNullOrWhiteSpace(busca)) return null;
            string b = NormalizarTexto(busca);
            var disponiveis = Catalog.Pratos.Where(p => p.NivelDesbloqueio <= _game.NivelAtual).ToList();

            var exato = disponiveis.FirstOrDefault(p => NormalizarTexto(p.Nome) == b);
            if (exato != null) return exato;

            var contem = disponiveis.FirstOrDefault(p => NormalizarTexto(p.Nome).Contains(b));
            if (contem != null && b.Length >= 3) return contem;

            DishRecipe melhor = null;
            int melhorDist = int.MaxValue;
            foreach (var p in disponiveis)
            {
                string n = NormalizarTexto(p.Nome);
                int d = DistanciaLevenshtein(b, n);
                int limite = n.Length >= 7 ? 2 : 1;
                if (d <= limite && d < melhorDist)
                {
                    melhorDist = d;
                    melhor = p;
                }
            }
            return melhor;
        }

        private List<string> SugerirComandosProximos(string fraseNormalizada, ScreenTab abaAtual)
        {
            var ranqueados = ComandosExemplo
                .Select(cmd => new
                {
                    Cmd = cmd,
                    Dist = DistanciaLevenshtein(fraseNormalizada, NormalizarTexto(cmd))
                })
                .OrderBy(x => x.Dist)
                .Take(2)
                .Select(x => x.Cmd)
                .ToList();

            string contextual = abaAtual switch
            {
                ScreenTab.Cozinha => "falar com Pedro",
                ScreenTab.Entregas => "abrir caixa 1",
                ScreenTab.Administracao => "contratar funcionario",
                ScreenTab.Banco => "pedir emprestimo 1500",
                _ => "ajuda"
            };

            if (!ranqueados.Contains(contextual))
                ranqueados.Add(contextual);

            return ranqueados.Take(3).ToList();
        }

        private string CorrigirToken(string token, out bool corrigido)
        {
            corrigido = false;
            if (string.IsNullOrWhiteSpace(token) || token.Length <= 2 || int.TryParse(token, out _))
                return token;

            if (ErrosComuns.TryGetValue(token, out string direto))
            {
                corrigido = direto != token;
                return direto;
            }

            if (VocabularioBase.Contains(token))
                return token;

            // Verifica se já é um ingrediente conhecido exatamente
            if (Catalog.TodosIngredientes.Any(i => NormalizarTexto(i.Nome).Split(' ').Contains(token)))
                return token;

            // Tenta fuzzy contra vocabulário base
            string melhor = null;
            int melhorDist = int.MaxValue;
            foreach (var palavra in VocabularioBase)
            {
                int limite = palavra.Length >= 7 ? 2 : (palavra.Length >= 4 ? 1 : 0);
                if (limite == 0) continue;
                int d = DistanciaLevenshtein(token, palavra);
                if (d > 0 && d <= limite && d < melhorDist)
                {
                    melhorDist = d;
                    melhor = palavra;
                }
            }

            if (melhor != null)
            {
                corrigido = true;
                return melhor;
            }

            return token;
        }

        public static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            string formD = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char ch in formD)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ch == '-')
                    sb.Append(ch == '-' ? ' ' : ch);
                else
                    sb.Append(' ');
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        public static int DistanciaLevenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            int n = a.Length;
            int m = b.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int custo = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + custo);
                }
            }
            return d[n, m];
        }
    }
}
