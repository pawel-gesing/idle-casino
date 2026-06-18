using System;
using System.Collections.Generic;

namespace DistilleryDiscovery
{
    [Serializable] public sealed class InventoryEntry { public string ingredientId; public int amount; }
    [Serializable] public sealed class ProductEntry
    {
        public string recipeId;
        public string rarityId;
        public int saleValue;
        public int amount;
    }

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
        public int version = 2;
        public int gold;
        public int experimentsCompleted;
        public int productionsCompleted;
        public int laboratoryLevel = 1;
        public List<InventoryEntry> inventory = new();
        public List<ProductEntry> products = new();
        public List<PlayerRecipeState> recipes = new();
        public List<string> activeContractIds = new();

        public int AmountOf(string id) => inventory.Find(x => x.ingredientId == id)?.amount ?? 0;
        public PlayerRecipeState RecipeState(string id) => recipes.Find(x => x.recipeId == id);

        public void AddIngredient(string id, int amount)
        {
            var entry = inventory.Find(x => x.ingredientId == id);
            if (entry == null) { entry = new InventoryEntry { ingredientId = id }; inventory.Add(entry); }
            entry.amount += amount;
        }

        public void AddProduct(string recipeId, string rarityId, int saleValue, int amount = 1)
        {
            var entry = products.Find(x => x.recipeId == recipeId && x.rarityId == rarityId && x.saleValue == saleValue);
            if (entry == null)
            {
                entry = new ProductEntry { recipeId = recipeId, rarityId = rarityId, saleValue = saleValue };
                products.Add(entry);
            }
            entry.amount += amount;
        }
    }

    public sealed class OutcomeChance { public string RecipeId; public int Weight; public float Probability; }
    public sealed class DeliveryResult { public readonly Dictionary<string, int> Items = new(); }
    public class ProductResult { public string RecipeId; public string RarityId; public int SaleValue; }
    public sealed class ExperimentResult : ProductResult { public bool WasDiscovered; public bool RarityImproved; }
    public sealed class SaleResult { public int ItemsSold; public int GoldEarned; }
    public sealed class ContractResult { public string ContractId; public int ProductsDelivered; public int GoldEarned; }
}
