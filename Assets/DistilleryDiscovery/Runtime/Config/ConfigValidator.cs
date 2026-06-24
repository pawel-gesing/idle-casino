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
            CheckUnique(config.Groups.Select(x => x.id), "ingredient group", errors);
            CheckUnique(config.Ingredients.Select(x => x.id), "ingredient", errors);
            CheckUnique(config.Recipes.Select(x => x.id), "recipe", errors);
            CheckUnique(config.Categories.Select(x => x.id), "recipe category", errors);
            CheckUnique(config.ContractTemplates.Select(x => x.id), "contract template", errors);
            CheckUnique(config.MasteryLevels.Select(x => x.id), "mastery level", errors);
            CheckUnique(config.Localizations.Select(x => x.key), "localization", errors);
            CheckUnique(config.Visuals.Select(x => x.id), "visual", errors);
            CheckUnique(config.Economy.deliveryPools.Select(x => x.id), "delivery pool", errors);

            var rarityIds = config.Rarities.Select(x => x.id).ToHashSet();
            var groupIds = config.Groups.Select(x => x.id).ToHashSet();
            var ingredientIds = config.Ingredients.Select(x => x.id).ToHashSet();
            var recipeIds = config.Recipes.Select(x => x.id).ToHashSet();
            var categoryIds = config.Categories.Select(x => x.id).ToHashSet();
            var tags = config.Recipes.SelectMany(x => x.tags ?? new List<string>()).ToHashSet();

            foreach (var rarity in config.Rarities) RequireLocalization(config, $"rarity.{rarity.id}", $"Rarity {rarity.id}", errors);
            foreach (var category in config.Categories) RequireLocalization(config, $"category.{category.id}", $"Category {category.id}", errors);
            foreach (var tag in tags) RequireLocalization(config, $"tag.{tag}", $"Tag {tag}", errors);
            foreach (var mastery in config.MasteryLevels) RequireLocalization(config, $"mastery.{mastery.id}", $"Mastery {mastery.id}", errors);

            if (config.Economy.ingredientsPerExperiment != 3 || config.Economy.ingredientsPerProduction != 3)
                errors.Add("Experiment and production ingredient counts must both equal 3 for content v2.");
            if (config.Economy.activeContractCount != 3) errors.Add("Active contract count must equal the three contract roles.");
            if (config.Economy.freeDeliveryIntervalSeconds <= 0 || config.Economy.maxStoredFreeDeliveries <= 0)
                errors.Add("Free delivery timing and storage must be positive.");
            if (config.Economy.experimentDurationSeconds <= 0 || config.Economy.productionDurationSeconds <= 0)
                errors.Add("Laboratory durations must be positive.");
            if (config.Economy.maxOfflineProgressSeconds < 0 || config.Economy.contractRefreshSeconds <= 0 || config.Economy.freeContractRerolls < 0)
                errors.Add("Offline cap and contract refresh/reroll values are invalid.");

            foreach (var group in config.Groups)
                RequireLocalization(config, $"group.{group.id}", $"Group {group.id}", errors);
            foreach (var ingredient in config.Ingredients)
            {
                if (!rarityIds.Contains(ingredient.rarityId)) errors.Add($"Ingredient {ingredient.id}.rarityId references unknown rarity {ingredient.rarityId}.");
                if (!string.IsNullOrEmpty(ingredient.groupId) && !groupIds.Contains(ingredient.groupId)) errors.Add($"Ingredient {ingredient.id}.groupId references unknown group {ingredient.groupId}.");
                if (string.IsNullOrEmpty(ingredient.baseIngredientId)) errors.Add($"Ingredient {ingredient.id}.baseIngredientId is empty.");
                RequireLocalization(config, $"ingredient.{ingredient.id}", $"Ingredient {ingredient.id}", errors);
            }

            var enabledIngredients = config.Ingredients.Where(x => x.enabled).ToList();
            foreach (var recipe in config.Recipes)
            {
                if (!rarityIds.Contains(recipe.collectionRarityId)) errors.Add($"Recipe {recipe.id}.collectionRarityId references unknown rarity {recipe.collectionRarityId}.");
                if (!categoryIds.Contains(recipe.categoryId)) errors.Add($"Recipe {recipe.id}.categoryId references unknown category {recipe.categoryId}.");
                if (recipe.baseValue <= 0) errors.Add($"Recipe {recipe.id}.baseValue must be positive.");
                if (recipe.baseWeight <= 0) errors.Add($"Recipe {recipe.id}.baseWeight must be positive.");
                if (recipe.requirements == null || recipe.requirements.Count == 0) errors.Add($"Recipe {recipe.id}.requirements is empty.");
                ValidateRequirements(recipe, ingredientIds, groupIds, errors);
                ValidateBonuses(recipe, ingredientIds, groupIds, errors);
                if (recipe.tags == null || recipe.tags.Count == 0 || recipe.tags.Any(string.IsNullOrEmpty) || recipe.tags.Distinct().Count() != recipe.tags.Count)
                    errors.Add($"Recipe {recipe.id}.tags must contain unique non-empty values.");
                RequireLocalization(config, $"recipe.{recipe.id}", $"Recipe {recipe.id}", errors);
                if (recipe.enabled && !HasThreeIngredientSolution(config, recipe, enabledIngredients))
                    errors.Add($"Recipe {recipe.id} cannot be produced with exactly three enabled ingredients.");
            }

            foreach (var ingredient in config.Ingredients.Where(x => x.enabled && (x.rarityId == "rarity_epic" || x.rarityId == "rarity_legendary")))
            {
                var direct = config.Recipes.Count(r => r.enabled && IsDirectAssociation(r, ingredient.id));
                var cap = ingredient.rarityId == "rarity_epic" ? 5 : 3;
                if (direct > cap) errors.Add($"Ingredient {ingredient.id} has {direct} direct recipe associations; cap is {cap}.");
            }

            foreach (var rarity in config.Economy.productRarityWeights)
            {
                if (!rarityIds.Contains(rarity.rarityId)) errors.Add($"Product rarity roll references unknown rarity {rarity.rarityId}.");
                if (rarity.weight <= 0) errors.Add($"Product rarity {rarity.rarityId}.weight must be positive.");
            }
            ValidateDeliveries(config, ingredientIds, errors);
            ValidateContracts(config, rarityIds, ingredientIds, groupIds, recipeIds, categoryIds, tags, errors);
            ValidateVisuals(config, errors);
            ValidateProgression(config, errors);
            return errors;
        }

        private static void ValidateRequirements(RecipeDefinition recipe, HashSet<string> ingredients, HashSet<string> groups, List<string> errors)
        {
            var signatures = new HashSet<string>();
            foreach (var clause in recipe.requirements ?? new List<RecipeRequirementClause>())
            {
                if (clause.count <= 0 || clause.count > 3) errors.Add($"Recipe {recipe.id}.requirements has invalid count {clause.count}.");
                var signature = $"{clause.type}:{clause.ingredientId}:{clause.groupId}:{string.Join(",", clause.ingredientIds ?? new List<string>())}";
                if (!signatures.Add(signature)) errors.Add($"Recipe {recipe.id}.requirements contains duplicate clause {signature}.");
                switch (clause.type)
                {
                    case RecipeRequirementType.Ingredient:
                        if (!ingredients.Contains(clause.ingredientId)) errors.Add($"Recipe {recipe.id}.requirements references unknown ingredient {clause.ingredientId}.");
                        break;
                    case RecipeRequirementType.Group:
                    case RecipeRequirementType.DistinctGroup:
                        if (!groups.Contains(clause.groupId)) errors.Add($"Recipe {recipe.id}.requirements references unknown group {clause.groupId}.");
                        break;
                    case RecipeRequirementType.AnyOf:
                        if (clause.ingredientIds == null || clause.ingredientIds.Count == 0 || clause.ingredientIds.Any(x => !ingredients.Contains(x)))
                            errors.Add($"Recipe {recipe.id}.requirements any_of contains no choices or unknown ingredients.");
                        break;
                    default: errors.Add($"Recipe {recipe.id}.requirements uses unknown type {clause.type}."); break;
                }
            }
        }

        private static void ValidateBonuses(RecipeDefinition recipe, HashSet<string> ingredients, HashSet<string> groups, List<string> errors)
        {
            var seen = new HashSet<string>();
            foreach (var bonus in recipe.weightBonuses ?? new List<RecipeWeightBonus>())
            {
                var hasIngredient = !string.IsNullOrEmpty(bonus.ingredientId);
                var hasGroup = !string.IsNullOrEmpty(bonus.groupId);
                if (hasIngredient == hasGroup) errors.Add($"Recipe {recipe.id}.weightBonuses must select exactly one ingredient or group.");
                if (hasIngredient && !ingredients.Contains(bonus.ingredientId)) errors.Add($"Recipe {recipe.id}.weightBonuses references unknown ingredient {bonus.ingredientId}.");
                if (hasGroup && !groups.Contains(bonus.groupId)) errors.Add($"Recipe {recipe.id}.weightBonuses references unknown group {bonus.groupId}.");
                if (bonus.weight <= 0) errors.Add($"Recipe {recipe.id}.weightBonuses has non-positive weight.");
                if (!seen.Add(hasIngredient ? "i:" + bonus.ingredientId : "g:" + bonus.groupId)) errors.Add($"Recipe {recipe.id}.weightBonuses contains a duplicate selector.");
            }
        }

        private static bool HasThreeIngredientSolution(GameConfig config, RecipeDefinition recipe, List<IngredientDefinition> ingredients)
        {
            for (var a = 0; a < ingredients.Count; a++)
                for (var b = a; b < ingredients.Count; b++)
                    for (var c = b; c < ingredients.Count; c++)
                        if (RecipeOutcomeResolver.IsEligible(config, recipe, new[] { ingredients[a].id, ingredients[b].id, ingredients[c].id })) return true;
            return false;
        }

        private static bool IsDirectAssociation(RecipeDefinition recipe, string ingredientId) =>
            (recipe.requirements ?? new()).Any(x => x.ingredientId == ingredientId || (x.ingredientIds ?? new()).Contains(ingredientId)) ||
            (recipe.weightBonuses ?? new()).Any(x => x.ingredientId == ingredientId);

        private static void ValidateDeliveries(GameConfig config, HashSet<string> ingredientIds, List<string> errors)
        {
            var delivered = new HashSet<string>();
            foreach (var pool in config.Economy.deliveryPools)
            {
                if (pool.rolls <= 0) errors.Add($"Delivery pool {pool.id}.rolls must be positive.");
                if (pool.entries == null || pool.entries.Count == 0) errors.Add($"Delivery pool {pool.id}.entries is empty.");
                foreach (var entry in pool.entries ?? new())
                {
                    delivered.Add(entry.ingredientId);
                    if (!ingredientIds.Contains(entry.ingredientId)) errors.Add($"Delivery pool {pool.id} references unknown ingredient {entry.ingredientId}.");
                    if (entry.weight <= 0 || entry.minAmount <= 0 || entry.maxAmount < entry.minAmount)
                        errors.Add($"Delivery pool {pool.id} entry {entry.ingredientId} has invalid weight or quantity.");
                }
            }
            foreach (var ingredient in config.Ingredients.Where(x => x.enabled && x.sourceRule != "contracts_only" && !delivered.Contains(x.id)))
                errors.Add($"Enabled ingredient {ingredient.id} is missing from delivery pools without sourceRule=contracts_only.");
        }

        private static void ValidateContracts(GameConfig config, HashSet<string> rarities, HashSet<string> ingredients,
            HashSet<string> groups, HashSet<string> recipes, HashSet<string> categories, HashSet<string> tags, List<string> errors)
        {
            var objectiveTypes = new HashSet<string> { ContractObjectiveType.Recipe, ContractObjectiveType.Category, ContractObjectiveType.Tag,
                ContractObjectiveType.Rarity, ContractObjectiveType.Ingredient, ContractObjectiveType.Group, ContractObjectiveType.Discover,
                ContractObjectiveType.DistinctRecipes, ContractObjectiveType.RecipeMinRarity, ContractObjectiveType.ImproveRecord, ContractObjectiveType.Source };
            foreach (var role in ContractRole.All)
                if (!config.ContractTemplates.Any(x => x.enabled && x.role == role)) errors.Add($"Contract role {role} has no enabled templates.");
            foreach (var template in config.ContractTemplates)
            {
                if (!ContractRole.All.Contains(template.role)) errors.Add($"Contract template {template.id}.role is invalid.");
                if (!objectiveTypes.Contains(template.objectiveType)) errors.Add($"Contract template {template.id}.objectiveType is invalid.");
                if (template.selectionWeight <= 0 || template.minAmount <= 0 || template.maxAmount < template.minAmount)
                    errors.Add($"Contract template {template.id} has invalid selection weight or amount range.");
                if (template.minGoldReward < 0 || template.maxGoldReward < template.minGoldReward)
                    errors.Add($"Contract template {template.id} has invalid gold reward range.");
                if (template.minLaboratoryLevel <= 0 || config.LaboratoryLevel(template.minLaboratoryLevel) == null)
                    errors.Add($"Contract template {template.id}.minLaboratoryLevel is invalid.");
                ValidateTemplateTargets(template, rarities, ingredients, groups, recipes, categories, tags, errors);
                foreach (var reward in template.ingredientRewards ?? new())
                {
                    var valid = reward.selectorType switch
                    {
                        RewardSelectorType.Ingredient => ingredients.Contains(reward.targetId),
                        RewardSelectorType.Group => groups.Contains(reward.targetId),
                        RewardSelectorType.Rarity => rarities.Contains(reward.targetId),
                        _ => false
                    };
                    if (!valid || reward.weight <= 0 || reward.minAmount <= 0 || reward.maxAmount < reward.minAmount)
                        errors.Add($"Contract template {template.id}.ingredientRewards has invalid selector, weight or quantity.");
                }
                RequireLocalization(config, $"contract.{template.id}", $"Contract template {template.id}", errors);
            }
        }

        private static void ValidateTemplateTargets(ContractTemplateDefinition t, HashSet<string> rarities, HashSet<string> ingredients,
            HashSet<string> groups, HashSet<string> recipes, HashSet<string> categories, HashSet<string> tags, List<string> errors)
        {
            bool Valid(string value) => t.targetSelector switch
            {
                ContractTargetSelector.None => string.IsNullOrEmpty(value),
                ContractTargetSelector.DiscoveredRecipe or ContractTargetSelector.UndiscoveredRecipe => string.IsNullOrEmpty(value) || recipes.Contains(value),
                ContractTargetSelector.Category => categories.Contains(value),
                ContractTargetSelector.Tag => tags.Contains(value),
                ContractTargetSelector.Rarity => rarities.Contains(value),
                ContractTargetSelector.Ingredient => ingredients.Contains(value),
                ContractTargetSelector.Group => groups.Contains(value),
                ContractTargetSelector.Source => value is LaboratoryJobType.Experiment or LaboratoryJobType.Production,
                _ => false
            };
            if (!string.IsNullOrEmpty(t.fixedTargetId) && !Valid(t.fixedTargetId)) errors.Add($"Contract template {t.id}.fixedTargetId cannot resolve.");
            if ((t.allowedTargetIds ?? new()).Any(x => !Valid(x))) errors.Add($"Contract template {t.id}.allowedTargetIds contains an unknown target.");
            if (!string.IsNullOrEmpty(t.minRarityId) && !rarities.Contains(t.minRarityId)) errors.Add($"Contract template {t.id}.minRarityId is unknown.");
            if (!string.IsNullOrEmpty(t.source) && t.source != LaboratoryJobType.Experiment && t.source != LaboratoryJobType.Production)
                errors.Add($"Contract template {t.id}.source is invalid.");
        }

        private static void ValidateProgression(GameConfig config, List<string> errors)
        {
            if (config.LaboratoryLevels.Count == 0 || config.LaboratoryLevel(1) == null) errors.Add("Laboratory levels must include level 1.");
            foreach (var level in config.LaboratoryLevels)
                if (level.level <= 0 || level.upgradeCost < 0 || level.experimentSlots <= 0 || level.productionSlots <= 0 ||
                    level.experimentTimeMultiplier <= 0 || level.productionTimeMultiplier <= 0)
                    errors.Add($"Laboratory level {level.level} has invalid cost, slots or time multipliers.");
            var ordered = config.MasteryLevels.OrderBy(x => x.requiredProductionCount).ToList();
            for (var i = 0; i < ordered.Count; i++)
                if (ordered[i].requiredProductionCount < 0 || ordered[i].rarityBonus < 0 || (i > 0 && ordered[i].requiredProductionCount <= ordered[i - 1].requiredProductionCount))
                    errors.Add($"Mastery level {ordered[i].id} has invalid progression values.");
        }

        private static void ValidateVisuals(GameConfig config, List<string> errors)
        {
            if (config.Visuals.Count == 0) return;

            foreach (var visual in config.Visuals)
            {
                if (!VisualIds.HasKnownPrefix(visual.id))
                    errors.Add($"Visual {visual.id} must use a known visual id prefix.");
                if (string.IsNullOrWhiteSpace(visual.spriteResource))
                    errors.Add($"Visual {visual.id}.spriteResource is empty.");
                else if (VisualIds.HasKnownPrefix(visual.id) && visual.spriteResource != VisualIds.SpriteResourcePath(visual.id))
                    errors.Add($"Visual {visual.id}.spriteResource must be {VisualIds.SpriteResourcePath(visual.id)}.");
                if (!string.IsNullOrEmpty(visual.tintHex) && !visual.tintHex.StartsWith("#"))
                    errors.Add($"Visual {visual.id}.tintHex must use #RRGGBB or #RRGGBBAA format.");
            }

            var ids = config.Visuals.Select(x => x.id).ToHashSet();
            foreach (var ingredient in config.Ingredients.Where(x => x.enabled))
                RequireVisual(ids, VisualIds.Ingredient(ingredient.id), $"Ingredient {ingredient.id}", errors);
            foreach (var group in config.Groups)
                RequireVisual(ids, VisualIds.Group(group.id), $"Ingredient group {group.id}", errors);
            foreach (var category in config.Categories)
                RequireVisual(ids, VisualIds.Category(category.id), $"Recipe category {category.id}", errors);
            foreach (var rarity in config.Rarities)
                RequireVisual(ids, VisualIds.Rarity(rarity.id), $"Rarity {rarity.id}", errors);

            RequireVisual(ids, VisualIds.NavExperiment, "Experiment navigation", errors);
            RequireVisual(ids, VisualIds.NavProduction, "Production navigation", errors);
            RequireVisual(ids, VisualIds.NavContracts, "Contracts navigation", errors);
            RequireVisual(ids, VisualIds.NavDelivery, "Delivery navigation", errors);
            RequireVisual(ids, VisualIds.NavLaboratory, "Laboratory navigation", errors);
            RequireVisual(ids, VisualIds.HeaderGold, "Gold header resource", errors);
            RequireVisual(ids, VisualIds.HeaderRecipes, "Recipes header resource", errors);
            RequireVisual(ids, VisualIds.HeaderIngredients, "Ingredients header resource", errors);
        }

        private static void RequireLocalization(GameConfig config, string key, string entity, List<string> errors)
        {
            var value = config.Localizations.Find(x => x.key == key);
            if (value == null || string.IsNullOrWhiteSpace(value.pl) || string.IsNullOrWhiteSpace(value.en))
                errors.Add($"{entity} is missing complete PL/EN localization at {key}.");
        }

        private static void RequireVisual(HashSet<string> ids, string id, string entity, List<string> errors)
        {
            if (!ids.Contains(id)) errors.Add($"{entity} is missing visual id {id}.");
        }

        private static void CheckUnique(IEnumerable<string> ids, string type, List<string> errors)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
                if (string.IsNullOrWhiteSpace(id)) errors.Add($"A {type} has an empty id.");
                else if (!seen.Add(id)) errors.Add($"Duplicate {type} id: {id}.");
        }
    }
}
