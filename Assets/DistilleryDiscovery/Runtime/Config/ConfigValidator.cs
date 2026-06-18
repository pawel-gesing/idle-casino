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
            CheckUnique(config.Economy.deliveryPools.Select(x => x.id), "delivery pool", errors);
            var rarityIds = config.Rarities.Select(x => x.id).ToHashSet();
            var recipeIds = config.Recipes.Select(x => x.id).ToHashSet();
            var ingredientIds = config.Ingredients.Select(x => x.id).ToHashSet();

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
                if (!config.Ingredients.Any(i => i.outcomeWeights.Any(o => o.recipeId == recipe.id && o.weight > 0))) errors.Add($"Recipe {recipe.id} has no contributing ingredient.");
            }
            foreach (var rarity in config.Economy.productRarityWeights)
            {
                if (!rarityIds.Contains(rarity.rarityId)) errors.Add($"Product roll uses unknown rarity {rarity.rarityId}.");
                if (rarity.weight <= 0) errors.Add($"Product rarity {rarity.rarityId} has non-positive weight.");
            }
            foreach (var pool in config.Economy.deliveryPools)
            {
                if (pool.entries.Count == 0) errors.Add($"Delivery pool {pool.id} is empty.");
                foreach (var entry in pool.entries)
                {
                    if (!ingredientIds.Contains(entry.ingredientId)) errors.Add($"Delivery pool {pool.id} references unknown ingredient {entry.ingredientId}.");
                    if (entry.weight <= 0 || entry.minAmount <= 0 || entry.maxAmount < entry.minAmount) errors.Add($"Delivery entry {entry.ingredientId} has invalid values.");
                }
            }
            return errors;
        }

        private static void CheckUnique(IEnumerable<string> ids, string type, List<string> errors)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids) if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) errors.Add($"Invalid or duplicate {type} id: '{id}'.");
        }
    }
}
