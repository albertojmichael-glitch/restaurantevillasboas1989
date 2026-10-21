using System;
using System.Collections.Generic;

namespace PizzariaTycoon
{
    public class LevelConfig
    {
        public int Nivel { get; set; }
        public string Titulo { get; set; }
        public double BonusNivel { get; set; }
        public double CustoFixoMensal { get; set; }
        public int MinFuncionarios { get; set; }
        public double CustoPorFuncionario { get; set; }
        public int MinLimpeza { get; set; }
        public double CustoPorLimpeza { get; set; }
        public int MinUpgrades { get; set; }
        public double CustoUpgrade { get; set; }
        public double PrecoMedioPrato { get; set; }
    }

    public class DishRecipe
    {
        public string Nome { get; set; }
        public int NivelDesbloqueio { get; set; }
        public string IngredienteBaseTopo { get; set; }
        public string IngredienteBaseFundo { get; set; } // Usado no Hambúrguer (Pão Inferior)
        public List<string> IngredientesVariaveis { get; set; }
    }

    public static class Catalog
    {
        public static readonly List<LevelConfig> Levels = new List<LevelConfig>
        {
            new LevelConfig {
                Nivel = 1, Titulo = "Iniciante", BonusNivel = 0, CustoFixoMensal = 200,
                MinFuncionarios = 0, CustoPorFuncionario = 100, MinLimpeza = 0, CustoPorLimpeza = 30,
                MinUpgrades = 0, CustoUpgrade = 120, PrecoMedioPrato = 80
            },
            new LevelConfig {
                Nivel = 2, Titulo = "Mini Comercio", BonusNivel = 1800, CustoFixoMensal = 250,
                MinFuncionarios = 10, CustoPorFuncionario = 250, MinLimpeza = 15, CustoPorLimpeza = 60,
                MinUpgrades = 15, CustoUpgrade = 250, PrecoMedioPrato = 220
            },
            new LevelConfig {
                Nivel = 3, Titulo = "Restaurante", BonusNivel = 2500, CustoFixoMensal = 400,
                MinFuncionarios = 25, CustoPorFuncionario = 350, MinLimpeza = 40, CustoPorLimpeza = 100,
                MinUpgrades = 40, CustoUpgrade = 500, PrecoMedioPrato = 550
            },
            new LevelConfig {
                Nivel = 4, Titulo = "Restaurante Nacional", BonusNivel = 3500, CustoFixoMensal = 800,
                MinFuncionarios = 40, CustoPorFuncionario = 450, MinLimpeza = 80, CustoPorLimpeza = 250,
                MinUpgrades = 60, CustoUpgrade = 950, PrecoMedioPrato = 1100
            },
            new LevelConfig {
                Nivel = 5, Titulo = "Restaurante Elite Global", BonusNivel = 8500, CustoFixoMensal = 1220,
                MinFuncionarios = 100, CustoPorFuncionario = 800, MinLimpeza = 150, CustoPorLimpeza = 250,
                MinUpgrades = 100, CustoUpgrade = 1800, PrecoMedioPrato = 2400
            }
        };

        public static readonly Dictionary<int, List<string>> IngredientesPorNivel = new Dictionary<int, List<string>>
        {
            { 1, new List<string> {
                "Pasta", "Massa", "Pao Superior", "Pao Inferior", "Alface", "Queijo",
                "Molho de Tomate", "Molho Branco", "Tomate", "Manjericao", "Sal", "Carne",
                "Carne Moida", "Salame", "Pepperoni", "Lombo", "Cogumelo", "Cebola",
                "Frango", "Farofa", "Azeite", "Molho"
            }},
            { 2, new List<string> {
                "Pao de Hot Dog", "Pao de Forma", "Salsicha", "Pure de Batata", "Milho",
                "Ervilha", "Batata Palha", "Ketchup", "Mostarda", "Ovos", "Presunto",
                "Bacon", "Espinafre", "Cenoura", "Maionese", "Requeijao"
            }},
            { 3, new List<string> {
                "Massa de Panqueca", "Massa de Lasanha", "Massa de Pastel", "Ricota",
                "Queijo Mussarela", "Queijo Parmesao", "Berinjela", "Palmito",
                "Azeitona", "Molho a Bolonhesa", "Requeijao Cremoso"
            }},
            { 4, new List<string> {
                "Tortilha", "Queijo Cheddar", "Feijao", "Feijao-Preto", "Guacamole",
                "Abacate", "Creme Azedo", "Molho Picante", "Camarao", "Brocolis",
                "Repolho", "Pimentao", "Molho Shoyu", "Macarrao para Yakisoba",
                "Arroz Arboreo", "Alho-Poro", "Manteiga", "Caldo de Legumes"
            }},
            { 5, new List<string> {
                "Arroz Japones", "Tortilha Grande", "Arroz Branco", "Alga Nori", "Salmao",
                "Atum", "Pepino", "Cream Cheese", "Gergelim", "Cebolinha", "Acai",
                "Banana", "Morango", "Granola", "Leite em Po", "Leite Condensado",
                "Pacoca", "Kiwi", "Gotas de Chocolate", "Mel", "Caracol (Escargot)",
                "Baiacu", "Alho", "Salsinha", "Vinho Branco", "Limao",
                "Pimenta-do-Reino", "Pao Frances", "Gengibre"
            }}
        };

