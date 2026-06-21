using System;
using System.Collections.Generic;

namespace DistilleryDiscovery
{
    [Serializable] public sealed class InventoryEntry { public string ingredientId; public int amount; }

    // Kept only so version 2 saves can turn stored products into gold during migration.
    [Serializable] public sealed class ProductEntry
    {
        public string recipeId;
        public string rarityId;
        public int saleValue;
        public int amount;
    }

    [Serializable] public sealed class ActiveContractState
    {
        public string contractId;
        public int progress;
    }

    [Serializable] public sealed class PendingContractProgress
    {
        public string contractId;
        public int previousProgress;
        public int currentProgress;
        public bool completed;
    }

    [Serializable] public sealed class PendingResultState
    {
        public string source;
        public string recipeId;
        public string rarityId;
        public int saleValue;
        public bool wasDiscovered;
        public bool rarityImproved;
        public List<PendingContractProgress> contractProgress = new();
    }

    [Serializable] public sealed class PlayerRecipeState
    {
        public string recipeId;
        public string highestProductRarityId;
        public int timesCreated;
        public string firstDiscoveredAt;
        public List<string> revealedIngredientIds = new();
    }

    public static class LaboratoryJobType { public const string Experiment = "experiment"; public const string Production = "production"; }
    public static class LaboratoryJobStatus { public const string Running = "running"; public const string Completed = "completed"; public const string Claimed = "claimed"; }

    [Serializable] public sealed class LaboratoryJobState
    {
        public string id;
        public string type;
        public string startTimeUtc;
        public string endTimeUtc;
        public string status;
        public string recipeId;
        public List<string> ingredientIds = new();
    }

    [Serializable] public sealed class PlayerState
    {
        public int version = 6;
        public int gold;
        public int experimentsCompleted;
        public int productionsCompleted;
        public int laboratoryLevel = 1;
        public string languageCode = "en";
        public List<InventoryEntry> inventory = new();
        public List<PlayerRecipeState> recipes = new();
        public List<ActiveContractState> activeContracts = new();
        public PendingResultState pendingResult;
        public string freeDeliveryLastUpdateUtc;
        public int availableFreeDeliveries;
        public List<LaboratoryJobState> laboratoryJobs = new();

        // Legacy version 2 fields. They are emptied when GameService normalizes the save.
        public List<ProductEntry> products = new();
        public List<string> activeContractIds = new();

        public int AmountOf(string id) => inventory.Find(x => x.ingredientId == id)?.amount ?? 0;
        public PlayerRecipeState RecipeState(string id) => recipes.Find(x => x.recipeId == id);
        public ActiveContractState ContractState(string id) => activeContracts.Find(x => x.contractId == id);

        public void AddIngredient(string id, int amount)
        {
            var entry = inventory.Find(x => x.ingredientId == id);
            if (entry == null) { entry = new InventoryEntry { ingredientId = id }; inventory.Add(entry); }
            entry.amount += amount;
        }
    }

    public sealed class OutcomeChance { public string RecipeId; public int Weight; public float Probability; }
    public sealed class DeliveryResult { public readonly Dictionary<string, int> Items = new(); }
    public class ProductResult { public string RecipeId; public string RarityId; public int SaleValue; }
    public sealed class ExperimentResult : ProductResult { public bool WasDiscovered; public bool RarityImproved; }
    public sealed class PendingClaimResult
    {
        public int ProductGold;
        public int ContractGold;
        public int TotalGold;
        public List<string> CompletedContractIds = new();
        public readonly Dictionary<string, int> IngredientRewards = new();
    }
}
