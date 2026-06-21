using System;
using System.Collections.Generic;
using System.Linq;

namespace DistilleryDiscovery
{
    public static class ConfigValidator
    {
        public static void ValidateOrThrow(GameConfig config)
        {
            var errors = Validate(config);
            if (errors.Count > 0) throw new InvalidOperationException("Game configuration errors:\n- " + string.Join("\n- ", errors));
        }

        public static List<string> Validate(GameConfig config)
        {
            var errors = new List<string>();
            CheckUnique(config.Rarities.Select(x => x.id), "rarity", errors);
            CheckUnique(config.Ingredients.Select(x => x.id), "ingredient", errors);
            CheckUnique(config.Recipes.Select(x => x.id), "recipe", errors);
            CheckUnique(config.Categories.Select(x => x.id), "recipe category", errors);
            CheckUnique(config.Contracts.Select(x => x.id), "contract", errors);
            CheckUnique(config.MasteryLevels.Select(x => x.id), "mastery level", errors);
            CheckUnique(config.Localizations.Select(x => x.key), "localization", errors);
            CheckUnique(config.Economy.deliveryPools.Select(x => x.id), "delivery pool", errors);
            var rarityIds = config.Rarities.Select(x => x.id).ToHashSet();
            var recipeIds = config.Recipes.Select(x => x.id).ToHashSet();
            var categoryIds = config.Categories.Select(x => x.id).ToHashSet();
            var ingredientIds = config.Ingredients.Select(x => x.id).ToHashSet();

            if (config.Economy.ingredientsPerExperiment <= 0 || config.Economy.ingredientsPerProduction <= 0)
                errors.Add("Experiment and production ingredient counts must be positive.");
            if (config.Economy.activeContractCount < 0) errors.Add("Active contract count cannot be negative.");
            if (config.Economy.freeDeliveryIntervalSeconds <= 0 || config.Economy.freeDeliveryMinItems <= 0 ||
                config.Economy.freeDeliveryMaxItems < config.Economy.freeDeliveryMinItems || config.Economy.maxStoredFreeDeliveries <= 0)
                errors.Add("Free delivery timing and item counts must be positive and ordered.");
            if (config.Economy.experimentDurationSeconds <= 0 || config.Economy.productionDurationSeconds <= 0)
                errors.Add("Laboratory durations must be positive.");
            if (config.Economy.maxOfflineProgressSeconds < 0) errors.Add("Offline cap cannot be negative.");

            foreach (var ingredient in config.Ingredients)
            {
                if (!rarityIds.Contains(ingredient.rarityId)) errors.Add($"Ingredient {ingredient.id} uses unknown rarity {ingredient.rarityId}.");
                foreach (var outcome in ingredient.outcomeWeights)
                {
                    if (!recipeIds.Contains(outcome.recipeId)) errors.Add($"Ingredient {ingredient.id} references unknown recipe {outcome.recipeId}.");
                    if (outcome.weight <= 0) errors.Add($"Ingredient {ingredient.id} has non-positive outcome weight.");
                }
            }
            foreach (var recipe in config.Recipes)
            {
                if (!rarityIds.Contains(recipe.collectionRarityId)) errors.Add($"Recipe {recipe.id} uses unknown rarity {recipe.collectionRarityId}.");
                if (!categoryIds.Contains(recipe.categoryId)) errors.Add($"Recipe {recipe.id} uses unknown category {recipe.categoryId}.");
                if (!config.Ingredients.Any(i => i.outcomeWeights.Any(o => o.recipeId == recipe.id && o.weight > 0))) errors.Add($"Recipe {recipe.id} has no contributing ingredient.");
                if (recipe.baseValue <= 0) errors.Add($"Recipe {recipe.id} has non-positive base value.");
            }
            foreach (var rarity in config.Economy.productRarityWeights)
            {
                if (!rarityIds.Contains(rarity.rarityId)) errors.Add($"Product roll uses unknown rarity {rarity.rarityId}.");
                if (rarity.weight <= 0) errors.Add($"Product rarity {rarity.rarityId} has non-positive weight.");
            }
            foreach (var pool in config.Economy.deliveryPools)
            {
                if (pool.rolls <= 0) errors.Add($"Delivery pool {pool.id} has non-positive roll count.");
                if (pool.entries.Count == 0) errors.Add($"Delivery pool {pool.id} is empty.");
                foreach (var entry in pool.entries)
                {
                    if (!ingredientIds.Contains(entry.ingredientId)) errors.Add($"Delivery pool {pool.id} references unknown ingredient {entry.ingredientId}.");
                    if (entry.weight <= 0 || entry.minAmount <= 0 || entry.maxAmount < entry.minAmount) errors.Add($"Delivery entry {entry.ingredientId} has invalid values.");
                }
            }

            var levels = config.LaboratoryLevels;
            if (levels.Count == 0 || levels[0].level != 1) errors.Add("Laboratory configuration must start at level 1.");
            for (var i = 0; i < levels.Count; i++)
            {
                if (levels[i].level != i + 1) errors.Add("Laboratory levels must be contiguous.");
                if ((i > 0 && levels[i].upgradeCost <= 0) || levels[i].upgradeCost < 0 || levels[i].productQualityBonus < 0f ||
                    levels[i].experimentSlots <= 0 || levels[i].productionSlots <= 0 ||
                    levels[i].experimentTimeMultiplier <= 0f || levels[i].productionTimeMultiplier <= 0f)
                    errors.Add($"Laboratory level {levels[i].level} has invalid cost, slots, or time multipliers.");
                if (i > 0 && levels[i].productQualityBonus < levels[i - 1].productQualityBonus) errors.Add("Laboratory quality bonuses cannot decrease.");
            }

            foreach (var contract in config.Contracts)
            {
                if (contract.amount <= 0 || contract.goldReward <= 0) errors.Add($"Contract {contract.id} has invalid amount or reward.");
                contract.ingredientRewards ??= new List<IngredientRewardDefinition>();
                foreach (var reward in contract.ingredientRewards)
                    if (!ingredientIds.Contains(reward.ingredientId) || reward.minAmount <= 0 || reward.maxAmount < reward.minAmount)
                        errors.Add($"Contract {contract.id} has invalid ingredient reward {reward.ingredientId}.");
                switch (contract.requirementType)
                {
                    case ContractRequirementType.Recipe:
                        if (!recipeIds.Contains(contract.targetId)) errors.Add($"Contract {contract.id} references unknown recipe {contract.targetId}.");
                        break;
                    case ContractRequirementType.Rarity:
                        if (!rarityIds.Contains(contract.targetId)) errors.Add($"Contract {contract.id} references unknown rarity {contract.targetId}.");
                        break;
                    case ContractRequirementType.Category:
                        if (!categoryIds.Contains(contract.targetId)) errors.Add($"Contract {contract.id} references unknown category {contract.targetId}.");
                        break;
                    default:
                        errors.Add($"Contract {contract.id} has unknown requirement type {contract.requirementType}.");
                        break;
                }
            }
            var masteryLevels = config.MasteryLevels.OrderBy(x => x.requiredProductionCount).ToList();
            if (masteryLevels.Count == 0 || masteryLevels[0].requiredProductionCount != 1)
                errors.Add("Mastery configuration must start at production count 1.");
            for (var i = 0; i < masteryLevels.Count; i++)
            {
                if (masteryLevels[i].rarityBonus < 0f || masteryLevels[i].rarityBonus >= 1f)
                    errors.Add($"Mastery level {masteryLevels[i].id} has invalid rarity bonus.");
                if (i > 0 && (masteryLevels[i].requiredProductionCount <= masteryLevels[i - 1].requiredProductionCount || masteryLevels[i].rarityBonus < masteryLevels[i - 1].rarityBonus))
                    errors.Add("Mastery thresholds must increase and bonuses cannot decrease.");
            }
            foreach (var localization in config.Localizations)
                if (string.IsNullOrWhiteSpace(localization.pl) || string.IsNullOrWhiteSpace(localization.en))
                    errors.Add($"Localization {localization.key} must define Polish and English text.");
            foreach (var rarity in config.Rarities) RequireLocalization(config, $"rarity.{rarity.id}", errors);
            foreach (var ingredient in config.Ingredients) RequireLocalization(config, $"ingredient.{ingredient.id}", errors);
            foreach (var recipe in config.Recipes) RequireLocalization(config, $"recipe.{recipe.id}", errors);
            foreach (var category in config.Categories) RequireLocalization(config, $"category.{category.id}", errors);
            foreach (var contract in config.Contracts) RequireLocalization(config, $"contract.{contract.id}", errors);
            foreach (var level in config.MasteryLevels) RequireLocalization(config, $"mastery.{level.id}", errors);
            return errors;
        }

        private static void RequireLocalization(GameConfig config, string key, List<string> errors)
        {
            if (!config.Localizations.Any(x => x.key == key)) errors.Add($"Missing localization: {key}.");
        }

        private static void CheckUnique(IEnumerable<string> ids, string type, List<string> errors)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids) if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) errors.Add($"Invalid or duplicate {type} id: '{id}'.");
        }
    }
}
