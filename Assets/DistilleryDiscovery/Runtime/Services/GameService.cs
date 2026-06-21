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
            languageCode = languageCode == "pl" ? "pl" : "en"
        };

        public void ReplaceState(PlayerState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            var version = State.version;
            State.inventory ??= new List<InventoryEntry>();
            State.recipes ??= new List<PlayerRecipeState>();
            State.products ??= new List<ProductEntry>();
            State.activeContractIds ??= new List<string>();
            State.activeContracts ??= new List<ActiveContractState>();
            State.laboratoryJobs ??= new List<LaboratoryJobState>();
            if (State.pendingResult != null) State.pendingResult.contractProgress ??= new List<PendingContractProgress>();
            if (State.laboratoryLevel < 1) State.laboratoryLevel = 1;
            if (Config.LaboratoryLevel(State.laboratoryLevel) == null) State.laboratoryLevel = Config.LaboratoryLevels.Max(x => x.level);
            if (State.languageCode != "pl" && State.languageCode != "en") State.languageCode = "en";

            if (version < 3)
            {
                State.gold = checked(State.gold + State.products.Sum(x => checked(x.saleValue * x.amount)));
                foreach (var id in State.activeContractIds.Where(id => Config.Contract(id)?.enabled == true).Distinct())
                    if (State.activeContracts.Count < Config.Economy.activeContractCount)
                        State.activeContracts.Add(new ActiveContractState { contractId = id });
                State.products.Clear();
            }
            State.products.RemoveAll(x => x == null || Config.Recipe(x.recipeId) == null || Config.Rarity(x.rarityId) == null || x.amount <= 0);
            State.activeContractIds.Clear();
            State.activeContracts.RemoveAll(x => x == null || Config.Contract(x.contractId)?.enabled != true);
            RemoveDuplicateContracts();

            if (version < 4)
            {
                var completed = State.activeContracts.Where(x => x.progress >= Config.Contract(x.contractId).amount).ToList();
                foreach (var contractState in completed)
                    State.gold = checked(State.gold + Config.Contract(contractState.contractId).goldReward);
                State.activeContracts.RemoveAll(completed.Contains);
            }

            var targetCount = Math.Min(Config.Economy.activeContractCount, Config.Contracts.Count(x => x.enabled));
            if (State.activeContracts.Count > targetCount)
                State.activeContracts.RemoveRange(targetCount, State.activeContracts.Count - targetCount);
            EnsureActiveContracts();
            if (version < 6 || !TryUtc(State.freeDeliveryLastUpdateUtc, out _))
            {
                State.freeDeliveryLastUpdateUtc = time.UtcNow.ToString("O");
                State.availableFreeDeliveries = 0;
            }
            State.availableFreeDeliveries = Math.Clamp(State.availableFreeDeliveries, 0, Config.Economy.maxStoredFreeDeliveries);
            State.laboratoryJobs.RemoveAll(x => x == null || !TryUtc(x.endTimeUtc, out _) ||
                (x.type != LaboratoryJobType.Experiment && x.type != LaboratoryJobType.Production) ||
                x.ingredientIds == null || x.ingredientIds.Any(id => Config.Ingredient(id) == null) ||
                (x.type == LaboratoryJobType.Production && Config.Recipe(x.recipeId) == null));
            foreach (var job in State.laboratoryJobs)
            {
                job.ingredientIds ??= new List<string>();
                if (job.status != LaboratoryJobStatus.Running && job.status != LaboratoryJobStatus.Completed && job.status != LaboratoryJobStatus.Claimed)
                    job.status = LaboratoryJobStatus.Running;
            }
            State.version = 7;
            var deliveriesBefore = State.availableFreeDeliveries;
            var completedBefore = State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Completed);
            var elapsed = TryUtc(State.lastSavedAtUtc, out var savedAt) && savedAt <= time.UtcNow ? time.UtcNow - savedAt : TimeSpan.Zero;
            UpdateTime();
            LastOfflineDeliveriesGained = Math.Max(0, State.availableFreeDeliveries - deliveriesBefore);
            LastOfflineJobsCompleted = Math.Max(0, State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Completed) - completedBefore);
            LastOfflineSummary = new OfflineProgressSummary
            {
                Elapsed = elapsed,
                DeliveriesGained = LastOfflineDeliveriesGained,
                JobsCompleted = LastOfflineJobsCompleted,
                ExperimentsReady = State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Completed && x.type == LaboratoryJobType.Experiment),
                ProductionsReady = State.laboratoryJobs.Count(x => x.status == LaboratoryJobStatus.Completed && x.type == LaboratoryJobType.Production)
            };
        }

        private LaboratoryLevelDefinition CurrentLaboratoryLevel => Config.LaboratoryLevel(State.laboratoryLevel) ?? Config.LaboratoryLevel(1);
        public int ExperimentSlotCount => CurrentLaboratoryLevel.experimentSlots;
        public int ProductionSlotCount => CurrentLaboratoryLevel.productionSlots;
        public int AvailableExperimentSlots => Math.Max(0, ExperimentSlotCount - State.laboratoryJobs.Count(x =>
            x.type == LaboratoryJobType.Experiment && (x.status == LaboratoryJobStatus.Running || x.status == LaboratoryJobStatus.Completed)));
        public int AvailableProductionSlots => Math.Max(0, ProductionSlotCount - State.laboratoryJobs.Count(x =>
            x.type == LaboratoryJobType.Production && (x.status == LaboratoryJobStatus.Running || x.status == LaboratoryJobStatus.Completed)));
        public int AvailableLaboratorySlots => AvailableExperimentSlots + AvailableProductionSlots;

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
            if (!TryUtc(State.freeDeliveryLastUpdateUtc, out var anchor) || anchor > now)
            {
                State.freeDeliveryLastUpdateUtc = now.ToString("O");
                anchor = now;
            }
            var rawElapsedSeconds = (now - anchor).TotalSeconds;
            var elapsedSeconds = rawElapsedSeconds;
            var cap = Config.Economy.maxOfflineProgressSeconds;
            if (cap > 0) elapsedSeconds = Math.Min(elapsedSeconds, cap);
            var intervals = (int)(elapsedSeconds / Config.Economy.freeDeliveryIntervalSeconds);
            if (intervals > 0)
            {
                State.availableFreeDeliveries = Math.Min(Config.Economy.maxStoredFreeDeliveries, State.availableFreeDeliveries + intervals);
                State.freeDeliveryLastUpdateUtc = cap > 0 && rawElapsedSeconds > cap
                    ? now.ToString("O")
                    : anchor.AddSeconds((long)intervals * Config.Economy.freeDeliveryIntervalSeconds).ToString("O");
            }
            foreach (var job in State.laboratoryJobs.Where(x => x.status == LaboratoryJobStatus.Running))
                if (TryUtc(job.endTimeUtc, out var end) && end <= now) job.status = LaboratoryJobStatus.Completed;
        }

        public bool DebugAdvanceTime(TimeSpan duration)
        {
            if (time is not AdjustableTimeProvider adjustable) return false;
            adjustable.Advance(duration);
            UpdateTime();
            return true;
        }

        public List<OutcomeChance> Preview(IReadOnlyList<string> ingredientIds)
        {
            ValidateIngredientSelection(ingredientIds, Config.Economy.ingredientsPerExperiment);
            var weights = new Dictionary<string, int>();
            foreach (var id in ingredientIds)
                foreach (var outcome in Config.Ingredient(id).outcomeWeights)
                    weights[outcome.recipeId] = weights.GetValueOrDefault(outcome.recipeId) + outcome.weight;
            var total = weights.Values.Sum();
            if (total <= 0) throw new InvalidOperationException("Selected ingredients cannot produce any recipe.");
            return weights.Select(x => new OutcomeChance { RecipeId = x.Key, Weight = x.Value, Probability = (float)x.Value / total })
                .OrderByDescending(x => x.Weight).ThenBy(x => x.RecipeId).ToList();
        }

        public LaboratoryJobState StartExperiment(IReadOnlyList<string> ingredientIds)
        {
            EnsureNoPendingResult();
            Preview(ingredientIds);
            EnsureFreeLaboratorySlot(LaboratoryJobType.Experiment);
            ConsumeIngredients(ingredientIds);
            return CreateJob(LaboratoryJobType.Experiment, null, ingredientIds, Config.Economy.experimentDurationSeconds);
        }

        public LaboratoryJobState StartProduction(string recipeId, IReadOnlyList<string> ingredientIds)
        {
            EnsureNoPendingResult();
            var recipe = Config.Recipe(recipeId) ?? throw new InvalidOperationException($"Unknown recipe: {recipeId}");
            if (State.RecipeState(recipeId) == null) throw new InvalidOperationException("Production requires a discovered recipe.");
            ValidateIngredientSelection(ingredientIds, Config.Economy.ingredientsPerProduction);
            foreach (var ingredientId in ingredientIds)
                if (!Config.Ingredient(ingredientId).outcomeWeights.Any(x => x.recipeId == recipe.id && x.weight > 0))
                    throw new InvalidOperationException($"{Config.Ingredient(ingredientId).displayName} does not contribute to {recipe.displayName}.");

            EnsureFreeLaboratorySlot(LaboratoryJobType.Production);
            ConsumeIngredients(ingredientIds);
            return CreateJob(LaboratoryJobType.Production, recipeId, ingredientIds, Config.Economy.productionDurationSeconds);
        }

        public ProductResult ClaimLaboratoryJob(string jobId)
        {
            EnsureNoPendingResult();
            UpdateTime();
            var job = State.laboratoryJobs.Find(x => x.id == jobId) ?? throw new InvalidOperationException("Unknown laboratory job.");
            if (job.status != LaboratoryJobStatus.Completed) throw new InvalidOperationException("Laboratory job is not ready.");
            ProductResult result;
            if (job.type == LaboratoryJobType.Experiment)
            {
                var outcomes = PreviewWithoutInventory(job.ingredientIds);
                var recipeId = WeightedPick(outcomes.Select(x => (x.RecipeId, x.Weight)).ToList());
                var product = CreateProduct(recipeId, job.ingredientIds);
                var change = UpdateRecipeBook(recipeId, product.RarityId, job.ingredientIds);
                State.experimentsCompleted++;
                State.AddProduct(product.RecipeId, product.RarityId, product.SaleValue);
                BeginPendingResult(job.type, product, change.discovered, change.improved);
                result = new ExperimentResult { RecipeId = product.RecipeId, RarityId = product.RarityId, SaleValue = product.SaleValue, WasDiscovered = change.discovered, RarityImproved = change.improved };
            }
            else
            {
                var product = CreateProduct(job.recipeId, job.ingredientIds);
                var change = UpdateRecipeBook(job.recipeId, product.RarityId, job.ingredientIds);
                State.productionsCompleted++;
                State.AddProduct(product.RecipeId, product.RarityId, product.SaleValue);
                BeginPendingResult(job.type, product, false, change.improved);
                result = product;
            }
            job.status = LaboratoryJobStatus.Claimed;
            return result;
        }

        public CollectAllResult CollectAll()
        {
            EnsureNoPendingResult();
            UpdateTime();
            var result = new CollectAllResult();
            foreach (var job in State.laboratoryJobs.Where(x => x.status == LaboratoryJobStatus.Completed).ToList())
            {
                var product = ClaimLaboratoryJob(job.id);
                var pending = State.pendingResult;
                result.JobsCollected++;
                if (job.type == LaboratoryJobType.Experiment) result.ExperimentsCollected++; else result.ProductionsCollected++;
                if (pending.wasDiscovered) result.NewRecipesDiscovered++;
                if (pending.rarityImproved) result.RarityRecordsImproved++;
                result.ProductsAdded++;
                var productLine = result.Products.Find(x => x.RecipeId == product.RecipeId && x.RarityId == product.RarityId);
                if (productLine == null)
                {
                    productLine = new CollectedProductSummary { RecipeId = product.RecipeId, RarityId = product.RarityId };
                    result.Products.Add(productLine);
                }
                productLine.Amount++;
                result.GoldGained = checked(result.GoldGained + ClaimPendingResult().TotalGold);
            }
            return result;
        }

        public PendingClaimResult ClaimPendingResult()
        {
            var pending = State.pendingResult ?? throw new InvalidOperationException("There is no result to claim.");
            var completedIds = pending.contractProgress.Where(x => x.completed).Select(x => x.contractId).Distinct().ToList();
            var contractGold = completedIds.Sum(id => Config.Contract(id)?.goldReward ?? 0);
            var total = checked(pending.saleValue + contractGold);
            State.gold = checked(State.gold + total);
            var ingredientRewards = new Dictionary<string, int>();
            foreach (var contractId in completedIds)
            {
                var rewards = Config.Contract(contractId)?.ingredientRewards ?? new List<IngredientRewardDefinition>();
                foreach (var reward in rewards)
                {
                    var amount = random.Range(reward.minAmount, reward.maxAmount + 1);
                    State.AddIngredient(reward.ingredientId, amount);
                    ingredientRewards[reward.ingredientId] = ingredientRewards.GetValueOrDefault(reward.ingredientId) + amount;
                }
            }
            State.activeContracts.RemoveAll(x => completedIds.Contains(x.contractId));
            State.pendingResult = null;
            EnsureActiveContracts(completedIds);
            var result = new PendingClaimResult
            {
                ProductGold = pending.saleValue,
                ContractGold = contractGold,
                TotalGold = total,
                CompletedContractIds = completedIds
            };
            foreach (var reward in ingredientRewards) result.IngredientRewards[reward.Key] = reward.Value;
            return result;
        }

        public LaboratoryLevelDefinition UpgradeLaboratory()
        {
            EnsureNoPendingResult();
            var next = Config.LaboratoryLevel(State.laboratoryLevel + 1)
                ?? throw new InvalidOperationException("Laboratory is already at maximum level.");
            if (State.gold < next.upgradeCost) throw new InvalidOperationException("Not enough gold for the laboratory upgrade.");
            State.gold -= next.upgradeCost;
            State.laboratoryLevel = next.level;
            return next;
        }

        public DeliveryResult ReceiveDelivery(string poolId = "pool_base")
        {
            EnsureNoPendingResult();
            UpdateTime();
            if (State.availableFreeDeliveries <= 0) throw new InvalidOperationException("No free delivery is ready.");
            var pool = Config.Economy.deliveryPools.Find(x => x.id == poolId) ?? throw new InvalidOperationException($"Unknown delivery pool: {poolId}");
            var result = new DeliveryResult();
            var rolls = random.Range(Config.Economy.freeDeliveryMinItems, Config.Economy.freeDeliveryMaxItems + 1);
            for (var i = 0; i < rolls; i++)
            {
                var entryId = WeightedPick(pool.entries.Select(x => (x.ingredientId, x.weight)).ToList());
                var entry = pool.entries.Find(x => x.ingredientId == entryId);
                var amount = random.Range(entry.minAmount, entry.maxAmount + 1);
                State.AddIngredient(entryId, amount);
                result.Items[entryId] = result.Items.GetValueOrDefault(entryId) + amount;
            }
            State.availableFreeDeliveries--;
            return result;
        }

        public List<WeightedRarity> ProductRarityWeights(IReadOnlyList<string> ingredientIds, string recipeId = null)
        {
            ValidateKnownIngredients(ingredientIds);
            var ingredientQuality = ingredientIds.Average(id =>
                Config.Rarity(Config.Ingredient(id).rarityId).qualityScore + Config.Ingredient(id).qualityBonus * 100f);
            var labQuality = (Config.LaboratoryLevel(State.laboratoryLevel)?.productQualityBonus ?? 0f) * 100f;
            var masteryQuality = recipeId == null ? 0f : (MasteryLevel(recipeId)?.rarityBonus ?? 0f) * 100f;
            var quality = ingredientQuality * Config.Economy.ingredientQualityInfluence
                + labQuality * Config.Economy.laboratoryQualityInfluence + masteryQuality;
            return Config.Economy.productRarityWeights.Select(entry =>
            {
                var rank = Config.Rarity(entry.rarityId).rank;
                var multiplier = 1f + quality * Math.Max(0, rank - 1) / 100f;
                return new WeightedRarity { rarityId = entry.rarityId, weight = Math.Max(1, (int)Math.Round(entry.weight * multiplier)) };
            }).ToList();
        }

        private ProductResult CreateProduct(string recipeId, IReadOnlyList<string> ingredientIds)
        {
            var rarityId = WeightedPick(ProductRarityWeights(ingredientIds, recipeId).Select(x => (x.rarityId, x.weight)).ToList());
            var recipe = Config.Recipe(recipeId);
            var rarity = Config.Rarity(rarityId);
            var ingredientBonus = ingredientIds.Average(id => Config.Ingredient(id).qualityBonus);
            var saleValue = Math.Max(1, (int)Math.Round(recipe.baseValue * rarity.valueMultiplier * (1f + ingredientBonus)));
            return new ProductResult { RecipeId = recipeId, RarityId = rarityId, SaleValue = saleValue };
        }

        public MasteryLevelDefinition MasteryLevel(string recipeId)
        {
            var recipeState = State.RecipeState(recipeId);
            return recipeState == null ? null : Config.MasteryLevelForCount(recipeState.timesCreated);
        }

        private void BeginPendingResult(string source, ProductResult product, bool wasDiscovered, bool rarityImproved)
        {
            State.pendingResult = new PendingResultState
            {
                source = source,
                recipeId = product.RecipeId,
                rarityId = product.RarityId,
                saleValue = product.SaleValue,
                wasDiscovered = wasDiscovered,
                rarityImproved = rarityImproved,
                contractProgress = AdvanceContracts(product.RecipeId, product.RarityId)
            };
        }

        private List<PendingContractProgress> AdvanceContracts(string recipeId, string rarityId)
        {
            var changes = new List<PendingContractProgress>();
            foreach (var state in State.activeContracts)
            {
                var contract = Config.Contract(state.contractId);
                if (contract == null || state.progress >= contract.amount || !Matches(contract, recipeId, rarityId)) continue;
                var previous = state.progress;
                state.progress++;
                changes.Add(new PendingContractProgress
                {
                    contractId = contract.id,
                    previousProgress = previous,
                    currentProgress = state.progress,
                    completed = previous < contract.amount && state.progress >= contract.amount
                });
            }
            return changes;
        }

        private bool Matches(ContractDefinition contract, string recipeId, string rarityId)
        {
            return contract.requirementType switch
            {
                ContractRequirementType.Recipe => recipeId == contract.targetId,
                ContractRequirementType.Rarity => rarityId == contract.targetId,
                ContractRequirementType.Category => Config.Recipe(recipeId)?.categoryId == contract.targetId,
                _ => false
            };
        }

        private void EnsureActiveContracts(IReadOnlyCollection<string> justCompletedIds = null)
        {
            var targetCount = Math.Min(Config.Economy.activeContractCount, Config.Contracts.Count(x => x.enabled));
            while (State.activeContracts.Count < targetCount)
            {
                var activeIds = State.activeContracts.Select(x => x.contractId).ToHashSet();
                var candidates = Config.Contracts.Where(x => x.enabled && !activeIds.Contains(x.id) && (justCompletedIds == null || !justCompletedIds.Contains(x.id))).ToList();
                if (candidates.Count == 0)
                    candidates = Config.Contracts.Where(x => x.enabled && !activeIds.Contains(x.id)).ToList();
                if (candidates.Count == 0) break;
                var selected = candidates[random.Range(0, candidates.Count)];
                State.activeContracts.Add(new ActiveContractState { contractId = selected.id });
            }
        }

        private void RemoveDuplicateContracts()
        {
            var seen = new HashSet<string>();
            State.activeContracts.RemoveAll(x => !seen.Add(x.contractId));
        }

        private (bool discovered, bool improved) UpdateRecipeBook(string recipeId, string rarityId, IReadOnlyList<string> ingredientIds)
        {
            var rarity = Config.Rarity(rarityId);
            var book = State.RecipeState(recipeId);
            var discovered = book == null;
            var improved = false;
            if (book == null)
            {
                book = new PlayerRecipeState { recipeId = recipeId, highestProductRarityId = rarityId, firstDiscoveredAt = time.UtcNow.ToString("O") };
                book.revealedIngredientIds.AddRange(ingredientIds.Distinct());
                State.recipes.Add(book);
            }
            else if (Config.Rarity(book.highestProductRarityId).rank < rarity.rank)
            {
                book.highestProductRarityId = rarityId;
                improved = true;
            }
            book.timesCreated++;
            return (discovered, improved);
        }

        private string WeightedPick(IReadOnlyList<(string id, int weight)> values)
        {
            var total = values.Sum(x => x.weight);
            if (total <= 0) throw new InvalidOperationException("Weighted selection has no positive entries.");
            var roll = random.Range(0, total);
            foreach (var value in values) { roll -= value.weight; if (roll < 0) return value.id; }
            return values[^1].id;
        }

        private void EnsureNoPendingResult()
        {
            if (State.pendingResult != null) throw new InvalidOperationException("Claim the current result first.");
        }

        private LaboratoryJobState CreateJob(string type, string recipeId, IReadOnlyList<string> ingredientIds, int baseDurationSeconds)
        {
            var now = time.UtcNow;
            var level = CurrentLaboratoryLevel;
            var multiplier = type == LaboratoryJobType.Experiment ? level.experimentTimeMultiplier : level.productionTimeMultiplier;
            var seconds = Math.Max(1, (int)Math.Ceiling(baseDurationSeconds * multiplier));
            var job = new LaboratoryJobState
            {
                id = Guid.NewGuid().ToString("N"), type = type, recipeId = recipeId,
                startTimeUtc = now.ToString("O"), endTimeUtc = now.AddSeconds(seconds).ToString("O"),
                status = LaboratoryJobStatus.Running, ingredientIds = ingredientIds.ToList()
            };
            State.laboratoryJobs.Add(job);
            return job;
        }

        private void EnsureFreeLaboratorySlot(string type)
        {
            UpdateTime();
            var available = type == LaboratoryJobType.Experiment ? AvailableExperimentSlots : AvailableProductionSlots;
            if (available <= 0) throw new InvalidOperationException($"No free {type} slot.");
        }

        private List<OutcomeChance> PreviewWithoutInventory(IReadOnlyList<string> ingredientIds)
        {
            ValidateKnownIngredients(ingredientIds);
            var weights = new Dictionary<string, int>();
            foreach (var id in ingredientIds)
                foreach (var outcome in Config.Ingredient(id).outcomeWeights)
                    weights[outcome.recipeId] = weights.GetValueOrDefault(outcome.recipeId) + outcome.weight;
            var total = weights.Values.Sum();
            if (total <= 0) throw new InvalidOperationException("Selected ingredients cannot produce any recipe.");
            return weights.Select(x => new OutcomeChance { RecipeId = x.Key, Weight = x.Value, Probability = (float)x.Value / total }).ToList();
        }

        private static bool TryUtc(string value, out DateTime utc) =>
            DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out utc);

        private void ConsumeIngredients(IReadOnlyList<string> ingredientIds)
        {
            foreach (var group in ingredientIds.GroupBy(x => x)) State.AddIngredient(group.Key, -group.Count());
        }

        private void ValidateIngredientSelection(IReadOnlyList<string> ingredientIds, int requiredCount)
        {
            if (ingredientIds == null || ingredientIds.Count != requiredCount)
                throw new InvalidOperationException($"Select exactly {requiredCount} ingredients.");
            ValidateKnownIngredients(ingredientIds);
            foreach (var group in ingredientIds.GroupBy(x => x))
                if (State.AmountOf(group.Key) < group.Count()) throw new InvalidOperationException($"Not enough {Config.Ingredient(group.Key).displayName}.");
        }

        private void ValidateKnownIngredients(IReadOnlyList<string> ingredientIds)
        {
            if (ingredientIds == null || ingredientIds.Count == 0) throw new InvalidOperationException("Select ingredients.");
            foreach (var id in ingredientIds)
                if (Config.Ingredient(id) == null) throw new InvalidOperationException($"Unknown ingredient: {id}");
        }
    }
}
