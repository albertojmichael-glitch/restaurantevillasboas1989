using System;
using System.Collections.Generic;
using System.Linq;

namespace PizzariaTycoon
{
    public enum IngredientCategory
    {
        Base,
        Proteina,
        Laticinio,
        Vegetal,
        MolhoTempero,
        DoceFruta
    }

    public class IngredientInfo
    {
        public string Nome { get; set; }
        public int Nivel { get; set; }
        public IngredientCategory Categoria { get; set; }
        public bool ExigeRefrigeracao { get; set; }
    }

    public class LevelConfig
    {
        public int Nivel { get; set; }
        public string Titulo { get; set; }
        public double BonusNivel { get; set; }
        public double CustoFixoMensal { get; set; }
        public int MinFuncionarios { get; set; }
        public int RecFuncionarios { get; set; } // Meta recomendada para o nível
        public double CustoPorFuncionario { get; set; }
        public int MinLimpeza { get; set; }
        public int RecLimpeza { get; set; }      // Limpeza recomendada para o nível
        public double CustoPorLimpeza { get; set; }
        public int MinUpgrades { get; set; }
        public int RecUpgrades { get; set; }     // Upgrades recomendados para o nível
        public double CustoUpgradeBase { get; set; }
        public double PrecoMedioPrato { get; set; }
    }

    public class DishRecipe
    {
        public string Nome { get; set; }
        public int NivelDesbloqueio { get; set; }
        public string IngredienteBaseTopo { get; set; }
        public string IngredienteBaseFundo { get; set; }
        public List<string> IngredientesPadrao { get; set; }
        public bool EPratoDeEvento { get; set; } = false;
        public string EstacaoFavorita { get; set; } = "TODAS"; // INVERNO, VERAO, EVENTO, TODAS
    }

    public static class Catalog
    {
        public static ConsoleColor ObterCorCategoria(IngredientCategory cat)
        {
            return cat switch
            {
                IngredientCategory.Base => ConsoleColor.DarkYellow,
                IngredientCategory.Proteina => ConsoleColor.DarkRed,
                IngredientCategory.Laticinio => ConsoleColor.DarkCyan,
                IngredientCategory.Vegetal => ConsoleColor.DarkGreen,
                IngredientCategory.MolhoTempero => ConsoleColor.DarkMagenta,
                IngredientCategory.DoceFruta => ConsoleColor.Blue,
                _ => ConsoleColor.DarkGray
            };
        }

        public static List<string> ObterItensEntregaPorNivel(int nivel)
        {
            return TodosIngredientes
                .Where(i => i.Nivel <= nivel)
                .Select(i => i.Nome)
                .ToList();
        }

        // Progressão calibrada: Início acessível -> Meio tático -> Final exigente
        public static readonly List<LevelConfig> Levels = new List<LevelConfig>
        {
            new LevelConfig {
                Nivel = 1, Titulo = "Iniciante", BonusNivel = 0, CustoFixoMensal = 150,
                MinFuncionarios = 0, RecFuncionarios = 6, CustoPorFuncionario = 80,
                MinLimpeza = 0, RecLimpeza = 8, CustoPorLimpeza = 25,
                MinUpgrades = 0, RecUpgrades = 6, CustoUpgradeBase = 95, PrecoMedioPrato = 95
            },
            new LevelConfig {
                Nivel = 2, Titulo = "Mini Comércio", BonusNivel = 2000, CustoFixoMensal = 320,
                MinFuncionarios = 8, RecFuncionarios = 16, CustoPorFuncionario = 180,
                MinLimpeza = 12, RecLimpeza = 24, CustoPorLimpeza = 55,
                MinUpgrades = 10, RecUpgrades = 20, CustoUpgradeBase = 190, PrecoMedioPrato = 240
            },
            new LevelConfig {
                Nivel = 3, Titulo = "Restaurante", BonusNivel = 3200, CustoFixoMensal = 650,
                MinFuncionarios = 20, RecFuncionarios = 32, CustoPorFuncionario = 310,
                MinLimpeza = 30, RecLimpeza = 50, CustoPorLimpeza = 95,
                MinUpgrades = 26, RecUpgrades = 42, CustoUpgradeBase = 380, PrecoMedioPrato = 580
            },
            new LevelConfig {
                Nivel = 4, Titulo = "Restaurante Nacional", BonusNivel = 5000, CustoFixoMensal = 1300,
                MinFuncionarios = 38, RecFuncionarios = 60, CustoPorFuncionario = 460,
                MinLimpeza = 60, RecLimpeza = 90, CustoPorLimpeza = 190,
                MinUpgrades = 50, RecUpgrades = 75, CustoUpgradeBase = 750, PrecoMedioPrato = 1180
            },
            new LevelConfig {
                Nivel = 5, Titulo = "Restaurante Elite Global", BonusNivel = 10000, CustoFixoMensal = 2600,
                MinFuncionarios = 80, RecFuncionarios = 110, CustoPorFuncionario = 780,
                MinLimpeza = 120, RecLimpeza = 160, CustoPorLimpeza = 260,
                MinUpgrades = 85, RecUpgrades = 115, CustoUpgradeBase = 1450, PrecoMedioPrato = 2450
            }
        };

