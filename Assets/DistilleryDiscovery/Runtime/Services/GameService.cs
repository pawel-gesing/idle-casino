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
        public int LastOfflineDeliveriesGained { get; private set; }
        public int LastOfflineJobsCompleted { get; private set; }
        public OfflineProgressSummary LastOfflineSummary { get; private set; } = new();
        private readonly IRandomSource random;
        private readonly ITimeProvider time;

        public GameService(GameConfig config, PlayerState state, IRandomSource random = null, ITimeProvider time = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            this.random = random ?? new SystemRandomSource();
            this.time = time ?? new SystemTimeProvider();
            ReplaceState(state);
        }

        public static PlayerState NewState(GameConfig config, string languageCode = "en") => new()
        {
            gold = config.Economy.startingGold,
            languageCode = languageCode == "pl" ? "pl" : "en",
            freeContractRerollsRemaining = config.Economy.freeContractRerolls,
            laboratories = new List<PlayerLaboratoryState> { new() { id = "lab_1", level = 1 } },
            laboratoryLevel = 1,
            nextLaboratoryNumber = 2
        };

        public void ReplaceState(PlayerState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            var version = State.version;
            State.inventory ??= new(); State.recipes ??= new(); State.products ??= new(); State.laboratories ??= new();
            State.activeContractIds ??= new(); State.activeContracts ??= new(); State.laboratoryJobs ??= new();
            NormalizeLegacyIngredientIds();
            State.inventory.RemoveAll(x => x == null || Config.Ingredient(x.ingredientId)?.enabled != true || x.amount <= 0);
            State.recipes.RemoveAll(x => x == null || Config.Recipe(x.recipeId)?.enabled != true);
            foreach (var recipe in State.recipes) recipe.revealedIngredientIds = (recipe.revealedIngredientIds ?? new()).Select(MapLegacyIngredientId)
                .Where(x => Config.Ingredient(x) != null).Distinct().ToList();
            State.products.RemoveAll(x => x == null || Config.Recipe(x.recipeId) == null || Config.Rarity(x.rarityId) == null || x.amount <= 0);
            if (State.pendingResult != null)
            {
                State.pendingResult.contractProgress ??= new(); State.pendingResult.ingredientIds ??= new();
                State.pendingResult.ingredientIds = State.pendingResult.ingredientIds.Select(MapLegacyIngredientId).Where(x => Config.Ingredient(x) != null).ToList();
                if (Config.Recipe(State.pendingResult.recipeId) == null || Config.Rarity(State.pendingResult.rarityId) == null || State.pendingResult.saleValue <= 0)
                    State.pendingResult = null;
                else if (version < 8) State.pendingResult.contractProgress.Clear(); // preserve product gold; never pay invalid legacy contracts
            }
            NormalizeLaboratories(version);
            if (State.languageCode != "pl" && State.languageCode != "en") State.languageCode = "en";

            if (version < 3)
            {
                State.gold = checked(State.gold + State.products.Sum(x => checked(x.saleValue * x.amount)));
                State.products.Clear();
            }
            State.activeContractIds.Clear();
            foreach (var contract in State.activeContracts) contract.seenRecipeIds ??= new();
            State.activeContracts.RemoveAll(x => x == null || Config.ContractTemplate(x.templateId)?.enabled != true ||
                !ContractRole.All.Contains(x.role) || string.IsNullOrEmpty(x.instanceId));
            RemoveDuplicateContracts();

            if (version < 8)
            {
                State.activeContracts.Clear();
                State.freeContractRerollsRemaining = Config.Economy.freeContractRerolls;
                State.contractRefreshUtc = time.UtcNow.AddSeconds(Config.Economy.contractRefreshSeconds).ToString("O");
            }
            if (!TryUtc(State.contractRefreshUtc, out _))
                State.contractRefreshUtc = time.UtcNow.AddSeconds(Config.Economy.contractRefreshSeconds).ToString("O");
            EnsureActiveContracts();

            if (version < 6 || !TryUtc(State.freeDeliveryLastUpdateUtc, out _))
            {
                State.freeDeliveryLastUpdateUtc = time.UtcNow.ToString("O");
                State.availableFreeDeliveries = 0;
            }
            State.availableFreeDeliveries = Math.Clamp(State.availableFreeDeliveries, 0, Config.Economy.maxStoredFreeDeliveries);
            State.laboratoryJobs.RemoveAll(x => x == null || !TryUtc(x.endTimeUtc, out _) ||
                (x.type != LaboratoryJobType.Experiment && x.type != LaboratoryJobType.Production) || x.ingredientIds == null ||
                x.ingredientIds.Any(id => Config.Ingredient(id) == null) || (x.type == LaboratoryJobType.Production && Config.Recipe(x.recipeId) == null));
            foreach (var job in State.laboratoryJobs)
            {
                job.ingredientIds = job.ingredientIds.Select(MapLegacyIngredientId).ToList();
                if (State.laboratories.All(x => x.id != job.laboratoryId)) job.laboratoryId = State.laboratories[0].id;
                if (job.status != LaboratoryJobStatus.Running && job.status != LaboratoryJobStatus.Completed && job.status != LaboratoryJobStatus.Claimed)
                    job.status = LaboratoryJobStatus.Running;
            }
            State.version = 9;
            var deliveriesBefore = State.availableFreeDeliveries;
            var completedBefore = State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Completed);
            var elapsed = TryUtc(State.lastSavedAtUtc, out var savedAt) && savedAt <= time.UtcNow ? time.UtcNow - savedAt : TimeSpan.Zero;
            UpdateTime();
            LastOfflineDeliveriesGained = Math.Max(0, State.availableFreeDeliveries - deliveriesBefore);
            LastOfflineJobsCompleted = Math.Max(0, State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Completed) - completedBefore);
            LastOfflineSummary = new OfflineProgressSummary
            {
                Elapsed = elapsed, DeliveriesGained = LastOfflineDeliveriesGained, JobsCompleted = LastOfflineJobsCompleted,
                ExperimentsReady = State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Completed && x.type == LaboratoryJobType.Experiment),
                ProductionsReady = State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Completed && x.type == LaboratoryJobType.Production)
            };
        }

        private void NormalizeLegacyIngredientIds()
        {
            var normalized = new Dictionary<string, int>();
            foreach (var entry in State.inventory.Where(x => x != null))
            {
                var id = MapLegacyIngredientId(entry.ingredientId);
                normalized[id] = normalized.GetValueOrDefault(id) + Math.Max(0, entry.amount);
            }
            State.inventory = normalized.Select(x => new InventoryEntry { ingredientId = x.Key, amount = x.Value }).ToList();
            foreach (var job in State.laboratoryJobs.Where(x => x?.ingredientIds != null))
                job.ingredientIds = job.ingredientIds.Select(MapLegacyIngredientId).ToList();
        }

        private static string MapLegacyIngredientId(string id) => id switch
        {
            "ingredient_malt_common" => "ingredient_barley_common",
            "ingredient_amber_epic" => "ingredient_amber_legendary",
            _ => id
        };

        private void NormalizeLaboratories(int version)
        {
            var maxLevel = Config.LaboratoryLevels.Count == 0 ? 1 : Config.LaboratoryLevels.Max(x => x.level);
            if (State.laboratoryLevel < 1) State.laboratoryLevel = 1;
            if (Config.LaboratoryLevel(State.laboratoryLevel) == null) State.laboratoryLevel = maxLevel;

            State.laboratories.RemoveAll(x => x == null);
            if (State.laboratories.Count == 0)
                State.laboratories.Add(new PlayerLaboratoryState { id = "lab_1", level = State.laboratoryLevel });
            var seen = new HashSet<string>();
            for (var i = 0; i < State.laboratories.Count; i++)
            {
                var lab = State.laboratories[i];
                if (string.IsNullOrEmpty(lab.id) || !seen.Add(lab.id)) lab.id = $"lab_{i + 1}";
                if (lab.level < 1) lab.level = 1;
                if (Config.LaboratoryLevel(lab.level) == null) lab.level = maxLevel;
            }
            if (version < 9 || (State.laboratories.Count == 1 && State.laboratoryLevel != State.laboratories[0].level && Config.LaboratoryLevel(State.laboratoryLevel) != null))
                State.laboratories[0].level = State.laboratoryLevel;

            State.laboratoryLevel = State.laboratories[0].level; // legacy mirror for old UI/tests/saves
            var maxNumber = State.laboratories.Select(x => int.TryParse((x.id ?? "").Replace("lab_", ""), out var n) ? n : 0).DefaultIfEmpty(1).Max();
            State.nextLaboratoryNumber = Math.Max(Math.Max(2, State.nextLaboratoryNumber), maxNumber + 1);
        }

        private LaboratoryLevelDefinition CurrentLaboratoryLevel => Config.LaboratoryLevel(State.laboratories.FirstOrDefault()?.level ?? State.laboratoryLevel) ?? Config.LaboratoryLevel(1);
        private int HighestLaboratoryLevel => State.laboratories.Count == 0 ? State.laboratoryLevel : State.laboratories.Max(x => x.level);
        public int LaboratoryCount => State.laboratories.Count;
        public int MaxLaboratoryCount => Config.LaboratoryLevels.Count;
        public int NextLaboratoryCost => State.laboratories.Count >= MaxLaboratoryCount ? -1 : Config.LaboratoryLevel(State.laboratories.Count + 1)?.upgradeCost ?? -1;
        public int ExperimentSlotCount => State.laboratories.Sum(x => Config.LaboratoryLevel(x.level)?.experimentSlots ?? 0);
        public int ProductionSlotCount => State.laboratories.Sum(x => Config.LaboratoryLevel(x.level)?.productionSlots ?? 0);
        public int AvailableExperimentSlots => State.laboratories.Sum(x => AvailableSlots(x.id, LaboratoryJobType.Experiment));
        public int AvailableProductionSlots => State.laboratories.Sum(x => AvailableSlots(x.id, LaboratoryJobType.Production));
        public int AvailableLaboratorySlots => AvailableExperimentSlots + AvailableProductionSlots;
        public LaboratoryLevelDefinition LaboratoryLevelFor(string laboratoryId) => Config.LaboratoryLevel(State.laboratories.Find(x => x.id == laboratoryId)?.level ?? State.laboratories.FirstOrDefault()?.level ?? 1);
        public int AvailableSlots(string laboratoryId, string type)
        {
            var lab = State.laboratories.Find(x => x.id == laboratoryId);
            if (lab == null) return 0;
            var level = Config.LaboratoryLevel(lab.level);
            var capacity = type == LaboratoryJobType.Experiment ? level.experimentSlots : level.productionSlots;
            var used = State.laboratoryJobs.Count(x => x.laboratoryId == laboratoryId && x.type == type && x.status != LaboratoryJobStatus.Claimed);
            return Math.Max(0, capacity - used);
        }

        public TimeSpan TimeUntilNextFreeDelivery
        {
            get
            {
                UpdateTime();
                if (State.availableFreeDeliveries >= Config.Economy.maxStoredFreeDeliveries) return TimeSpan.Zero;
                TryUtc(State.freeDeliveryLastUpdateUtc, out var anchor);
                var remaining = anchor.AddSeconds(Config.Economy.freeDeliveryIntervalSeconds) - time.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public TimeSpan TimeRemaining(LaboratoryJobState job)
        {
            if (job == null || !TryUtc(job.endTimeUtc, out var end)) return TimeSpan.Zero;
            var remaining = end - time.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public void UpdateTime()
        {
            var now = time.UtcNow;
            if (!TryUtc(State.freeDeliveryLastUpdateUtc, out var anchor) || anchor > now) { State.freeDeliveryLastUpdateUtc = now.ToString("O"); anchor = now; }
            var rawElapsedSeconds = (now - anchor).TotalSeconds;
            var elapsedSeconds = Config.Economy.maxOfflineProgressSeconds > 0 ? Math.Min(rawElapsedSeconds, Config.Economy.maxOfflineProgressSeconds) : rawElapsedSeconds;
            var intervals = (int)(elapsedSeconds / Config.Economy.freeDeliveryIntervalSeconds);
            if (intervals > 0)
            {
                State.availableFreeDeliveries = Math.Min(Config.Economy.maxStoredFreeDeliveries, State.availableFreeDeliveries + intervals);
                State.freeDeliveryLastUpdateUtc = Config.Economy.maxOfflineProgressSeconds > 0 && rawElapsedSeconds > Config.Economy.maxOfflineProgressSeconds
                    ? now.ToString("O") : anchor.AddSeconds((long)intervals * Config.Economy.freeDeliveryIntervalSeconds).ToString("O");
            }
            foreach (var job in State.laboratoryJobs.Where(x => x.status == LaboratoryJobStatus.Running))
                if (TryUtc(job.endTimeUtc, out var end) && end <= now) job.status = LaboratoryJobStatus.Completed;
            if (State.pendingResult == null && TryUtc(State.contractRefreshUtc, out var refresh) && refresh <= now)
            {
                State.activeContracts.Clear();
                State.freeContractRerollsRemaining = Config.Economy.freeContractRerolls;
                State.contractRefreshUtc = now.AddSeconds(Config.Economy.contractRefreshSeconds).ToString("O");
                EnsureActiveContracts();
            }
        }

        public bool DebugAdvanceTime(TimeSpan duration)
        {
            if (time is not AdjustableTimeProvider adjustable) return false;
            adjustable.Advance(duration); UpdateTime(); return true;
        }

        public List<OutcomeChance> Preview(IReadOnlyList<string> ingredientIds)
        {
            ValidateIngredientSelection(ingredientIds, Config.Economy.ingredientsPerExperiment);
            return CalculateOutcomes(ingredientIds);
        }

        public List<OutcomeChance> CalculateOutcomes(IReadOnlyList<string> ingredientIds)
        {
            ValidateKnownIngredients(ingredientIds);
            return RecipeOutcomeResolver.Calculate(Config, ingredientIds);
        }

        public bool IngredientsSatisfyRecipe(string recipeId, IReadOnlyList<string> ingredientIds)
        {
            ValidateKnownIngredients(ingredientIds);
            return RecipeOutcomeResolver.IsEligible(Config, Config.Recipe(recipeId), ingredientIds);
        }

        public LaboratoryJobState StartExperiment(IReadOnlyList<string> ingredientIds, string laboratoryId = null)
        {
            EnsureNoPendingResult(); Preview(ingredientIds); laboratoryId = ResolveFreeLaboratory(laboratoryId, LaboratoryJobType.Experiment);
            ConsumeIngredients(ingredientIds); return CreateJob(LaboratoryJobType.Experiment, null, ingredientIds, Config.Economy.experimentDurationSeconds, laboratoryId);
        }

        public LaboratoryJobState StartProduction(string recipeId, IReadOnlyList<string> ingredientIds, string laboratoryId = null)
        {
            EnsureNoPendingResult();
            var recipe = Config.Recipe(recipeId) ?? throw new InvalidOperationException($"Unknown recipe: {recipeId}");
            if (State.RecipeState(recipeId) == null) throw new InvalidOperationException("Production requires a discovered recipe.");
            ValidateIngredientSelection(ingredientIds, Config.Economy.ingredientsPerProduction);
            if (!RecipeOutcomeResolver.IsEligible(Config, recipe, ingredientIds))
                throw new InvalidOperationException($"Selected ingredients do not satisfy the requirements for {recipe.displayName}.");
            laboratoryId = ResolveFreeLaboratory(laboratoryId, LaboratoryJobType.Production); ConsumeIngredients(ingredientIds);
            return CreateJob(LaboratoryJobType.Production, recipeId, ingredientIds, Config.Economy.productionDurationSeconds, laboratoryId);
        }

        public ProductResult ClaimLaboratoryJob(string jobId)
        {
            EnsureNoPendingResult(); UpdateTime();
            var job = State.laboratoryJobs.Find(x => x.id == jobId) ?? throw new InvalidOperationException("Unknown laboratory job.");
            if (job.status != LaboratoryJobStatus.Completed) throw new InvalidOperationException("Laboratory job is not ready.");
            string recipeId;
            if (job.type == LaboratoryJobType.Experiment)
                recipeId = WeightedPick(CalculateOutcomes(job.ingredientIds).Select(x => (x.RecipeId, x.Weight)).ToList());
            else
            {
                if (!RecipeOutcomeResolver.IsEligible(Config, Config.Recipe(job.recipeId), job.ingredientIds))
                    throw new InvalidOperationException("Production job ingredients no longer satisfy its recipe.");
                recipeId = job.recipeId;
            }
            var product = CreateProduct(recipeId, job.ingredientIds, job.laboratoryId);
            var change = UpdateRecipeBook(recipeId, product.RarityId, job.ingredientIds);
            if (job.type == LaboratoryJobType.Experiment) State.experimentsCompleted++; else State.productionsCompleted++;
            State.AddProduct(product.RecipeId, product.RarityId, product.SaleValue);
            BeginPendingResult(job.type, product, job.ingredientIds, change.discovered, change.improved);
            job.status = LaboratoryJobStatus.Claimed;
            return job.type == LaboratoryJobType.Experiment
                ? new ExperimentResult { RecipeId = product.RecipeId, RarityId = product.RarityId, SaleValue = product.SaleValue, WasDiscovered = change.discovered, RarityImproved = change.improved }
                : product;
        }

        public CollectAllResult CollectAll()
        {
            EnsureNoPendingResult(); UpdateTime(); var result = new CollectAllResult();
            foreach (var job in State.laboratoryJobs.Where(x => x.status == LaboratoryJobStatus.Completed).ToList())
            {
                var product = ClaimLaboratoryJob(job.id); var pending = State.pendingResult;
                result.JobsCollected++; if (job.type == LaboratoryJobType.Experiment) result.ExperimentsCollected++; else result.ProductionsCollected++;
                if (pending.wasDiscovered) result.NewRecipesDiscovered++; if (pending.rarityImproved) result.RarityRecordsImproved++;
                result.ProductsAdded++;
                var line = result.Products.Find(x => x.RecipeId == product.RecipeId && x.RarityId == product.RarityId);
                if (line == null) { line = new CollectedProductSummary { RecipeId = product.RecipeId, RarityId = product.RarityId }; result.Products.Add(line); }
                line.Amount++; result.GoldGained = checked(result.GoldGained + ClaimPendingResult().TotalGold);
            }
            return result;
        }

        public PendingClaimResult ClaimPendingResult()
        {
            var pending = State.pendingResult ?? throw new InvalidOperationException("There is no result to claim.");
            var completedIds = pending.contractProgress.Where(x => x.completed).Select(x => x.contractId).Distinct().ToList();
            var completed = State.activeContracts.Where(x => completedIds.Contains(x.instanceId)).ToList();
            var contractGold = completed.Sum(x => x.goldReward);
            var total = checked(pending.saleValue + contractGold); State.gold = checked(State.gold + total);
            var ingredientRewards = new Dictionary<string, int>();
            foreach (var contract in completed) GrantContractIngredientReward(contract, ingredientRewards);
            State.activeContracts.RemoveAll(x => completedIds.Contains(x.instanceId));
            State.pendingResult = null; EnsureActiveContracts();
            var result = new PendingClaimResult { ProductGold = pending.saleValue, ContractGold = contractGold, TotalGold = total, CompletedContractIds = completedIds };
            foreach (var reward in ingredientRewards) result.IngredientRewards[reward.Key] = reward.Value;
            return result;
        }

        private void GrantContractIngredientReward(ActiveContractState state, Dictionary<string, int> result)
        {
            var pool = Config.ContractTemplate(state.templateId)?.ingredientRewards?.Where(x => x.weight > 0).ToList() ?? new();
            if (pool.Count == 0) return;
            var selected = WeightedPick(pool.Select((x, i) => (i.ToString(), x.weight)).ToList());
            var reward = pool[int.Parse(selected)];
            var candidates = reward.selectorType switch
            {
                RewardSelectorType.Ingredient => Config.Ingredients.Where(x => x.id == reward.targetId),
                RewardSelectorType.Group => Config.Ingredients.Where(x => x.enabled && x.groupId == reward.targetId),
                RewardSelectorType.Rarity => Config.Ingredients.Where(x => x.enabled && x.rarityId == reward.targetId),
                _ => Enumerable.Empty<IngredientDefinition>()
            };
            var list = candidates.ToList(); if (list.Count == 0) return;
            var ingredientId = list[random.Range(0, list.Count)].id;
            var amount = random.Range(reward.minAmount, reward.maxAmount + 1);
            State.AddIngredient(ingredientId, amount); result[ingredientId] = result.GetValueOrDefault(ingredientId) + amount;
        }

        public PlayerLaboratoryState PurchaseLaboratory()
        {
            EnsureNoPendingResult();
            var cost = NextLaboratoryCost;
            if (cost < 0) throw new InvalidOperationException("Maximum number of laboratories reached.");
            if (State.gold < cost) throw new InvalidOperationException("Not enough gold for a new laboratory.");
            State.gold -= cost;
            var lab = new PlayerLaboratoryState { id = $"lab_{State.nextLaboratoryNumber++}", level = 1 };
            State.laboratories.Add(lab);
            return lab;
        }

        public LaboratoryLevelDefinition UpgradeLaboratory(string laboratoryId = null)
        {
            EnsureNoPendingResult();
            var lab = string.IsNullOrEmpty(laboratoryId) ? State.laboratories.FirstOrDefault() : State.laboratories.Find(x => x.id == laboratoryId);
            if (lab == null) throw new InvalidOperationException("Unknown laboratory.");
            var next = Config.LaboratoryLevel(lab.level + 1) ?? throw new InvalidOperationException("Laboratory is already at maximum level.");
            if (State.gold < next.upgradeCost) throw new InvalidOperationException("Not enough gold for the laboratory upgrade.");
            State.gold -= next.upgradeCost; lab.level = next.level; State.laboratoryLevel = State.laboratories[0].level; return next;
        }

        public DeliveryResult ReceiveDelivery(string poolId = "pool_base")
        {
            EnsureNoPendingResult(); UpdateTime();
            if (State.availableFreeDeliveries <= 0) throw new InvalidOperationException("No free delivery is ready.");
            var pool = Config.Economy.deliveryPools.Find(x => x.id == poolId) ?? throw new InvalidOperationException($"Unknown delivery pool: {poolId}");
            var result = new DeliveryResult();
            for (var i = 0; i < pool.rolls; i++)
            {
                var entryId = WeightedPick(pool.entries.Select(x => (x.ingredientId, x.weight)).ToList());
                var entry = pool.entries.Find(x => x.ingredientId == entryId); var amount = random.Range(entry.minAmount, entry.maxAmount + 1);
                State.AddIngredient(entryId, amount); result.Items[entryId] = result.Items.GetValueOrDefault(entryId) + amount;
            }
            State.availableFreeDeliveries--;
            State.freeDeliveryLastUpdateUtc = time.UtcNow.ToString("O");
            return result;
        }

        public List<WeightedRarity> ProductRarityWeights(IReadOnlyList<string> ingredientIds, string recipeId = null, string laboratoryId = null)
        {
            ValidateKnownIngredients(ingredientIds);
            var ingredientQuality = ingredientIds.Average(id => Config.Rarity(Config.Ingredient(id).rarityId).qualityScore + Config.Ingredient(id).qualityBonus * 100f);
            var labQuality = (LaboratoryLevelFor(laboratoryId)?.productQualityBonus ?? 0f) * 100f;
            var masteryQuality = recipeId == null ? 0f : (MasteryLevel(recipeId)?.rarityBonus ?? 0f) * 100f;
            var quality = ingredientQuality * Config.Economy.ingredientQualityInfluence + labQuality * Config.Economy.laboratoryQualityInfluence + masteryQuality;
            return Config.Economy.productRarityWeights.Select(entry =>
            {
                var rank = Config.Rarity(entry.rarityId).rank; var multiplier = 1f + quality * Math.Max(0, rank - 1) / 100f;
                return new WeightedRarity { rarityId = entry.rarityId, weight = Math.Max(1, (int)Math.Round(entry.weight * multiplier)) };
            }).ToList();
        }

        private ProductResult CreateProduct(string recipeId, IReadOnlyList<string> ingredientIds, string laboratoryId)
        {
            var rarityId = WeightedPick(ProductRarityWeights(ingredientIds, recipeId, laboratoryId).Select(x => (x.rarityId, x.weight)).ToList());
            var recipe = Config.Recipe(recipeId); var rarity = Config.Rarity(rarityId);
            var ingredientBonus = ingredientIds.Average(id => Config.Ingredient(id).qualityBonus);
            var saleValue = Math.Max(1, (int)Math.Round(recipe.baseValue * rarity.valueMultiplier * (1f + ingredientBonus)));
            return new ProductResult { RecipeId = recipeId, RarityId = rarityId, SaleValue = saleValue };
        }

        public MasteryLevelDefinition MasteryLevel(string recipeId)
        {
            var recipeState = State.RecipeState(recipeId); return recipeState == null ? null : Config.MasteryLevelForCount(recipeState.timesCreated);
        }

        private void BeginPendingResult(string source, ProductResult product, IReadOnlyList<string> ingredientIds, bool wasDiscovered, bool rarityImproved)
        {
            var recipe = Config.Recipe(product.RecipeId);
            var productionEvent = new ProductionEvent(product.RecipeId, product.RarityId, recipe.categoryId, recipe.tags,
                ingredientIds, ingredientIds.Select(x => Config.Ingredient(x).groupId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList(),
                source, wasDiscovered, rarityImproved);
            State.pendingResult = new PendingResultState
            {
                source = source, recipeId = product.RecipeId, rarityId = product.RarityId, saleValue = product.SaleValue,
                wasDiscovered = wasDiscovered, rarityImproved = rarityImproved, ingredientIds = ingredientIds.ToList(),
                contractProgress = AdvanceContracts(productionEvent)
            };
        }

        private List<PendingContractProgress> AdvanceContracts(ProductionEvent productionEvent)
        {
            var changes = new List<PendingContractProgress>();
            foreach (var state in State.activeContracts)
            {
                if (state.progress >= state.amount || !Matches(state, productionEvent)) continue;
                var previous = state.progress;
                if (state.objectiveType == ContractObjectiveType.DistinctRecipes)
                {
                    if (state.seenRecipeIds.Contains(productionEvent.RecipeId)) continue;
                    state.seenRecipeIds.Add(productionEvent.RecipeId); state.progress = state.seenRecipeIds.Count;
                }
                else state.progress++;
                changes.Add(new PendingContractProgress { contractId = state.instanceId, previousProgress = previous,
                    currentProgress = state.progress, completed = previous < state.amount && state.progress >= state.amount });
            }
            return changes;
        }

        public bool Matches(ActiveContractState contract, ProductionEvent e)
        {
            if (!string.IsNullOrEmpty(contract.source) && contract.source != e.Source) return false;
            if (!string.IsNullOrEmpty(contract.minRarityId) && Config.Rarity(e.RarityId).rank < Config.Rarity(contract.minRarityId).rank) return false;
            return contract.objectiveType switch
            {
                ContractObjectiveType.Recipe => e.RecipeId == contract.targetId,
                ContractObjectiveType.Category => e.CategoryId == contract.targetId,
                ContractObjectiveType.Tag => e.Tags.Contains(contract.targetId),
                ContractObjectiveType.Rarity => e.RarityId == contract.targetId,
                ContractObjectiveType.Ingredient => e.IngredientIds.Contains(contract.targetId),
                ContractObjectiveType.Group => e.GroupIds.Contains(contract.targetId),
                ContractObjectiveType.Discover => e.WasDiscovered && (string.IsNullOrEmpty(contract.targetId) || e.RecipeId == contract.targetId),
                ContractObjectiveType.DistinctRecipes => MatchesOptionalTarget(contract.targetId, e),
                ContractObjectiveType.RecipeMinRarity => e.RecipeId == contract.targetId,
                ContractObjectiveType.ImproveRecord => e.RarityImproved && (string.IsNullOrEmpty(contract.targetId) || e.RecipeId == contract.targetId),
                ContractObjectiveType.Source => e.Source == contract.targetId,
                _ => false
            };
        }

        private bool MatchesOptionalTarget(string targetId, ProductionEvent e)
        {
            if (string.IsNullOrEmpty(targetId)) return true;
            if (Config.Category(targetId) != null) return e.CategoryId == targetId;
            return e.Tags.Contains(targetId);
        }

        public ActiveContractState RerollContract(string role)
        {
            EnsureNoPendingResult();
            if (!ContractRole.All.Contains(role)) throw new InvalidOperationException("Unknown contract role.");
            if (State.freeContractRerollsRemaining <= 0) throw new InvalidOperationException("No free contract rerolls remain.");
            State.activeContracts.RemoveAll(x => x.role == role); State.freeContractRerollsRemaining--;
            EnsureActiveContracts(); return State.activeContracts.Find(x => x.role == role);
        }

        private void EnsureActiveContracts()
        {
            foreach (var role in ContractRole.All)
                if (!State.activeContracts.Any(x => x.role == role))
                {
                    var generated = GenerateContract(role);
                    if (generated != null) State.activeContracts.Add(generated);
                }
        }

        private ActiveContractState GenerateContract(string role)
        {
            var activeTemplateIds = State.activeContracts.Select(x => x.templateId).ToHashSet();
            var activeTargets = State.activeContracts.Select(x => $"{x.objectiveType}:{x.targetId}").ToHashSet();
            var options = new List<(ContractTemplateDefinition template, string target)>();
            foreach (var template in Config.ContractTemplates.Where(x => x.enabled && x.role == role && x.minLaboratoryLevel <= HighestLaboratoryLevel && !activeTemplateIds.Contains(x.id)))
                foreach (var target in ResolveTargets(template).DefaultIfEmpty(null))
                    if (target != null && !activeTargets.Contains($"{template.objectiveType}:{target}")) options.Add((template, target));
            if (options.Count == 0) return null;
            var templateIds = options.Select(x => x.template.id).Distinct().ToList();
            var selectedTemplateId = WeightedPick(templateIds.Select(id => (id, Config.ContractTemplate(id).selectionWeight)).ToList());
            var selectedOptions = options.Where(x => x.template.id == selectedTemplateId).ToList();
            var selected = selectedOptions[random.Range(0, selectedOptions.Count)];
            return new ActiveContractState
            {
                instanceId = Guid.NewGuid().ToString("N"), templateId = selected.template.id, role = role,
                objectiveType = selected.template.objectiveType, targetId = selected.target,
                minRarityId = selected.template.minRarityId, source = selected.template.source,
                amount = random.Range(selected.template.minAmount, selected.template.maxAmount + 1),
                goldReward = random.Range(selected.template.minGoldReward, selected.template.maxGoldReward + 1),
                generatedAtUtc = time.UtcNow.ToString("O")
            };
        }

        private IEnumerable<string> ResolveTargets(ContractTemplateDefinition template)
        {
            var discovered = State.recipes.Select(x => x.recipeId).ToHashSet();
            var maxRarityRank = Config.Rarities.Max(x => x.rank);
            bool CanDiscoverMore() => Config.Recipes.Any(x => x.enabled && !discovered.Contains(x.id) && RecipeAccessible(x));
            bool CanImprove(string recipeId)
            {
                var state = State.RecipeState(recipeId);
                var rarity = state == null ? null : Config.Rarity(state.highestProductRarityId);
                return rarity != null && rarity.rank < maxRarityRank;
            }
            if (template.objectiveType == ContractObjectiveType.Discover && template.targetSelector == ContractTargetSelector.None && !CanDiscoverMore())
                return Array.Empty<string>();
            if (template.objectiveType == ContractObjectiveType.ImproveRecord && template.targetSelector == ContractTargetSelector.None)
                return State.recipes.Any(x => CanImprove(x.recipeId)) ? new[] { string.Empty } : Array.Empty<string>();
            if (!string.IsNullOrEmpty(template.fixedTargetId)) return new[] { template.fixedTargetId };
            if (template.allowedTargetIds != null && template.allowedTargetIds.Count > 0) return template.allowedTargetIds;
            return template.targetSelector switch
            {
                ContractTargetSelector.None => new[] { string.Empty },
                ContractTargetSelector.DiscoveredRecipe => Config.Recipes.Where(x => x.enabled && discovered.Contains(x.id) &&
                    (template.objectiveType != ContractObjectiveType.ImproveRecord || CanImprove(x.id))).Select(x => x.id),
                ContractTargetSelector.UndiscoveredRecipe => Config.Recipes.Where(x => x.enabled && !discovered.Contains(x.id) && RecipeAccessible(x)).Select(x => x.id),
                ContractTargetSelector.Category => Config.Categories.Where(c => Config.Recipes.Count(r => r.enabled && r.categoryId == c.id && RecipeAccessible(r)) >=
                    (template.objectiveType == ContractObjectiveType.DistinctRecipes ? template.maxAmount : 1)).Select(x => x.id),
                ContractTargetSelector.Tag => Config.Recipes.Where(x => x.enabled && RecipeAccessible(x)).SelectMany(x => x.tags).Distinct().Where(tag =>
                    template.objectiveType != ContractObjectiveType.DistinctRecipes || Config.Recipes.Count(r => r.enabled && r.tags.Contains(tag) && RecipeAccessible(r)) >= template.maxAmount),
                ContractTargetSelector.Rarity => Config.Rarities.Select(x => x.id),
                ContractTargetSelector.Ingredient => Config.Ingredients.Where(x => x.enabled && IngredientAccessible(x) &&
                    Config.Rarity(x.rarityId).rank <= (template.role == ContractRole.Basic ? 1 : template.role == ContractRole.Specialist ? 2 : 4)).Select(x => x.id),
                ContractTargetSelector.Group => Config.Groups.Where(g => Config.Ingredients.Any(i => i.enabled && i.groupId == g.id && IngredientAccessible(i))).Select(x => x.id),
                ContractTargetSelector.Source => new[] { LaboratoryJobType.Experiment, LaboratoryJobType.Production },
                _ => Array.Empty<string>()
            };
        }

        private bool IngredientAccessible(IngredientDefinition ingredient) => ingredient.rarityId != "rarity_legendary" || HighestLaboratoryLevel >= 3;
        private bool RecipeAccessible(RecipeDefinition recipe) => recipe.requirements.All(clause => clause.type switch
        {
            RecipeRequirementType.Ingredient => IngredientAccessible(Config.Ingredient(clause.ingredientId)),
            RecipeRequirementType.AnyOf => clause.ingredientIds.Any(id => IngredientAccessible(Config.Ingredient(id))),
            _ => true
        });

        private void RemoveDuplicateContracts()
        {
            var roles = new HashSet<string>(); var ids = new HashSet<string>();
            State.activeContracts.RemoveAll(x => !roles.Add(x.role) || !ids.Add(x.instanceId));
        }

        private (bool discovered, bool improved) UpdateRecipeBook(string recipeId, string rarityId, IReadOnlyList<string> ingredientIds)
        {
            var rarity = Config.Rarity(rarityId); var book = State.RecipeState(recipeId); var discovered = book == null; var improved = false;
            if (book == null)
            {
                book = new PlayerRecipeState { recipeId = recipeId, highestProductRarityId = rarityId, firstDiscoveredAt = time.UtcNow.ToString("O") };
                book.revealedIngredientIds.AddRange(ingredientIds.Distinct()); State.recipes.Add(book);
            }
            else if (Config.Rarity(book.highestProductRarityId).rank < rarity.rank) { book.highestProductRarityId = rarityId; improved = true; }
            book.timesCreated++; return (discovered, improved);
        }

        private string WeightedPick(IReadOnlyList<(string id, int weight)> values)
        {
            var total = values.Sum(x => x.weight); if (total <= 0) throw new InvalidOperationException("Weighted selection has no positive entries.");
            var roll = random.Range(0, total); foreach (var value in values) { roll -= value.weight; if (roll < 0) return value.id; }
            return values[^1].id;
        }

        private void EnsureNoPendingResult() { if (State.pendingResult != null) throw new InvalidOperationException("Claim the current result first."); }
        private LaboratoryJobState CreateJob(string type, string recipeId, IReadOnlyList<string> ingredientIds, int baseDurationSeconds, string laboratoryId)
        {
            var now = time.UtcNow; var level = LaboratoryLevelFor(laboratoryId) ?? CurrentLaboratoryLevel;
            var multiplier = type == LaboratoryJobType.Experiment ? level.experimentTimeMultiplier : level.productionTimeMultiplier;
            var seconds = Math.Max(1, (int)Math.Ceiling(baseDurationSeconds * multiplier));
            var job = new LaboratoryJobState { id = Guid.NewGuid().ToString("N"), type = type, recipeId = recipeId,
                laboratoryId = laboratoryId, startTimeUtc = now.ToString("O"), endTimeUtc = now.AddSeconds(seconds).ToString("O"), status = LaboratoryJobStatus.Running, ingredientIds = ingredientIds.ToList() };
            State.laboratoryJobs.Add(job); return job;
        }
        private string ResolveFreeLaboratory(string laboratoryId, string type)
        {
            UpdateTime();
            var lab = string.IsNullOrEmpty(laboratoryId)
                ? State.laboratories.FirstOrDefault(x => AvailableSlots(x.id, type) > 0)
                : State.laboratories.Find(x => x.id == laboratoryId);
            if (lab == null) throw new InvalidOperationException("Unknown laboratory.");
            if (AvailableSlots(lab.id, type) <= 0) throw new InvalidOperationException($"No free {type} slot in the selected laboratory.");
            return lab.id;
        }
        private static bool TryUtc(string value, out DateTime utc) => DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out utc);
        private void ConsumeIngredients(IReadOnlyList<string> ingredientIds) { foreach (var group in ingredientIds.GroupBy(x => x)) State.AddIngredient(group.Key, -group.Count()); }
        private void ValidateIngredientSelection(IReadOnlyList<string> ingredientIds, int requiredCount)
        {
            if (ingredientIds == null || ingredientIds.Count != requiredCount) throw new InvalidOperationException($"Select exactly {requiredCount} ingredients.");
            ValidateKnownIngredients(ingredientIds);
            foreach (var group in ingredientIds.GroupBy(x => x)) if (State.AmountOf(group.Key) < group.Count()) throw new InvalidOperationException($"Not enough {Config.Ingredient(group.Key).displayName}.");
        }
        private void ValidateKnownIngredients(IReadOnlyList<string> ingredientIds)
        {
            if (ingredientIds == null || ingredientIds.Count == 0) throw new InvalidOperationException("Select ingredients.");
            foreach (var id in ingredientIds) if (Config.Ingredient(id)?.enabled != true) throw new InvalidOperationException($"Unknown ingredient: {id}");
        }
    }
}