        public static readonly List<DishRecipe>Pratos = new List<DishRecipe>
        {
            // NÍVEL 1
            new DishRecipe {
                Nome = "Macarrao", NivelDesbloqueio = 1, IngredienteBaseTopo = "Pasta",
                IngredientesVariaveis = new List<string> { "Queijo", "Molho de Tomate", "Molho Branco", "Tomate", "Manjericao", "Sal", "Carne" }
            },
            new DishRecipe {
                Nome = "Pizza", NivelDesbloqueio = 1, IngredienteBaseTopo = "Massa",
                IngredientesVariaveis = new List<string> { "Molho de Tomate", "Queijo", "Pepperoni", "Salame", "Lombo", "Tomate", "Cogumelo", "Cebola" }
            },
            new DishRecipe {
                Nome = "Hamburguer", NivelDesbloqueio = 1, IngredienteBaseTopo = "Pao Superior", IngredienteBaseFundo = "Pao Inferior",
                IngredientesVariaveis = new List<string> { "Carne", "Cebola", "Alface", "Queijo", "Tomate", "Molho" }
            },
            new DishRecipe {
                Nome = "Salada", NivelDesbloqueio = 1, IngredienteBaseTopo = "Alface",
                IngredientesVariaveis = new List<string> { "Cebola", "Frango", "Tomate", "Farofa", "Sal", "Azeite", "Molho" }
            },

            // NÍVEL 2
            new DishRecipe {
                Nome = "Cachorro-quente", NivelDesbloqueio = 2, IngredienteBaseTopo = "Pao de Hot Dog",
                IngredientesVariaveis = new List<string> { "Salsicha", "Pure de Batata", "Milho", "Ervilha", "Batata Palha", "Ketchup", "Mostarda" }
            },
            new DishRecipe {
                Nome = "Omelete", NivelDesbloqueio = 2, IngredienteBaseTopo = "Ovos",
                IngredientesVariaveis = new List<string> { "Presunto", "Queijo", "Bacon", "Espinafre", "Tomate", "Cebola", "Sal" }
            },
            new DishRecipe {
                Nome = "Sanduiche Natural", NivelDesbloqueio = 2, IngredienteBaseTopo = "Pao de Forma",
                IngredientesVariaveis = new List<string> { "Frango", "Cenoura", "Alface", "Tomate", "Maionese", "Requeijao", "Milho" }
            },

            // NÍVEL 3
            new DishRecipe {
                Nome = "Panqueca", NivelDesbloqueio = 3, IngredienteBaseTopo = "Massa de Panqueca",
                IngredientesVariaveis = new List<string> { "Carne Moida", "Frango", "Molho a Bolonhesa", "Queijo Mussarela", "Requeijao Cremoso", "Ricota" }
            },
            new DishRecipe {
                Nome = "Lasanha", NivelDesbloqueio = 3, IngredienteBaseTopo = "Massa de Lasanha",
                IngredientesVariaveis = new List<string> { "Molho a Bolonhesa", "Molho Branco", "Presunto", "Queijo Mussarela", "Queijo Parmesao", "Berinjela" }
            },
            new DishRecipe {
                Nome = "Pastel", NivelDesbloqueio = 3, IngredienteBaseTopo = "Massa de Pastel",
                IngredientesVariaveis = new List<string> { "Carne Moida", "Queijo Mussarela", "Palmito", "Azeitona", "Frango", "Requeijao Cremoso" }
            },

            // NÍVEL 4
            new DishRecipe {
                Nome = "Tacos", NivelDesbloqueio = 4, IngredienteBaseTopo = "Tortilha",
                IngredientesVariaveis = new List<string> { "Carne Moida", "Queijo Cheddar", "Feijao", "Guacamole", "Creme Azedo", "Molho Picante", "Alface" }
            },
            new DishRecipe {
                Nome = "Yakisoba", NivelDesbloqueio = 4, IngredienteBaseTopo = "Macarrao para Yakisoba",
                IngredientesVariaveis = new List<string> { "Carne", "Frango", "Camarao", "Brocolis", "Repolho", "Cenoura", "Pimentao", "Molho Shoyu" }
            },
            new DishRecipe {
                Nome = "Risoto", NivelDesbloqueio = 4, IngredienteBaseTopo = "Arroz Arboreo",
                IngredientesVariaveis = new List<string> { "Camarao", "Cogumelo", "Alho-Poro", "Manteiga", "Caldo de Legumes", "Queijo Parmesao" }
            },

            // NÍVEL 5
            new DishRecipe {
                Nome = "Sushi", NivelDesbloqueio = 5, IngredienteBaseTopo = "Arroz Japones",
                IngredientesVariaveis = new List<string> { "Alga Nori", "Salmao", "Atum", "Pepino", "Cream Cheese", "Gergelim", "Cebolinha" }
            },
            new DishRecipe {
                Nome = "Burrito", NivelDesbloqueio = 5, IngredienteBaseTopo = "Tortilha Grande",
                IngredientesVariaveis = new List<string> { "Arroz Branco", "Carne", "Feijao-Preto", "Queijo Cheddar", "Guacamole", "Creme Azedo", "Molho Picante" }
            },
            new DishRecipe {
                Nome = "Acai na Tigela", NivelDesbloqueio = 5, IngredienteBaseTopo = "Acai",
                IngredientesVariaveis = new List<string> { "Banana", "Morango", "Kiwi", "Granola", "Leite em Po", "Leite Condensado", "Pacoca", "Gotas de Chocolate", "Mel" }
            },
            new DishRecipe {
                Nome = "Escargot", NivelDesbloqueio = 5, IngredienteBaseTopo = "Caracol (Escargot)",
                IngredientesVariaveis = new List<string> { "Manteiga", "Alho", "Salsinha", "Vinho Branco", "Sal", "Pimenta-do-Reino", "Pao Frances" }
            },
            new DishRecipe {
                Nome = "Baiacu", NivelDesbloqueio = 5, IngredienteBaseTopo = "Baiacu",
                IngredientesVariaveis = new List<string> { "Limao", "Gengibre", "Cebolinha", "Molho Shoyu", "Alho", "Salsinha", "Pimenta-do-Reino" }
            }
        };

