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

    [Serializable] public sealed class ActiveContractState
    {
        public string instanceId;
        public string templateId;
        public string role;
        public string objectiveType;
        public string targetId;
        public string minRarityId;
        public string source;
        public int amount;
        public int progress;
        public int goldReward;
        public List<string> seenRecipeIds = new();
        public string generatedAtUtc;
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
        public List<string> ingredientIds = new();
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
        public int version = 8;
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
        public string lastSavedAtUtc;
        public int freeContractRerollsRemaining;
        public string contractRefreshUtc;

        public List<ProductEntry> products = new();
        // Legacy version 2 field. It is emptied when GameService normalizes the save.
        public List<string> activeContractIds = new();

        public int AmountOf(string id) => inventory.Find(x => x.ingredientId == id)?.amount ?? 0;
        public PlayerRecipeState RecipeState(string id) => recipes.Find(x => x.recipeId == id);
        public ActiveContractState ContractState(string id) => activeContracts.Find(x => x.instanceId == id);
        public int ProductAmount(string recipeId, string rarityId) =>
            products.Find(x => x.recipeId == recipeId && x.rarityId == rarityId)?.amount ?? 0;

        public void AddIngredient(string id, int amount)
        {
            var entry = inventory.Find(x => x.ingredientId == id);
            if (entry == null) { entry = new InventoryEntry { ingredientId = id }; inventory.Add(entry); }
            entry.amount += amount;
        }

        public void AddProduct(string recipeId, string rarityId, int saleValue, int amount = 1)
        {
            var entry = products.Find(x => x.recipeId == recipeId && x.rarityId == rarityId && x.saleValue == saleValue);
            if (entry == null) { entry = new ProductEntry { recipeId = recipeId, rarityId = rarityId, saleValue = saleValue }; products.Add(entry); }
            entry.amount += amount;
        }
    }

    public sealed class OutcomeChance { public string RecipeId; public int Weight; public float Probability; }
    public sealed class ProductionEvent
    {
        public string RecipeId { get; }
        public string RarityId { get; }
        public string CategoryId { get; }
        public IReadOnlyList<string> Tags { get; }
        public IReadOnlyList<string> IngredientIds { get; }
        public IReadOnlyList<string> GroupIds { get; }
        public string Source { get; }
        public bool WasDiscovered { get; }
        public bool RarityImproved { get; }

        public ProductionEvent(string recipeId, string rarityId, string categoryId, IReadOnlyList<string> tags,
            IReadOnlyList<string> ingredientIds, IReadOnlyList<string> groupIds, string source,
            bool wasDiscovered, bool rarityImproved)
        {
            RecipeId = recipeId; RarityId = rarityId; CategoryId = categoryId;
            Tags = tags == null ? Array.Empty<string>() : new List<string>(tags).AsReadOnly();
            IngredientIds = ingredientIds == null ? Array.Empty<string>() : new List<string>(ingredientIds).AsReadOnly();
            GroupIds = groupIds == null ? Array.Empty<string>() : new List<string>(groupIds).AsReadOnly();
            Source = source; WasDiscovered = wasDiscovered; RarityImproved = rarityImproved;
        }
    }
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

    public sealed class CollectedProductSummary
    {
        public string RecipeId;
        public string RarityId;
        public int Amount;
    }

    public sealed class CollectAllResult
    {
        public int JobsCollected;
        public int ExperimentsCollected;
        public int ProductionsCollected;
        public int NewRecipesDiscovered;
        public int RarityRecordsImproved;
        public int ProductsAdded;
        public int GoldGained;
        public readonly List<CollectedProductSummary> Products = new();
    }

    public sealed class OfflineProgressSummary
    {
        public TimeSpan Elapsed;
        public int DeliveriesGained;
        public int JobsCompleted;
        public int ExperimentsReady;
        public int ProductionsReady;
        public bool HasProgress => Elapsed > TimeSpan.Zero || DeliveriesGained > 0 || JobsCompleted > 0;
    }
}