        public static readonly List<IngredientInfo> TodosIngredientes = new List<IngredientInfo>
        {
            // NÍVEL 1 (22 ingredientes)
            new IngredientInfo { Nome = "Pasta", Nivel = 1, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Massa", Nivel = 1, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Pão Superior", Nivel = 1, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Pão Inferior", Nivel = 1, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Alface", Nivel = 1, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Queijo", Nivel = 1, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Molho de Tomate", Nivel = 1, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Molho Branco", Nivel = 1, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Tomate", Nivel = 1, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Manjericão", Nivel = 1, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Sal", Nivel = 1, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Carne", Nivel = 1, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Carne Moída", Nivel = 1, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Salame", Nivel = 1, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Pepperoni", Nivel = 1, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Lombo", Nivel = 1, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Cogumelo", Nivel = 1, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Cebola", Nivel = 1, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Frango", Nivel = 1, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Farofa", Nivel = 1, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Azeite", Nivel = 1, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Molho", Nivel = 1, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = true },

            // NÍVEL 2 (+16 ingredientes)
            new IngredientInfo { Nome = "Pão de Hot Dog", Nivel = 2, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Pão de Forma", Nivel = 2, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Salsicha", Nivel = 2, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Purê de Batata", Nivel = 2, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Milho", Nivel = 2, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Ervilha", Nivel = 2, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Batata Palha", Nivel = 2, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Ketchup", Nivel = 2, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Mostarda", Nivel = 2, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Ovos", Nivel = 2, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Presunto", Nivel = 2, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Bacon", Nivel = 2, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Espinafre", Nivel = 2, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Cenoura", Nivel = 2, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Maionese", Nivel = 2, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Requeijão", Nivel = 2, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },

            // NÍVEL 3 (+11 ingredientes)
            new IngredientInfo { Nome = "Massa de Panqueca", Nivel = 3, Categoria = IngredientCategory.Base, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Massa de Lasanha", Nivel = 3, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Massa de Pastel", Nivel = 3, Categoria = IngredientCategory.Base, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Ricota", Nivel = 3, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Queijo Mussarela", Nivel = 3, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Queijo Parmesão", Nivel = 3, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Berinjela", Nivel = 3, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Palmito", Nivel = 3, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Azeitona", Nivel = 3, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Molho à Bolonhesa", Nivel = 3, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Requeijão Cremoso", Nivel = 3, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },

            // NÍVEL 4 (+18 ingredientes)
            new IngredientInfo { Nome = "Tortilha", Nivel = 4, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Queijo Cheddar", Nivel = 4, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Feijão", Nivel = 4, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Feijão-Preto", Nivel = 4, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Guacamole", Nivel = 4, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Abacate", Nivel = 4, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Creme Azedo", Nivel = 4, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Molho Picante", Nivel = 4, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Camarão", Nivel = 4, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Brócolis", Nivel = 4, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Repolho", Nivel = 4, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Pimentão", Nivel = 4, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Molho Shoyu", Nivel = 4, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Macarrão para Yakisoba", Nivel = 4, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Arroz Arbóreo", Nivel = 4, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Alho-Poró", Nivel = 4, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Manteiga", Nivel = 4, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Caldo de Legumes", Nivel = 4, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },

            // NÍVEL 5 (+29 ingredientes = 96 total)
            new IngredientInfo { Nome = "Arroz Japonês", Nivel = 5, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Tortilha Grande", Nivel = 5, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Arroz Branco", Nivel = 5, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Alga Nori", Nivel = 5, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Salmão", Nivel = 5, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Atum", Nivel = 5, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Pepino", Nivel = 5, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Cream Cheese", Nivel = 5, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Gergelim", Nivel = 5, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Cebolinha", Nivel = 5, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Açaí", Nivel = 5, Categoria = IngredientCategory.Base, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Banana", Nivel = 5, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Morango", Nivel = 5, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Granola", Nivel = 5, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Leite em Pó", Nivel = 5, Categoria = IngredientCategory.Laticinio, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Leite Condensado", Nivel = 5, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Paçoca", Nivel = 5, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Kiwi", Nivel = 5, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Gotas de Chocolate", Nivel = 5, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Mel", Nivel = 5, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Caracol (Escargot)", Nivel = 5, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Baiacu", Nivel = 5, Categoria = IngredientCategory.Proteina, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Alho", Nivel = 5, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Salsinha", Nivel = 5, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Vinho Branco", Nivel = 5, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Limão", Nivel = 5, Categoria = IngredientCategory.DoceFruta, ExigeRefrigeracao = true },
            new IngredientInfo { Nome = "Pimenta-do-Reino", Nivel = 5, Categoria = IngredientCategory.MolhoTempero, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Pão Francês", Nivel = 5, Categoria = IngredientCategory.Base, ExigeRefrigeracao = false },
            new IngredientInfo { Nome = "Gengibre", Nivel = 5, Categoria = IngredientCategory.Vegetal, ExigeRefrigeracao = false }
        };

        // 100% dos 96 ingredientes são utilizados nas receitas abaixo (incluindo pratos sazonais/eventos)
        public static readonly List<DishRecipe> Pratos = new List<DishRecipe>
        {
            // NÍVEL 1 (Usa todos os 22 ingredientes do Nível 1)
            new DishRecipe {
                Nome = "Macarrão", NivelDesbloqueio = 1, IngredienteBaseTopo = "Pasta", EstacaoFavorita = "INVERNO",
                IngredientesPadrao = new List<string> { "Molho de Tomate", "Molho Branco", "Carne Moída", "Queijo", "Tomate", "Manjericão", "Sal" }
            },
            new DishRecipe {
                Nome = "Pizza", NivelDesbloqueio = 1, IngredienteBaseTopo = "Massa", EstacaoFavorita = "TODAS",
                IngredientesPadrao = new List<string> { "Molho de Tomate", "Queijo", "Pepperoni", "Salame", "Lombo", "Tomate", "Cogumelo", "Cebola" }
            },
            new DishRecipe {
                Nome = "Hambúrguer", NivelDesbloqueio = 1, IngredienteBaseTopo = "Pão Superior", IngredienteBaseFundo = "Pão Inferior", EstacaoFavorita = "TODAS",
                IngredientesPadrao = new List<string> { "Carne", "Queijo", "Alface", "Tomate", "Cebola", "Molho" }
            },
            new DishRecipe {
                Nome = "Salada", NivelDesbloqueio = 1, IngredienteBaseTopo = "Alface", EstacaoFavorita = "VERAO",
                IngredientesPadrao = new List<string> { "Tomate", "Cebola", "Frango", "Farofa", "Azeite", "Sal" }
            },
            new DishRecipe {
                Nome = "Pizza Rústica (Chef)", NivelDesbloqueio = 1, IngredienteBaseTopo = "Massa", EPratoDeEvento = true, EstacaoFavorita = "EVENTO",
                IngredientesPadrao = new List<string> { "Molho Branco", "Queijo", "Salame", "Lombo", "Cogumelo", "Manjericão", "Azeite" }
            },

            // NÍVEL 2 (Usa todos os 16 ingredientes do Nível 2)
            new DishRecipe {
                Nome = "Cachorro-quente", NivelDesbloqueio = 2, IngredienteBaseTopo = "Pão de Hot Dog",
                IngredientesPadrao = new List<string> { "Salsicha", "Purê de Batata", "Milho", "Ervilha", "Ketchup", "Mostarda", "Batata Palha" }
            },
            new DishRecipe {
                Nome = "Omelete", NivelDesbloqueio = 2, IngredienteBaseTopo = "Ovos",
                IngredientesPadrao = new List<string> { "Presunto", "Queijo", "Bacon", "Espinafre", "Tomate", "Cebola", "Sal" }
            },
            new DishRecipe {
                Nome = "Sanduíche Natural", NivelDesbloqueio = 2, IngredienteBaseTopo = "Pão de Forma", EstacaoFavorita = "VERAO",
                IngredientesPadrao = new List<string> { "Maionese", "Requeijão", "Frango", "Cenoura", "Alface", "Tomate", "Milho" }
            },

            // NÍVEL 3 (Usa todos os 11 ingredientes do Nível 3)
            new DishRecipe {
                Nome = "Panqueca", NivelDesbloqueio = 3, IngredienteBaseTopo = "Massa de Panqueca",
                IngredientesPadrao = new List<string> { "Carne Moída", "Molho à Bolonhesa", "Queijo Mussarela", "Requeijão Cremoso", "Ricota" }
            },
            new DishRecipe {
                Nome = "Lasanha", NivelDesbloqueio = 3, IngredienteBaseTopo = "Massa de Lasanha", EstacaoFavorita = "INVERNO",
                IngredientesPadrao = new List<string> { "Molho à Bolonhesa", "Molho Branco", "Presunto", "Queijo Mussarela", "Queijo Parmesão", "Berinjela" }
            },
            new DishRecipe {
                Nome = "Pastel", NivelDesbloqueio = 3, IngredienteBaseTopo = "Massa de Pastel",
                IngredientesPadrao = new List<string> { "Carne Moída", "Queijo Mussarela", "Palmito", "Azeitona", "Requeijão Cremoso" }
            },

            // NÍVEL 4 (Usa todos os 18 ingredientes do Nível 4, incluindo Abacate, Pimentão e Feijão-Preto)
            new DishRecipe {
                Nome = "Tacos", NivelDesbloqueio = 4, IngredienteBaseTopo = "Tortilha",
                IngredientesPadrao = new List<string> { "Carne Moída", "Feijão", "Feijão-Preto", "Queijo Cheddar", "Guacamole", "Abacate", "Creme Azedo", "Molho Picante", "Alface" }
            },
            new DishRecipe {
                Nome = "Yakisoba", NivelDesbloqueio = 4, IngredienteBaseTopo = "Macarrão para Yakisoba", EstacaoFavorita = "INVERNO",
                IngredientesPadrao = new List<string> { "Carne", "Frango", "Camarão", "Brócolis", "Repolho", "Cenoura", "Pimentão", "Molho Shoyu" }
            },
            new DishRecipe {
                Nome = "Risoto", NivelDesbloqueio = 4, IngredienteBaseTopo = "Arroz Arbóreo", EstacaoFavorita = "INVERNO",
                IngredientesPadrao = new List<string> { "Caldo de Legumes", "Manteiga", "Camarão", "Cogumelo", "Alho-Poró", "Queijo Parmesão" }
            },

            // NÍVEL 5 (Usa todos os 29 ingredientes do Nível 5, incluindo Gotas de Chocolate e Mel)
            new DishRecipe {
                Nome = "Sushi", NivelDesbloqueio = 5, IngredienteBaseTopo = "Arroz Japonês", EstacaoFavorita = "VERAO",
                IngredientesPadrao = new List<string> { "Alga Nori", "Salmão", "Atum", "Cream Cheese", "Pepino", "Gergelim", "Cebolinha" }
            },
            new DishRecipe {
                Nome = "Burrito", NivelDesbloqueio = 5, IngredienteBaseTopo = "Tortilha Grande",
                IngredientesPadrao = new List<string> { "Arroz Branco", "Carne", "Feijão-Preto", "Queijo Cheddar", "Guacamole", "Creme Azedo", "Molho Picante" }
            },
            new DishRecipe {
                Nome = "Açaí na Tigela", NivelDesbloqueio = 5, IngredienteBaseTopo = "Açaí", EstacaoFavorita = "VERAO",
                IngredientesPadrao = new List<string> { "Banana", "Morango", "Kiwi", "Granola", "Leite em Pó", "Leite Condensado", "Paçoca", "Gotas de Chocolate", "Mel" }
            },
            new DishRecipe {
                Nome = "Escargot", NivelDesbloqueio = 5, IngredienteBaseTopo = "Caracol (Escargot)", EstacaoFavorita = "EVENTO",
                IngredientesPadrao = new List<string> { "Manteiga", "Alho", "Salsinha", "Vinho Branco", "Sal", "Pimenta-do-Reino", "Pão Francês" }
            },
            new DishRecipe {
                Nome = "Baiacu", NivelDesbloqueio = 5, IngredienteBaseTopo = "Baiacu", EstacaoFavorita = "EVENTO",
                IngredientesPadrao = new List<string> { "Limão", "Gengibre", "Cebolinha", "Molho Shoyu", "Alho", "Salsinha", "Pimenta-do-Reino" }
            },
            new DishRecipe {
                Nome = "Feijoada Imperial (Chef)", NivelDesbloqueio = 4, IngredienteBaseTopo = "Feijão-Preto", EPratoDeEvento = true, EstacaoFavorita = "EVENTO",
                IngredientesPadrao = new List<string> { "Carne", "Lombo", "Bacon", "Salsicha", "Farofa", "Cebola", "Sal" }
            }
        };

        public static readonly List<string> ItensEntregaIntrusos = new List<string>
        {
            "Canetas", "Caixa de Parafusos", "Grampeador", "Pneu de Bicicleta",
            "Sabão em Pó", "Lâmpada Fluorescente", "Caderno Escolar", "Chave de Fenda"
        };

        public static readonly Dictionary<string, string> ErrosDigitacao = new Dictionary<string, string>
        {
            { "Queijo", "Quejo" },
            { "Queijo Cheddar", "Queijo Xedar" },
            { "Pepperoni", "Peperone" },
            { "Molho de Tomate", "Molho de Tomati" },
            { "Maionese", "Maioneze" },
            { "Manjericão", "Manjericão-bravo" },
            { "Cogumelo", "Cugumelo" },
            { "Salsicha", "Salxicha" },
            { "Camarão", "Camaraum" }
        };

        public static readonly List<string> CidadesValidas = new List<string> { "São Paulo", "Curitiba", "Rio de Janeiro" };
        public static readonly List<string> CidadesInvalidas = new List<string> { "Belo Horizonte", "Porto Alegre", "Campinas", "Salvador", "Recife", "Brasília" };
        public static readonly List<string> FornecedoresValidos = new List<string> { "AgroFoods Brasil", "Distribuidora Sul", "Laticínios Real", "Horta Fresca SA" };
        public static readonly List<string> FornecedoresInvalidos = new List<string> { "Importadora Pirata", "Comércio Fantasma", "Frios Duvidosos Ltda", "Entregas Relâmpago X" };
    }
}