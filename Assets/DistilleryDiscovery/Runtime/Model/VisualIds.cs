namespace DistilleryDiscovery
{
    public static class VisualIds
    {
        public const string NavExperiment = "nav_experiment";
        public const string NavProduction = "nav_production";
        public const string NavContracts = "nav_contracts";
        public const string NavDelivery = "nav_delivery";
        public const string NavLaboratory = "nav_laboratory";

        public const string HeaderGold = "header_gold";
        public const string HeaderRecipes = "header_recipes";
        public const string HeaderIngredients = "header_ingredients";

        public static string Ingredient(string id) => HasPrefix(id, "ingredient_") ? id : $"ingredient_{id}";
        public static string Group(string id) => HasPrefix(id, "group_") ? id : $"group_{id}";
        public static string Category(string id) => HasPrefix(id, "category_") ? id : $"category_{id}";
        public static string Rarity(string id) => HasPrefix(id, "rarity_") ? id : $"rarity_{id}";
        public static string SpriteResourcePath(string visualId) => $"Visuals/Sprites/{Folder(visualId)}/{visualId}";

        public static bool HasKnownPrefix(string visualId) =>
            HasPrefix(visualId, "ingredient_") || HasPrefix(visualId, "group_") || HasPrefix(visualId, "category_") ||
            HasPrefix(visualId, "rarity_") || HasPrefix(visualId, "nav_") || HasPrefix(visualId, "header_") ||
            HasPrefix(visualId, "ui_");

        private static string Folder(string visualId)
        {
            if (HasPrefix(visualId, "ingredient_")) return "Ingredients";
            if (HasPrefix(visualId, "group_")) return "Groups";
            if (HasPrefix(visualId, "category_")) return "Categories";
            if (HasPrefix(visualId, "rarity_")) return "Rarities";
            if (HasPrefix(visualId, "nav_")) return "Navigation";
            if (HasPrefix(visualId, "header_")) return "Header";
            if (HasPrefix(visualId, "ui_")) return "UI";
            return "";
        }

        private static bool HasPrefix(string id, string prefix) => !string.IsNullOrEmpty(id) && id.StartsWith(prefix);
    }
}
