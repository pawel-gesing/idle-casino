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

    [Serializable] public sealed class WeightedRarity { public string rarityId; public int weight; }
    [Serializable] public sealed class DeliveryEntry { public string ingredientId; public int weight; public int minAmount; public int maxAmount; }
    [Serializable] public sealed class DeliveryPool { public string id; public int rolls; public List<DeliveryEntry> entries = new(); }
    [Serializable] public sealed class EconomyDefinition
    {
        public int startingGold;
        public int ingredientsPerExperiment = 3;
        public float ingredientQualityInfluence = 1f;
        public List<WeightedRarity> productRarityWeights = new();
        public List<DeliveryPool> deliveryPools = new();
    }

    public sealed class GameConfig
    {
        public List<RarityDefinition> Rarities { get; }
        public List<IngredientDefinition> Ingredients { get; }
        public List<RecipeDefinition> Recipes { get; }
        public EconomyDefinition Economy { get; }

        public GameConfig(List<RarityDefinition> rarities, List<IngredientDefinition> ingredients, List<RecipeDefinition> recipes, EconomyDefinition economy)
        {
            Rarities = rarities ?? new(); Ingredients = ingredients ?? new(); Recipes = recipes ?? new(); Economy = economy ?? new();
        }

        public IngredientDefinition Ingredient(string id) => Ingredients.Find(x => x.id == id);
        public RecipeDefinition Recipe(string id) => Recipes.Find(x => x.id == id);
        public RarityDefinition Rarity(string id) => Rarities.Find(x => x.id == id);
    }
}

