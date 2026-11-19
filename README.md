# Pizzaria Tycoon (C# Console TUI + Mouse + Comandos Naturais)

Simulador de gerenciamento de restaurante em modo texto (TUI) desenvolvido em **C# (.NET 8)**, com interface unificada, suporte a mouse no terminal (Windows), teclado acessível, comandos em português natural, múltiplos slots de salvamento com proteção contra corrupção, modos de jogo, dificuldade ajustável e conquistas.

---

## Requisitos

- **.NET 8 SDK** (ou superior) para compilar e executar a partir do código-fonte.
- Terminal com suporte a UTF-8 (Windows Terminal, PowerShell, CMD, Bash, Zsh, iTerm2).
  - Para terminais com fontes limitadas ou leitores de tela, ative o **Modo Semântico (ASCII)** nas opções (`[O]`).
- **Opcional:** executáveis independentes para Windows, Linux e macOS gerados automaticamente via GitHub Actions.

---

## Instalação e Execução

```bash
# Clonar o repositório
git clone https://github.com/albertojmichael-glitch/restaurantevillasboas1989.git
cd restaurantevillasboas1989

# Executar o jogo
dotnet run --project PizzariaTycoon.csproj

# Executar a suíte de testes automatizados
dotnet run --project PizzariaTycoon.Tests/PizzariaTycoon.Tests.csproj
```

### Gerar executável local (Release)

```bash
# Windows (x64)
dotnet publish PizzariaTycoon.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./saida_exe/win-x64

# Linux (x64)
dotnet publish PizzariaTycoon.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./saida_exe/linux-x64

# macOS (x64)
dotnet publish PizzariaTycoon.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o ./saida_exe/osx-x64
```

---

## Objetivo, Modos de Jogo e Dificuldade

### Modos de Jogo (selecionáveis em `[R] Novo Jogo`)
1. **Carreira (Clássico):** evolua da **Fase 1/5 (Iniciante)** até a **Fase 5/5 (Restaurante Elite Global)**, acumulando **R$ 50.000 líquidos** e **Reputação ≥ 4,5\***.
2. **Campanha (12 Dias):** desafio com prazo fechado de 12 dias (3 meses) para alcançar **Fase 3/5**, **R$ 8.000 líquidos** e **Reputação ≥ 4,0\***.
3. **Gestão Livre (Sandbox):** começa com **R$ 5.000**, sem risco de falência e sem prazo limite — ideal para testar receitas, upgrades e estratégias.

### Níveis de Dificuldade
- **Fácil:** mais capital inicial (R$ 1.500), +2 Energia/dia, multas reduzidas (-30%), custos fixos -15%, juros de 2,5% a.m. e +1 inspeção grátis nas Docas.
- **Normal:** equilíbrio padrão calibrado entre margem, equipe, folha mensal e eventos.
- **Difícil:** capital inicial reduzido (R$ 850), -1 Energia/dia, custos fixos +20%, multas +25% e juros de 4% a.m.

### Eficiência e Consequências Duráveis
- **Combo de Eficiência:** pedidos perfeitos consecutivos (3x, 5x, 10x) concedem bônus progressivo de lucro (+5%, +10%, +20%).
- **Bônus de Eficiência Diária:** fechar o expediente com lucro positivo e zero erros gera bônus em caixa, reputação e moral.
- **Narrativa Persistente:** suas escolhas alteram permanentemente **Moral da Equipe**, **Selo Sanitário** e **Confiança dos Fornecedores**, refletindo nos diálogos de **Pedro Almeida** (Subchefe), **Ana Beatriz** (Conferente) e **Carlos Eduardo** (Gerente Financeiro), além de 10 conquistas desbloqueáveis.

---

## Controles e Atalhos

### Navegação entre Localidades
| Tecla | Localidade | Descrição |
| :---: | --- | --- |
| `1` | **Cozinha** | Montagem de pedidos no turno das 18h, Guia visual, compra emergencial e reposição rápida |
| `2` | **Docas (Entregas)** | Auditoria de 7 caixas (nota fiscal, fornecedor, cidade, data, lote e temperatura) |
| `3` | **Cardápio & Receitas** | Livro completo de receitas da fase e ativação/desativação de pratos (mínimo 2) |
| `4` | **Despensa & Estoque** | Ocupação da câmara fria, alertas de itens esgotados/contaminados e fornecedores válidos |
| `5` | **Escritório (Gerência)** | Metas da fase, equipe, limpeza, upgrades, salários e preços |
| `6` | **Banco Empresarial** | Limite de crédito, empréstimos e amortização antecipada |
| `7` | **Histórico & Conquistas** | Linha do tempo navegável, últimos comandos, conquistas e crônica da partida |
| `8` | **Terminal Dev** | Console de desenvolvedor protegido por senha (`xit89`) |

