using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DistilleryDiscovery.Tests
{
    public sealed class GameServiceTests
    {
        [Test] public void Configuration_LoadsTimingValues()
        {
            var config = ConfigLoader.LoadFromResources();
            Assert.That(config.Economy.freeDeliveryIntervalSeconds, Is.EqualTo(1200));
            Assert.That(config.Economy.experimentDurationSeconds, Is.EqualTo(3600));
            Assert.That(config.Economy.productionDurationSeconds, Is.EqualTo(1800));
            Assert.That(config.Economy.maxStoredFreeDeliveries, Is.EqualTo(3));
            Assert.That(config.LaboratoryLevel(3).experimentSlots, Is.EqualTo(2));
            Assert.That(config.LaboratoryLevel(3).productionSlots, Is.EqualTo(2));
        }

        [Test] public void Validator_RejectsInvalidTimingValues()
        {
            var config = MinimalConfig(); config.Economy.experimentDurationSeconds = 0;
            Assert.That(ConfigValidator.Validate(config), Has.Some.Contains("durations"));
        }

        [Test] public void FreeDelivery_IsUnavailableBeforeInterval()
        {
            var (game, clock) = CreateGame(); clock.AdvanceSeconds(59); game.UpdateTime();
            Assert.That(game.State.availableFreeDeliveries, Is.Zero);
        }

        [Test] public void FreeDelivery_BecomesAvailableAfterInterval()
        {
            var (game, clock) = CreateGame(); clock.AdvanceSeconds(60); game.UpdateTime();
            Assert.That(game.State.availableFreeDeliveries, Is.EqualTo(1));
        }

        [Test] public void OfflineProgress_AccumulatesMultipleDeliveries()
        {
            var (game, clock) = CreateGame(); clock.AdvanceSeconds(120); game.UpdateTime();
            Assert.That(game.State.availableFreeDeliveries, Is.EqualTo(2));
        }

        [Test] public void OfflineProgress_RespectsStoredDeliveryLimit()
        {
            var (game, clock) = CreateGame(); clock.AdvanceSeconds(600); game.UpdateTime();
            Assert.That(game.State.availableFreeDeliveries, Is.EqualTo(3));
        }

        [Test] public void OfflineTimeCap_IsNotAppliedAgainOnRepeatedUpdate()
        {
            var config = MinimalConfig(); config.Economy.maxOfflineProgressSeconds = 120;
            var clock = new ManualTime(); var game = new GameService(config, GameService.NewState(config), new MinRandom(), clock);
            clock.AdvanceSeconds(600); game.UpdateTime(); var first = game.State.availableFreeDeliveries; game.UpdateTime();
            Assert.That(first, Is.EqualTo(2));
            Assert.That(game.State.availableFreeDeliveries, Is.EqualTo(first));
        }

        [Test] public void ClaimingDelivery_DecrementsAvailabilityAndAddsItems()
        {
            var (game, clock) = CreateGame(); clock.AdvanceSeconds(60); game.UpdateTime();
            var result = game.ReceiveDelivery();
            Assert.That(game.State.availableFreeDeliveries, Is.Zero);
            Assert.That(result.Items["ingredient_test"], Is.EqualTo(2));
        }

        [Test] public void ClaimingDelivery_StartsNextTimerAtClaimTime()
        {
            var (game, clock) = CreateGame(); clock.AdvanceSeconds(90); game.UpdateTime();
            game.ReceiveDelivery();
            Assert.That(game.TimeUntilNextFreeDelivery, Is.EqualTo(TimeSpan.FromSeconds(60)).Within(TimeSpan.FromSeconds(1)));
        }

        [Test] public void StartingExperiment_ConsumesIngredientsAndOccupiesSlot()
        {
            var (game, _) = CreateGame(3); var job = game.StartExperiment(Three());
            Assert.That(game.State.AmountOf("ingredient_test"), Is.Zero);
            Assert.That(game.AvailableExperimentSlots, Is.Zero);
            Assert.That(job.status, Is.EqualTo(LaboratoryJobStatus.Running));
        }

        [Test] public void CannotStartJobWithoutFreeSlot()
        {
            var (game, _) = CreateGame(6); game.StartExperiment(Three());
            Assert.Throws<InvalidOperationException>(() => game.StartExperiment(Three()));
            Assert.That(game.State.AmountOf("ingredient_test"), Is.EqualTo(3));
        }

        [Test] public void Job_IsNotReadyBeforeEndTime()
        {
            var (game, clock) = CreateGame(3); var job = game.StartExperiment(Three()); clock.AdvanceSeconds(29); game.UpdateTime();
            Assert.That(job.status, Is.EqualTo(LaboratoryJobStatus.Running));
            Assert.Throws<InvalidOperationException>(() => game.ClaimLaboratoryJob(job.id));
        }

        [Test] public void Job_IsReadyAfterEndTime()
        {
            var (game, clock) = CreateGame(3); var job = game.StartExperiment(Three()); clock.AdvanceSeconds(30); game.UpdateTime();
            Assert.That(job.status, Is.EqualTo(LaboratoryJobStatus.Completed));
        }

        [Test] public void ClaimingExperiment_AddsProductToRecipeBookOnlyAtClaim()
        {
            var (game, clock) = CreateGame(3); var job = game.StartExperiment(Three());
            Assert.That(game.State.RecipeState("recipe_test"), Is.Null);
            clock.AdvanceSeconds(30); game.ClaimLaboratoryJob(job.id);
            Assert.That(game.State.RecipeState("recipe_test").timesCreated, Is.EqualTo(1));
            Assert.That(game.State.pendingResult.source, Is.EqualTo(LaboratoryJobType.Experiment));
        }

        [Test] public void ClaimingProduction_AddsGuaranteedRecipeProduct()
        {
            var (game, clock) = CreateGame(3); Discover(game); var job = game.StartProduction("recipe_test", Three());
            clock.AdvanceSeconds(20); var result = game.ClaimLaboratoryJob(job.id);
            Assert.That(result.RecipeId, Is.EqualTo("recipe_test"));
            Assert.That(game.State.RecipeState("recipe_test").timesCreated, Is.EqualTo(1));
        }

        [Test] public void ResultRandomization_IsDeferredUntilClaim()
        {
            var random = new CountingRandom(); var (game, clock) = CreateGame(3, random); var before = random.Calls;
            var job = game.StartExperiment(Three()); Assert.That(random.Calls, Is.EqualTo(before));
            clock.AdvanceSeconds(30); game.ClaimLaboratoryJob(job.id);
            Assert.That(random.Calls, Is.GreaterThan(before));
        }

        [Test] public void SaveLoad_PreservesActiveJobs()
        {
            var (game, _) = CreateGame(3); var job = game.StartExperiment(Three()); var saves = new SaveService(new MemoryStorage());
            saves.Save(game.State); var loaded = saves.Load();
            Assert.That(loaded.laboratoryJobs.Single().id, Is.EqualTo(job.id));
            Assert.That(loaded.laboratoryJobs.Single().ingredientIds, Is.EquivalentTo(Three()));
        }

        [Test] public void LoadingAfterJobEnd_MarksItReadyButDoesNotResolveIt()
        {
            var (game, clock) = CreateGame(3); game.StartExperiment(Three()); var saves = new SaveService(new MemoryStorage()); saves.Save(game.State);
            clock.AdvanceSeconds(30); var loadedGame = new GameService(game.Config, saves.Load(), new MinRandom(), clock);
            Assert.That(loadedGame.State.laboratoryJobs.Single().status, Is.EqualTo(LaboratoryJobStatus.Completed));
            Assert.That(loadedGame.State.RecipeState("recipe_test"), Is.Null);
        }

        [Test] public void VersionFiveSaveWithoutTimerFields_MigratesWithoutLosingProgress()
        {
            var config = MinimalConfig(); var clock = new ManualTime();
            var old = new PlayerState { version = 5, gold = 777 }; old.AddIngredient("ingredient_test", 9); Discover(old);
            var game = new GameService(config, old, new MinRandom(), clock);
            Assert.That(game.State.version, Is.EqualTo(9));
            Assert.That(game.State.gold, Is.EqualTo(777));
            Assert.That(game.State.AmountOf("ingredient_test"), Is.EqualTo(9));
            Assert.That(game.State.RecipeState("recipe_test"), Is.Not.Null);
            Assert.That(game.State.availableFreeDeliveries, Is.Zero);
        }

        [Test] public void ProductionDuration_UsesConfiguredMultiplier()
        {
            var config = MinimalConfig(); config.LaboratoryLevel(2).productionTimeMultiplier = .75f;
            var clock = new ManualTime(); var state = GameService.NewState(config); state.laboratoryLevel = 2; state.AddIngredient("ingredient_test", 3); Discover(state);
            var game = new GameService(config, state, new MinRandom(), clock); var job = game.StartProduction("recipe_test", Three());
            Assert.That(DateTime.Parse(job.endTimeUtc) - DateTime.Parse(job.startTimeUtc), Is.EqualTo(TimeSpan.FromSeconds(15)));
        }

        [Test] public void CollectAll_ClaimsEveryReadyJob()
        {
            var (game, clock) = CreateGame(6); Discover(game); game.StartExperiment(Three()); game.StartProduction("recipe_test", Three());
            clock.AdvanceSeconds(30); var result = game.CollectAll();
            Assert.That(result.JobsCollected, Is.EqualTo(2));
            Assert.That(game.State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Claimed), Is.EqualTo(2));
        }

        [Test] public void CollectAll_DoesNotClaimRunningJobs()
        {
            var (game, clock) = CreateGame(6); Discover(game); var ready = game.StartProduction("recipe_test", Three()); var running = game.StartExperiment(Three());
            clock.AdvanceSeconds(20); var result = game.CollectAll();
            Assert.That(result.JobsCollected, Is.EqualTo(1));
            Assert.That(ready.status, Is.EqualTo(LaboratoryJobStatus.Claimed));
            Assert.That(running.status, Is.EqualTo(LaboratoryJobStatus.Running));
        }

        [Test] public void CollectAll_UpdatesRecipeBookAndMastery()
        {
            var (game, clock) = CreateGame(3); game.StartExperiment(Three()); clock.AdvanceSeconds(30); var result = game.CollectAll();
            Assert.That(result.NewRecipesDiscovered, Is.EqualTo(1));
            Assert.That(game.State.RecipeState("recipe_test").timesCreated, Is.EqualTo(1));
        }

        [Test] public void CollectAll_AddsProductsToInventory()
        {
            var (game, clock) = CreateGame(3); game.StartExperiment(Three()); clock.AdvanceSeconds(30); var result = game.CollectAll();
            Assert.That(result.ProductsAdded, Is.EqualTo(1));
            Assert.That(game.State.products.Sum(x => x.amount), Is.EqualTo(1));
        }

        [Test] public void CollectAll_UsesTheSameProgressAndRewardFlowAsIndividualClaim()
        {
            var (single, singleClock) = CreateGame(3); var singleJob = single.StartExperiment(Three()); singleClock.AdvanceSeconds(30);
            single.ClaimLaboratoryJob(singleJob.id); var singleReward = single.ClaimPendingResult();
            var (batch, batchClock) = CreateGame(3); batch.StartExperiment(Three()); batchClock.AdvanceSeconds(30); var batchResult = batch.CollectAll();
            Assert.That(batchResult.GoldGained, Is.EqualTo(singleReward.TotalGold));
            Assert.That(batch.State.experimentsCompleted, Is.EqualTo(single.State.experimentsCompleted));
            Assert.That(batch.State.RecipeState("recipe_test").timesCreated, Is.EqualTo(single.State.RecipeState("recipe_test").timesCreated));
            Assert.That(batch.State.products.Sum(x => x.amount), Is.EqualTo(single.State.products.Sum(x => x.amount)));
        }

        [Test] public void OfflineSummary_ReportsAccumulatedDeliveries()
        {
            var config = MinimalConfig(); var clock = new ManualTime(); var state = GameService.NewState(config);
            state.freeDeliveryLastUpdateUtc = clock.UtcNow.ToString("O"); state.lastSavedAtUtc = clock.UtcNow.ToString("O"); state.version = 7;
            clock.AdvanceSeconds(120); var game = new GameService(config, state, new MinRandom(), clock);
            Assert.That(game.LastOfflineSummary.DeliveriesGained, Is.EqualTo(2));
            Assert.That(game.LastOfflineSummary.Elapsed, Is.EqualTo(TimeSpan.FromSeconds(120)));
        }

        [Test] public void OfflineSummary_ReportsCompletedJobTypesWithoutClaiming()
        {
            var (game, clock) = CreateGame(6); Discover(game); game.StartExperiment(Three()); game.StartProduction("recipe_test", Three());
            game.State.lastSavedAtUtc = clock.UtcNow.ToString("O"); clock.AdvanceSeconds(30);
            var loaded = new GameService(game.Config, game.State, new MinRandom(), clock);
            Assert.That(loaded.LastOfflineSummary.JobsCompleted, Is.EqualTo(2));
            Assert.That(loaded.LastOfflineSummary.ExperimentsReady, Is.EqualTo(1));
            Assert.That(loaded.LastOfflineSummary.ProductionsReady, Is.EqualTo(1));
            Assert.That(loaded.State.laboratoryJobs, Has.All.Matches<LaboratoryJobState>(x => x.status == LaboratoryJobStatus.Completed));
        }

        [Test] public void LaboratoryLevel_ChangesExperimentSlots()
        {
            var config = MinimalConfig(); var state = GameService.NewState(config); state.laboratoryLevel = 2;
            Assert.That(new GameService(config, state).ExperimentSlotCount, Is.EqualTo(2));
        }

        [Test] public void LaboratoryLevel_ChangesProductionSlots()
        {
            var config = MinimalConfig(); var state = GameService.NewState(config); state.laboratoryLevel = 3;
            Assert.That(new GameService(config, state).ProductionSlotCount, Is.EqualTo(2));
        }

        [Test] public void LaboratoryLevel_ShortensExperimentDuration()
        {
            var config = MinimalConfig(); var clock = new ManualTime(); var state = GameService.NewState(config); state.laboratoryLevel = 2; state.AddIngredient("ingredient_test", 3);
            var job = new GameService(config, state, new MinRandom(), clock).StartExperiment(Three());
            Assert.That(DateTime.Parse(job.endTimeUtc) - DateTime.Parse(job.startTimeUtc), Is.EqualTo(TimeSpan.FromSeconds(27)));
        }

        [Test] public void Validator_RejectsInvalidLaboratoryLevel()
        {
            var config = MinimalConfig(); config.LaboratoryLevel(2).experimentSlots = 0;
            Assert.That(ConfigValidator.Validate(config), Has.Some.Contains("slots"));
        }

        [Test] public void SaveLoad_PreservesLaboratoryLevelAndJobs()
        {
            var (game, _) = CreateGame(3); game.State.laboratoryLevel = 2; var job = game.StartExperiment(Three());
            var saves = new SaveService(new MemoryStorage()); saves.Save(game.State); var loaded = saves.Load();
            Assert.That(loaded.laboratoryLevel, Is.EqualTo(2));
            Assert.That(loaded.laboratoryJobs.Single().id, Is.EqualTo(job.id));
        }

        [Test] public void ClaimingPendingReward_PreservesExistingContractAndGoldFlow()
        {
            var (game, clock) = CreateGame(3); var startGold = game.State.gold; var job = game.StartExperiment(Three()); clock.AdvanceSeconds(30);
            var product = game.ClaimLaboratoryJob(job.id); var claim = game.ClaimPendingResult();
            Assert.That(claim.ProductGold, Is.EqualTo(product.SaleValue));
            Assert.That(game.State.gold, Is.GreaterThan(startGold));
        }

        [Test] public void Preview_SumsWeightsWithoutConsumingInventory()
        {
            var (game, _) = CreateGame(3); var preview = game.Preview(Three());
            Assert.That(preview.Single().Weight, Is.EqualTo(10));
            Assert.That(game.State.AmountOf("ingredient_test"), Is.EqualTo(3));
        }

        [Test] public void Production_RequiresDiscoveredRecipe()
        {
            var (game, _) = CreateGame(3);
            Assert.Throws<InvalidOperationException>(() => game.StartProduction("recipe_test", Three()));
        }

        [Test] public void Production_RejectsSelectionMissingRequiredCountWithoutConsumingAny()
        {
            var (game, _) = CreateGame(2); Discover(game); game.State.AddIngredient("ingredient_invalid", 1);
            Assert.Throws<InvalidOperationException>(() => game.StartProduction("recipe_test", new[] { "ingredient_test", "ingredient_test", "ingredient_invalid" }));
            Assert.That(game.State.AmountOf("ingredient_test"), Is.EqualTo(2));
        }

        [Test] public void LaboratoryUpgrade_UsesConfiguredGoldCost()
        {
            var (game, _) = CreateGame(); game.State.gold = 20; game.UpgradeLaboratory();
            Assert.That(game.State.laboratoryLevel, Is.EqualTo(2));
            Assert.That(game.State.gold, Is.EqualTo(10));
        }

        [Test] public void LaboratoryPurchase_AddsIndependentExperimentCapacity()
        {
            var (game, _) = CreateGame(6); game.State.gold = 20; var lab = game.PurchaseLaboratory();
            var first = game.StartExperiment(Three(), "lab_1");
            var second = game.StartExperiment(Three(), lab.id);
            Assert.That(first.laboratoryId, Is.EqualTo("lab_1"));
            Assert.That(second.laboratoryId, Is.EqualTo(lab.id));
            Assert.That(game.ExperimentSlotCount, Is.EqualTo(2));
            Assert.That(game.AvailableExperimentSlots, Is.Zero);
        }

        [Test] public void InvalidEmptyPendingResult_IsDiscardedOnLoad()
        {
            var config = MinimalConfig(); var state = GameService.NewState(config); state.pendingResult = new PendingResultState();
            var game = new GameService(config, state, new MinRandom(), new ManualTime());
            Assert.That(game.State.pendingResult, Is.Null);
        }

        [Test] public void ProductClaim_IncrementsConfiguredMasteryCount()
        {
            var (game, clock) = CreateGame(3); var job = game.StartExperiment(Three()); clock.AdvanceSeconds(30); game.ClaimLaboratoryJob(job.id);
            Assert.That(game.MasteryLevel("recipe_test").id, Is.EqualTo("first"));
        }

        [Test] public void HigherMastery_IncreasesHigherRarityWeight()
        {
            var (game, _) = CreateGame(3); Discover(game); var state = game.State.RecipeState("recipe_test"); state.timesCreated = 1;
            var before = game.ProductRarityWeights(Three(), "recipe_test").Single(x => x.rarityId == "rarity_rare").weight;
            state.timesCreated = 2;
            var after = game.ProductRarityWeights(Three(), "recipe_test").Single(x => x.rarityId == "rarity_rare").weight;
            Assert.That(after, Is.GreaterThan(before));
        }

        [Test] public void PendingResult_BlocksStartingAnotherJob()
        {
            var (game, clock) = CreateGame(6); var job = game.StartExperiment(Three()); clock.AdvanceSeconds(30); game.ClaimLaboratoryJob(job.id);
            Assert.Throws<InvalidOperationException>(() => game.StartExperiment(Three()));
        }

        [Test] public void CompletedContract_IsPaidWithProductReward()
        {
            var (game, clock) = CreateGame(3); game.State.activeContracts.Add(new ActiveContractState { instanceId = "contract_recipe", templateId = "contract_recipe", role = ContractRole.Basic, objectiveType = ContractObjectiveType.Recipe, targetId = "recipe_test", amount = 1, goldReward = 40 });
            var job = game.StartExperiment(Three()); clock.AdvanceSeconds(30); game.ClaimLaboratoryJob(job.id); var claim = game.ClaimPendingResult();
            Assert.That(claim.ContractGold, Is.EqualTo(40));
            Assert.That(claim.CompletedContractIds, Does.Contain("contract_recipe"));
        }

        [Test] public void SaveLoad_PreservesPendingResult()
        {
            var (game, clock) = CreateGame(3); var job = game.StartExperiment(Three()); clock.AdvanceSeconds(30); game.ClaimLaboratoryJob(job.id);
            var saves = new SaveService(new MemoryStorage()); saves.Save(game.State);
            Assert.That(saves.Load().pendingResult.recipeId, Is.EqualTo("recipe_test"));
        }

        private static (GameService game, ManualTime clock) CreateGame(int ingredients = 0, IRandomSource random = null)
        {
            var config = MinimalConfig(); var clock = new ManualTime(); var state = GameService.NewState(config); state.AddIngredient("ingredient_test", ingredients);
            return (new GameService(config, state, random ?? new MinRandom(), clock), clock);
        }

        private static string[] Three() => new[] { "ingredient_test", "ingredient_test", "ingredient_test" };
        private static void Discover(GameService game) => Discover(game.State);
        private static void Discover(PlayerState state) => state.recipes.Add(new PlayerRecipeState { recipeId = "recipe_test", highestProductRarityId = "rarity_common" });

        private static GameConfig MinimalConfig()
        {
            var rarities = new List<RarityDefinition> { new() { id = "rarity_common", rank = 1, valueMultiplier = 1f }, new() { id = "rarity_rare", rank = 2, valueMultiplier = 2f } };
            var ingredients = new List<IngredientDefinition> {
                new() { id = "ingredient_test", displayName = "Test", rarityId = "rarity_common", groupId = "group_test" },
                new() { id = "ingredient_invalid", displayName = "Invalid", rarityId = "rarity_common" }
            };
            var recipes = new List<RecipeDefinition> { new() { id = "recipe_test", categoryId = "category_test", collectionRarityId = "rarity_common", baseValue = 10, baseWeight = 10, tags = new List<string> { "test" }, requirements = new List<RecipeRequirementClause> { new() { type = RecipeRequirementType.Ingredient, ingredientId = "ingredient_test", count = 3 } } } };
            var economy = new EconomyDefinition {
                ingredientsPerExperiment = 3, ingredientsPerProduction = 3, activeContractCount = 0,
                freeDeliveryIntervalSeconds = 60, maxStoredFreeDeliveries = 3, contractRefreshSeconds = 86400,
                experimentDurationSeconds = 30, productionDurationSeconds = 20,
                productRarityWeights = new List<WeightedRarity> { new() { rarityId = "rarity_common", weight = 100 }, new() { rarityId = "rarity_rare", weight = 100 } },
                deliveryPools = new List<DeliveryPool> { new() { id = "pool_base", rolls = 2, entries = new List<DeliveryEntry> { new() { ingredientId = "ingredient_test", weight = 1, minAmount = 1, maxAmount = 1 } } } }
            };
            return new GameConfig(rarities, ingredients, recipes, economy,
                new List<RecipeCategoryDefinition> { new() { id = "category_test" } },
                new List<LaboratoryLevelDefinition> {
                    new() { level = 1, experimentSlots = 1, productionSlots = 1, experimentTimeMultiplier = 1f, productionTimeMultiplier = 1f },
                    new() { level = 2, upgradeCost = 10, experimentSlots = 2, productionSlots = 1, experimentTimeMultiplier = .9f, productionTimeMultiplier = 1f },
                    new() { level = 3, upgradeCost = 20, experimentSlots = 2, productionSlots = 2, experimentTimeMultiplier = .8f, productionTimeMultiplier = .75f }
                },
                new List<ContractTemplateDefinition> { new() { id = "contract_recipe", role = ContractRole.Basic, objectiveType = ContractObjectiveType.Recipe, targetSelector = ContractTargetSelector.DiscoveredRecipe, selectionWeight = 1, minAmount = 1, maxAmount = 1, minGoldReward = 40, maxGoldReward = 40 } },
                masteryLevels: new List<MasteryLevelDefinition> { new() { id = "first", requiredProductionCount = 1 }, new() { id = "second", requiredProductionCount = 2, rarityBonus = .2f } },
                groups: new List<IngredientGroupDefinition> { new() { id = "group_test" } });
        }

        private sealed class ManualTime : ITimeProvider
        {
            public DateTime UtcNow { get; private set; } = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            public void AdvanceSeconds(int seconds) => UtcNow = UtcNow.AddSeconds(seconds);
        }
        private sealed class MinRandom : IRandomSource { public int Range(int minInclusive, int maxExclusive) => minInclusive; }
        private sealed class CountingRandom : IRandomSource { public int Calls; public int Range(int minInclusive, int maxExclusive) { Calls++; return minInclusive; } }
        private sealed class MemoryStorage : IStateStorage { private string json; public bool Exists => json != null; public void Write(string value) => json = value; public string Read() => json; public void Delete() => json = null; }
    }
}
