using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PizzariaTycoon;

int total = 0;
int falhas = 0;

void Assert(bool condicao, string nome)
{
    total++;
    if (condicao)
    {
        Console.WriteLine($"[PASS] {nome}");
    }
    else
    {
        Console.WriteLine($"[FAIL] {nome}");
        falhas++;
    }
}

string CriarDirTemp()
{
    string dir = Path.Combine(Path.GetTempPath(), "PizzariaTycoonTests_" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(dir);
    return dir;
}

string tempRoot = CriarDirTemp();
try
{
    // 1. DETERMINISMO (mesma seed -> mesmo estado inicial)
    var p1 = new PersistenceService(Path.Combine(tempRoot, "det1"));
    var p2 = new PersistenceService(Path.Combine(tempRoot, "det2"));
    var g1 = new GameEngine(seed: 1989, persistence: p1);
    var g2 = new GameEngine(seed: 1989, persistence: p2);
    g1.Configuracoes.SonsAtivos = false;
    g2.Configuracoes.SonsAtivos = false;

    Assert(g1.Diversao == g2.Diversao, "Determinismo: Diversao identica com mesma seed");
    Assert(g1.PedidoAtual.Receita.Nome == g2.PedidoAtual.Receita.Nome, "Determinismo: Primeiro pedido identico");
    Assert(g1.PedidoAtual.OrdemExigida.SequenceEqual(g2.PedidoAtual.OrdemExigida), "Determinismo: Ordem exigida identica");
    Assert(g1.EntregaAtual.Count == 7 && g1.EntregaAtual[0].NomeNota == g2.EntregaAtual[0].NomeNota, "Determinismo: Carga de entregas identica");

    // 2. DIFICULDADE E MODOS DE JOGO
    var gFacil = new GameEngine(seed: 42, persistence: new PersistenceService(Path.Combine(tempRoot, "dif_facil")));
    gFacil.Configuracoes.SonsAtivos = false;
    gFacil.IniciarNovaRun(false, GameMode.Carreira, DifficultyMode.Facil);

    var gDificil = new GameEngine(seed: 42, persistence: new PersistenceService(Path.Combine(tempRoot, "dif_dificil")));
    gDificil.Configuracoes.SonsAtivos = false;
    gDificil.IniciarNovaRun(false, GameMode.Carreira, DifficultyMode.Dificil);

    Assert(gFacil.Dinheiro > gDificil.Dinheiro, "Dificuldade: Modo Facil inicia com mais caixa que Dificil");
    Assert(gFacil.AcoesMaximasDia > gDificil.AcoesMaximasDia, "Dificuldade: Modo Facil tem mais Energia diaria que Dificil");
    Assert(gFacil.TaxaJurosMensal < gDificil.TaxaJurosMensal, "Dificuldade: Modo Facil tem juros menores que Dificil");

    // Modo Gestao Livre (sem falencia)
    var gLivre = new GameEngine(seed: 7, persistence: new PersistenceService(Path.Combine(tempRoot, "livre")));
    gLivre.Configuracoes.SonsAtivos = false;
    gLivre.IniciarNovaRun(false, GameMode.GestaoLivre, DifficultyMode.Normal);
    Assert(gLivre.Dinheiro == 5000.0, "Modo Gestao Livre inicia com R$ 5.000");
    gLivre.DevAlterarDinheiro(-7000);
    gLivre.ComprarLimpezaLote(1);
    Assert(!gLivre.GameOver, "Modo Gestao Livre nao sofre GameOver por caixa negativo");

    // Modo Campanha 12 Dias
    var gCamp = new GameEngine(seed: 10, persistence: new PersistenceService(Path.Combine(tempRoot, "camp")));
    gCamp.Configuracoes.SonsAtivos = false;
    gCamp.IniciarNovaRun(false, GameMode.CampanhaDias, DifficultyMode.Normal);
    Assert(gCamp.ObterTextoProximaMeta().Contains("Campanha"), "Modo Campanha exibe objetivo com prazo de dias");
    gCamp.DevAlterarNivel(2); // Fase 3
    gCamp.DevAlterarDinheiro(10000);
    gCamp.DevAlterarReputacao(1.0);
    gCamp.ComprarLimpezaLote(1); // dispara checagem de vitoria
    Assert(gCamp.JogoVencido, "Modo Campanha vence ao atingir Fase 3, R$ 8k liquidos e 4.0*");

    // 3. EFICIENCIA (COMBO), CONQUISTAS, DELTAS E DESFAZER
    var persist = new PersistenceService(Path.Combine(tempRoot, "main"));
    var game = new GameEngine(seed: 2026, persistence: persist);
    game.Configuracoes.SonsAtivos = false;
    game.ExibirTutorial = false;
    var ui = new ConsoleUI(game);

    // Protecao contra prato vazio
    int energiaAntes = game.AcoesRestantesDia;
    game.EntregarPedido();
    Assert(game.AcoesRestantesDia == energiaAntes && game.FeedbackAtual.Severidade == FeedbackSeverity.Erro,
        "Entrega de prato vazio bloqueada sem consumir energia");

    // Montar e desfazer ingrediente + limpar bancada + restaurar
    string primeiroIng = game.PedidoAtual.OrdemExigida[0];
    game.AdicionarIngredienteAoPrato(primeiroIng);
    Assert(game.PodeDesfazer && game.PedidoAtual.PratoMontado.Count == 1, "Adicionar ingrediente habilita Desfazer");
    game.LimparPratoAtual();
    Assert(game.PedidoAtual.PratoMontado.Count == 0 && game.PodeDesfazer, "Limpar bancada esvazia e e reversivel");
    game.DesfazerUltimaAcao();
    Assert(game.PedidoAtual.PratoMontado.Count == 1, "Desfazer apos limpar bancada restaurou ingredientes");
    game.DesfazerUltimaAcao();
    Assert(game.PedidoAtual.PratoMontado.Count == 0, "Desfazer removeu ingrediente adicionado");

    // Entregar 5 pedidos perfeitos consecutivos -> combo + conquista PRIMEIRO_PRATO e COMBO_5
    for (int p = 0; p < 5; p++)
    {
        foreach (var ing in game.PedidoAtual.OrdemExigida.ToList())
        {
            if (game.Estoque[ing] <= 0) game.CompraEmergencialIngrediente(ing);
            game.AdicionarIngredienteAoPrato(ing);
        }
        game.EntregarPedido();
    }
    Assert(game.PedidosPerfeitosTotal == 5, "5 pedidos perfeitos contabilizados");
    Assert(game.SequenciaPerfeita == 5 && game.BonusEficienciaCombo >= 0.10, "Combo de eficiencia de 5x ativa bonus +10%");
    Assert(game.ConquistasDesbloqueadas.Contains("PRIMEIRO_PRATO"), "Conquista PRIMEIRO_PRATO desbloqueada");
    Assert(game.ConquistasDesbloqueadas.Contains("COMBO_5"), "Conquista COMBO_5 desbloqueada");
    Assert(game.FeedbackAtual.Deltas.Any(d => d.Texto.Contains("Dinheiro +")), "Deltas de estado exibem variacao de Dinheiro");

    // Consequencia narrativa duravel (salario -> moral) + desfazer
    int moralAntes = game.Narrativa.MoralEquipe;
    game.AjustarSalario(0.10);
    Assert(game.Narrativa.MoralEquipe > moralAntes, "Aumentar salario eleva Moral da Equipe");
    game.DesfazerUltimaAcao();
    Assert(game.Narrativa.MoralEquipe == moralAntes, "Desfazer ajuste de salario restaura Moral anterior");

    // Quitacao de divida -> conquista SEM_DIVIDAS
    game.PedirEmprestimo(1500);
    Assert(game.DividaTotalBanco == 1500, "Emprestimo registrado corretamente");
    game.AmortizarDivida(1500);
    Assert(game.DividaTotalBanco == 0 && game.ConquistasDesbloqueadas.Contains("SEM_DIVIDAS"),
        "Quitar divida desbloqueia conquista SEM_DIVIDAS");

    // 4. PERSISTENCIA MULTI-SLOT, AUTOSAVE, VERSIONAMENTO, MIGRACAO V1 E RECUPERACAO DE CORRUPCAO
    game.SalvarJogo("slot1");
    game.SalvarJogo("slot2");
    game.SalvarAutomatico("teste");
    var slots = game.ListarSlotsSave();
    Assert(slots.Count == 4 && slots.Count(s => s.Existe) >= 3, "Multiplos slots + autosave listados corretamente");

    // Salva segunda vez no slot1 para gerar backup .bak
    double dinheiroSalvoAntesCorrupcao = game.Dinheiro;
    game.ContratarFuncionariosLote(1);
    game.SalvarJogo("slot1"); // agora existe slot1.json e slot1.json.bak!
    string slot1Path = persist.ObterCaminhoSlot("slot1");
    Assert(File.Exists(slot1Path + ".bak"), "Backup .bak gerado automaticamente antes de sobrescrever");

    // Corromper intencionalmente slot1.json e testar recuperacao automatica
    File.WriteAllText(slot1Path, "{ JSON CORROMPIDO !!! ");
    var loadCorrompido = persist.Carregar("slot1");
    Assert(loadCorrompido.Sucesso && loadCorrompido.RecuperadoDeBackup,
        "Save corrompido detectado e restaurado automaticamente via backup .bak");
    Assert(Math.Abs(loadCorrompido.Data.Dinheiro - dinheiroSalvoAntesCorrupcao) < 0.01,
        "Dados restaurados do backup correspondem ao estado integro anterior");
    Assert(File.Exists(persist.ErrorLogPath), "Erro de corrupcao registrado com seguranca em logs/errors.log");

    // Testar adulteracao de checksum SHA-256
    string slot2Path = persist.ObterCaminhoSlot("slot2");
    string jsonOriginal = File.ReadAllText(slot2Path);
    File.WriteAllText(slot2Path, jsonOriginal.Replace("\"NivelAtual\": 1", "\"NivelAtual\": 4"));
    var loadChecksum = persist.Carregar("slot2");
    Assert(!loadChecksum.Sucesso, "Adulteracao de payload detectada pelo Checksum SHA-256");

    // Testar migracao de formato legado v1 (sem FormatVersion/Envelope)
    var legadoV1 = new
    {
        Diversao = 50,
        NivelAtual = 2,
        DataAtual = new DateTime(2026, 2, 7),
        DiaSemana = 2,
        AcoesRestantesDia = 7,
        Dinheiro = 3456.78,
        Reputacao = 4.1,
        Funcionarios = 10,
        Limpeza = 15,
        UpgForno = 3,
        UpgGeladeira = 3,
        UpgBalcao = 2,
        UpgLogistica = 2,
        MultiplicadorSalario = 1.0,
        MultiplicadorPrecoPrato = 1.0,
        DividaTotalBanco = 0.0,
        MesesRestantesDivida = 0,
        PedidosPerfeitosTotal = 12,
        RecordePontuacao = 9000,
        Estoque = new Dictionary<string, int> { { "Massa", 8 } },
        PratosAtivos = new List<string> { "Pizza", "Macarrão" }
    };
    string pV1Dir = Path.Combine(tempRoot, "v1_mig");
    Directory.CreateDirectory(pV1Dir);
    File.WriteAllText(Path.Combine(pV1Dir, "savegame.json"), JsonSerializer.Serialize(legadoV1));
    var pV1 = new PersistenceService(pV1Dir);
    var resV1 = pV1.Carregar("slot1");
    Assert(resV1.Sucesso && resV1.MigradoDeVersaoAntiga && Math.Abs(resV1.Data.Dinheiro - 3456.78) < 0.01,
        "Migracao automatica de save legado v1 para formato v2");

    // Persistir configuracoes de acessibilidade
    game.Configuracoes.AltoContraste = true;
    game.Configuracoes.ModoSemantico = true;
    game.Configuracoes.VelocidadeTexto = TextSpeed.Instantaneo;
    game.SalvarConfiguracoes();
    var cfgRecarregada = persist.CarregarConfiguracoes();
    Assert(cfgRecarregada.AltoContraste && cfgRecarregada.ModoSemantico && cfgRecarregada.VelocidadeTexto == TextSpeed.Instantaneo,
        "Configuracoes de acessibilidade persistidas em settings.json");

    // 5. COMANDOS NATURAIS, CORRECAO ORTOGRAFICA E ENTRADAS INESPERADAS/EXTREMAS
    ui.ExecutarComando("falar com Pedro");
    Assert(game.FeedbackAtual.Resposta.Contains("Pedro Almeida"), "Comando 'falar com Pedro' responde com identidade narrativa");

    ui.ExecutarComando("ABRIR COSINHA");
    Assert(ui.AbaAtual == ScreenTab.Cozinha && game.FeedbackAtual.CorrecaoOrtografica.Contains("cosinha"),
        "Correcao ortografica case-insensitive: ABRIR COSINHA");

    ui.ExecutarComando("usar caixa");
    Assert(ui.AbaAtual == ScreenTab.Banco, "Comando 'usar caixa' abre Banco/Caixa financeiro");

    ui.ExecutarComando("alto contraste");
    Assert(!game.Configuracoes.AltoContraste, "Comando 'alto contraste' alterna acessibilidade");

    ui.ExecutarComando("som off");
    Assert(!game.Configuracoes.SonsAtivos, "Comando 'som off' desativa audio");

    ui.ExecutarComando("conquistas");
    Assert(ui.AbaAtual == ScreenTab.Historico && ui.FiltroHistorico == "CONQUISTAS", "Comando 'conquistas' abre painel de conquistas");

    // Entradas inesperadas / extremas nao travam nem geram excecao
    ui.ExecutarComando(null);
    ui.ExecutarComando("");
    ui.ExecutarComando("   ");
    ui.ExecutarComando(new string('A', 500));
    Assert(game.FeedbackAtual.Severidade == FeedbackSeverity.Aviso, "Entrada gigante (>100 chars) tratada com seguranca sem travar");
    ui.ExecutarComando("!@#$%^&*()\0\n\t");
    Assert(game.FeedbackAtual.Severidade == FeedbackSeverity.Erro && !string.IsNullOrWhiteSpace(game.FeedbackAtual.Sugestao),
        "Caracteres inesperados rejeitados com explicacao e sugestao");

    // 6. RENDERIZACAO ACESSIVEL E REDIMENSIONAMENTO (100x28, 118x30, 150x38)
    foreach (bool hc in new[] { false, true })
    foreach (bool sem in new[] { false, true })
    {
        game.Configuracoes.AltoContraste = hc;
        game.Configuracoes.ModoSemantico = sem;

        foreach (var (w, h) in new[] { (100, 28), (118, 30), (150, 38) })
        {
            ui.SetVirtualDimensions(w, h);
            foreach (ScreenTab aba in Enum.GetValues<ScreenTab>())
            {
                ui.ExecutarComando(((int)aba).ToString());
                ui.RenderToBufferOnly();
            }
            // Testar modais principais
            ui.ExecutarComando("opcoes"); ui.RenderToBufferOnly();
            ui.ExecutarComando("partidas"); ui.RenderToBufferOnly();
            ui.ExecutarComando("ajuda"); ui.RenderToBufferOnly();
            game.ExibirTutorial = false;
            ui.ExecutarComando("recomecar"); ui.RenderToBufferOnly();
            ui.ExecutarComando("sair"); ui.RenderToBufferOnly();
            ui.HandleKeyPress(ConsoleKey.Escape); // fecha confirmacao saida
        }
    }
    Assert(true, "Renderizacao sem excecoes em 100x28, 118x30 e 150x38 com Alto Contraste e Modo Semantico");

    // 7. VERIFICAR LIMPEZA DE CLIQUE FORA DOS LIMITES
    ui.HandleMouseClick(-10, -10);
    ui.HandleMouseClick(9999, 9999);
    Assert(true, "Cliques fora dos limites da tela ignorados com seguranca");
}
finally
{
    try { Directory.Delete(tempRoot, recursive: true); } catch { }
}

Console.WriteLine();
Console.WriteLine($"RESULTADO: {total - falhas}/{total} testes passaram.");
Environment.Exit(falhas == 0 ? 0 : 1);
