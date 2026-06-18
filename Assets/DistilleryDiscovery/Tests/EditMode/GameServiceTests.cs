using System;
using System.Collections.Generic;
using System.Linq;
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
            Assert.That(config.Categories, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(config.Contracts, Has.Count.GreaterThanOrEqualTo(4));
            Assert.That(config.Localizations, Has.Count.GreaterThanOrEqualTo(40));
            Assert.That(config.Text("ui.nav.state", "pl"), Is.EqualTo("STAN"));
            Assert.That(config.Text("ui.nav.state", "en"), Is.EqualTo("STATE"));
            Assert.That(config.LaboratoryLevel(2).upgradeCost, Is.EqualTo(1500));
            Assert.That(config.Economy.activeContractCount, Is.EqualTo(3));
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
        public void Experiment_DiscoversRecipeAndAutomaticallySellsProduct()
        {
            var game = GameWithInventory(new MinRandom(), 3);
            var startingGold = game.State.gold;
            var result = game.RunExperiment(ThreeIngredients());
            Assert.That(result.WasDiscovered, Is.True);
            Assert.That(game.State.RecipeState("recipe_test"), Is.Not.Null);
            Assert.That(game.State.gold, Is.EqualTo(startingGold + result.SaleValue));
            Assert.That(game.State.products, Is.Empty);
        }

        [Test]
        public void HigherRarity_UpdatesRecipeRecord()
        {
            var game = GameWithInventory(new MaxRandom(), 3);
            Discover(game, "rarity_common");
            var result = game.RunExperiment(ThreeIngredients());
            Assert.That(result.RarityImproved, Is.True);
            Assert.That(game.State.RecipeState("recipe_test").highestProductRarityId, Is.EqualTo("rarity_rare"));
        }

        [Test]
        public void Production_RequiresDiscoveredRecipe()
        {
            var game = GameWithInventory(new MinRandom(), 3);
            Assert.Throws<InvalidOperationException>(() => game.RunProduction("recipe_test", ThreeIngredients()));
            Assert.That(game.State.AmountOf("ingredient_test"), Is.EqualTo(3));
        }

        [Test]
        public void Production_RejectsIngredientThatDoesNotContributeToRecipe()
        {
            var game = GameWithInventory(new MinRandom(), 2);
            game.State.AddIngredient("ingredient_invalid", 1);
            Discover(game);
            var selection = new[] { "ingredient_test", "ingredient_test", "ingredient_invalid" };
            Assert.Throws<InvalidOperationException>(() => game.RunProduction("recipe_test", selection));
            Assert.That(game.State.AmountOf("ingredient_test"), Is.EqualTo(2));
        }

        [Test]
        public void Production_ConsumesIngredientsAndAutomaticallySellsGuaranteedProduct()
        {
            var game = GameWithInventory(new MinRandom(), 3);
            Discover(game);
            var startingGold = game.State.gold;
            var result = game.RunProduction("recipe_test", ThreeIngredients());
            Assert.That(game.State.AmountOf("ingredient_test"), Is.Zero);
            Assert.That(result.RecipeId, Is.EqualTo("recipe_test"));
            Assert.That(game.State.gold, Is.EqualTo(startingGold + result.SaleValue));
            Assert.That(game.State.products, Is.Empty);
        }

        [Test]
        public void ProductCreation_AdvancesRecipeRarityAndCategoryContracts()
        {
            var game = GameWithInventory(new MinRandom(), 6);
            game.RunExperiment(ThreeIngredients());
            DiscoverIfNeeded(game);
            game.RunProduction("recipe_test", ThreeIngredients());
            Assert.That(game.State.ContractState("contract_recipe").progress, Is.EqualTo(2));
            Assert.That(game.State.ContractState("contract_rarity").progress, Is.EqualTo(2));
            Assert.That(game.State.ContractState("contract_category").progress, Is.EqualTo(2));
        }

        [Test]
        public void CompletedContract_RequiresClaimBeforeReward()
        {
            var game = GameWithInventory(new MinRandom(), 6);
            var startingGold = game.State.gold;
            var first = game.RunExperiment(ThreeIngredients());
            var second = game.RunExperiment(ThreeIngredients());
            var salesGold = first.SaleValue + second.SaleValue;
            Assert.That(game.State.ContractState("contract_recipe").progress, Is.EqualTo(2));
            Assert.That(game.State.gold, Is.EqualTo(startingGold + salesGold));
            Assert.That(game.State.activeContracts.Select(x => x.contractId), Does.Contain("contract_recipe"));

            var result = game.ClaimContract("contract_recipe");
            Assert.That(result.GoldEarned, Is.EqualTo(40));
            Assert.That(game.State.gold, Is.EqualTo(startingGold + salesGold + 40));
        }

        [Test]
        public void IncompleteContract_CannotBeClaimed()
        {
            var game = GameWithInventory(new MinRandom(), 3);
            game.RunExperiment(ThreeIngredients());
            Assert.Throws<InvalidOperationException>(() => game.ClaimContract("contract_recipe"));
        }

        [Test]
        public void ClaimingContract_ImmediatelyActivatesDistinctReplacement()
        {
            var game = GameWithInventory(new MinRandom(), 6);
            game.RunExperiment(ThreeIngredients());
            game.RunExperiment(ThreeIngredients());
            game.ClaimContract("contract_recipe");
            Assert.That(game.State.activeContracts, Has.Count.EqualTo(3));
            Assert.That(game.State.activeContracts.Select(x => x.contractId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(game.State.activeContracts.Select(x => x.contractId), Does.Contain("contract_spare"));
        }

        [Test]
        public void LaboratoryUpgrade_CostsConfiguredGold()
        {
            var game = GameWithInventory(new MinRandom(), 0);
            game.State.gold = 500;
            game.UpgradeLaboratory();
            Assert.That(game.State.laboratoryLevel, Is.EqualTo(2));
            Assert.That(game.State.gold, Is.EqualTo(200));
        }

        [Test]
        public void LaboratoryBonus_ChangesProductionRarityAtControlledRollThreshold()
        {
            var game = GameWithInventory(new FractionRandom(0.49f), 6);
            game.State.gold = 500;
            Discover(game);
            var beforeUpgrade = game.RunProduction("recipe_test", ThreeIngredients());
            game.UpgradeLaboratory();
            var afterUpgrade = game.RunProduction("recipe_test", ThreeIngredients());
            Assert.That(beforeUpgrade.RarityId, Is.EqualTo("rarity_common"));
            Assert.That(afterUpgrade.RarityId, Is.EqualTo("rarity_rare"));
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
        public void VersionTwoSave_MigratesStoredProductsAndContracts()
        {
            var config = MinimalConfig();
            var legacy = new PlayerState { version = 2, gold = 5 };
            legacy.products.Add(new ProductEntry { recipeId = "recipe_test", rarityId = "rarity_common", saleValue = 10, amount = 3 });
            legacy.activeContractIds.Add("contract_recipe");
            var game = new GameService(config, legacy, new MinRandom());
            Assert.That(game.State.version, Is.EqualTo(3));
            Assert.That(game.State.gold, Is.EqualTo(35));
            Assert.That(game.State.products, Is.Empty);
            Assert.That(game.State.ContractState("contract_recipe"), Is.Not.Null);
            Assert.That(game.State.activeContracts, Has.Count.EqualTo(3));
        }

        [Test]
        public void SaveAndLoad_PreservesLanguageAndContractProgress()
        {
            var storage = new MemoryStorage();
            var saves = new SaveService(storage);
            var original = new PlayerState { gold = 777, laboratoryLevel = 3, languageCode = "pl" };
            original.activeContracts.Add(new ActiveContractState { contractId = "contract_recipe", progress = 1 });
            saves.Save(original);
            var loaded = saves.Load();
            Assert.That(loaded.languageCode, Is.EqualTo("pl"));
            Assert.That(loaded.activeContracts[0].contractId, Is.EqualTo("contract_recipe"));
            Assert.That(loaded.activeContracts[0].progress, Is.EqualTo(1));
        }

        private static GameService GameWithInventory(IRandomSource random, int amount)
        {
            var config = MinimalConfig();
            var state = GameService.NewState(config);
            state.AddIngredient("ingredient_test", amount);
            return new GameService(config, state, random);
        }

        private static void Discover(GameService game, string rarityId = "rarity_common") =>
            game.State.recipes.Add(new PlayerRecipeState { recipeId = "recipe_test", highestProductRarityId = rarityId });

        private static void DiscoverIfNeeded(GameService game) { if (game.State.RecipeState("recipe_test") == null) Discover(game); }
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
                new() { id = "ingredient_test", displayName = "Test Ingredient", rarityId = "rarity_common", outcomeWeights = new List<OutcomeWeight> { new() { recipeId = "recipe_test", weight = 10 } } },
                new() { id = "ingredient_invalid", displayName = "Invalid Ingredient", rarityId = "rarity_common", outcomeWeights = new List<OutcomeWeight>() }
            };
            var recipes = new List<RecipeDefinition>
            {
                new() { id = "recipe_test", displayName = "Test Recipe", categoryId = "category_test", collectionRarityId = "rarity_common", baseValue = 10 }
            };
            var economy = new EconomyDefinition
            {
                startingGold = 5,
                ingredientsPerExperiment = 3,
                ingredientsPerProduction = 3,
                activeContractCount = 3,
                productRarityWeights = new List<WeightedRarity> { new() { rarityId = "rarity_common", weight = 100 }, new() { rarityId = "rarity_rare", weight = 100 } },
                deliveryPools = new List<DeliveryPool> { new() { id = "pool_base", rolls = 2, entries = new List<DeliveryEntry> { new() { ingredientId = "ingredient_test", weight = 1, minAmount = 1, maxAmount = 1 } } } }
            };
            var categories = new List<RecipeCategoryDefinition> { new() { id = "category_test", displayName = "Test" } };
            var labs = new List<LaboratoryLevelDefinition>
            {
                new() { level = 1, upgradeCost = 0, productQualityBonus = 0f },
                new() { level = 2, upgradeCost = 300, productQualityBonus = .5f }
            };
            var contracts = new List<ContractDefinition>
            {
                new() { id = "contract_recipe", displayName = "Recipe", requirementType = ContractRequirementType.Recipe, targetId = "recipe_test", amount = 2, goldReward = 40 },
                new() { id = "contract_rarity", displayName = "Rarity", requirementType = ContractRequirementType.Rarity, targetId = "rarity_common", amount = 2, goldReward = 40 },
                new() { id = "contract_category", displayName = "Category", requirementType = ContractRequirementType.Category, targetId = "category_test", amount = 2, goldReward = 40 },
                new() { id = "contract_spare", displayName = "Spare", requirementType = ContractRequirementType.Recipe, targetId = "recipe_test", amount = 3, goldReward = 50 }
            };
            return new GameConfig(rarities, ingredients, recipes, economy, categories, labs, contracts);
        }

        private sealed class MinRandom : IRandomSource { public int Range(int minInclusive, int maxExclusive) => minInclusive; }
        private sealed class MaxRandom : IRandomSource { public int Range(int minInclusive, int maxExclusive) => maxExclusive - 1; }
        private sealed class FractionRandom : IRandomSource
        {
            private readonly float fraction;
            public FractionRandom(float fraction) => this.fraction = fraction;
            public int Range(int minInclusive, int maxExclusive) => minInclusive + (int)((maxExclusive - minInclusive) * fraction);
        }
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
