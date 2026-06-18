using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DistilleryDiscovery.Tests
{
    public sealed class GameServiceTests
    {
        [Test]
        public void Configuration_LoadsAllRequiredContent()
        {
            var config = ConfigLoader.LoadFromResources();
            Assert.That(config.Ingredients, Has.Count.GreaterThanOrEqualTo(8));
            Assert.That(config.Recipes, Has.Count.GreaterThanOrEqualTo(15));
            Assert.That(config.Rarities, Has.Count.GreaterThanOrEqualTo(4));
            Assert.That(config.Economy.deliveryPools, Has.Count.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Validator_RejectsDuplicateIds()
        {
            var config = MinimalConfig();
            config.Ingredients.Add(config.Ingredients[0]);
            Assert.That(ConfigValidator.Validate(config), Has.Some.Contains("duplicate ingredient"));
        }

        [Test]
        public void Experiment_ConsumesExactlyThreeIngredients()
        {
            var game = GameWithInventory(new MinRandom(), 3);
            game.RunExperiment(ThreeIngredients());
            Assert.That(game.State.AmountOf("ingredient_test"), Is.Zero);
        }

        [Test]
        public void Experiment_CannotRunWithoutRequiredInventory()
        {
            var game = GameWithInventory(new MinRandom(), 2);
            Assert.Throws<InvalidOperationException>(() => game.RunExperiment(ThreeIngredients()));
            Assert.That(game.State.AmountOf("ingredient_test"), Is.EqualTo(2));
        }

        [Test]
        public void Preview_SumsOutcomeWeightsAcrossIngredients()
        {
            var game = GameWithInventory(new MinRandom(), 3);
            var preview = game.Preview(ThreeIngredients());
            Assert.That(preview, Has.Count.EqualTo(1));
            Assert.That(preview[0].Weight, Is.EqualTo(30));
            Assert.That(preview[0].Probability, Is.EqualTo(1f));
        }

        [Test]
        public void Experiment_AddsNewRecipeToBook()
        {
            var game = GameWithInventory(new MinRandom(), 3);
            var result = game.RunExperiment(ThreeIngredients());
            Assert.That(result.WasDiscovered, Is.True);
            Assert.That(game.State.RecipeState("recipe_test"), Is.Not.Null);
            Assert.That(game.State.RecipeState("recipe_test").revealedIngredientIds, Does.Contain("ingredient_test"));
        }

        [Test]
        public void HigherRarity_UpdatesRecipeRecord()
        {
            var game = GameWithInventory(new MaxRandom(), 3);
            game.State.recipes.Add(new PlayerRecipeState { recipeId = "recipe_test", highestProductRarityId = "rarity_common" });
            var result = game.RunExperiment(ThreeIngredients());
            Assert.That(result.RarityImproved, Is.True);
            Assert.That(game.State.RecipeState("recipe_test").highestProductRarityId, Is.EqualTo("rarity_rare"));
        }

        [Test]
        public void LowerRarity_DoesNotOverwriteRecipeRecord()
        {
            var game = GameWithInventory(new MinRandom(), 3);
            game.State.recipes.Add(new PlayerRecipeState { recipeId = "recipe_test", highestProductRarityId = "rarity_rare" });
            game.RunExperiment(ThreeIngredients());
            Assert.That(game.State.RecipeState("recipe_test").highestProductRarityId, Is.EqualTo("rarity_rare"));
        }

        [Test]
        public void Delivery_AddsIngredients()
        {
            var game = GameWithInventory(new MinRandom(), 0);
            var result = game.ReceiveDelivery();
            Assert.That(result.Items["ingredient_test"], Is.EqualTo(2));
            Assert.That(game.State.AmountOf("ingredient_test"), Is.EqualTo(2));
        }

        [Test]
        public void SaveAndLoad_PreservesPlayerState()
        {
            var storage = new MemoryStorage();
            var saves = new SaveService(storage);
            var original = new PlayerState { gold = 777, experimentsCompleted = 12 };
            original.AddIngredient("ingredient_test", 9);
            original.recipes.Add(new PlayerRecipeState { recipeId = "recipe_test", highestProductRarityId = "rarity_rare", timesCreated = 4 });
            saves.Save(original);
            var loaded = saves.Load();
            Assert.That(loaded.gold, Is.EqualTo(777));
            Assert.That(loaded.experimentsCompleted, Is.EqualTo(12));
            Assert.That(loaded.AmountOf("ingredient_test"), Is.EqualTo(9));
            Assert.That(loaded.RecipeState("recipe_test").timesCreated, Is.EqualTo(4));
        }

        private static GameService GameWithInventory(IRandomSource random, int amount)
        {
            var config = MinimalConfig(); var state = GameService.NewState(config); state.AddIngredient("ingredient_test", amount);
            return new GameService(config, state, random);
        }

        private static string[] ThreeIngredients() => new[] { "ingredient_test", "ingredient_test", "ingredient_test" };

        private static GameConfig MinimalConfig()
        {
            var rarities = new List<RarityDefinition>
            {
                new() { id = "rarity_common", displayName = "Common", rank = 1, valueMultiplier = 1f },
                new() { id = "rarity_rare", displayName = "Rare", rank = 2, valueMultiplier = 2f, qualityScore = 10 }
            };
            var ingredients = new List<IngredientDefinition>
            {
                new() { id = "ingredient_test", displayName = "Test Ingredient", rarityId = "rarity_common", outcomeWeights = new List<OutcomeWeight> { new() { recipeId = "recipe_test", weight = 10 } } }
            };
            var recipes = new List<RecipeDefinition>
            {
                new() { id = "recipe_test", displayName = "Test Recipe", collectionRarityId = "rarity_common", baseValue = 10 }
            };
            var economy = new EconomyDefinition
            {
                startingGold = 5,
                ingredientsPerExperiment = 3,
                productRarityWeights = new List<WeightedRarity> { new() { rarityId = "rarity_common", weight = 1 }, new() { rarityId = "rarity_rare", weight = 1 } },
                deliveryPools = new List<DeliveryPool> { new() { id = "pool_base", rolls = 2, entries = new List<DeliveryEntry> { new() { ingredientId = "ingredient_test", weight = 1, minAmount = 1, maxAmount = 1 } } } }
            };
            return new GameConfig(rarities, ingredients, recipes, economy);
        }

        private sealed class MinRandom : IRandomSource { public int Range(int minInclusive, int maxExclusive) => minInclusive; }
        private sealed class MaxRandom : IRandomSource { public int Range(int minInclusive, int maxExclusive) => maxExclusive - 1; }
        private sealed class MemoryStorage : IStateStorage
        {
            private string json;
            public bool Exists => json != null;
            public void Write(string value) => json = value;
            public string Read() => json;
            public void Delete() => json = null;
        }
    }
}

