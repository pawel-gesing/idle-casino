using System;
using System.Collections.Generic;
using System.Linq;

namespace DistilleryDiscovery
{
    [Serializable] public sealed class RarityDefinition { public string id; public string displayName; public int rank; public string colorHex; public float valueMultiplier; public int qualityScore; }
    [Serializable] public sealed class RarityFile { public List<RarityDefinition> rarities = new(); }

    [Serializable] public sealed class IngredientGroupDefinition { public string id; public string displayName; }
    [Serializable] public sealed class IngredientGroupFile { public List<IngredientGroupDefinition> groups = new(); }
    [Serializable] public sealed class IngredientDefinition
    {
        public string id;
        public string baseIngredientId;
        public string displayName;
        public string groupId;
        public string rarityId;
        public string sourceRule = "delivery";
        public float qualityBonus;
        public bool enabled = true;
    }
    [Serializable] public sealed class IngredientFile { public List<IngredientDefinition> ingredients = new(); }

    public static class RecipeRequirementType
    {
        public const string Ingredient = "ingredient";
        public const string Group = "group";
        public const string DistinctGroup = "distinct_group";
        public const string AnyOf = "any_of";
    }
    [Serializable] public sealed class RecipeRequirementClause
    {
        public string type;
        public string ingredientId;
        public string groupId;
        public int count = 1;
        public List<string> ingredientIds = new();
    }
    [Serializable] public sealed class RecipeWeightBonus
    {
        public string ingredientId;
        public string groupId;
        public int weight;
    }
    [Serializable] public sealed class RecipeDefinition
    {
        public string id;
        public string displayName;
        public string categoryId;
        public List<string> tags = new();
        public int baseValue;
        public string collectionRarityId;
        public int baseWeight;
        public List<RecipeRequirementClause> requirements = new();
        public List<RecipeWeightBonus> weightBonuses = new();
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
        public int experimentDurationSeconds = 3600;
        public int productionDurationSeconds = 1800;
        public int maxStoredFreeDeliveries = 3;
        public int maxOfflineProgressSeconds;
        public int freeContractRerolls = 1;
        public int contractRefreshSeconds = 86400;
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

    public static class ContractRole
    {
        public const string Basic = "basic";
        public const string Specialist = "specialist";
        public const string Prestige = "prestige";
        public static readonly string[] All = { Basic, Specialist, Prestige };
    }
    public static class ContractObjectiveType
    {
        public const string Recipe = "produce_recipe";
        public const string Category = "produce_category";
        public const string Tag = "produce_tag";
        public const string Rarity = "produce_rarity";
        public const string Ingredient = "use_ingredient";
        public const string Group = "use_group";
        public const string Discover = "discover_recipes";
        public const string DistinctRecipes = "distinct_recipes";
        public const string RecipeMinRarity = "recipe_min_rarity";
        public const string ImproveRecord = "improve_record";
        public const string Source = "produce_source";
    }
    public static class ContractTargetSelector
    {
        public const string None = "none";
        public const string DiscoveredRecipe = "discovered_recipe";
        public const string UndiscoveredRecipe = "undiscovered_recipe";
        public const string Category = "category";
        public const string Tag = "tag";
        public const string Rarity = "rarity";
        public const string Ingredient = "ingredient";
        public const string Group = "group";
        public const string Source = "source";
    }
    public static class RewardSelectorType
    {
        public const string Ingredient = "ingredient";
        public const string Group = "group";
        public const string Rarity = "rarity";
    }
    [Serializable] public sealed class ContractRewardDefinition
    {
        public string selectorType;
        public string targetId;
        public int weight = 1;
        public int minAmount = 1;
        public int maxAmount = 1;
    }
    [Serializable] public sealed class ContractTemplateDefinition
    {
        public string id;
        public string displayName;
        public string role;
        public int tier = 1;
        public int selectionWeight = 1;
        public string objectiveType;
        public string targetSelector = ContractTargetSelector.None;
        public string fixedTargetId;
        public List<string> allowedTargetIds = new();
        public int minAmount = 1;
        public int maxAmount = 1;
        public string minRarityId;
        public string source;
        public int minLaboratoryLevel = 1;
        public bool enabled = true;
        public int minGoldReward;
        public int maxGoldReward;
        public List<ContractRewardDefinition> ingredientRewards = new();
    }
    [Serializable] public sealed class ContractFile { public List<ContractTemplateDefinition> templates = new(); }

    [Serializable] public sealed class LocalizationDefinition { public string key; public string pl; public string en; }
    [Serializable] public sealed class LocalizationFile { public List<LocalizationDefinition> entries = new(); }

    public sealed class GameConfig
    {
        public List<RarityDefinition> Rarities { get; }
        public List<IngredientGroupDefinition> Groups { get; }
        public List<IngredientDefinition> Ingredients { get; }
        public List<RecipeDefinition> Recipes { get; }
        public List<RecipeCategoryDefinition> Categories { get; }
        public List<LaboratoryLevelDefinition> LaboratoryLevels { get; }
        public List<MasteryLevelDefinition> MasteryLevels { get; }
        public List<ContractTemplateDefinition> ContractTemplates { get; }
        public List<LocalizationDefinition> Localizations { get; }
        public EconomyDefinition Economy { get; }

        public GameConfig(List<RarityDefinition> rarities, List<IngredientDefinition> ingredients,
            List<RecipeDefinition> recipes, EconomyDefinition economy,
            List<RecipeCategoryDefinition> categories = null,
            List<LaboratoryLevelDefinition> laboratoryLevels = null,
            List<ContractTemplateDefinition> contractTemplates = null,
            List<LocalizationDefinition> localizations = null,
            List<MasteryLevelDefinition> masteryLevels = null,
            List<IngredientGroupDefinition> groups = null)
        {
            Rarities = rarities ?? new(); Ingredients = ingredients ?? new(); Recipes = recipes ?? new();
            Economy = economy ?? new(); Categories = categories ?? new(); LaboratoryLevels = laboratoryLevels ?? new();
            MasteryLevels = masteryLevels ?? new(); ContractTemplates = contractTemplates ?? new();
            Localizations = localizations ?? new(); Groups = groups ?? new();
        }

        public IngredientDefinition Ingredient(string id) => Ingredients.Find(x => x.id == id);
        public IngredientGroupDefinition Group(string id) => Groups.Find(x => x.id == id);
        public RecipeDefinition Recipe(string id) => Recipes.Find(x => x.id == id);
        public RarityDefinition Rarity(string id) => Rarities.Find(x => x.id == id);
        public RecipeCategoryDefinition Category(string id) => Categories.Find(x => x.id == id);
        public ContractTemplateDefinition ContractTemplate(string id) => ContractTemplates.Find(x => x.id == id);
        public LaboratoryLevelDefinition LaboratoryLevel(int level) => LaboratoryLevels.Find(x => x.level == level);
        public MasteryLevelDefinition MasteryLevelForCount(int count) => MasteryLevels
            .Where(x => count >= x.requiredProductionCount).OrderByDescending(x => x.requiredProductionCount).FirstOrDefault();
        public MasteryLevelDefinition NextMasteryLevel(int count) => MasteryLevels
            .Where(x => x.requiredProductionCount > count).OrderBy(x => x.requiredProductionCount).FirstOrDefault();
        public string Text(string key, string languageCode, string fallback = null)
        {
            var entry = Localizations.Find(x => x.key == key);
            if (entry == null) return fallback ?? key;
            var value = languageCode == "pl" ? entry.pl : entry.en;
            return string.IsNullOrEmpty(value) ? fallback ?? key : value;
        }
    }
}
