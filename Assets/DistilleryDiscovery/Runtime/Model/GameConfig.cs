using System;
using System.Collections.Generic;

namespace DistilleryDiscovery
{
    [Serializable] public sealed class RarityDefinition { public string id; public string displayName; public int rank; public string colorHex; public float valueMultiplier; public int qualityScore; }
    [Serializable] public sealed class RarityFile { public List<RarityDefinition> rarities = new(); }

    [Serializable] public sealed class OutcomeWeight { public string recipeId; public int weight; }
    [Serializable] public sealed class IngredientDefinition
    {
        public string id;
        public string baseIngredientId;
        public string displayName;
        public string rarityId;
        public float qualityBonus;
        public List<OutcomeWeight> outcomeWeights = new();
        public bool enabled = true;
    }
    [Serializable] public sealed class IngredientFile { public List<IngredientDefinition> ingredients = new(); }

    [Serializable] public sealed class RecipeDefinition
    {
        public string id;
        public string displayName;
        public string categoryId;
        public int baseValue;
        public string collectionRarityId;
        public bool enabled = true;
    }
    [Serializable] public sealed class RecipeFile { public List<RecipeDefinition> recipes = new(); }

    [Serializable] public sealed class RecipeCategoryDefinition { public string id; public string displayName; }
    [Serializable] public sealed class RecipeCategoryFile { public List<RecipeCategoryDefinition> categories = new(); }

    [Serializable] public sealed class WeightedRarity { public string rarityId; public int weight; }
    [Serializable] public sealed class DeliveryEntry { public string ingredientId; public int weight; public int minAmount; public int maxAmount; }
    [Serializable] public sealed class DeliveryPool { public string id; public int rolls; public List<DeliveryEntry> entries = new(); }
    [Serializable] public sealed class EconomyDefinition
    {
        public int startingGold;
        public int ingredientsPerExperiment = 3;
        public int ingredientsPerProduction = 3;
        public int activeContractCount = 3;
        public float ingredientQualityInfluence = 1f;
        public float laboratoryQualityInfluence = 1f;
        public int freeDeliveryIntervalSeconds = 7200;
        public int freeDeliveryMinItems = 6;
        public int freeDeliveryMaxItems = 6;
        public int experimentDurationSeconds = 3600;
        public int productionDurationSeconds = 1800;
        public int maxStoredFreeDeliveries = 3;
        public int maxOfflineProgressSeconds;
        public List<WeightedRarity> productRarityWeights = new();
        public List<DeliveryPool> deliveryPools = new();
    }

    [Serializable] public sealed class LaboratoryLevelDefinition
    {
        public int level;
        public int upgradeCost;
        public float productQualityBonus;
        public int experimentSlots = 1;
        public int productionSlots = 1;
        public float experimentTimeMultiplier = 1f;
        public float productionTimeMultiplier = 1f;
    }
    [Serializable] public sealed class LaboratoryFile { public List<LaboratoryLevelDefinition> levels = new(); }

    [Serializable] public sealed class MasteryLevelDefinition
    {
        public string id;
        public string displayName;
        public int requiredProductionCount;
        public float rarityBonus;
    }
    [Serializable] public sealed class MasteryFile { public List<MasteryLevelDefinition> levels = new(); }

    public static class ContractRequirementType
    {
        public const string Recipe = "produce_recipe";
        public const string Rarity = "produce_rarity";
        public const string Category = "produce_category";
    }

    [Serializable] public sealed class ContractDefinition
    {
        public string id;
        public string displayName;
        public string requirementType;
        public string targetId;
        public int amount;
        public int goldReward;
        public List<IngredientRewardDefinition> ingredientRewards = new();
        public bool enabled = true;
    }
    [Serializable] public sealed class IngredientRewardDefinition { public string ingredientId; public int minAmount; public int maxAmount; }
    [Serializable] public sealed class ContractFile { public List<ContractDefinition> contracts = new(); }

    [Serializable] public sealed class LocalizationDefinition { public string key; public string pl; public string en; }
    [Serializable] public sealed class LocalizationFile { public List<LocalizationDefinition> entries = new(); }

    public sealed class GameConfig
    {
        public List<RarityDefinition> Rarities { get; }
        public List<IngredientDefinition> Ingredients { get; }
        public List<RecipeDefinition> Recipes { get; }
        public List<RecipeCategoryDefinition> Categories { get; }
        public List<LaboratoryLevelDefinition> LaboratoryLevels { get; }
        public List<MasteryLevelDefinition> MasteryLevels { get; }
        public List<ContractDefinition> Contracts { get; }
        public List<LocalizationDefinition> Localizations { get; }
        public EconomyDefinition Economy { get; }

        public GameConfig(
            List<RarityDefinition> rarities,
            List<IngredientDefinition> ingredients,
            List<RecipeDefinition> recipes,
            EconomyDefinition economy,
            List<RecipeCategoryDefinition> categories = null,
            List<LaboratoryLevelDefinition> laboratoryLevels = null,
            List<ContractDefinition> contracts = null,
            List<LocalizationDefinition> localizations = null,
            List<MasteryLevelDefinition> masteryLevels = null)
        {
            Rarities = rarities ?? new();
            Ingredients = ingredients ?? new();
            Recipes = recipes ?? new();
            Economy = economy ?? new();
            Categories = categories ?? new();
            LaboratoryLevels = laboratoryLevels ?? new();
            MasteryLevels = masteryLevels ?? new();
            Contracts = contracts ?? new();
            Localizations = localizations ?? new();
        }

        public IngredientDefinition Ingredient(string id) => Ingredients.Find(x => x.id == id);
        public RecipeDefinition Recipe(string id) => Recipes.Find(x => x.id == id);
        public RarityDefinition Rarity(string id) => Rarities.Find(x => x.id == id);
        public RecipeCategoryDefinition Category(string id) => Categories.Find(x => x.id == id);
        public LaboratoryLevelDefinition LaboratoryLevel(int level) => LaboratoryLevels.Find(x => x.level == level);
        public MasteryLevelDefinition MasteryLevelForCount(int count)
        {
            MasteryLevelDefinition result = null;
            foreach (var level in MasteryLevels)
                if (count >= level.requiredProductionCount && (result == null || level.requiredProductionCount > result.requiredProductionCount)) result = level;
            return result;
        }
        public MasteryLevelDefinition NextMasteryLevel(int count)
        {
            MasteryLevelDefinition result = null;
            foreach (var level in MasteryLevels)
                if (level.requiredProductionCount > count && (result == null || level.requiredProductionCount < result.requiredProductionCount)) result = level;
            return result;
        }
        public ContractDefinition Contract(string id) => Contracts.Find(x => x.id == id);
        public string Text(string key, string languageCode, string fallback = null)
        {
            var entry = Localizations.Find(x => x.key == key);
            if (entry == null) return fallback ?? key;
            var value = languageCode == "pl" ? entry.pl : entry.en;
            return string.IsNullOrEmpty(value) ? fallback ?? key : value;
        }
    }
}
