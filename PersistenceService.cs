using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PizzariaTycoon
{
    public class PersistenceService
    {
        public const int CurrentFormatVersion = 2;
        public static readonly string[] SlotIdsPadrao = { "autosave", "slot1", "slot2", "slot3" };

        private static readonly JsonSerializerOptions JsonPretty = new JsonSerializerOptions { WriteIndented = true };
        private static readonly JsonSerializerOptions JsonCompact = new JsonSerializerOptions { WriteIndented = false };
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public string BaseDirectory { get; }
        public string SavesDirectory => Path.Combine(BaseDirectory, "saves");
        public string LogsDirectory => Path.Combine(BaseDirectory, "logs");
        public string ErrorLogPath => Path.Combine(LogsDirectory, "errors.log");
        public string SettingsPath => Path.Combine(BaseDirectory, "settings.json");
        public string LegacySavePath => Path.Combine(BaseDirectory, "savegame.json");

        public PersistenceService(string baseDirectory = null)
        {
            BaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppContext.BaseDirectory
                : baseDirectory;

            // Em execução local com `dotnet run`, prefere o diretório de trabalho atual se não foi especificado
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                try
                {
                    BaseDirectory = Directory.GetCurrentDirectory();
                }
                catch
                {
                    BaseDirectory = AppContext.BaseDirectory;
                }
            }
        }

        public static string NormalizarSlotId(string slotId)
        {
            string s = (slotId ?? "slot1").Trim().ToLowerInvariant();
            return s switch
            {
                "auto" or "autosave" or "0" => "autosave",
                "1" or "slot1" => "slot1",
                "2" or "slot2" => "slot2",
                "3" or "slot3" => "slot3",
                _ => "slot1"
            };
        }

        public static string ObterTituloSlot(string slotId) => NormalizarSlotId(slotId) switch
        {
            "autosave" => "Autosave (Automático)",
            "slot1" => "Partida 1 (Slot 1)",
            "slot2" => "Partida 2 (Slot 2)",
            "slot3" => "Partida 3 (Slot 3)",
            _ => "Partida"
        };

        public string ObterCaminhoSlot(string slotId)
        {
            string norm = NormalizarSlotId(slotId);
            return Path.Combine(SavesDirectory, $"{norm}.json");
        }

        public static string CalcularChecksum(SaveStateData data)
        {
            string payload = JsonSerializer.Serialize(data, JsonCompact);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash);
        }

        public static bool ValidarIntegridade(SaveStateData data, out string motivo)
        {
            motivo = "";
            if (data == null) { motivo = "Dados de save vazios."; return false; }
            if (data.NivelAtual < 1 || data.NivelAtual > 5) { motivo = $"Nível inválido ({data.NivelAtual})."; return false; }
            if (double.IsNaN(data.Dinheiro) || double.IsInfinity(data.Dinheiro)) { motivo = "Saldo financeiro inválido."; return false; }
            if (double.IsNaN(data.Reputacao) || data.Reputacao < 0.0 || data.Reputacao > 5.5) { motivo = "Reputação fora dos limites."; return false; }
            if (data.Funcionarios < 0 || data.Limpeza < 0 || data.UpgForno < 0 || data.UpgGeladeira < 0 || data.UpgBalcao < 0 || data.UpgLogistica < 0)
            {
                motivo = "Atributos operacionais negativos detectados.";
                return false;
            }
            if (data.DiaSemana < 0 || data.DiaSemana > 10) { motivo = "Dia da semana inválido."; return false; }
            return true;
        }

        public PersistenceOperationResult Salvar(string slotId, SaveStateData data)
        {
            string norm = NormalizarSlotId(slotId);
            string destino = ObterCaminhoSlot(norm);
            string backup = destino + ".bak";
            string temp = destino + ".tmp";

            try
            {
                Directory.CreateDirectory(SavesDirectory);

                if (!ValidarIntegridade(data, out string erroIntegridade))
                {
                    return new PersistenceOperationResult
                    {
                        Sucesso = false,
                        Mensagem = $"Salvamento cancelado por integridade: {erroIntegridade}"
                    };
                }

                data.FormatVersion = CurrentFormatVersion;

                var envelope = new SaveFileEnvelope
                {
                    FormatVersion = CurrentFormatVersion,
                    SlotId = norm,
                    SavedAtUtc = DateTime.UtcNow,
                    Checksum = CalcularChecksum(data),
                    Data = data
                };

                string json = JsonSerializer.Serialize(envelope, JsonPretty);

                // Se já existe save válido no slot, preserva backup .bak antes de sobrescrever
                if (File.Exists(destino))
                {
                    if (TentarLerArquivoValidado(destino, out _, out _, out _))
                    {
                        File.Copy(destino, backup, overwrite: true);
                    }
                }

                // Escrita atômica via arquivo temporário
                File.WriteAllText(temp, json, Encoding.UTF8);
                File.Move(temp, destino, overwrite: true);

                return new PersistenceOperationResult
                {
                    Sucesso = true,
                    Mensagem = $"Partida salva em {ObterTituloSlot(norm)} (formato v{CurrentFormatVersion})."
                };
            }
            catch (Exception ex)
            {
                LogError($"Salvar({norm})", ex);
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                return new PersistenceOperationResult
                {
                    Sucesso = false,
                    Mensagem = $"Erro ao salvar em {ObterTituloSlot(norm)}: {ex.Message}",
                    Sugestao = "Verifique espaço em disco e permissões de escrita na pasta do jogo."
                };
            }
        }

        public PersistenceOperationResult Carregar(string slotId)
        {
            string norm = NormalizarSlotId(slotId);
            string caminho = ObterCaminhoSlot(norm);
            string backup = caminho + ".bak";

            // Suporte transparente ao arquivo legado savegame.json ao carregar slot1 se slot1 ainda não existir
            if (!File.Exists(caminho) && norm == "slot1" && File.Exists(LegacySavePath))
            {
                caminho = LegacySavePath;
                backup = LegacySavePath + ".bak";
            }

            if (!File.Exists(caminho))
            {
                // Se pediu slot inexistente, tenta backup se existir
                if (File.Exists(backup) && TentarLerArquivoValidado(backup, out var dadosBak, out bool migradoBak, out _))
                {
                    try { File.Copy(backup, caminho, overwrite: true); } catch { }
                    return new PersistenceOperationResult
                    {
                        Sucesso = true,
                        RecuperadoDeBackup = true,
                        MigradoDeVersaoAntiga = migradoBak,
                        Data = dadosBak,
                        Mensagem = $"Partida restaurada automaticamente a partir do backup de {ObterTituloSlot(norm)}!"
                    };
                }

                return new PersistenceOperationResult
                {
                    Sucesso = false,
                    Mensagem = $"Nenhuma partida encontrada em {ObterTituloSlot(norm)}.",
                    Sugestao = "Salve primeiro com [S] ou escolha outro slot no Gerenciador de Partidas [G]."
                };
            }

            if (TentarLerArquivoValidado(caminho, out var dados, out bool migrado, out string erroLeitura))
            {
                if (migrado)
                {
                    // Regrava automaticamente no formato v2 no slot correspondente
                    Salvar(norm, dados);
                }

                return new PersistenceOperationResult
                {
                    Sucesso = true,
                    MigradoDeVersaoAntiga = migrado,
                    Data = dados,
                    Mensagem = migrado
                        ? $"Save legado (v1) migrado automaticamente para v{CurrentFormatVersion} e carregado em {ObterTituloSlot(norm)}!"
                        : $"Partida carregada de {ObterTituloSlot(norm)} (Fase {dados.NivelAtual}/5)."
                };
            }

            // Arquivo principal corrompido: registra erro, coloca em quarentena e tenta recuperar do .bak
            LogWarning($"Carregar({norm})", $"Arquivo corrompido detectado em '{caminho}': {erroLeitura}");
            QuarentenarArquivoCorrompido(caminho);

            if (File.Exists(backup) && TentarLerArquivoValidado(backup, out var dadosRecuperados, out bool migradoRec, out _))
            {
                try
                {
                    Directory.CreateDirectory(SavesDirectory);
                    File.Copy(backup, ObterCaminhoSlot(norm), overwrite: true);
                }
                catch (Exception ex)
                {
                    LogError($"RestaurarBackup({norm})", ex);
                }

                return new PersistenceOperationResult
                {
                    Sucesso = true,
                    RecuperadoDeBackup = true,
                    MigradoDeVersaoAntiga = migradoRec,
                    Data = dadosRecuperados,
                    Mensagem = $"Arquivo corrompido em {ObterTituloSlot(norm)} foi restaurado automaticamente via backup (.bak)!",
                    Sugestao = "O arquivo danificado foi isolado e registrado em logs/errors.log."
                };
            }

            return new PersistenceOperationResult
            {
                Sucesso = false,
                Mensagem = $"O arquivo de {ObterTituloSlot(norm)} está corrompido ({erroLeitura}) e não possui backup íntegro.",
                Sugestao = norm != "autosave"
                    ? "Tente carregar o Autosave ou outro slot no Gerenciador de Partidas [G]."
                    : "Carregue um dos Slots manuais (1-3) ou inicie uma nova partida [R]."
            };
        }

        public bool ExcluirSlot(string slotId, out string mensagem)
        {
            string norm = NormalizarSlotId(slotId);
            string caminho = ObterCaminhoSlot(norm);
            string backup = caminho + ".bak";
            try
            {
                bool apagou = false;
                if (File.Exists(caminho)) { File.Delete(caminho); apagou = true; }
                if (File.Exists(backup)) { File.Delete(backup); apagou = true; }
                mensagem = apagou
                    ? $"{ObterTituloSlot(norm)} removido com segurança."
                    : $"{ObterTituloSlot(norm)} já estava vazio.";
                return apagou;
            }
            catch (Exception ex)
            {
                LogError($"ExcluirSlot({norm})", ex);
                mensagem = $"Não foi possível remover {ObterTituloSlot(norm)}: {ex.Message}";
                return false;
            }
        }

        public List<SaveSlotSummary> ListarSlots()
        {
            var lista = new List<SaveSlotSummary>();
            foreach (var id in SlotIdsPadrao)
            {
                string path = ObterCaminhoSlot(id);
                string bak = path + ".bak";
                bool existe = File.Exists(path);
                bool temBak = File.Exists(bak);

                if (!existe && id == "slot1" && File.Exists(LegacySavePath))
                {
                    path = LegacySavePath;
                    existe = true;
                }

                var sum = new SaveSlotSummary
                {
                    SlotId = id,
                    Titulo = ObterTituloSlot(id),
                    Existe = existe,
                    TemBackup = temBak
                };

                if (!existe)
                {
                    sum.Resumo = temBak ? "Backup disponível para restauração" : "Vazio";
                    lista.Add(sum);
                    continue;
                }

                if (TentarLerArquivoValidado(path, out var data, out bool migrado, out string erro))
                {
                    sum.FormatVersion = migrado ? 1 : CurrentFormatVersion;
                    try { sum.SavedAtLocal = File.GetLastWriteTime(path); } catch { }
                    string modo = LocalityCatalog.ObterNomeModo(data.ModoJogo);
                    string dif = LocalityCatalog.ObterNomeDificuldade(data.Dificuldade);
                    sum.Resumo = $"Fase {data.NivelAtual}/5 · {modo} ({dif}) · R$ {data.Dinheiro.ToString("N2", PtBr)} · Dia {data.DiaSemana}/4 Mês {Math.Max(1, data.MesesJogados)}";
                }
                else
                {
                    sum.Corrompido = true;
                    sum.Resumo = temBak
                        ? "CORROMPIDO (recuperável via backup .bak)"
                        : $"CORROMPIDO ({erro})";
                }

                lista.Add(sum);
            }
            return lista;
        }

        public bool TentarLerArquivoValidado(string caminho, out SaveStateData data, out bool migradoV1, out string erro)
        {
            data = null;
            migradoV1 = false;
            erro = "";

            try
            {
                string json = File.ReadAllText(caminho, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    erro = "Arquivo vazio.";
                    return false;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    erro = "Estrutura JSON inválida.";
                    return false;
                }

                // Formato v2+ (Envelope com versão e checksum)
                if (root.TryGetProperty("FormatVersion", out var verProp) && root.TryGetProperty("Data", out _))
                {
                    int ver = verProp.GetInt32();
                    if (ver > CurrentFormatVersion)
                    {
                        erro = $"Versão futura não suportada (v{ver}).";
                        return false;
                    }

                    var env = JsonSerializer.Deserialize<SaveFileEnvelope>(json);
                    if (env?.Data == null)
                    {
                        erro = "Payload de dados ausente no envelope.";
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(env.Checksum))
                    {
                        string esperado = CalcularChecksum(env.Data);
                        if (!string.Equals(esperado, env.Checksum, StringComparison.OrdinalIgnoreCase))
                        {
                            erro = "Checksum inválido (arquivo alterado ou truncado).";
                            return false;
                        }
                    }

                    if (!ValidarIntegridade(env.Data, out erro))
                        return false;

                    NormalizarCamposPadrao(env.Data);
                    data = env.Data;
                    return true;
                }

                // Formato legado v1 (SaveStateData direto, sem envelope)
                if (root.TryGetProperty("NivelAtual", out _) && root.TryGetProperty("Dinheiro", out _))
                {
                    var legado = JsonSerializer.Deserialize<SaveStateData>(json);
                    if (!ValidarIntegridade(legado, out erro))
                        return false;

                    NormalizarCamposPadrao(legado);
                    migradoV1 = true;
                    data = legado;
                    return true;
                }

                erro = "Arquivo não reconhecido como save do Pizzaria Tycoon.";
                return false;
            }
            catch (JsonException jex)
            {
                erro = $"JSON malformado ({jex.Message})";
                return false;
            }
            catch (Exception ex)
            {
                erro = ex.Message;
                return false;
            }
        }

        private static void NormalizarCamposPadrao(SaveStateData d)
        {
            d.FormatVersion = CurrentFormatVersion;
            if (d.MesesJogados <= 0) d.MesesJogados = 1;
            if (d.DiaSemana <= 0) d.DiaSemana = 1;
            if (d.MultiplicadorSalario <= 0) d.MultiplicadorSalario = 1.0;
            if (d.MultiplicadorPrecoPrato <= 0) d.MultiplicadorPrecoPrato = 1.0;
            if (d.Dificuldade == 0) d.Dificuldade = DifficultyMode.Normal;
            if (d.ModoJogo == 0) d.ModoJogo = GameMode.Carreira;
            d.Estoque ??= new Dictionary<string, int>();
            d.EstoqueContaminado ??= new Dictionary<string, int>();
            d.PratosAtivos ??= new List<string>();
            d.ConquistasDesbloqueadas ??= new List<string>();
            d.Narrativa ??= new NarrativeState();
            d.Narrativa.Cronicas ??= new List<string>();
        }

        private void QuarentenarArquivoCorrompido(string caminho)
        {
            try
            {
                if (!File.Exists(caminho)) return;
                string carimbo = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string quarentena = $"{caminho}.corrupted.{carimbo}";
                File.Move(caminho, quarentena, overwrite: true);
            }
            catch (Exception ex)
            {
                LogError("QuarentenarArquivoCorrompido", ex);
            }
        }

        // ================== CONFIGURAÇÕES DE ACESSIBILIDADE ==================
        public AccessibilitySettings CarregarConfiguracoes()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new AccessibilitySettings();

                string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<AccessibilitySettings>(json);
                return cfg ?? new AccessibilitySettings();
            }
            catch (Exception ex)
            {
                LogError("CarregarConfiguracoes", ex);
                return new AccessibilitySettings();
            }
        }

        public void SalvarConfiguracoes(AccessibilitySettings settings)
        {
            if (settings == null) return;
            try
            {
                Directory.CreateDirectory(BaseDirectory);
                string json = JsonSerializer.Serialize(settings, JsonPretty);
                string temp = SettingsPath + ".tmp";
                File.WriteAllText(temp, json, Encoding.UTF8);
                File.Move(temp, SettingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                LogError("SalvarConfiguracoes", ex);
            }
        }

        // ================== LOG SEGURO DE ERROS ==================
        public void LogError(string contexto, Exception ex)
        {
            LogLine("ERROR", contexto, ex != null ? $"{ex.GetType().Name}: {ex.Message}" : "Exceção desconhecida");
        }

        public void LogWarning(string contexto, string mensagem)
        {
            LogLine("WARN", contexto, mensagem);
        }

        private void LogLine(string nivel, string contexto, string detalhe)
        {
            try
            {
                Directory.CreateDirectory(LogsDirectory);
                string linha = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{nivel}] [{contexto}] {detalhe}{Environment.NewLine}";
                File.AppendAllText(ErrorLogPath, linha, Encoding.UTF8);
            }
            catch
            {
                // Nunca derruba a aplicação ao falhar escrita de log
            }
        }
    }
}
