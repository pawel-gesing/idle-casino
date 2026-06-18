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
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.random = random ?? new SystemRandomSource();
        }

        public static PlayerState NewState(GameConfig config) => new() { gold = config.Economy.startingGold };
        public void ReplaceState(PlayerState state) => State = state ?? throw new ArgumentNullException(nameof(state));

        public List<OutcomeChance> Preview(IReadOnlyList<string> ingredientIds)
        {
            ValidateSelection(ingredientIds);
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
            foreach (var group in ingredientIds.GroupBy(x => x)) State.AddIngredient(group.Key, -group.Count());

            var recipeId = WeightedPick(outcomes.Select(x => (x.RecipeId, x.Weight)).ToList());
            var rarityId = RollProductRarity(ingredientIds);
            var recipe = Config.Recipe(recipeId);
            var rarity = Config.Rarity(rarityId);
            var ingredientBonus = ingredientIds.Average(id => Config.Ingredient(id).qualityBonus);
            var saleValue = Math.Max(1, (int)Math.Round(recipe.baseValue * rarity.valueMultiplier * (1f + ingredientBonus)));

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
            State.gold += saleValue;
            State.experimentsCompleted++;
            return new ExperimentResult { RecipeId = recipeId, RarityId = rarityId, SaleValue = saleValue, WasDiscovered = discovered, RarityImproved = improved };
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

        private string RollProductRarity(IReadOnlyList<string> ingredientIds)
        {
            var quality = ingredientIds.Average(id => Config.Rarity(Config.Ingredient(id).rarityId).qualityScore + Config.Ingredient(id).qualityBonus * 100f);
            var weights = Config.Economy.productRarityWeights.Select(entry =>
            {
                var rank = Config.Rarity(entry.rarityId).rank;
                var multiplier = 1f + quality * Config.Economy.ingredientQualityInfluence * Math.Max(0, rank - 1) / 100f;
                return (entry.rarityId, Math.Max(1, (int)Math.Round(entry.weight * multiplier)));
            }).ToList();
            return WeightedPick(weights);
        }

        private string WeightedPick(IReadOnlyList<(string id, int weight)> values)
        {
            var total = values.Sum(x => x.weight);
            var roll = random.Range(0, total);
            foreach (var value in values) { roll -= value.weight; if (roll < 0) return value.id; }
            return values[^1].id;
        }

        private void ValidateSelection(IReadOnlyList<string> ingredientIds)
        {
            if (ingredientIds == null || ingredientIds.Count != Config.Economy.ingredientsPerExperiment)
                throw new InvalidOperationException($"Select exactly {Config.Economy.ingredientsPerExperiment} ingredients.");
            foreach (var group in ingredientIds.GroupBy(x => x))
            {
                if (Config.Ingredient(group.Key) == null) throw new InvalidOperationException($"Unknown ingredient: {group.Key}");
                if (State.AmountOf(group.Key) < group.Count()) throw new InvalidOperationException($"Not enough {Config.Ingredient(group.Key).displayName}.");
            }
        }
    }
}

