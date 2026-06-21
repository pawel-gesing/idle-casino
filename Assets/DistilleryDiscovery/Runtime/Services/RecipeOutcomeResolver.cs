using System;
using System.Collections.Generic;
using System.Linq;

namespace DistilleryDiscovery
{
    public static class RecipeOutcomeResolver
    {
        public static bool IsEligible(GameConfig config, RecipeDefinition recipe, IReadOnlyList<string> ingredientIds)
        {
            if (config == null || recipe == null || ingredientIds == null || !recipe.enabled) return false;
            var counts = ingredientIds.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
            foreach (var clause in recipe.requirements ?? new List<RecipeRequirementClause>())
            {
                var needed = Math.Max(1, clause.count);
                var matched = clause.type switch
                {
                    RecipeRequirementType.Ingredient => counts.GetValueOrDefault(clause.ingredientId) >= needed,
                    RecipeRequirementType.Group => ingredientIds.Count(id => config.Ingredient(id)?.groupId == clause.groupId) >= needed,
                    RecipeRequirementType.DistinctGroup => ingredientIds.Where(id => config.Ingredient(id)?.groupId == clause.groupId).Distinct().Count() >= needed,
                    RecipeRequirementType.AnyOf => (clause.ingredientIds ?? new List<string>()).Sum(id => counts.GetValueOrDefault(id)) >= needed,
                    _ => false
                };
                if (!matched) return false;
            }
            return recipe.requirements != null && recipe.requirements.Count > 0;
        }

        public static int Score(GameConfig config, RecipeDefinition recipe, IReadOnlyList<string> ingredientIds)
        {
            if (!IsEligible(config, recipe, ingredientIds)) return 0;
            var selected = ingredientIds.ToHashSet();
            var groups = selected.Select(id => config.Ingredient(id)?.groupId).Where(x => !string.IsNullOrEmpty(x)).ToHashSet();
            return checked(recipe.baseWeight + (recipe.weightBonuses ?? new List<RecipeWeightBonus>())
                .Where(x => (!string.IsNullOrEmpty(x.ingredientId) && selected.Contains(x.ingredientId)) ||
                            (!string.IsNullOrEmpty(x.groupId) && groups.Contains(x.groupId)))
                .Sum(x => x.weight));
        }

        public static List<OutcomeChance> Calculate(GameConfig config, IReadOnlyList<string> ingredientIds)
        {
            var scored = config.Recipes.Where(x => x.enabled)
                .Select(x => new { Recipe = x, Weight = Score(config, x, ingredientIds) })
                .Where(x => x.Weight > 0).ToList();
            var total = scored.Sum(x => x.Weight);
            if (total <= 0) throw new InvalidOperationException("Selected ingredients cannot produce any recipe.");
            return scored.Select(x => new OutcomeChance
                {
                    RecipeId = x.Recipe.id,
                    Weight = x.Weight,
                    Probability = (float)x.Weight / total
                })
                .OrderByDescending(x => x.Weight).ThenBy(x => x.RecipeId).ToList();
        }
    }
}