        // Lista oficial de 32 itens para conferência de entregas
        public static readonly List<string> ItensEntregaValidos = new List<string>
        {
            "Acucar de confeiteiro", "Queijo suico", "Queijo cheddar", "Queijo em cubos",
            "Alface roxo", "Alface verde", "Cebola roxa", "Cebola", "Carne", "Pepperoni",
            "Lombo", "Salame", "Frango", "Mostarda", "Molho de tomate", "Maionese",
            "Maionese verde", "Molho supremo", "Pimenta", "Pao", "Tomate", "Cogumelo",
            "Pasta", "Pasta vegana", "Sal", "Sal rosa", "Farofa", "Azeite", "Manjericao",
            "Farinha de confeiteiro", "Acucar", "Agua"
        };

        public static readonly List<string> ItensEntregaIntrusos = new List<string>
        {
            "Canetas", "Caixa de Parafusos", "Grampeador", "Pneu de Bicicleta",
            "Sabao em Po", "Lampada Fluorescente", "Caderno Escolar", "Chave de Fenda"
        };

        public static readonly Dictionary<string, string> ErrosDigitacao = new Dictionary<string, string>
        {
            { "Queijo suico", "Quejo suico" },
            { "Queijo cheddar", "Queijo xedar" },
            { "Pepperoni", "Peperoni" },
            { "Molho de tomate", "Molho de tomati" },
            { "Maionese", "Maioneze" },
            { "Manjericao", "Manjericao-bravo" },
            { "Farinha de confeiteiro", "Farinha de confeteiro" },
            { "Cogumelo", "Cugumelo" }
        };

        public static readonly List<string> CidadesValidas = new List<string>
        {
            "Sao Paulo", "Curitiba", "Rio de Janeiro"
        };

        public static readonly List<string> CidadesInvalidas = new List<string>
        {
            "Belo Horizonte", "Porto Alegre", "Campinas", "Salvador", "Recife", "Brasilia"
        };

        public static readonly List<string> FornecedoresValidos = new List<string>
        {
            "AgroFoods Brasil", "Distribuidora Sul", "Laticinios Real", "Horta Fresca SA"
        };

        public static readonly List<string> FornecedoresInvalidos = new List<string>
        {
            "Importadora Pirata", "Comercio Fantasma", "Frios Duvidosos Ltda", "Entregas Relampago X"
        };
    }
}