using System;
using System.Collections.Generic;
using System.Linq;

namespace DistilleryDiscovery
{
    public interface IRandomSource { int Range(int minInclusive, int maxExclusive); }
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random;
        public SystemRandomSource(int? seed = null) => random = seed.HasValue ? new Random(seed.Value) : new Random();
        public int Range(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);
    }

    public sealed class GameService
    {
        public GameConfig Config { get; }
        public PlayerState State { get; private set; }
        private readonly IRandomSource random;

        public GameService(GameConfig config, PlayerState state, IRandomSource random = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            this.random = random ?? new SystemRandomSource();
            ReplaceState(state);
        }

        public static PlayerState NewState(GameConfig config)
        {
            var state = new PlayerState { gold = config.Economy.startingGold };
            state.activeContractIds.AddRange(config.Contracts.Where(x => x.enabled).Take(config.Economy.activeContractCount).Select(x => x.id));
            return state;
        }

        public void ReplaceState(PlayerState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            var wasLegacySave = State.version < 2;
            State.inventory ??= new List<InventoryEntry>();
            State.products ??= new List<ProductEntry>();
            State.recipes ??= new List<PlayerRecipeState>();
            State.activeContractIds ??= new List<string>();
            if (State.laboratoryLevel < 1) State.laboratoryLevel = 1;
            if (wasLegacySave && State.activeContractIds.Count == 0)
                State.activeContractIds.AddRange(Config.Contracts.Where(x => x.enabled).Take(Config.Economy.activeContractCount).Select(x => x.id));
            State.version = 2;
        }

        public List<OutcomeChance> Preview(IReadOnlyList<string> ingredientIds)
        {
            ValidateIngredientSelection(ingredientIds, Config.Economy.ingredientsPerExperiment);
            var weights = new Dictionary<string, int>();
            foreach (var id in ingredientIds)
                foreach (var outcome in Config.Ingredient(id).outcomeWeights)
                    weights[outcome.recipeId] = weights.GetValueOrDefault(outcome.recipeId) + outcome.weight;
            var total = weights.Values.Sum();
            if (total <= 0) throw new InvalidOperationException("Selected ingredients cannot produce any recipe.");
            return weights.Select(x => new OutcomeChance { RecipeId = x.Key, Weight = x.Value, Probability = (float)x.Value / total })
                .OrderByDescending(x => x.Weight).ThenBy(x => x.RecipeId).ToList();
        }

        public ExperimentResult RunExperiment(IReadOnlyList<string> ingredientIds)
        {
            var outcomes = Preview(ingredientIds);
            ConsumeIngredients(ingredientIds);

            var recipeId = WeightedPick(outcomes.Select(x => (x.RecipeId, x.Weight)).ToList());
            var product = CreateProduct(recipeId, ingredientIds);
            var bookChange = UpdateRecipeBook(recipeId, product.RarityId, ingredientIds);
            State.experimentsCompleted++;
            return new ExperimentResult
            {
                RecipeId = product.RecipeId,
                RarityId = product.RarityId,
                SaleValue = product.SaleValue,
                WasDiscovered = bookChange.discovered,
                RarityImproved = bookChange.improved
            };
        }

        public ProductResult RunProduction(string recipeId, IReadOnlyList<string> ingredientIds)
        {
            var recipe = Config.Recipe(recipeId) ?? throw new InvalidOperationException($"Unknown recipe: {recipeId}");
            if (State.RecipeState(recipeId) == null) throw new InvalidOperationException("Production requires a discovered recipe.");
            ValidateIngredientSelection(ingredientIds, Config.Economy.ingredientsPerProduction);
            foreach (var ingredientId in ingredientIds)
                if (!Config.Ingredient(ingredientId).outcomeWeights.Any(x => x.recipeId == recipe.id && x.weight > 0))
                    throw new InvalidOperationException($"{Config.Ingredient(ingredientId).displayName} does not contribute to {recipe.displayName}.");

            ConsumeIngredients(ingredientIds);
            var product = CreateProduct(recipeId, ingredientIds);
            UpdateRecipeBook(recipeId, product.RarityId, ingredientIds);
            State.productionsCompleted++;
            return product;
        }

        public SaleResult SellProduct(string recipeId, string rarityId, int saleValue, int amount = 1)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var product = State.products.Find(x => x.recipeId == recipeId && x.rarityId == rarityId && x.saleValue == saleValue);
            if (product == null || product.amount < amount) throw new InvalidOperationException("Not enough products to sell.");
            product.amount -= amount;
            var earned = checked(product.saleValue * amount);
            State.gold = checked(State.gold + earned);
            if (product.amount == 0) State.products.Remove(product);
            return new SaleResult { ItemsSold = amount, GoldEarned = earned };
        }

        public SaleResult SellProductStack(string recipeId, string rarityId, int saleValue)
        {
            var product = State.products.Find(x => x.recipeId == recipeId && x.rarityId == rarityId && x.saleValue == saleValue)
                ?? throw new InvalidOperationException("Product stack does not exist.");
            return SellProduct(recipeId, rarityId, saleValue, product.amount);
        }

        public SaleResult SellAllProducts()
        {
            var result = new SaleResult
            {
                ItemsSold = State.products.Sum(x => x.amount),
                GoldEarned = State.products.Sum(x => checked(x.saleValue * x.amount))
            };
            State.gold = checked(State.gold + result.GoldEarned);
            State.products.Clear();
            return result;
        }

        public ContractResult FulfillContract(string contractId)
        {
            if (!State.activeContractIds.Contains(contractId)) throw new InvalidOperationException("Contract is not active.");
            var contract = Config.Contract(contractId) ?? throw new InvalidOperationException($"Unknown contract: {contractId}");
            var matching = State.products.Where(x => Matches(contract, x)).ToList();
            if (matching.Sum(x => x.amount) < contract.amount) throw new InvalidOperationException("Not enough matching products for this contract.");

            var remaining = contract.amount;
            foreach (var product in matching)
            {
                var used = Math.Min(product.amount, remaining);
                product.amount -= used;
                remaining -= used;
                if (remaining == 0) break;
            }
            State.products.RemoveAll(x => x.amount <= 0);
            State.gold = checked(State.gold + contract.goldReward);
            State.activeContractIds.Remove(contractId);
            ActivateNextContract(contractId);
            return new ContractResult { ContractId = contractId, ProductsDelivered = contract.amount, GoldEarned = contract.goldReward };
        }

        public LaboratoryLevelDefinition UpgradeLaboratory()
        {
            var next = Config.LaboratoryLevel(State.laboratoryLevel + 1)
                ?? throw new InvalidOperationException("Laboratory is already at maximum level.");
            if (State.gold < next.upgradeCost) throw new InvalidOperationException("Not enough gold for the laboratory upgrade.");
            State.gold -= next.upgradeCost;
            State.laboratoryLevel = next.level;
            return next;
        }

        public DeliveryResult ReceiveDelivery(string poolId = "pool_base")
        {
            var pool = Config.Economy.deliveryPools.Find(x => x.id == poolId) ?? throw new InvalidOperationException($"Unknown delivery pool: {poolId}");
            var result = new DeliveryResult();
            for (var i = 0; i < pool.rolls; i++)
            {
                var entryId = WeightedPick(pool.entries.Select(x => (x.ingredientId, x.weight)).ToList());
                var entry = pool.entries.Find(x => x.ingredientId == entryId);
                var amount = random.Range(entry.minAmount, entry.maxAmount + 1);
                State.AddIngredient(entryId, amount);
                result.Items[entryId] = result.Items.GetValueOrDefault(entryId) + amount;
            }
            return result;
        }

        public List<WeightedRarity> ProductRarityWeights(IReadOnlyList<string> ingredientIds)
        {
            ValidateKnownIngredients(ingredientIds);
            var ingredientQuality = ingredientIds.Average(id =>
                Config.Rarity(Config.Ingredient(id).rarityId).qualityScore + Config.Ingredient(id).qualityBonus * 100f);
            var labQuality = (Config.LaboratoryLevel(State.laboratoryLevel)?.productQualityBonus ?? 0f) * 100f;
            var quality = ingredientQuality * Config.Economy.ingredientQualityInfluence
                + labQuality * Config.Economy.laboratoryQualityInfluence;
            return Config.Economy.productRarityWeights.Select(entry =>
            {
                var rank = Config.Rarity(entry.rarityId).rank;
                var multiplier = 1f + quality * Math.Max(0, rank - 1) / 100f;
                return new WeightedRarity { rarityId = entry.rarityId, weight = Math.Max(1, (int)Math.Round(entry.weight * multiplier)) };
            }).ToList();
        }

        private ProductResult CreateProduct(string recipeId, IReadOnlyList<string> ingredientIds)
        {
            var rarityId = WeightedPick(ProductRarityWeights(ingredientIds).Select(x => (x.rarityId, x.weight)).ToList());
            var recipe = Config.Recipe(recipeId);
            var rarity = Config.Rarity(rarityId);
            var ingredientBonus = ingredientIds.Average(id => Config.Ingredient(id).qualityBonus);
            var saleValue = Math.Max(1, (int)Math.Round(recipe.baseValue * rarity.valueMultiplier * (1f + ingredientBonus)));
            State.AddProduct(recipeId, rarityId, saleValue);
            return new ProductResult { RecipeId = recipeId, RarityId = rarityId, SaleValue = saleValue };
        }

        private (bool discovered, bool improved) UpdateRecipeBook(string recipeId, string rarityId, IReadOnlyList<string> ingredientIds)
        {
            var rarity = Config.Rarity(rarityId);
            var book = State.RecipeState(recipeId);
            var discovered = book == null;
            var improved = false;
            if (book == null)
            {
                book = new PlayerRecipeState { recipeId = recipeId, highestProductRarityId = rarityId, firstDiscoveredAt = DateTime.UtcNow.ToString("O") };
                book.revealedIngredientIds.AddRange(ingredientIds.Distinct());
                State.recipes.Add(book);
            }
            else if (Config.Rarity(book.highestProductRarityId).rank < rarity.rank)
            {
                book.highestProductRarityId = rarityId;
                improved = true;
            }
            book.timesCreated++;
            return (discovered, improved);
        }

        private bool Matches(ContractDefinition contract, ProductEntry product)
        {
            return contract.requirementType switch
            {
                ContractRequirementType.Recipe => product.recipeId == contract.targetId,
                ContractRequirementType.Rarity => product.rarityId == contract.targetId,
                ContractRequirementType.Category => Config.Recipe(product.recipeId)?.categoryId == contract.targetId,
                _ => false
            };
        }

        private void ActivateNextContract(string justCompletedId)
        {
            var next = Config.Contracts.FirstOrDefault(x => x.enabled && x.id != justCompletedId && !State.activeContractIds.Contains(x.id));
            if (next != null && State.activeContractIds.Count < Config.Economy.activeContractCount) State.activeContractIds.Add(next.id);
        }

        private string WeightedPick(IReadOnlyList<(string id, int weight)> values)
        {
            var total = values.Sum(x => x.weight);
            if (total <= 0) throw new InvalidOperationException("Weighted selection has no positive entries.");
            var roll = random.Range(0, total);
            foreach (var value in values) { roll -= value.weight; if (roll < 0) return value.id; }
            return values[^1].id;
        }

        private void ConsumeIngredients(IReadOnlyList<string> ingredientIds)
        {
            foreach (var group in ingredientIds.GroupBy(x => x)) State.AddIngredient(group.Key, -group.Count());
        }

        private void ValidateIngredientSelection(IReadOnlyList<string> ingredientIds, int requiredCount)
        {
            if (ingredientIds == null || ingredientIds.Count != requiredCount)
                throw new InvalidOperationException($"Select exactly {requiredCount} ingredients.");
            ValidateKnownIngredients(ingredientIds);
            foreach (var group in ingredientIds.GroupBy(x => x))
                if (State.AmountOf(group.Key) < group.Count()) throw new InvalidOperationException($"Not enough {Config.Ingredient(group.Key).displayName}.");
        }

        private void ValidateKnownIngredients(IReadOnlyList<string> ingredientIds)
        {
            if (ingredientIds == null || ingredientIds.Count == 0) throw new InvalidOperationException("Select ingredients.");
            foreach (var id in ingredientIds)
                if (Config.Ingredient(id) == null) throw new InvalidOperationException($"Unknown ingredient: {id}");
        }
    }
}
