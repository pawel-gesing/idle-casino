using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DistilleryDiscovery.Tests
{
    public sealed class ContentExpansionV2Tests
    {
        private GameConfig config;
        [SetUp] public void SetUp() => config = ConfigLoader.LoadFromResources();

        [Test] public void Catalog_HasCanonicalCountsAndCompleteLocalization()
        {
            Assert.That(config.Ingredients.Count(x => x.enabled), Is.EqualTo(22));
            Assert.That(config.Groups.Count, Is.EqualTo(5));
            Assert.That(config.Recipes.Count(x => x.enabled), Is.EqualTo(175));
            Assert.That(config.ContractTemplates.Count(x => x.enabled), Is.InRange(30, 40));
            Assert.That(ConfigValidator.Validate(config), Is.Empty);
        }

        [Test] public void Requirements_SupportExactGroupDistinctAndAnyOf()
        {
            var selection = Ids("barley", "wheat", "honey");
            Assert.That(Eligible(new RecipeRequirementClause { type = RecipeRequirementType.Ingredient, ingredientId = Id("barley") }, selection), Is.True);
            Assert.That(Eligible(new RecipeRequirementClause { type = RecipeRequirementType.Group, groupId = "grain", count = 2 }, selection), Is.True);
            Assert.That(Eligible(new RecipeRequirementClause { type = RecipeRequirementType.DistinctGroup, groupId = "grain", count = 2 }, selection), Is.True);
            Assert.That(Eligible(new RecipeRequirementClause { type = RecipeRequirementType.AnyOf, ingredientIds = new List<string> { Id("apple"), Id("honey") } }, selection), Is.True);
            Assert.That(Eligible(new RecipeRequirementClause { type = RecipeRequirementType.Ingredient, ingredientId = Id("rye") }, selection), Is.False);
        }

        [Test] public void DistinctRequirement_DoesNotCountDuplicates()
        {
            var clause = new RecipeRequirementClause { type = RecipeRequirementType.DistinctGroup, groupId = "grain", count = 2 };
            Assert.That(Eligible(clause, Ids("barley", "barley", "honey")), Is.False);
        }

        [Test] public void OptionalBonus_AppliesOnceForDuplicateIngredient()
        {
            var tableBeer = config.Recipes.Single(x => x.id.StartsWith("recipe_001_"));
            var one = RecipeOutcomeResolver.Score(config, tableBeer, Ids("barley", "barley", "barley"));
            var mixed = RecipeOutcomeResolver.Score(config, tableBeer, Ids("barley", "barley", "hops"));
            Assert.That(one, Is.EqualTo(tableBeer.baseWeight + 20));
            Assert.That(mixed, Is.EqualTo(tableBeer.baseWeight + 20 + 15));
        }

        [Test] public void OutcomeProbabilities_AreNormalizedAndOnlyEligible()
        {
            var outcomes = RecipeOutcomeResolver.Calculate(config, Ids("barley", "hops", "honey"));
            Assert.That(outcomes.Sum(x => x.Probability), Is.EqualTo(1f).Within(.0001f));
            Assert.That(outcomes, Has.All.Matches<OutcomeChance>(x => RecipeOutcomeResolver.IsEligible(config, config.Recipe(x.RecipeId), Ids("barley", "hops", "honey"))));
        }

        [Test] public void ProductionRequirements_AllowNeutralFillersButRejectMissingClause()
        {
            var recipe = config.Recipes.Single(x => x.id.StartsWith("recipe_002_")); // barley is mandatory
            Assert.That(RecipeOutcomeResolver.IsEligible(config, recipe, Ids("barley", "mastic", "mastic")), Is.True);
            Assert.That(RecipeOutcomeResolver.IsEligible(config, recipe, Ids("wheat", "mastic", "mastic")), Is.False);
        }

        [Test] public void EveryRecipe_IsReachableWithExactlyThreeIngredients()
        {
            var ids = config.Ingredients.Where(x => x.enabled).Select(x => x.id).ToList();
            var unreachable = new List<string>();
            foreach (var recipe in config.Recipes.Where(x => x.enabled))
            {
                var found = false;
                for (var a = 0; a < ids.Count && !found; a++) for (var b = a; b < ids.Count && !found; b++) for (var c = b; c < ids.Count && !found; c++)
                    found = RecipeOutcomeResolver.IsEligible(config, recipe, new[] { ids[a], ids[b], ids[c] });
                if (!found) unreachable.Add(recipe.id);
            }
            Assert.That(unreachable, Is.Empty);
        }

        [Test] public void EpicAndLegendaryIngredients_RespectDirectAssociationCaps()
        {
            foreach (var ingredient in config.Ingredients.Where(x => x.rarityId is "rarity_epic" or "rarity_legendary"))
            {
                var direct = config.Recipes.Count(r => r.requirements.Any(x => x.ingredientId == ingredient.id || x.ingredientIds.Contains(ingredient.id)) || r.weightBonuses.Any(x => x.ingredientId == ingredient.id));
                Assert.That(direct, Is.LessThanOrEqualTo(ingredient.rarityId == "rarity_epic" ? 5 : 3), ingredient.id);
            }
        }

        [Test] public void DeliveryPool_CoversCanonicalIngredientsAndUsesConfiguredRolls()
        {
            var pool = config.Economy.deliveryPools.Single(x => x.id == "pool_base");
            Assert.That(pool.entries.Select(x => x.ingredientId), Is.EquivalentTo(config.Ingredients.Select(x => x.id)));
            Assert.That(pool.rolls, Is.EqualTo(8));
            Assert.That(pool.entries.Where(x => config.Ingredient(x.ingredientId).rarityId == "rarity_legendary").Max(x => x.weight), Is.LessThanOrEqualTo(1));
        }

        [Test] public void Contracts_GenerateOneAttainableTargetPerRoleWithoutDuplicates()
        {
            var game = new GameService(config, GameService.NewState(config), new MinRandom(), new ManualTime());
            Assert.That(game.State.activeContracts.Select(x => x.role), Is.EquivalentTo(ContractRole.All));
            Assert.That(game.State.activeContracts.Select(x => x.templateId), Is.Unique);
            Assert.That(game.State.activeContracts.Select(x => $"{x.objectiveType}:{x.targetId}"), Is.Unique);
            Assert.That(game.State.activeContracts, Has.All.Matches<ActiveContractState>(x => x.amount > 0 && x.goldReward >= 0));
        }

        [Test] public void ContractGeneration_RemainsUnblockedAcrossSeedsAndProgressStates()
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var state = GameService.NewState(config); state.laboratoryLevel = seed % config.LaboratoryLevels.Count + 1;
                var progressMode = seed % 3;
                var known = progressMode == 0 ? 0 : progressMode == 1 ? 50 : config.Recipes.Count;
                for (var i = 0; i < known; i++) state.recipes.Add(new PlayerRecipeState { recipeId = config.Recipes[i].id,
                    highestProductRarityId = progressMode == 2 ? "rarity_mythic" : "rarity_common", timesCreated = 1 });
                var game = new GameService(config, state, new SystemRandomSource(seed), new ManualTime());
                Assert.That(game.State.activeContracts.Select(x => x.role), Is.EquivalentTo(ContractRole.All), $"seed {seed}");
                Assert.That(game.State.activeContracts.Select(x => $"{x.objectiveType}:{x.targetId}"), Is.Unique, $"seed {seed}");
                if (progressMode == 2)
                {
                    Assert.That(game.State.activeContracts, Has.None.Matches<ActiveContractState>(x => x.objectiveType == ContractObjectiveType.Discover), $"seed {seed}");
                    Assert.That(game.State.activeContracts, Has.None.Matches<ActiveContractState>(x => x.objectiveType == ContractObjectiveType.ImproveRecord), $"seed {seed}");
                }
                foreach (var contract in game.State.activeContracts.Where(x => x.objectiveType == ContractObjectiveType.DistinctRecipes))
                {
                    var matching = config.Category(contract.targetId) != null
                        ? config.Recipes.Count(x => x.categoryId == contract.targetId)
                        : config.Recipes.Count(x => x.tags.Contains(contract.targetId));
                    Assert.That(matching, Is.GreaterThanOrEqualTo(contract.amount), $"seed {seed}, target {contract.targetId}");
                }
            }
        }

        [Test] public void ContractMatcher_SupportsEveryObjectiveAndFullProductionEvent()
        {
            var game = new GameService(config, GameService.NewState(config), new MinRandom(), new ManualTime());
            var recipe = config.Recipes.Single(x => x.id.StartsWith("recipe_001_"));
            var e = new ProductionEvent(recipe.id, "rarity_epic", recipe.categoryId, recipe.tags,
                Ids("barley", "hops", "orange"), new[] { "grain", "fruit" }, LaboratoryJobType.Experiment, true, true);
            var contracts = new[]
            {
                C(ContractObjectiveType.Recipe, recipe.id), C(ContractObjectiveType.Category, recipe.categoryId),
                C(ContractObjectiveType.Tag, "beer"), C(ContractObjectiveType.Rarity, "rarity_epic"),
                C(ContractObjectiveType.Ingredient, Id("hops")), C(ContractObjectiveType.Group, "grain"),
                C(ContractObjectiveType.Discover, recipe.id), C(ContractObjectiveType.DistinctRecipes, recipe.categoryId),
                C(ContractObjectiveType.RecipeMinRarity, recipe.id, "rarity_rare"), C(ContractObjectiveType.ImproveRecord, recipe.id),
                C(ContractObjectiveType.Source, LaboratoryJobType.Experiment)
            };
            Assert.That(contracts, Has.All.Matches<ActiveContractState>(x => game.Matches(x, e)));
        }

        [Test] public void ContractRewards_CoverExactGroupAndRaritySelectors()
        {
            var selectors = config.ContractTemplates.SelectMany(x => x.ingredientRewards).Select(x => x.selectorType).Distinct();
            Assert.That(selectors, Is.EquivalentTo(new[] { RewardSelectorType.Ingredient, RewardSelectorType.Group, RewardSelectorType.Rarity }));
        }

        [TestCase(RewardSelectorType.Ingredient)]
        [TestCase(RewardSelectorType.Group)]
        [TestCase(RewardSelectorType.Rarity)]
        public void ContractRewardSelectors_GrantOnlyValidCanonicalIngredient(string selector)
        {
            var template = config.ContractTemplates[0];
            var target = selector == RewardSelectorType.Ingredient ? Id("hops") : selector == RewardSelectorType.Group ? "grain" : "rarity_rare";
            template.ingredientRewards = new List<ContractRewardDefinition> { new() { selectorType = selector, targetId = target, weight = 1, minAmount = 2, maxAmount = 2 } };
            var game = new GameService(config, GameService.NewState(config), new MinRandom(), new ManualTime());
            game.State.activeContracts.Clear();
            game.State.activeContracts.Add(new ActiveContractState { instanceId = "reward", templateId = template.id, role = ContractRole.Basic,
                objectiveType = ContractObjectiveType.Discover, amount = 1, progress = 1, goldReward = 0 });
            game.State.pendingResult = new PendingResultState { saleValue = 0,
                contractProgress = new List<PendingContractProgress> { new() { contractId = "reward", completed = true } } };
            var result = game.ClaimPendingResult();
            Assert.That(result.IngredientRewards.Values.Single(), Is.EqualTo(2));
            Assert.That(result.IngredientRewards.Keys, Has.All.Matches<string>(id => config.Ingredient(id) != null));
        }

        [Test] public void ContractDistinctState_RoundTripsThroughSaveJson()
        {
            var state = GameService.NewState(config);
            state.activeContracts.Add(new ActiveContractState { instanceId = "x", templateId = config.ContractTemplates[0].id, role = ContractRole.Basic,
                objectiveType = ContractObjectiveType.DistinctRecipes, amount = 3, progress = 2, seenRecipeIds = new List<string> { "a", "b" } });
            var loaded = JsonUtility.FromJson<PlayerState>(JsonUtility.ToJson(state));
            Assert.That(loaded.activeContracts.Single().seenRecipeIds, Is.EquivalentTo(new[] { "a", "b" }));
        }

        [Test] public void VersionSevenMigration_PreservesValidProgressAndRegeneratesContracts()
        {
            var state = new PlayerState { version = 7, gold = 4321, laboratoryLevel = 2 };
            state.AddIngredient(Id("barley"), 7);
            var recipe = config.Recipes[0]; state.recipes.Add(new PlayerRecipeState { recipeId = recipe.id, highestProductRarityId = "rarity_rare", timesCreated = 4 });
            var game = new GameService(config, state, new MinRandom(), new ManualTime());
            Assert.That(game.State.version, Is.EqualTo(8));
            Assert.That(game.State.gold, Is.EqualTo(4321));
            Assert.That(game.State.AmountOf(Id("barley")), Is.EqualTo(7));
            Assert.That(game.State.RecipeState(recipe.id).timesCreated, Is.EqualTo(4));
            Assert.That(game.State.activeContracts.Select(x => x.role), Is.EquivalentTo(ContractRole.All));
        }

        [Test] public void FreeReroll_ReplacesOnlyRequestedRoleAndIsPersisted()
        {
            var game = new GameService(config, GameService.NewState(config), new MinRandom(), new ManualTime());
            var other = game.State.activeContracts.Where(x => x.role != ContractRole.Basic).Select(x => x.instanceId).ToList();
            var old = game.State.activeContracts.Single(x => x.role == ContractRole.Basic).instanceId;
            var replacement = game.RerollContract(ContractRole.Basic);
            Assert.That(replacement.instanceId, Is.Not.EqualTo(old));
            Assert.That(game.State.activeContracts.Where(x => x.role != ContractRole.Basic).Select(x => x.instanceId), Is.EquivalentTo(other));
            Assert.That(game.State.freeContractRerollsRemaining, Is.Zero);
        }

        private bool Eligible(RecipeRequirementClause clause, IReadOnlyList<string> ids)
        {
            var recipe = new RecipeDefinition { id = "test", enabled = true, baseWeight = 1, requirements = new List<RecipeRequirementClause> { clause } };
            return RecipeOutcomeResolver.IsEligible(config, recipe, ids);
        }
        private string Id(string baseId) => config.Ingredients.Single(x => x.baseIngredientId == baseId).id;
        private string[] Ids(params string[] baseIds) => baseIds.Select(Id).ToArray();
        private static ActiveContractState C(string type, string target, string rarity = null) => new() { objectiveType = type, targetId = target, minRarityId = rarity, amount = 1 };
        private sealed class MinRandom : IRandomSource { public int Range(int minInclusive, int maxExclusive) => minInclusive; }
        private sealed class ManualTime : ITimeProvider { public DateTime UtcNow { get; } = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc); }
    }
}
