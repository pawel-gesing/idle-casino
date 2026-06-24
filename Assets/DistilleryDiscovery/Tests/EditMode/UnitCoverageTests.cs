using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DistilleryDiscovery.Tests
{
    public sealed class UnitCoverageTests
    {
        [Test] public void OutcomeResolver_RejectsNullDisabledAndRequirementlessRecipes()
        {
            var config = MinimalConfig();

            Assert.That(RecipeOutcomeResolver.IsEligible(null, config.Recipe("recipe_a"), ThreeKnown()), Is.False);
            Assert.That(RecipeOutcomeResolver.IsEligible(config, null, ThreeKnown()), Is.False);
            Assert.That(RecipeOutcomeResolver.IsEligible(config, config.Recipe("recipe_a"), null), Is.False);
            Assert.That(RecipeOutcomeResolver.IsEligible(config, new RecipeDefinition { id = "disabled", enabled = false }, ThreeKnown()), Is.False);
            Assert.That(RecipeOutcomeResolver.IsEligible(config, new RecipeDefinition { id = "empty", enabled = true }, ThreeKnown()), Is.False);
        }

        [Test] public void OutcomeResolver_CalculateThrowsWhenNoRecipeMatches()
        {
            Assert.Throws<InvalidOperationException>(() =>
                RecipeOutcomeResolver.Calculate(MinimalConfig(), new[] { "ingredient_c", "ingredient_c", "ingredient_c" }));
        }

        [Test] public void OutcomeResolver_OrdersByWeightThenRecipeId()
        {
            var outcomes = RecipeOutcomeResolver.Calculate(MinimalConfig(), ThreeKnown());

            Assert.That(outcomes.Select(x => x.RecipeId), Is.EqualTo(new[] { "recipe_a", "recipe_b" }));
            Assert.That(outcomes.Select(x => x.Weight), Is.EqualTo(new[] { 15, 10 }));
            Assert.That(outcomes.Sum(x => x.Probability), Is.EqualTo(1f).Within(.0001f));
        }

        [Test] public void OutcomeResolver_DefaultsRequirementCountToOne()
        {
            var config = MinimalConfig();
            var recipe = new RecipeDefinition
            {
                id = "default_count",
                enabled = true,
                baseWeight = 1,
                requirements = new List<RecipeRequirementClause>
                {
                    new() { type = RecipeRequirementType.Ingredient, ingredientId = "ingredient_a", count = 0 }
                }
            };

            Assert.That(RecipeOutcomeResolver.IsEligible(config, recipe, ThreeKnown()), Is.True);
        }

        [Test] public void GameConfig_TextUsesLanguageFallbacks()
        {
            var config = MinimalConfig();

            Assert.That(config.Text("ui.title", "pl"), Is.EqualTo("Tytul"));
            Assert.That(config.Text("ui.title", "en"), Is.EqualTo("Title"));
            Assert.That(config.Text("missing", "en", "Fallback"), Is.EqualTo("Fallback"));
            Assert.That(config.Text("ui.empty", "en", "Fallback"), Is.EqualTo("Fallback"));
        }

        [Test] public void GameConfig_ReturnsCurrentAndNextMasteryLevels()
        {
            var config = MinimalConfig();

            Assert.That(config.MasteryLevelForCount(0).id, Is.EqualTo("starter"));
            Assert.That(config.MasteryLevelForCount(2).id, Is.EqualTo("adept"));
            Assert.That(config.NextMasteryLevel(0).id, Is.EqualTo("adept"));
            Assert.That(config.NextMasteryLevel(3), Is.Null);
        }

        [Test] public void PlayerState_AccumulatesInventoryAndProductsByIdentity()
        {
            var state = new PlayerState();

            state.AddIngredient("ingredient_a", 3);
            state.AddIngredient("ingredient_a", -1);
            state.AddProduct("recipe_a", "rarity_common", 10);
            state.AddProduct("recipe_a", "rarity_common", 10, 2);
            state.AddProduct("recipe_a", "rarity_common", 11);

            Assert.That(state.AmountOf("ingredient_a"), Is.EqualTo(2));
            Assert.That(state.ProductAmount("recipe_a", "rarity_common"), Is.EqualTo(3));
            Assert.That(state.products, Has.Count.EqualTo(2));
        }

        [Test] public void SaveService_LoadWithoutSaveThrowsAndResetClearsStorage()
        {
            var storage = new MemoryStorage();
            var saves = new SaveService(storage);

            Assert.That(saves.HasSave, Is.False);
            Assert.Throws<InvalidOperationException>(() => saves.Load());

            saves.Save(new PlayerState { gold = 123, languageCode = "pl" });
            Assert.That(saves.HasSave, Is.True);
            Assert.That(DateTime.TryParse(saves.Load().lastSavedAtUtc, out _), Is.True);
            Assert.That(saves.Load().gold, Is.EqualTo(123));

            saves.Reset();
            Assert.That(saves.HasSave, Is.False);
        }

        [Test] public void GameService_NewStateNormalizesLanguageAndSeedsEconomy()
        {
            var config = MinimalConfig();

            var polish = GameService.NewState(config, "pl");
            var fallback = GameService.NewState(config, "de");

            Assert.That(polish.languageCode, Is.EqualTo("pl"));
            Assert.That(fallback.languageCode, Is.EqualTo("en"));
            Assert.That(polish.gold, Is.EqualTo(config.Economy.startingGold));
            Assert.That(polish.freeContractRerollsRemaining, Is.EqualTo(config.Economy.freeContractRerolls));
            Assert.That(polish.laboratories.Single().id, Is.EqualTo("lab_1"));
        }

        [Test] public void GameService_ConstructorRejectsMissingDependencies()
        {
            var config = MinimalConfig();

            Assert.Throws<ArgumentNullException>(() => new GameService(null, new PlayerState()));
            Assert.Throws<ArgumentNullException>(() => new GameService(config, null));
        }

        [Test] public void GameService_NormalizesLegacyAndInvalidSaveData()
        {
            var config = MinimalConfig();
            var state = GameService.NewState(config, "fr");
            state.inventory.Add(new InventoryEntry { ingredientId = "ingredient_malt_common", amount = 2 });
            state.inventory.Add(new InventoryEntry { ingredientId = "ingredient_barley_common", amount = 1 });
            state.inventory.Add(new InventoryEntry { ingredientId = "missing", amount = 5 });
            state.inventory.Add(new InventoryEntry { ingredientId = "ingredient_a", amount = -2 });
            state.recipes.Add(new PlayerRecipeState { recipeId = "missing_recipe", highestProductRarityId = "rarity_common" });
            state.products.Add(new ProductEntry { recipeId = "recipe_a", rarityId = "missing_rarity", saleValue = 10, amount = 1 });

            var game = new GameService(config, state, new MinRandom(), new ManualTime());

            Assert.That(game.State.languageCode, Is.EqualTo("en"));
            Assert.That(game.State.AmountOf("ingredient_barley_common"), Is.EqualTo(3));
            Assert.That(game.State.AmountOf("missing"), Is.Zero);
            Assert.That(game.State.AmountOf("ingredient_a"), Is.Zero);
            Assert.That(game.State.recipes, Is.Empty);
            Assert.That(game.State.products, Is.Empty);
        }

        [Test] public void GameService_NormalizesLaboratoriesAndNextNumber()
        {
            var config = MinimalConfig();
            var state = GameService.NewState(config);
            state.laboratories.Clear();
            state.laboratories.Add(new PlayerLaboratoryState { id = "", level = -1 });
            state.laboratories.Add(new PlayerLaboratoryState { id = "", level = 99 });
            state.nextLaboratoryNumber = 1;

            var game = new GameService(config, state, new MinRandom(), new ManualTime());

            Assert.That(game.State.laboratories.Select(x => x.id), Is.EquivalentTo(new[] { "lab_1", "lab_2" }));
            Assert.That(game.State.laboratories.Select(x => x.level), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(game.State.nextLaboratoryNumber, Is.EqualTo(3));
        }

        [Test] public void GameService_RemovesDuplicateContractsByRoleAndInstance()
        {
            var config = MinimalConfig(withContract: true);
            var state = GameService.NewState(config);
            state.activeContracts.Add(new ActiveContractState { instanceId = "a", templateId = "contract_basic", role = ContractRole.Basic });
            state.activeContracts.Add(new ActiveContractState { instanceId = "b", templateId = "contract_basic", role = ContractRole.Basic });
            state.activeContracts.Add(new ActiveContractState { instanceId = "a", templateId = "contract_basic", role = ContractRole.Specialist });

            var game = new GameService(config, state, new MinRandom(), new ManualTime());

            Assert.That(game.State.activeContracts.Select(x => x.instanceId), Is.EquivalentTo(new[] { "a" }));
            Assert.That(game.State.activeContracts.Single().role, Is.EqualTo(ContractRole.Basic));
        }

        [Test] public void GameService_UpdateTimeResetsFutureDeliveryAnchor()
        {
            var config = MinimalConfig();
            var clock = new ManualTime();
            var state = GameService.NewState(config);
            state.freeDeliveryLastUpdateUtc = clock.UtcNow.AddHours(1).ToString("O");

            var game = new GameService(config, state, new MinRandom(), clock);

            Assert.That(game.State.availableFreeDeliveries, Is.Zero);
            Assert.That(game.State.freeDeliveryLastUpdateUtc, Is.EqualTo(clock.UtcNow.ToString("O")));
        }

        [Test] public void GameService_DebugAdvanceTimeRequiresAdjustableClock()
        {
            var config = MinimalConfig();
            var fixedGame = new GameService(config, GameService.NewState(config), new MinRandom(), new ManualTime());
            var adjustable = new AdjustableTimeProvider(new ManualTime());
            var adjustableGame = new GameService(config, GameService.NewState(config), new MinRandom(), adjustable);

            Assert.That(fixedGame.DebugAdvanceTime(TimeSpan.FromSeconds(60)), Is.False);
            Assert.That(adjustableGame.DebugAdvanceTime(TimeSpan.FromSeconds(60)), Is.True);
            Assert.That(adjustableGame.State.availableFreeDeliveries, Is.EqualTo(1));
        }

        [Test] public void GameService_ReceiveDeliveryThrowsWithoutConsumingAvailability()
        {
            var config = MinimalConfig();
            var clock = new ManualTime();
            var state = GameService.NewState(config);
            state.availableFreeDeliveries = 1;
            state.freeDeliveryLastUpdateUtc = clock.UtcNow.ToString("O");
            var game = new GameService(config, state, new MinRandom(), clock);

            Assert.Throws<InvalidOperationException>(() => game.ReceiveDelivery("missing_pool"));
            Assert.That(game.State.availableFreeDeliveries, Is.EqualTo(1));
        }

        [Test] public void GameService_PurchaseLaboratoryRequiresGoldAndStopsAtMaximum()
        {
            var config = MinimalConfig();
            var game = new GameService(config, GameService.NewState(config), new MinRandom(), new ManualTime());

            Assert.Throws<InvalidOperationException>(() => game.PurchaseLaboratory());
            Assert.That(game.LaboratoryCount, Is.EqualTo(1));

            game.State.gold = game.NextLaboratoryCost;
            game.PurchaseLaboratory();

            Assert.That(game.NextLaboratoryCost, Is.EqualTo(-1));
            Assert.Throws<InvalidOperationException>(() => game.PurchaseLaboratory());
        }

        [Test] public void GameService_UpgradeLaboratoryValidatesTargetGoldAndMaximum()
        {
            var config = MinimalConfig();
            var game = new GameService(config, GameService.NewState(config), new MinRandom(), new ManualTime());

            Assert.Throws<InvalidOperationException>(() => game.UpgradeLaboratory("missing_lab"));
            Assert.Throws<InvalidOperationException>(() => game.UpgradeLaboratory());

            game.State.gold = config.LaboratoryLevel(2).upgradeCost;
            var level = game.UpgradeLaboratory();

            Assert.That(level.level, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => game.UpgradeLaboratory());
        }

        [Test] public void GameService_TimeRemainingHandlesNullInvalidAndPastJobs()
        {
            var clock = new ManualTime();
            var game = new GameService(MinimalConfig(), GameService.NewState(MinimalConfig()), new MinRandom(), clock);

            Assert.That(game.TimeRemaining(null), Is.EqualTo(TimeSpan.Zero));
            Assert.That(game.TimeRemaining(new LaboratoryJobState { endTimeUtc = "bad-date" }), Is.EqualTo(TimeSpan.Zero));
            Assert.That(game.TimeRemaining(new LaboratoryJobState { endTimeUtc = clock.UtcNow.AddSeconds(-1).ToString("O") }), Is.EqualTo(TimeSpan.Zero));
            Assert.That(game.TimeRemaining(new LaboratoryJobState { endTimeUtc = clock.UtcNow.AddSeconds(10).ToString("O") }), Is.EqualTo(TimeSpan.FromSeconds(10)));
        }

        [Test] public void VisualCatalog_TintParsesValidHexAndFallsBackForInvalidDefinitions()
        {
            var config = MinimalConfig();
            var catalog = new VisualCatalog(config);

            Assert.That(catalog.Contains("visual.good"), Is.True);
            Assert.That(catalog.Contains(""), Is.False);
            Assert.That(catalog.Tint("visual.good", Color.white), Is.EqualTo(new Color(0f, 1f, 0f, 1f)));
            Assert.That(catalog.Tint("visual.bad", Color.magenta), Is.EqualTo(Color.magenta));
            Assert.That(catalog.Tint("missing", Color.cyan), Is.EqualTo(Color.cyan));
        }

        private static string[] ThreeKnown() => new[] { "ingredient_a", "ingredient_b", "ingredient_c" };

        private static GameConfig MinimalConfig(bool withContract = false)
        {
            var rarities = new List<RarityDefinition>
            {
                new() { id = "rarity_common", rank = 1, valueMultiplier = 1f, qualityScore = 1 },
                new() { id = "rarity_rare", rank = 2, valueMultiplier = 2f, qualityScore = 2 }
            };
            var groups = new List<IngredientGroupDefinition>
            {
                new() { id = "grain" },
                new() { id = "fruit" }
            };
            var ingredients = new List<IngredientDefinition>
            {
                new() { id = "ingredient_a", baseIngredientId = "a", displayName = "A", rarityId = "rarity_common", groupId = "grain" },
                new() { id = "ingredient_b", baseIngredientId = "b", displayName = "B", rarityId = "rarity_common", groupId = "grain" },
                new() { id = "ingredient_c", baseIngredientId = "c", displayName = "C", rarityId = "rarity_rare", groupId = "fruit" },
                new() { id = "ingredient_barley_common", baseIngredientId = "barley", displayName = "Barley", rarityId = "rarity_common", groupId = "grain" }
            };
            var recipes = new List<RecipeDefinition>
            {
                new()
                {
                    id = "recipe_a", categoryId = "category_drink", tags = new List<string> { "tag_a" }, baseValue = 10,
                    collectionRarityId = "rarity_common", baseWeight = 10,
                    requirements = new List<RecipeRequirementClause>
                    {
                        new() { type = RecipeRequirementType.Ingredient, ingredientId = "ingredient_a" }
                    },
                    weightBonuses = new List<RecipeWeightBonus> { new() { ingredientId = "ingredient_a", weight = 5 } }
                },
                new()
                {
                    id = "recipe_b", categoryId = "category_drink", tags = new List<string> { "tag_b" }, baseValue = 12,
                    collectionRarityId = "rarity_common", baseWeight = 10,
                    requirements = new List<RecipeRequirementClause>
                    {
                        new() { type = RecipeRequirementType.Ingredient, ingredientId = "ingredient_b" }
                    }
                }
            };
            var economy = new EconomyDefinition
            {
                startingGold = 5,
                ingredientsPerExperiment = 3,
                ingredientsPerProduction = 3,
                activeContractCount = 0,
                freeDeliveryIntervalSeconds = 60,
                maxStoredFreeDeliveries = 3,
                experimentDurationSeconds = 30,
                productionDurationSeconds = 20,
                contractRefreshSeconds = 600,
                freeContractRerolls = 1,
                productRarityWeights = new List<WeightedRarity> { new() { rarityId = "rarity_common", weight = 10 } },
                deliveryPools = new List<DeliveryPool>
                {
                    new()
                    {
                        id = "pool_base",
                        rolls = 1,
                        entries = new List<DeliveryEntry>
                        {
                            new() { ingredientId = "ingredient_a", weight = 1, minAmount = 1, maxAmount = 1 }
                        }
                    }
                }
            };
            var contracts = withContract
                ? new List<ContractTemplateDefinition>
                {
                    new()
                    {
                        id = "contract_basic", role = ContractRole.Basic, objectiveType = ContractObjectiveType.Discover,
                        targetSelector = ContractTargetSelector.None, minGoldReward = 1, maxGoldReward = 1
                    }
                }
                : new List<ContractTemplateDefinition>();
            return new GameConfig(rarities, ingredients, recipes, economy,
                new List<RecipeCategoryDefinition> { new() { id = "category_drink" } },
                new List<LaboratoryLevelDefinition>
                {
                    new() { level = 1, upgradeCost = 0, experimentSlots = 1, productionSlots = 1, experimentTimeMultiplier = 1f, productionTimeMultiplier = 1f },
                    new() { level = 2, upgradeCost = 25, experimentSlots = 1, productionSlots = 1, experimentTimeMultiplier = .8f, productionTimeMultiplier = .8f }
                },
                contracts,
                new List<LocalizationDefinition>
                {
                    new() { key = "ui.title", pl = "Tytul", en = "Title" },
                    new() { key = "ui.empty", pl = "", en = "" }
                },
                new List<MasteryLevelDefinition>
                {
                    new() { id = "starter", requiredProductionCount = 0 },
                    new() { id = "adept", requiredProductionCount = 2, rarityBonus = .25f }
                },
                groups,
                new List<VisualDefinition>
                {
                    new() { id = "visual.good", tintHex = "#00FF00" },
                    new() { id = "visual.bad", tintHex = "not-a-color" },
                    new() { id = "" }
                });
        }

        private sealed class ManualTime : ITimeProvider
        {
            public DateTime UtcNow { get; private set; } = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        private sealed class MinRandom : IRandomSource
        {
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
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
