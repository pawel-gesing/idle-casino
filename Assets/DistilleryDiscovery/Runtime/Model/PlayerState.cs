using System;
using System.Collections.Generic;

namespace DistilleryDiscovery
{
    [Serializable] public sealed class InventoryEntry { public string ingredientId; public int amount; }
    [Serializable] public sealed class PlayerRecipeState
    {
        public string recipeId;
        public string highestProductRarityId;
        public int timesCreated;
        public string firstDiscoveredAt;
        public List<string> revealedIngredientIds = new();
    }

    [Serializable] public sealed class PlayerState
    {
        public int version = 1;
        public int gold;
        public int experimentsCompleted;
        public List<InventoryEntry> inventory = new();
        public List<PlayerRecipeState> recipes = new();

        public int AmountOf(string id) => inventory.Find(x => x.ingredientId == id)?.amount ?? 0;
        public PlayerRecipeState RecipeState(string id) => recipes.Find(x => x.recipeId == id);

        public void AddIngredient(string id, int amount)
        {
            var entry = inventory.Find(x => x.ingredientId == id);
            if (entry == null) { entry = new InventoryEntry { ingredientId = id }; inventory.Add(entry); }
            entry.amount += amount;
        }
    }

    public sealed class OutcomeChance { public string RecipeId; public int Weight; public float Probability; }
    public sealed class DeliveryResult { public readonly Dictionary<string, int> Items = new(); }
    public sealed class ExperimentResult { public string RecipeId; public string RarityId; public int SaleValue; public bool WasDiscovered; public bool RarityImproved; }
}