### Atalhos Globais e Contextuais
| Tecla | Ação |
| :---: | --- |
| `/` ou `:` | Abrir a **Barra de Comando Natural** (`↑`/`↓` navega nos últimos comandos digitados) |
| `Tab` / `Shift+Tab` / Setas | Mover foco acessível entre botões (exibe rótulo do elemento focado no rodapé) |
| `Espaço` / `Enter` | Ativar o elemento focado (ou entregar prato na Cozinha / concluir auditoria nas Docas) |
| `E` / `X` / `B` / `M` | Na Cozinha: **Entregar**, **Limpar bancada**, **Repor faltantes**, **Alternar modo** |
| `A` | Nas Docas: **Concluir auditoria** |
| `P` / `N` / `C` | Falar com **Pedro Almeida** / **Ana Beatriz** / **Carlos Eduardo** |
| `Z` | **Desfazer** última ação simples reversível (bancada, caixas, cardápio, salários/preços) |
| `H` | Abrir **Ajuda Contextual** (`[1]` Tela Atual, `[2]` Tutorial Curto, `[3]` Comandos) |
| `O` | Abrir **Opções de Acessibilidade & Áudio** |
| `G` | Abrir **Gerenciador de Partidas** (Autosave + Slots 1, 2 e 3) |
| `S` / `L` | Salvar / Carregar rapidamente no slot ativo |
| `R` | Iniciar **Nova Partida** (com confirmação, modo e dificuldade) |
| `F` | **Encerrar dia** (gera relatório diário e grava Autosave) |
| `Esc` | Abrir **Decisão antes de Encerrar** (Salvar no Slot, Autosave, Sair ou Voltar) |

---

## Comandos Naturais

Pressione `/` (ou clique na barra inferior) para digitar comandos em português natural. O interpretador ignora maiúsculas/minúsculas e acentos, corrige erros comuns de ortografia (ex.: `"ABRIR COSINHA"` → `"abrir cozinha"`, `"fala com pedor"` → `"falar com Pedro"`) e sugere alternativas válidas em caso de erro.

Exemplos:
- **Conversar com personagens:** `falar com Pedro`, `falar com Ana`, `falar com Carlos`, `falar com cliente`
- **Navegar:** `abrir cozinha`, `abrir docas`, `abrir receitas`, `abrir estoque`, `abrir gerencia`, `abrir banco`, `abrir historico`
- **Operação:** `usar caixa`, `abrir caixa 1`, `aceitar caixa 2`, `recusar caixa 3`, `concluir entrega`, `adicionar massa`, `comprar queijo`, `comprar faltantes`, `entregar prato`, `limpar bancada`
- **Gestão e Finanças:** `contratar 2 funcionarios`, `comprar limpeza`, `melhorar forno`, `aumentar preco`, `reduzir salario`, `pedir emprestimo 1500`, `pagar divida 500`, `quitar divida`
- **Tribunal:** `fazer acordo`, `ir a julgamento`
- **Persistência, Acessibilidade e Sistema:** `salvar slot 2`, `carregar autosave`, `partidas`, `conquistas`, `opcoes`, `alto contraste`, `modo semantico`, `velocidade texto`, `som off`, `desfazer`, `status`, `ultimos comandos`, `encerrar dia`, `ajuda`, `tutorial`, `recomecar`, `sair`

---

## Persistência, Segurança e Acessibilidade

- **Múltiplos Slots + Autosave:** salva em `saves/autosave.json`, `saves/slot1.json`, `saves/slot2.json` e `saves/slot3.json`.
- **Formato Versionado (`v2`) + Checksum SHA-256:** detecta arquivos truncados ou corrompidos e migra automaticamente saves legados (`savegame.json` v1).
- **Backup e Recuperação Automática:** antes de sobrescrever um slot íntegro, cria `.bak`. Se um arquivo estiver corrompido, ele é isolado em quarentena (`.corrupted.<timestamp>`), registrado em `logs/errors.log` e restaurado automaticamente a partir do `.bak`.
- **Acessibilidade (`settings.json`):** Alto Contraste, Modo Semântico (ASCII puro), controle de Velocidade do Texto, opção de Desativar Efeitos Visuais, Sons Opcionais e redimensionamento adaptativo a partir de `100×28`.

---

## Estrutura do Projeto

```text
restaurantevillasboas1989/
├── PizzariaTycoon.csproj          # Projeto principal (.NET 8)
├── Program.cs                     # Ponto de entrada, loop mouse/teclado e proteção contra erros fatais
├── Catalog.cs                     # Fases, 96 ingredientes, 20 receitas, fornecedores e 10 conquistas
├── GameModels.cs                  # Modelos de domínio, enums, snapshots/deltas, acessibilidade e narrativa
├── GameEngine.cs                  # Regras de negócio determinísticas (suporta seed), economia, undo e eventos
├── CommandProcessor.cs            # Interpretador de linguagem natural com correção ortográfica (Levenshtein)
├── PersistenceService.cs          # Persistência multi-slot versionada, checksum SHA-256, backup e logs
├── ConsoleUI.cs                   # Camada de apresentação TUI acessível, painel lateral fixo e modais
├── PizzariaTycoon.Tests/
│   ├── PizzariaTycoon.Tests.csproj
│   └── Program.cs                 # Suíte de testes automatizados (mecânicas, persistência, corrupção, UI)
└── .github/workflows/
    ├── ci.yml                     # Integração contínua (compila e testa em Windows e Linux)
    └── gerar-exe.yml              # Build automatizada para Windows, Linux e macOS
```
