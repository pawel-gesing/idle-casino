using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DistilleryDiscovery
{
    public sealed class PrototypeUI : MonoBehaviour
    {
        private enum FooterMode { Experiment, Production, Laboratory, None }
        private sealed class LocalizedBinding { public Text Text; public string Key; public string Fallback; }
        private sealed class TileOption { public string Label; public Action Action; }

        private static readonly Color Ink = new(0.09f, 0.08f, 0.12f);
        private static readonly Color Plum = new(0.25f, 0.08f, 0.22f);
        private static readonly Color Gold = new(0.95f, 0.68f, 0.18f);
        private static readonly Color Cream = new(0.96f, 0.91f, 0.79f);
        private readonly List<string> selection = new();
        private readonly List<LocalizedBinding> localizedBindings = new();
        private GameService game;
        private SaveService save;
        private Font font;
        private CanvasScaler canvasScaler;
        private RectTransform safeAreaRoot;
        private GridLayoutGroup navigationGrid;
        private ScrollRect scrollRect;
        private GameObject textContentObject;
        private RectTransform textContentRect;
        private GameObject tileContentObject;
        private RectTransform tileContentRect;
        private GameObject tileGridObject;
        private VerticalLayoutGroup tileLayout;
        private GridLayoutGroup tileGrid;
        private Text experimentPreviewText;
        private Text statusText;
        private Text contentText;
        private Text selectionText;
        private Text secondaryActionText;
        private Text primaryActionText;
        private Text goldHeaderText;
        private Text recipesHeaderText;
        private Text ingredientsHeaderText;
        private Text pendingSummaryText;
        private GameObject settingsModal;
        private GameObject pendingModal;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private FooterMode footerMode;
        private string selectedProductionRecipeId;

        private string Language => game.State.languageCode == "pl" ? "pl" : "en";

        private void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafeArea || screenSize != lastScreenSize) ApplyMobileLayout();
        }

        public void Initialize(GameService gameService, SaveService saveService)
        {
            game = gameService;
            save = saveService;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            if (game.State.pendingResult != null) ShowPendingResult();
            else ShowState(T("ui.status.ready", "Ready. Receive a delivery to begin."));
        }

        private void Build()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            var canvasObject = Node("Canvas", transform, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasScaler = canvasObject.GetComponent<CanvasScaler>(); canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; canvasScaler.referenceResolution = new Vector2(1080, 1920);
            if (FindAnyObjectByType<EventSystem>() == null) Node("EventSystem", transform, typeof(EventSystem), typeof(StandaloneInputModule));
            safeAreaRoot = Node("SafeArea", canvasObject.transform, typeof(RectTransform)).GetComponent<RectTransform>(); Stretch(safeAreaRoot.gameObject);
            var background = Panel("Background", safeAreaRoot, Ink); Stretch(background);

            BuildHeader(background.transform);
            var status = Panel("Status", background.transform, new Color(.14f, .12f, .18f)); Rect(status, 0, .80f, 1, .86f);
            statusText = Label("", status.transform, 28, TextAnchor.MiddleCenter); Stretch(statusText.gameObject, 18, 6);

            var nav = Panel("Menu", background.transform, new Color(.11f, .10f, .14f)); Rect(nav, 0, .67f, 1, .80f);
            navigationGrid = nav.AddComponent<GridLayoutGroup>(); navigationGrid.padding = new RectOffset(18, 18, 10, 10); navigationGrid.spacing = new Vector2(10, 8); navigationGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; navigationGrid.constraintCount = 3;
            AddLocalizedButton(nav.transform, "ui.nav.experiment", "EXPERIMENT", ShowExperiment);
            AddLocalizedButton(nav.transform, "ui.nav.production", "PRODUCTION", ShowProduction);
            AddLocalizedButton(nav.transform, "ui.nav.contracts", "CONTRACTS", ShowContracts);
            AddLocalizedButton(nav.transform, "ui.nav.delivery", "DELIVERY", ReceiveDelivery);
            AddLocalizedButton(nav.transform, "ui.nav.laboratory", "LABORATORY", ShowLaboratory);

            BuildContent(background.transform);
            BuildFooter(background.transform);
            BuildSettingsModal();
            BuildPendingModal();
            ApplyMobileLayout();
            RefreshHeaderCounters();
        }

        private void BuildHeader(Transform parent)
        {
            var header = Panel("Header", parent, Plum); Rect(header, 0, .86f, 1, 1);
            var title = Label("DISTILLERY DISCOVERY", header.transform, 38, TextAnchor.MiddleLeft); Rect(title.gameObject, .035f, .52f, .82f, .98f);
            var goldTile = AddButton(header.transform, "", () => ShowState(), Gold, Ink); Rect(goldTile, .03f, .08f, .28f, .47f); goldHeaderText = goldTile.GetComponentInChildren<Text>();
            var recipesTile = AddButton(header.transform, "", ShowRecipeBook); Rect(recipesTile, .30f, .08f, .57f, .47f); recipesHeaderText = recipesTile.GetComponentInChildren<Text>();
            var ingredientsTile = AddButton(header.transform, "", ShowIngredientInventory); Rect(ingredientsTile, .59f, .08f, .82f, .47f); ingredientsHeaderText = ingredientsTile.GetComponentInChildren<Text>();
            var settings = AddButton(header.transform, "⚙", OpenSettings, Gold, Ink); Rect(settings, .86f, .25f, .97f, .75f);
        }

        private void BuildContent(Transform parent)
        {
            var scroll = Panel("Content", parent, new Color(.07f, .065f, .09f)); Rect(scroll, .025f, .16f, .975f, .66f);
            scrollRect = scroll.AddComponent<ScrollRect>();
            var viewport = Panel("Viewport", scroll.transform, Color.clear); Stretch(viewport); viewport.AddComponent<RectMask2D>();

            textContentObject = Node("TextContent", viewport.transform, typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
            contentText = textContentObject.GetComponent<Text>(); contentText.font = font; contentText.fontSize = 32; contentText.color = Cream; contentText.alignment = TextAnchor.UpperLeft; contentText.supportRichText = true;
            textContentRect = textContentObject.GetComponent<RectTransform>(); textContentRect.anchorMin = new Vector2(0, 1); textContentRect.anchorMax = new Vector2(1, 1); textContentRect.pivot = new Vector2(.5f, 1); textContentRect.offsetMin = new Vector2(28, 0); textContentRect.offsetMax = new Vector2(-28, 0);
            textContentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tileContentObject = Node("TileContent", viewport.transform, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            tileContentRect = tileContentObject.GetComponent<RectTransform>(); tileContentRect.anchorMin = new Vector2(0, 1); tileContentRect.anchorMax = new Vector2(1, 1); tileContentRect.pivot = new Vector2(.5f, 1); tileContentRect.offsetMin = new Vector2(12, 0); tileContentRect.offsetMax = new Vector2(-12, 0);
            tileLayout = tileContentObject.GetComponent<VerticalLayoutGroup>(); tileLayout.padding = new RectOffset(8, 8, 12, 12); tileLayout.spacing = 20; tileLayout.childControlWidth = true; tileLayout.childControlHeight = true; tileLayout.childForceExpandWidth = true; tileLayout.childForceExpandHeight = false;
            tileContentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tileGridObject = Node("TileGrid", tileContentObject.transform, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            tileGrid = tileGridObject.GetComponent<GridLayoutGroup>(); tileGrid.spacing = new Vector2(12, 12); tileGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; tileGrid.constraintCount = 2;
            tileGridObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            experimentPreviewText = Label("", tileContentObject.transform, 30, TextAnchor.UpperLeft);
            experimentPreviewText.supportRichText = true;
            experimentPreviewText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            experimentPreviewText.gameObject.SetActive(false);
            tileContentObject.SetActive(false);

            scrollRect.viewport = viewport.GetComponent<RectTransform>(); scrollRect.content = textContentRect; scrollRect.horizontal = false;
        }

        private void BuildFooter(Transform parent)
        {
            var footer = Panel("ActionBar", parent, Plum); Rect(footer, 0, 0, 1, .15f);
            selectionText = Label("", footer.transform, 28, TextAnchor.MiddleCenter); Rect(selectionText.gameObject, .03f, .54f, .97f, .95f);
            var secondary = AddButton(footer.transform, "—", ClearSelection); Rect(secondary, .02f, .10f, .30f, .48f); secondaryActionText = secondary.GetComponentInChildren<Text>();
            var primary = AddButton(footer.transform, "—", DispatchPrimary, Gold, Ink); Rect(primary, .32f, .10f, .98f, .48f); primaryActionText = primary.GetComponentInChildren<Text>();
            UpdateSelection();
        }

        private void BuildSettingsModal()
        {
            settingsModal = Panel("SettingsModal", safeAreaRoot, new Color(0f, 0f, 0f, .78f)); Stretch(settingsModal);
            var card = Panel("SettingsCard", settingsModal.transform, new Color(.13f, .10f, .16f)); Rect(card, .12f, .20f, .88f, .80f);
            var title = Label("", card.transform, 42, TextAnchor.MiddleCenter); Rect(title.gameObject, .10f, .83f, .80f, .96f); Bind(title, "ui.settings.title", "SETTINGS");
            var close = AddButton(card.transform, "X", CloseSettings, Gold, Ink); Rect(close, .84f, .85f, .96f, .96f);
            var saveButton = AddLocalizedButton(card.transform, "ui.settings.save", "SAVE", SaveGame); Rect(saveButton, .12f, .65f, .88f, .76f);
            var loadButton = AddLocalizedButton(card.transform, "ui.settings.load", "LOAD", LoadGame); Rect(loadButton, .12f, .52f, .88f, .63f);
            var resetButton = AddLocalizedButton(card.transform, "ui.settings.reset", "RESET", ResetGame); Rect(resetButton, .12f, .39f, .88f, .50f);
            var languageButton = AddLocalizedButton(card.transform, "ui.settings.language", "LANGUAGE: ENGLISH", ToggleLanguage); Rect(languageButton, .12f, .26f, .88f, .37f);
            var exitButton = AddLocalizedButton(card.transform, "ui.settings.exit", "EXIT", Application.Quit); Rect(exitButton, .12f, .10f, .88f, .21f);
            settingsModal.SetActive(false);
        }

        private void BuildPendingModal()
        {
            pendingModal = Panel("PendingResultModal", safeAreaRoot, new Color(0f, 0f, 0f, .86f)); Stretch(pendingModal);
            var card = Panel("ResultCard", pendingModal.transform, new Color(.13f, .10f, .16f)); Rect(card, .07f, .09f, .93f, .91f);
            pendingSummaryText = Label("", card.transform, 30, TextAnchor.UpperLeft); Rect(pendingSummaryText.gameObject, .06f, .23f, .94f, .94f);
            var claim = AddLocalizedButton(card.transform, "ui.action.claim", "CLAIM", ClaimPendingResult, Gold, Ink); Rect(claim, .12f, .06f, .88f, .18f);
            pendingModal.SetActive(false);
        }

        private void ApplyMobileLayout()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            var raw = Screen.safeArea; var safe = raw; const float portraitAspect = 9f / 16f;
            if (safe.height > 0f && safe.width / safe.height > portraitAspect) { var width = safe.height * portraitAspect; safe.x += (safe.width - width) * .5f; safe.width = width; }
            canvasScaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;
            safeAreaRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height); safeAreaRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height); safeAreaRoot.offsetMin = Vector2.zero; safeAreaRoot.offsetMax = Vector2.zero;
            lastSafeArea = raw; lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            Canvas.ForceUpdateCanvases();
            ResizeGrid(navigationGrid, 3, 2);
            var tileWidth = (tileContentRect.rect.width - tileLayout.padding.horizontal - tileGrid.spacing.x) / 2f;
            tileGrid.cellSize = new Vector2(Mathf.Max(1f, tileWidth), 112f);
        }

        private static void ResizeGrid(GridLayoutGroup grid, int columns, int rows)
        {
            var rect = grid.GetComponent<RectTransform>().rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            grid.cellSize = new Vector2((rect.width - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns, (rect.height - grid.padding.vertical - grid.spacing.y * (rows - 1)) / rows);
        }

        private void ShowState(string message = null)
        {
            PrepareTextView(FooterMode.None); SetStatus(message ?? T("ui.status.state", "Current player state"));
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.state", "PLAYER STATE")}</b></color>\n\n{T("ui.label.gold", "Gold")}: <b>{game.State.gold}</b>\n{T("ui.label.laboratory", "Laboratory")}: <b>{T("ui.label.level", "level")} {game.State.laboratoryLevel}</b>\n{T("ui.label.experiments", "Experiments")}: <b>{game.State.experimentsCompleted}</b>\n{T("ui.label.productions", "Productions")}: <b>{game.State.productionsCompleted}</b>\n{T("ui.label.ingredients", "Ingredients")}: <b>{game.State.inventory.Sum(x => Math.Max(0, x.amount))}</b>\n{T("ui.label.recipes", "Discovered recipes")}: <b>{game.State.recipes.Count}/{game.Config.Recipes.Count}</b> ({Completion():0.#}%)";
        }

        private void ReceiveDelivery()
        {
            try
            {
                PrepareTextView(FooterMode.None); var delivery = game.ReceiveDelivery();
                contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.delivery", "NEW DELIVERY")}</b></color>\n\n" + string.Join("\n", delivery.Items.Select(x => $"+{x.Value}  {IngredientName(x.Key)}"));
                SetStatus(T("ui.status.delivery", "Delivery received")); RefreshHeaderCounters();
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void ShowIngredientInventory()
        {
            PrepareTextView(FooterMode.None); SetStatus(T("ui.status.ingredients", "Ingredient storage"));
            var sb = new StringBuilder($"<color=#F2AD2E><b>{T("ui.heading.ingredients", "INGREDIENTS")}</b></color>\n\n");
            foreach (var item in game.Config.Ingredients) { var rarity = game.Config.Rarity(item.rarityId); sb.Append($"<color={rarity.colorHex}>●</color>  {IngredientName(item.id),-20}  <b>x{game.State.AmountOf(item.id)}</b>\n"); }
            contentText.text = sb.ToString();
        }

        private void ShowRecipeBook()
        {
            PrepareTextView(FooterMode.None); SetStatus(F("ui.status.recipe_book", "Recipe book — {0:0.#}% complete", Completion()));
            var sb = new StringBuilder($"<color=#F2AD2E><b>{T("ui.heading.recipe_book", "RECIPE BOOK")}  {game.State.recipes.Count}/{game.Config.Recipes.Count}</b></color>\n\n");
            foreach (var recipe in game.Config.Recipes)
            {
                var state = game.State.RecipeState(recipe.id);
                if (state == null) sb.Append($"◇  ???\n    {T("ui.recipe.undiscovered", "Undiscovered recipe")}\n\n");
                else { var rarity = game.Config.Rarity(state.highestProductRarityId); sb.Append($"◆  <b>{RecipeName(recipe.id)}</b>\n    {T("ui.label.record", "Record")}: <color={rarity.colorHex}>{RarityName(rarity.id)}</color> · {T("ui.label.created", "Created")}: {state.timesCreated}\n    {T("ui.label.discovered_ingredients", "Discovered ingredients")}: {string.Join(", ", state.revealedIngredientIds.Select(IngredientName))}\n\n"); }
            }
            contentText.text = sb.ToString();
        }

        private void ShowExperiment()
        {
            selection.Clear(); selectedProductionRecipeId = null; SetMode(FooterMode.Experiment);
            SetStatus(T("ui.status.choose_ingredient_tiles", "Tap ingredient tiles to reserve exactly three"));
            ShowIngredientTiles(game.Config.Ingredients);
        }

        private void ShowProduction()
        {
            selection.Clear(); selectedProductionRecipeId = null; SetMode(FooterMode.Production);
            var recipes = game.Config.Recipes.Where(x => game.State.RecipeState(x.id) != null).ToList();
            SetStatus(T("ui.status.choose_recipe_tile", "Choose a discovered recipe"));
            ShowTiles(recipes.Select(recipe => new TileOption { Label = $"{RecipeName(recipe.id)}\n{CategoryName(recipe.categoryId)}", Action = () => SelectProductionRecipe(recipe.id) }));
        }

        private void SelectProductionRecipe(string recipeId)
        {
            selectedProductionRecipeId = recipeId; selection.Clear(); UpdateSelection();
            SetStatus(F("ui.status.chosen_recipe", "Recipe: {0}. Select three ingredients.", RecipeName(recipeId)));
            ShowIngredientTiles(game.Config.Ingredients.Where(i => i.outcomeWeights.Any(x => x.recipeId == recipeId && x.weight > 0)));
        }

        private void ShowIngredientTiles(IEnumerable<IngredientDefinition> ingredients)
        {
            ShowTiles(ingredients.Select(item =>
            {
                var available = game.State.AmountOf(item.id) - selection.Count(x => x == item.id);
                return new TileOption { Label = $"{IngredientName(item.id)}\nx{Math.Max(0, available)}", Action = () => ReserveIngredient(item.id) };
            }));
            UpdateExperimentPreview();
        }

        private void UpdateExperimentPreview()
        {
            experimentPreviewText.gameObject.SetActive(footerMode == FooterMode.Experiment && selection.Count == game.Config.Economy.ingredientsPerExperiment);
            if (!experimentPreviewText.gameObject.activeSelf) return;

            var sb = new StringBuilder($"<color=#F2AD2E><b>{T("ui.heading.outcomes", "POSSIBLE OUTCOMES")}</b></color>\n\n");
            foreach (var outcome in game.Preview(selection))
            {
                var productName = game.State.RecipeState(outcome.RecipeId) == null ? "???" : RecipeName(outcome.RecipeId);
                sb.Append($"{productName}  <b>{outcome.Probability:P1}</b>\n");
            }
            experimentPreviewText.text = sb.ToString();
            SetStatus(T("ui.status.preview", "Preview ready"));
            Canvas.ForceUpdateCanvases();
        }

        private void ReserveIngredient(string ingredientId)
        {
            if (selection.Count >= 3) { SetStatus(T("ui.error.selection_full", "Three ingredients are already selected.")); return; }
            if (game.State.AmountOf(ingredientId) <= selection.Count(x => x == ingredientId)) { SetStatus(T("ui.error.no_ingredient", "No more of this ingredient.")); return; }
            selection.Add(ingredientId); UpdateSelection();
            if (footerMode == FooterMode.Experiment) ShowIngredientTiles(game.Config.Ingredients);
            else ShowIngredientTiles(game.Config.Ingredients.Where(i => i.outcomeWeights.Any(x => x.recipeId == selectedProductionRecipeId && x.weight > 0)));
        }

        private void ShowContracts()
        {
            PrepareTextView(FooterMode.None); SetStatus(T("ui.status.contracts", "Three contracts are active at once"));
            var sb = new StringBuilder($"<color=#F2AD2E><b>{T("ui.heading.contracts", "ACTIVE CONTRACTS")}</b></color>\n\n");
            foreach (var state in game.State.activeContracts)
            {
                var contract = game.Config.Contract(state.contractId);
                sb.Append($"<b>{ContractName(contract.id)}</b>\n{DescribeRequirement(contract)}\n{T("ui.label.progress", "Progress")}: <b>{state.progress}/{contract.amount}</b> · {T("ui.label.reward", "Reward")}: <b>{contract.goldReward} {T("ui.label.gold_lower", "gold")}</b>\n\n");
            }
            contentText.text = sb.ToString();
        }

        private void ShowLaboratory()
        {
            PrepareTextView(FooterMode.Laboratory); var current = game.Config.LaboratoryLevel(game.State.laboratoryLevel); var next = game.Config.LaboratoryLevel(game.State.laboratoryLevel + 1);
            SetStatus(T("ui.status.laboratory", "Laboratory improves product rarity"));
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.laboratory", "LABORATORY")} · {T("ui.label.level", "LEVEL")} {current.level}</b></color>\n\n{T("ui.label.rarity_bonus", "Higher rarity bonus")}: <b>+{current.productQualityBonus:P0}</b>\n\n" + (next == null ? T("ui.laboratory.max", "Maximum level reached.") : $"{T("ui.label.next_level", "Next level")}: <b>{next.level}</b>\n{T("ui.label.new_bonus", "New bonus")}: <b>+{next.productQualityBonus:P0}</b>\n{T("ui.label.cost", "Cost")}: <b>{next.upgradeCost} {T("ui.label.gold_lower", "gold")}</b>");
        }

        private void ShowPendingResult()
        {
            var pending = game.State.pendingResult; if (pending == null) return;
            var completed = pending.contractProgress.Where(x => x.completed).ToList();
            var contractGold = completed.Sum(x => game.Config.Contract(x.contractId)?.goldReward ?? 0);
            var sb = new StringBuilder();
            sb.Append($"<color=#F2AD2E><b>{(pending.source == "experiment" ? T("ui.heading.experiment_result", "EXPERIMENT RESULT") : T("ui.heading.production_result", "PRODUCTION COMPLETE"))}</b></color>\n\n");
            sb.Append($"{T("ui.label.created_product", "Product")}: <b>{RecipeName(pending.recipeId)}</b>\n{T("ui.label.quality", "Rarity")}: <b>{RarityName(pending.rarityId)}</b>\n{T("ui.label.reward", "Reward")}: <b>{pending.saleValue} {T("ui.label.gold_lower", "gold")}</b>\n");
            if (pending.wasDiscovered) sb.Append($"\n<color=#63D889><b>{T("ui.result.discovered", "NEW RECIPE DISCOVERED!")}</b></color>\n");
            else if (pending.rarityImproved) sb.Append($"\n<color=#63D889><b>{T("ui.result.record", "NEW RARITY RECORD!")}</b></color>\n");
            sb.Append($"\n<b>{T("ui.pending.contract_progress", "CONTRACT PROGRESS")}</b>\n");
            if (pending.contractProgress.Count == 0) sb.Append(T("ui.pending.no_progress", "No contract progress.") + "\n");
            foreach (var change in pending.contractProgress)
            {
                var contract = game.Config.Contract(change.contractId);
                sb.Append($"{ContractName(change.contractId)}: <b>{change.currentProgress}/{contract.amount}</b>");
                if (change.completed) sb.Append($"  <color=#63D889><b>+{contract.goldReward} {T("ui.label.gold_lower", "gold")}</b></color>");
                sb.Append("\n");
            }
            if (completed.Count > 0) sb.Append($"\n<b>{T("ui.pending.contract_bonus", "Contract bonus")}: +{contractGold} {T("ui.label.gold_lower", "gold")}</b>\n");
            sb.Append($"\n<color=#F2AD2E><b>{T("ui.pending.total_reward", "TOTAL REWARD")}: {pending.saleValue + contractGold} {T("ui.label.gold_lower", "gold")}</b></color>");
            pendingSummaryText.text = sb.ToString(); pendingModal.SetActive(true); RefreshHeaderCounters();
        }

        private void ClaimPendingResult()
        {
            try
            {
                var result = game.ClaimPendingResult(); save.Save(game.State); pendingModal.SetActive(false); RefreshHeaderCounters();
                ShowState(F("ui.status.reward_claimed", "Claimed {0} gold", result.TotalGold));
            }
            catch (Exception ex) { pendingSummaryText.text = ex.Message; }
        }

        private void DispatchPrimary()
        {
            try
            {
                if (footerMode == FooterMode.Experiment) RunExperiment();
                else if (footerMode == FooterMode.Production) RunProduction();
                else if (footerMode == FooterMode.Laboratory) { game.UpgradeLaboratory(); RefreshHeaderCounters(); ShowLaboratory(); }
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void RunExperiment()
        {
            game.RunExperiment(selection); selection.Clear(); save.Save(game.State); RefreshHeaderCounters(); ShowPendingResult();
        }

        private void RunProduction()
        {
            if (string.IsNullOrEmpty(selectedProductionRecipeId)) throw new InvalidOperationException(T("ui.error.choose_recipe", "Choose a discovered recipe."));
            game.RunProduction(selectedProductionRecipeId, selection); selection.Clear(); save.Save(game.State); RefreshHeaderCounters(); ShowPendingResult();
        }

        private void ClearSelection()
        {
            selection.Clear(); UpdateSelection();
            if (footerMode == FooterMode.Experiment) ShowIngredientTiles(game.Config.Ingredients);
            else if (footerMode == FooterMode.Production && !string.IsNullOrEmpty(selectedProductionRecipeId)) ShowIngredientTiles(game.Config.Ingredients.Where(i => i.outcomeWeights.Any(x => x.recipeId == selectedProductionRecipeId && x.weight > 0)));
        }

        private void PrepareTextView(FooterMode mode)
        {
            selection.Clear(); selectedProductionRecipeId = null; SetMode(mode);
            textContentObject.SetActive(true); tileContentObject.SetActive(false); scrollRect.content = textContentRect; scrollRect.verticalNormalizedPosition = 1f;
        }

        private void ShowTiles(IEnumerable<TileOption> options)
        {
            experimentPreviewText.gameObject.SetActive(false);
            foreach (Transform child in tileGridObject.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (var option in options) AddButton(tileGridObject.transform, option.Label, option.Action);
            textContentObject.SetActive(false); tileContentObject.SetActive(true); scrollRect.content = tileContentRect; scrollRect.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
        }

        private void SetMode(FooterMode mode)
        {
            footerMode = mode;
            secondaryActionText.text = mode is FooterMode.Experiment or FooterMode.Production ? T("ui.action.clear", "CLEAR") : "—";
            primaryActionText.text = mode switch
            {
                FooterMode.Experiment => T("ui.action.run_experiment", "RUN EXPERIMENT"),
                FooterMode.Production => T("ui.action.produce", "PRODUCE"),
                FooterMode.Laboratory => T("ui.action.upgrade", "UPGRADE LABORATORY"),
                _ => "—"
            };
            UpdateSelection();
        }

        private string DescribeRequirement(ContractDefinition contract) => contract.requirementType switch
        {
            ContractRequirementType.Recipe => F("ui.contract.recipe", "Create {0} × {1}", contract.amount, RecipeName(contract.targetId)),
            ContractRequirementType.Rarity => F("ui.contract.rarity", "Create {0} × {1} product", contract.amount, RarityName(contract.targetId)),
            ContractRequirementType.Category => F("ui.contract.category", "Create {0} × {1}", contract.amount, CategoryName(contract.targetId)),
            _ => contract.targetId
        };

        private void RefreshHeaderCounters()
        {
            if (goldHeaderText == null) return;
            goldHeaderText.text = $"{T("ui.header.gold", "GOLD")}\n{game.State.gold}";
            recipesHeaderText.text = $"{T("ui.header.recipes", "RECIPES")}\n{game.State.recipes.Count}/{game.Config.Recipes.Count}";
            ingredientsHeaderText.text = $"{T("ui.header.ingredients", "INGREDIENTS")}\n{game.State.inventory.Sum(x => Math.Max(0, x.amount))}";
        }

        private void OpenSettings() => settingsModal.SetActive(true);
        private void CloseSettings() => settingsModal.SetActive(false);
        private void ToggleLanguage() { game.State.languageCode = Language == "pl" ? "en" : "pl"; RefreshLocalizedBindings(); RefreshHeaderCounters(); ShowState(T("ui.status.language_changed", "Language changed")); settingsModal.SetActive(true); }
        private void SaveGame() { save.Save(game.State); CloseSettings(); ShowState(T("ui.status.saved", "Game saved locally")); }
        private void LoadGame() { try { game.ReplaceState(save.Load()); RefreshLocalizedBindings(); RefreshHeaderCounters(); CloseSettings(); if (game.State.pendingResult != null) ShowPendingResult(); else ShowState(T("ui.status.loaded", "Local save loaded")); } catch (Exception ex) { SetStatus(ex.Message); } }
        private void ResetGame() { save.Reset(); game.ReplaceState(GameService.NewState(game.Config, Language)); RefreshLocalizedBindings(); RefreshHeaderCounters(); CloseSettings(); ShowState(T("ui.status.reset", "Save deleted and game reset")); }

        private string T(string key, string fallback) => game.Config.Text(key, Language, fallback);
        private string F(string key, string fallback, params object[] args) => string.Format(T(key, fallback), args);
        private string IngredientName(string id) => game.Config.Text($"ingredient.{id}", Language, game.Config.Ingredient(id)?.displayName ?? id);
        private string RecipeName(string id) => game.Config.Text($"recipe.{id}", Language, game.Config.Recipe(id)?.displayName ?? id);
        private string RarityName(string id) => game.Config.Text($"rarity.{id}", Language, game.Config.Rarity(id)?.displayName ?? id);
        private string CategoryName(string id) => game.Config.Text($"category.{id}", Language, game.Config.Category(id)?.displayName ?? id);
        private string ContractName(string id) => game.Config.Text($"contract.{id}", Language, game.Config.Contract(id)?.displayName ?? id);
        private void UpdateSelection() { if (selectionText != null) selectionText.text = T("ui.label.selected", "Selected") + ": " + (selection.Count == 0 ? "—" : string.Join(" + ", selection.Select(IngredientName))) + $"  ({selection.Count}/3)"; }
        private float Completion() => game.Config.Recipes.Count == 0 ? 0 : 100f * game.State.recipes.Count / game.Config.Recipes.Count;
        private void SetStatus(string value) => statusText.text = value;
        private void Bind(Text text, string key, string fallback) { localizedBindings.Add(new LocalizedBinding { Text = text, Key = key, Fallback = fallback }); text.text = T(key, fallback); }
        private void RefreshLocalizedBindings() { foreach (var binding in localizedBindings) binding.Text.text = T(binding.Key, binding.Fallback); SetMode(footerMode); }
        private GameObject AddLocalizedButton(Transform parent, string key, string fallback, Action action, Color? color = null, Color? textColor = null) { var node = AddButton(parent, T(key, fallback), action, color, textColor); Bind(node.GetComponentInChildren<Text>(), key, fallback); return node; }
        private GameObject Node(string name, Transform parent, params Type[] components) { var node = new GameObject(name, components); node.transform.SetParent(parent, false); return node; }
        private GameObject Panel(string name, Transform parent, Color color) { var node = Node(name, parent, typeof(RectTransform), typeof(Image)); node.GetComponent<Image>().color = color; return node; }
        private Text Label(string value, Transform parent, int size, TextAnchor anchor) { var node = Node("Label", parent, typeof(RectTransform), typeof(Text)); var text = node.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = Cream; text.alignment = anchor; text.text = value; text.supportRichText = true; return text; }
        private GameObject AddButton(Transform parent, string label, Action action, Color? color = null, Color? textColor = null) { var node = Panel(label, parent, color ?? new Color(.28f, .20f, .31f)); node.AddComponent<Button>().onClick.AddListener(() => action()); var text = Label(label, node.transform, 24, TextAnchor.MiddleCenter); text.color = textColor ?? Cream; text.fontStyle = FontStyle.Bold; text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = 24; Stretch(text.gameObject, 5, 3); return node; }
        private static void Stretch(GameObject node, float x = 0, float y = 0) { var rect = node.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y); }
        private static void Rect(GameObject node, float xMin, float yMin, float xMax, float yMax) { var rect = node.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(xMin, yMin); rect.anchorMax = new Vector2(xMax, yMax); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    }
}
