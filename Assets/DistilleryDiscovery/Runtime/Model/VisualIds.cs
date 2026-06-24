namespace DistilleryDiscovery
{
    public static class VisualIds
    {
        public const string NavExperiment = "nav.experiment";
        public const string NavProduction = "nav.production";
        public const string NavContracts = "nav.contracts";
        public const string NavDelivery = "nav.delivery";
        public const string NavLaboratory = "nav.laboratory";

        public const string HeaderGold = "header.gold";
        public const string HeaderRecipes = "header.recipes";
        public const string HeaderIngredients = "header.ingredients";

        public static string Ingredient(string id) => $"ingredient.{id}";
        public static string Group(string id) => $"group.{id}";
        public static string Category(string id) => $"category.{id}";
        public static string Rarity(string id) => $"rarity.{id}";
    }
}
