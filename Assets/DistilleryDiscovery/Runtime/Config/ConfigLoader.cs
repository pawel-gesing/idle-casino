using UnityEngine;

namespace DistilleryDiscovery
{
    public static class ConfigLoader
    {
        public static GameConfig LoadFromResources()
        {
            var rarities = Load<RarityFile>("GameData/rarities");
            var ingredients = Load<IngredientFile>("GameData/ingredients");
            var recipes = Load<RecipeFile>("GameData/recipes");
            var economy = Load<EconomyDefinition>("GameData/economy");
            var categories = Load<RecipeCategoryFile>("GameData/categories");
            var laboratories = Load<LaboratoryFile>("GameData/laboratories");
            var contracts = Load<ContractFile>("GameData/contracts");
            var localization = Load<LocalizationFile>("GameData/localization");
            var mastery = Load<MasteryFile>("GameData/mastery");
            var config = new GameConfig(rarities.rarities, ingredients.ingredients, recipes.recipes, economy,
                categories.categories, laboratories.levels, contracts.contracts, localization.entries, mastery.levels);
            ConfigValidator.ValidateOrThrow(config);
            return config;
        }

        private static T Load<T>(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new System.InvalidOperationException($"Missing configuration: Resources/{path}.json");
            var value = JsonUtility.FromJson<T>(asset.text);
            if (value == null) throw new System.InvalidOperationException($"Invalid JSON configuration: {path}");
            return value;
        }
    }
}
