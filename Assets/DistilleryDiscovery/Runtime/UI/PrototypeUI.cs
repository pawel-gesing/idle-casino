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
        private enum FooterMode { Experiment, Production, Laboratory, Delivery, None }
        private sealed class LocalizedBinding { public Text Text; public string Key; public string Fallback; }
        private sealed class TileOption { public string Label; public Action Action; }
        private sealed class TileSection { public string Heading; public List<TileOption> Options = new(); }

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
        private readonly List<GridLayoutGroup> activeTileGrids = new();
        private Text experimentPreviewText;
        private Text statusText;
        private Text contentText;
        private Text selectionText;
        private Text secondaryActionText;
        private Text primaryActionText;
        private Button primaryActionButton;
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
        private string selectedLaboratoryId;
        private float tileCellHeight = 112f;
        private float nextTimerRefresh;
        private bool startupRewardFlow;
        private DeliveryResult pendingDeliveryAcknowledgement;

        private string Language => game.State.languageCode == "pl" ? "pl" : "en";

        private void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafeArea || screenSize != lastScreenSize) ApplyMobileLayout();
            if (game != null && Time.unscaledTime >= nextTimerRefresh)
            {
                nextTimerRefresh = Time.unscaledTime + 1f;
                game.UpdateTime();
                if (footerMode == FooterMode.Delivery) ShowDelivery();
                else if (footerMode == FooterMode.Laboratory) ShowLaboratory();
            }
        }

        public void Initialize(GameService gameService, SaveService saveService)
        {
            game = gameService;
            save = saveService;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            StartStartupRewardFlow();
        }

        private void StartStartupRewardFlow()
        {
            startupRewardFlow = true;
            ContinueStartupRewardFlow();
        }

        private void ContinueStartupRewardFlow()
        {
            game.UpdateTime();
            if (game.State.pendingResult != null) { ShowPendingResult(); return; }
            var readyJob = game.State.laboratoryJobs
                .Where(x => x.status == LaboratoryJobStatus.Completed)
                .OrderBy(x => x.endTimeUtc)
                .FirstOrDefault();
            if (readyJob != null)
            {
                try { game.ClaimLaboratoryJob(readyJob.id); save.Save(game.State); ShowPendingResult(); }
                catch (Exception ex) { startupRewardFlow = false; ShowState(ex.Message); }
                return;
            }
            if (game.State.availableFreeDeliveries > 0)
            {
                try
                {
                    pendingDeliveryAcknowledgement = game.ReceiveDelivery();
                    save.Save(game.State);
                    ShowDeliveryAcknowledgement();
                }
                catch (Exception ex) { startupRewardFlow = false; ShowState(ex.Message); }
                return;
            }
            startupRewardFlow = false;
            ShowState(OfflineStatus(T("ui.status.ready", "Ready. Receive a delivery to begin.")));
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
            AddLocalizedButton(nav.transform, "ui.nav.delivery", "DELIVERY", ShowDelivery);
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
            activeTileGrids.Add(tileGrid);

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
            var secondary = AddButton(footer.transform, "—", DispatchSecondary); Rect(secondary, .02f, .10f, .30f, .48f); secondaryActionText = secondary.GetComponentInChildren<Text>();
            var primary = AddButton(footer.transform, "—", DispatchPrimary, Gold, Ink); Rect(primary, .32f, .10f, .98f, .48f); primaryActionText = primary.GetComponentInChildren<Text>(); primaryActionButton = primary.GetComponent<Button>();
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
            var debug15 = AddButton(card.transform, "DEBUG: +15 MIN", () => AdvanceDebugTime(TimeSpan.FromMinutes(15))); Rect(debug15, .12f, .17f, .48f, .24f);
            var debugHour = AddButton(card.transform, "DEBUG: +1 H", () => AdvanceDebugTime(TimeSpan.FromHours(1))); Rect(debugHour, .52f, .17f, .88f, .24f);
            var exitButton = AddLocalizedButton(card.transform, "ui.settings.exit", "EXIT", Application.Quit); Rect(exitButton, .12f, .06f, .88f, .14f);
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
            foreach (var grid in activeTileGrids.Where(x => x != null)) ResizeTileGrid(grid);
        }

        private void ResizeTileGrid(GridLayoutGroup grid)
        {
            var tileWidth = (tileContentRect.rect.width - tileLayout.padding.horizontal - grid.spacing.x) / 2f;
            grid.cellSize = new Vector2(Mathf.Max(1f, tileWidth), tileCellHeight);
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
            var labs = string.Join(", ", game.State.laboratories.Select(x => $"{LaboratoryName(x)} {T("ui.label.level", "level")} {x.level}"));
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.state", "PLAYER STATE")}</b></color>\n\n{T("ui.label.gold", "Gold")}: <b>{game.State.gold}</b>\n{T("ui.label.laboratories", "Laboratories")}: <b>{labs}</b>\n{T("ui.label.experiments", "Experiments")}: <b>{game.State.experimentsCompleted}</b>\n{T("ui.label.productions", "Productions")}: <b>{game.State.productionsCompleted}</b>\n{T("ui.label.ingredients", "Ingredients")}: <b>{game.State.inventory.Sum(x => Math.Max(0, x.amount))}</b>\n{T("ui.label.recipes", "Discovered recipes")}: <b>{game.State.recipes.Count}/{game.Config.Recipes.Count}</b> ({Completion():0.#}%)";
        }

        private void ReceiveDelivery()
        {
            try
            {
                PrepareTextView(FooterMode.None); var delivery = game.ReceiveDelivery();
                contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.delivery", "NEW DELIVERY")}</b></color>\n\n" + string.Join("\n", delivery.Items.Select(x => $"+{x.Value}  {IngredientName(x.Key)}"));
                save.Save(game.State); SetStatus(T("ui.status.delivery", "Delivery received")); RefreshHeaderCounters();
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void ShowDelivery()
        {
            game.UpdateTime(); PrepareTextView(FooterMode.Delivery);
            var remaining = game.TimeUntilNextFreeDelivery;
            var timer = game.State.availableFreeDeliveries >= game.Config.Economy.maxStoredFreeDeliveries ? "MAX" : FormatDuration(remaining);
            var rolls = game.Config.Economy.deliveryPools.FirstOrDefault()?.rolls ?? 0;
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.delivery", "FREE DELIVERY")}</b></color>\n\n{T("ui.delivery.available", "Available")}: <b>{game.State.availableFreeDeliveries}/{game.Config.Economy.maxStoredFreeDeliveries}</b>\n{T("ui.delivery.next", "Next")}: <b>{timer}</b>\n{T("ui.delivery.rolls", "Rolls per delivery")}: <b>{rolls}</b>";
            SetStatus(game.State.availableFreeDeliveries > 0 ? T("ui.status.delivery_ready", "Free delivery ready to claim") : T("ui.status.delivery_waiting", "Waiting for the next free delivery"));
        }

        private void ShowIngredientInventory()
        {
            PrepareTextView(FooterMode.None); SetStatus(T("ui.status.ingredients", "Ingredient storage"));
            var sections = IngredientSections(game.Config.Ingredients, item => new TileOption
            {
                Label = $"{IngredientName(item.id)}\n{RarityName(item.rarityId)}\nx{game.State.AmountOf(item.id)}",
                Action = ShowIngredientInventory
            });
            var products = game.State.products.Count == 0
                ? new List<TileOption> { new() { Label = T("ui.inventory.no_products", "No products yet"), Action = ShowIngredientInventory } }
                : game.State.products.Select(product => new TileOption { Label = $"{RecipeName(product.recipeId)}\n{RarityName(product.rarityId)}\nx{product.amount}", Action = ShowIngredientInventory }).ToList();
            sections.Add(new TileSection { Heading = T("ui.heading.products", "PRODUCTS"), Options = products });
            ShowTileSections(sections, 132f);
        }

        private void ShowRecipeBook()
        {
            PrepareTextView(FooterMode.None); SetStatus(F("ui.status.recipe_book", "Recipe book — {0:0.#}% complete", Completion()));
            var sections = game.Config.Categories.Select(category => new TileSection
            {
                Heading = $"{CategoryName(category.id)}  {game.Config.Recipes.Count(r => r.categoryId == category.id && game.State.RecipeState(r.id) != null)}/{game.Config.Recipes.Count(r => r.categoryId == category.id)}",
                Options = game.Config.Recipes.Where(recipe => recipe.categoryId == category.id).Select(RecipeBookTile).ToList()
            }).ToList();
            ShowTileSections(sections, 220f);
        }

        private void ShowExperiment()
        {
            selection.Clear(); selectedProductionRecipeId = null; selectedLaboratoryId = null; SetMode(FooterMode.Experiment);
            SetStatus(T("ui.status.choose_laboratory", "Choose a free laboratory"));
            ShowFreeLaboratoryTiles(LaboratoryJobType.Experiment, () => ShowIngredientTiles(game.Config.Ingredients));
        }

        private void ShowProduction()
        {
            selection.Clear(); selectedProductionRecipeId = null; selectedLaboratoryId = null; SetMode(FooterMode.Production);
            var recipes = game.Config.Recipes.Where(x => game.State.RecipeState(x.id) != null).ToList();
            SetStatus(T("ui.status.choose_recipe_tile", "Choose a discovered recipe"));
            if (recipes.Count == 0) ShowTiles(new[] { new TileOption { Label = T("ui.production.none", "No discovered recipes."), Action = ShowProduction } });
            else ShowTileSections(game.Config.Categories.Select(category => new TileSection
            {
                Heading = CategoryName(category.id),
                Options = recipes.Where(recipe => recipe.categoryId == category.id).Select(recipe =>
                    new TileOption { Label = $"{RecipeName(recipe.id)}\n{DescribeRecipeRequirements(recipe)}", Action = () => SelectProductionRecipe(recipe.id) }).ToList()
            }), 160f);
        }

        private void SelectProductionRecipe(string recipeId)
        {
            selectedProductionRecipeId = recipeId; selectedLaboratoryId = null; selection.Clear(); UpdateSelection();
            SetStatus(F("ui.status.chosen_recipe", "Recipe: {0}. Select three ingredients.", RecipeName(recipeId)) + " " + DescribeRecipeRequirements(game.Config.Recipe(recipeId)));
            ShowFreeLaboratoryTiles(LaboratoryJobType.Production, () => ShowIngredientTiles(game.Config.Ingredients.Where(i => i.enabled)));
        }

        private void ShowIngredientTiles(IEnumerable<IngredientDefinition> ingredients)
        {
            ShowTileSections(IngredientSections(ingredients, item =>
            {
                var available = game.State.AmountOf(item.id) - selection.Count(x => x == item.id);
                return new TileOption { Label = $"{IngredientName(item.id)}\n{GroupName(item.groupId)} · {RarityName(item.rarityId)}\nx{Math.Max(0, available)}", Action = () => ReserveIngredient(item.id) };
            }), 142f);
            UpdateExperimentPreview();
        }

        private List<TileSection> IngredientSections(IEnumerable<IngredientDefinition> ingredients, Func<IngredientDefinition, TileOption> map)
        {
            var ingredientList = ingredients.Where(item => item.enabled).ToList();
            var knownGroupIds = new HashSet<string>(game.Config.Groups.Select(group => group.id));
            var sections = game.Config.Groups.Select(group => new TileSection
            {
                Heading = GroupName(group.id),
                Options = ingredientList.Where(item => item.groupId == group.id)
                    .OrderBy(item => game.Config.Rarity(item.rarityId)?.rank ?? 0).ThenBy(item => IngredientName(item.id))
                    .Select(map).ToList()
            }).Where(section => section.Options.Count > 0).ToList();
            var other = ingredientList.Where(item => string.IsNullOrEmpty(item.groupId) || !knownGroupIds.Contains(item.groupId))
                .OrderBy(item => game.Config.Rarity(item.rarityId)?.rank ?? 0).ThenBy(item => IngredientName(item.id))
                .Select(map).ToList();
            if (other.Count > 0) sections.Add(new TileSection { Heading = T("ui.group.other", "Other"), Options = other });
            return sections;
        }

        private TileOption RecipeBookTile(RecipeDefinition recipe)
        {
            var state = game.State.RecipeState(recipe.id);
            if (state == null) return new TileOption { Label = $"???\n{T("ui.recipe.undiscovered", "Undiscovered recipe")}", Action = ShowRecipeBook };
            var rarity = game.Config.Rarity(state.highestProductRarityId);
            var mastery = game.MasteryLevel(recipe.id);
            var next = game.Config.NextMasteryLevel(state.timesCreated);
            var progress = next == null ? T("ui.mastery.maximum", "Maximum level reached") : F("ui.mastery.to_next", "To next level: {0}", next.requiredProductionCount - state.timesCreated);
            return new TileOption
            {
                Label = $"{RecipeName(recipe.id)}\n{DescribeRecipeRequirements(recipe)}\n{T("ui.label.record", "Record")}: {RarityName(rarity.id)} · {T("ui.label.created", "Created")}: {state.timesCreated}\n{T("ui.label.mastery", "Mastery")}: {MasteryName(mastery)}\n{progress}",
                Action = ShowRecipeBook
            };
        }

        private void ShowFreeLaboratoryTiles(string jobType, Action afterSelect)
        {
            var options = game.State.laboratories
                .Where(lab => game.AvailableSlots(lab.id, jobType) > 0)
                .Select(lab =>
                {
                    var level = game.Config.LaboratoryLevel(lab.level);
                    var label = $"{LaboratoryName(lab)}\n{T("ui.label.level", "level")} {lab.level}\n{T("ui.label.rarity_bonus", "Higher rarity bonus")}: +{level.productQualityBonus:P0}";
                    return new TileOption { Label = label, Action = () => { selectedLaboratoryId = lab.id; SetStatus($"{T("ui.label.laboratory", "Laboratory")}: {LaboratoryName(lab)}"); afterSelect(); } };
                }).ToList();
            if (options.Count == 0) options.Add(new TileOption { Label = T("ui.laboratory.no_free", "No free laboratory"), Action = ShowLaboratory });
            ShowTileSections(new[] { new TileSection { Heading = T("ui.heading.laboratories", "LABORATORIES"), Options = options } }, 150f);
            UpdateSelection();
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
            else ShowIngredientTiles(game.Config.Ingredients.Where(i => i.enabled));
        }

        private void ShowContracts()
        {
            PrepareTextView(FooterMode.None); SetStatus(T("ui.status.contracts", "Three contracts are active at once"));
            game.UpdateTime();
            var options = new List<TileOption>();
            foreach (var role in ContractRole.All)
            {
                var state = game.State.activeContracts.Find(x => x.role == role);
                if (state == null)
                {
                    options.Add(new TileOption
                    {
                        Label = $"{RoleName(role)}\n{T("ui.contract.waiting", "New contract in")} {FormatDuration(game.TimeUntilContractAvailable(role))}",
                        Action = ShowContracts
                    });
                    continue;
                }
                var template = game.Config.ContractTemplate(state.templateId);
                var distinct = state.objectiveType == ContractObjectiveType.DistinctRecipes ? $" ({state.seenRecipeIds.Count} {T("ui.objective.distinct", "Distinct recipes")})" : "";
                options.Add(new TileOption
                {
                    Label = $"{RoleName(role)} - {ContractName(state.templateId)}\n{DescribeRequirement(state)}\n{T("ui.label.progress", "Progress")}: {state.progress}/{state.amount}{distinct}\n{T("ui.label.reward", "Reward")}: {ContractRewardDescription(state, template)}",
                    Action = ShowContracts
                });
            }
            ShowTileSections(new[] { new TileSection { Heading = T("ui.heading.contracts", "ACTIVE CONTRACTS"), Options = options } }, 210f);
        }

        private void ShowLaboratory()
        {
            game.UpdateTime();
            selection.Clear(); selectedProductionRecipeId = null; SetMode(FooterMode.Laboratory);
            SetStatus(T("ui.status.laboratory", "Laboratory improves product rarity"));
            var labOptions = game.State.laboratories.Select(lab =>
            {
                var level = game.Config.LaboratoryLevel(lab.level);
                var upgrade = game.Config.LaboratoryLevel(lab.level + 1);
                var label = $"{LaboratoryName(lab)}\n{T("ui.label.level", "level")} {lab.level} - {T("ui.label.rarity_bonus", "Higher rarity bonus")} +{level.productQualityBonus:P0}\n" +
                    (upgrade == null ? T("ui.laboratory.max", "Maximum level reached.") : $"{T("ui.action.upgrade", "UPGRADE")}: {upgrade.upgradeCost} {T("ui.label.gold_lower", "gold")}");
                return new TileOption { Label = label, Action = () => UpgradeLaboratory(lab.id) };
            }).ToList();
            if (game.NextLaboratoryCost >= 0)
                labOptions.Add(new TileOption { Label = $"{T("ui.action.buy_laboratory", "BUY LABORATORY")}\n{T("ui.label.cost", "Cost")}: {game.NextLaboratoryCost} {T("ui.label.gold_lower", "gold")}", Action = PurchaseLaboratory });
            var jobOptions = game.State.laboratoryJobs.Where(x => x.status != LaboratoryJobStatus.Claimed)
                .OrderBy(x => x.status == LaboratoryJobStatus.Completed ? 0 : x.type == LaboratoryJobType.Experiment ? 1 : 2)
                .Select(job => new TileOption { Label = JobLabel(job), Action = job.status == LaboratoryJobStatus.Completed ? () => ClaimJob(job.id) : ShowLaboratory }).ToList();
            if (jobOptions.Count == 0) jobOptions.Add(new TileOption { Label = T("ui.laboratory.no_jobs", "No active jobs"), Action = ShowLaboratory });
            ShowTileSections(new[]
            {
                new TileSection { Heading = T("ui.heading.laboratories", "LABORATORIES"), Options = labOptions },
                new TileSection { Heading = T("ui.heading.jobs", "JOBS"), Options = jobOptions }
            }, 190f);
            experimentPreviewText.gameObject.SetActive(true);
            experimentPreviewText.text = $"<color=#F2AD2E><b>{T("ui.heading.laboratory", "LABORATORY")}</b></color>\n" +
                $"{T("ui.label.laboratories", "Laboratories")}: <b>{game.LaboratoryCount}/{game.MaxLaboratoryCount}</b>";
        }

        private string JobLabel(LaboratoryJobState job)
        {
            var ready = job.status == LaboratoryJobStatus.Completed;
            var title = job.type == LaboratoryJobType.Experiment
                ? ready ? T("ui.job.experiment_ready", "Experiment ready") : T("ui.job.experiment_running", "Experiment in progress")
                : ready ? F("ui.job.production_ready", "Production ready: {0}", RecipeName(job.recipeId)) : F("ui.job.production_running", "Production: {0}", RecipeName(job.recipeId));
            var ingredientLine = string.Join(" + ", job.ingredientIds.Select(IngredientName));
            var statusLine = ready ? T("ui.job.claim", "Ready - claim") : FormatDuration(game.TimeRemaining(job));
            return $"{LaboratoryName(game.State.laboratories.Find(x => x.id == job.laboratoryId))}\n{title}\n{ingredientLine}\n{statusLine}";
        }

        private void ClaimJob(string jobId)
        {
            try { game.ClaimLaboratoryJob(jobId); save.Save(game.State); ShowPendingResult(); }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void UpgradeLaboratory(string laboratoryId)
        {
            try { selectedLaboratoryId = laboratoryId; game.UpgradeLaboratory(laboratoryId); save.Save(game.State); RefreshHeaderCounters(); ShowLaboratory(); }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void PurchaseLaboratory()
        {
            try { game.PurchaseLaboratory(); save.Save(game.State); RefreshHeaderCounters(); ShowLaboratory(); }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void ShowPendingResult()
        {
            var pending = game.State.pendingResult; if (pending == null) return;
            pendingDeliveryAcknowledgement = null;
            var completed = pending.contractProgress.Where(x => x.completed).ToList();
            var contractGold = completed.Sum(x => game.State.ContractState(x.contractId)?.goldReward ?? 0);
            var sb = new StringBuilder();
            sb.Append($"<color=#F2AD2E><b>{(pending.source == "experiment" ? T("ui.heading.experiment_result", "EXPERIMENT RESULT") : T("ui.heading.production_result", "PRODUCTION COMPLETE"))}</b></color>\n\n");
            sb.Append($"{T("ui.label.created_product", "Product")}: <b>{RecipeName(pending.recipeId)}</b>\n{T("ui.label.quality", "Rarity")}: <b>{RarityName(pending.rarityId)}</b>\n{T("ui.label.reward", "Reward")}: <b>{pending.saleValue} {T("ui.label.gold_lower", "gold")}</b>\n");
            if (pending.wasDiscovered) sb.Append($"\n<color=#63D889><b>{T("ui.result.discovered", "NEW RECIPE DISCOVERED!")}</b></color>\n");
            else if (pending.rarityImproved) sb.Append($"\n<color=#63D889><b>{T("ui.result.record", "NEW RARITY RECORD!")}</b></color>\n");
            sb.Append($"\n<b>{T("ui.pending.contract_progress", "CONTRACT PROGRESS")}</b>\n");
            if (pending.contractProgress.Count == 0) sb.Append(T("ui.pending.no_progress", "No contract progress.") + "\n");
            foreach (var change in pending.contractProgress)
            {
                var contract = game.State.ContractState(change.contractId);
                sb.Append($"{ContractName(contract?.templateId)}: <b>{change.currentProgress}/{contract?.amount}</b>");
                if (change.completed) sb.Append($"  <color=#63D889><b>+{ContractRewardDescription(contract, game.Config.ContractTemplate(contract?.templateId))}</b></color>");
                sb.Append("\n");
            }
            if (completed.Count > 0) sb.Append($"\n<b>{T("ui.pending.contract_bonus", "Contract bonus")}: +{contractGold} {T("ui.label.gold_lower", "gold")}</b>\n");
            sb.Append($"\n<color=#F2AD2E><b>{T("ui.pending.total_reward", "TOTAL REWARD")}: {pending.saleValue + contractGold} {T("ui.label.gold_lower", "gold")}</b></color>");
            pendingSummaryText.text = sb.ToString(); pendingModal.SetActive(true); RefreshHeaderCounters();
        }

        private void ShowDeliveryAcknowledgement()
        {
            var delivery = pendingDeliveryAcknowledgement; if (delivery == null) return;
            var sb = new StringBuilder();
            sb.Append($"<color=#F2AD2E><b>{T("ui.heading.delivery", "NEW DELIVERY")}</b></color>\n\n");
            foreach (var item in delivery.Items.OrderBy(x => IngredientName(x.Key)))
                sb.Append($"+{item.Value}  <b>{IngredientName(item.Key)}</b>\n");
            sb.Append($"\n{T("ui.delivery.next_after_claim", "The next delivery timer has started.")}");
            pendingSummaryText.text = sb.ToString(); pendingModal.SetActive(true); RefreshHeaderCounters();
        }

        private void ClaimPendingResult()
        {
            try
            {
                if (pendingDeliveryAcknowledgement != null)
                {
                    pendingDeliveryAcknowledgement = null; pendingModal.SetActive(false); RefreshHeaderCounters();
                    if (startupRewardFlow) ContinueStartupRewardFlow(); else ShowDelivery();
                    return;
                }
                var result = game.ClaimPendingResult(); save.Save(game.State); pendingModal.SetActive(false); RefreshHeaderCounters();
                var ingredients = result.IngredientRewards.Count == 0 ? "" : " - " + string.Join(", ", result.IngredientRewards.Select(x => $"+{x.Value} {IngredientName(x.Key)}"));
                if (startupRewardFlow) ContinueStartupRewardFlow();
                else ShowState(F("ui.status.reward_claimed", "Claimed {0} gold", result.TotalGold) + ingredients);
            }
            catch (Exception ex) { pendingSummaryText.text = ex.Message; }
        }

        private void DispatchPrimary()
        {
            try
            {
                if (footerMode == FooterMode.Experiment) RunExperiment();
                else if (footerMode == FooterMode.Production) RunProduction();
                else if (footerMode == FooterMode.Delivery) { ReceiveDelivery(); ShowDelivery(); }
                else if (footerMode == FooterMode.Laboratory)
                {
                    var result = game.CollectAll(); save.Save(game.State); RefreshHeaderCounters(); ShowLaboratory();
                    SetStatus(F("ui.status.collect_all", "Collected {0} jobs - {1} products - +{2} gold", result.JobsCollected, result.ProductsAdded, result.GoldGained));
                }
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void RunExperiment()
        {
            if (string.IsNullOrEmpty(selectedLaboratoryId)) throw new InvalidOperationException(T("ui.error.choose_laboratory", "Choose a free laboratory."));
            game.StartExperiment(selection, selectedLaboratoryId); selection.Clear(); selectedLaboratoryId = null; save.Save(game.State); RefreshHeaderCounters(); ShowLaboratory();
        }

        private void RunProduction()
        {
            if (string.IsNullOrEmpty(selectedProductionRecipeId)) throw new InvalidOperationException(T("ui.error.choose_recipe", "Choose a discovered recipe."));
            if (string.IsNullOrEmpty(selectedLaboratoryId)) throw new InvalidOperationException(T("ui.error.choose_laboratory", "Choose a free laboratory."));
            game.StartProduction(selectedProductionRecipeId, selection, selectedLaboratoryId); selection.Clear(); selectedLaboratoryId = null; save.Save(game.State); RefreshHeaderCounters(); ShowLaboratory();
        }

        private void ClearSelection()
        {
            selection.Clear(); UpdateSelection();
            if (footerMode == FooterMode.Experiment && !string.IsNullOrEmpty(selectedLaboratoryId)) ShowIngredientTiles(game.Config.Ingredients);
            else if (footerMode == FooterMode.Production && !string.IsNullOrEmpty(selectedProductionRecipeId) && !string.IsNullOrEmpty(selectedLaboratoryId)) ShowIngredientTiles(game.Config.Ingredients.Where(i => i.enabled));
        }

        private void DispatchSecondary()
        {
            if (footerMode != FooterMode.Laboratory) { ClearSelection(); return; }
            try { game.UpgradeLaboratory(selectedLaboratoryId); save.Save(game.State); RefreshHeaderCounters(); ShowLaboratory(); }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void PrepareTextView(FooterMode mode)
        {
            selection.Clear(); selectedProductionRecipeId = null; selectedLaboratoryId = null; SetMode(mode);
            textContentObject.SetActive(true); tileContentObject.SetActive(false); scrollRect.content = textContentRect; scrollRect.verticalNormalizedPosition = 1f;
        }

        private void ShowTiles(IEnumerable<TileOption> options)
        {
            ShowTileSections(new[] { new TileSection { Options = options.ToList() } });
        }

        private void ShowTileSections(IEnumerable<TileSection> sections, float cellHeight = 112f)
        {
            tileCellHeight = cellHeight;
            foreach (Transform child in tileContentObject.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            activeTileGrids.Clear();
            foreach (var section in sections.Where(x => x.Options.Count > 0))
            {
                if (!string.IsNullOrEmpty(section.Heading))
                {
                    var heading = Label(section.Heading, tileContentObject.transform, 30, TextAnchor.MiddleLeft);
                    heading.color = Gold; heading.fontStyle = FontStyle.Bold;
                    var fitter = heading.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                var gridObject = Node("TileGrid", tileContentObject.transform, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
                var grid = gridObject.GetComponent<GridLayoutGroup>(); grid.spacing = new Vector2(12, 12); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
                gridObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                activeTileGrids.Add(grid); tileGrid = grid; tileGridObject = gridObject;
                foreach (var option in section.Options) AddButton(gridObject.transform, option.Label, option.Action ?? (() => { }));
            }
            experimentPreviewText = Label("", tileContentObject.transform, 30, TextAnchor.UpperLeft);
            experimentPreviewText.supportRichText = true;
            experimentPreviewText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            experimentPreviewText.gameObject.SetActive(false);
            textContentObject.SetActive(false); tileContentObject.SetActive(true); scrollRect.content = tileContentRect; scrollRect.verticalNormalizedPosition = 1f;
            foreach (var grid in activeTileGrids) ResizeTileGrid(grid);
            Canvas.ForceUpdateCanvases();
        }

        private void SetMode(FooterMode mode)
        {
            footerMode = mode;
            secondaryActionText.text = mode is FooterMode.Experiment or FooterMode.Production ? T("ui.action.clear", "CLEAR") : mode == FooterMode.Laboratory ? T("ui.action.upgrade", "UPGRADE") : "—";
            primaryActionText.text = mode switch
            {
                FooterMode.Experiment => T("ui.action.run_experiment", "RUN EXPERIMENT"),
                FooterMode.Production => T("ui.action.produce", "PRODUCE"),
                FooterMode.Laboratory => T("ui.action.collect_all", "COLLECT ALL"),
                FooterMode.Delivery => game.State.availableFreeDeliveries > 0 ? T("ui.action.claim", "CLAIM") : T("ui.action.waiting", "WAITING"),
                _ => "—"
            };
            primaryActionButton.interactable = mode != FooterMode.None && (mode != FooterMode.Delivery || game.State.availableFreeDeliveries > 0) &&
                (mode != FooterMode.Laboratory || game.State.laboratoryJobs.Any(x => x.status == LaboratoryJobStatus.Completed));
            UpdateSelection();
        }

        private string DescribeRequirement(ActiveContractState contract)
        {
            var readableTarget = contract.objectiveType switch
            {
                ContractObjectiveType.Recipe or ContractObjectiveType.RecipeMinRarity => RecipeName(contract.targetId),
                ContractObjectiveType.Category => CategoryName(contract.targetId),
                ContractObjectiveType.Rarity => RarityName(contract.targetId),
                ContractObjectiveType.Ingredient => IngredientName(contract.targetId),
                ContractObjectiveType.Group => GroupName(contract.targetId),
                ContractObjectiveType.Tag => TagName(contract.targetId),
                ContractObjectiveType.DistinctRecipes => string.IsNullOrEmpty(contract.targetId) ? T("ui.contract.any", "any") : game.Config.Category(contract.targetId) != null ? CategoryName(contract.targetId) : TagName(contract.targetId),
                ContractObjectiveType.Source => SourceName(contract.targetId),
                ContractObjectiveType.Discover => string.IsNullOrEmpty(contract.targetId) ? T("ui.contract.new_recipes", "new recipes") : RecipeName(contract.targetId),
                ContractObjectiveType.ImproveRecord => string.IsNullOrEmpty(contract.targetId) ? T("ui.contract.any_recipe", "any recipe") : RecipeName(contract.targetId),
                _ => contract.targetId
            };
            var readableQuality = string.IsNullOrEmpty(contract.minRarityId) ? "" : $" · {T("ui.contract.min_quality", "min.")} {RarityName(contract.minRarityId)}";
            var readableSource = string.IsNullOrEmpty(contract.source) ? "" : $" · {SourceName(contract.source)}";
            return $"{ObjectiveName(contract.objectiveType)}: {readableTarget} × {contract.amount}{readableQuality}{readableSource}";
        }

        private string DescribeRecipeRequirements(RecipeDefinition recipe) => string.Join(" + ", recipe.requirements.Select(x => x.type switch
        {
            RecipeRequirementType.Ingredient => $"{x.count}× {IngredientName(x.ingredientId)}",
            RecipeRequirementType.Group => $"{x.count}× {GroupName(x.groupId)}",
            RecipeRequirementType.DistinctGroup => $"{x.count} distinct {GroupName(x.groupId)}",
            RecipeRequirementType.AnyOf => $"{x.count}× ({string.Join(" / ", x.ingredientIds.Select(IngredientName))})",
            _ => x.type
        }));

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
        private void LoadGame() { try { game.ReplaceState(save.Load()); RefreshLocalizedBindings(); RefreshHeaderCounters(); CloseSettings(); StartStartupRewardFlow(); } catch (Exception ex) { SetStatus(ex.Message); } }
        private void ResetGame() { save.Reset(); game.ReplaceState(GameService.NewState(game.Config, Language)); RefreshLocalizedBindings(); RefreshHeaderCounters(); CloseSettings(); ShowState(T("ui.status.reset", "Save deleted and game reset")); }
        private void AdvanceDebugTime(TimeSpan duration) { if (game.DebugAdvanceTime(duration)) { save.Save(game.State); CloseSettings(); ShowLaboratory(); } }
        private string OfflineStatus(string prefix)
        {
            var offline = game.LastOfflineSummary;
            if (!offline.HasProgress) return prefix;
            return $"{prefix} · Offline {FormatDuration(offline.Elapsed)} · +{offline.DeliveriesGained} deliveries · " +
                $"{offline.JobsCompleted} completed ({offline.ExperimentsReady} experiments, {offline.ProductionsReady} productions ready)";
        }
        private static string FormatDuration(TimeSpan duration) { if (duration < TimeSpan.Zero) duration = TimeSpan.Zero; return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"; }

        private string MasteryName(MasteryLevelDefinition level) => level == null ? "—" : game.Config.Text($"mastery.{level.id}", Language, level.displayName);
        private string ContractRewardDescription(ActiveContractState contract, ContractTemplateDefinition template)
        {
            if (contract == null) return "-";
            var rewards = new List<string> { $"{contract.goldReward} {T("ui.label.gold_lower", "gold")}" };
            if (!string.IsNullOrEmpty(contract.rewardIngredientId) && contract.rewardIngredientAmount > 0)
                rewards.Add($"{contract.rewardIngredientAmount}x {IngredientName(contract.rewardIngredientId)}");
            return string.Join(" + ", rewards);
        }

        private string ContractIngredientRewardDescription(ContractRewardDefinition reward)
        {
            var amount = reward.minAmount == reward.maxAmount ? reward.minAmount.ToString() : $"{reward.minAmount}-{reward.maxAmount}";
            var target = reward.selectorType switch
            {
                RewardSelectorType.Ingredient => IngredientName(reward.targetId),
                RewardSelectorType.Group => $"{GroupName(reward.targetId)} ({ShortIngredientList(game.Config.Ingredients.Where(x => x.enabled && x.groupId == reward.targetId).Select(x => x.id), 3)})",
                RewardSelectorType.Rarity => $"{RarityName(reward.targetId)} ({ShortIngredientList(game.Config.Ingredients.Where(x => x.enabled && x.rarityId == reward.targetId).Select(x => x.id), 3)})",
                _ => reward.targetId
            };
            return $"{amount}× {target}";
        }

        private string ShortIngredientList(IEnumerable<string> ids, int max)
        {
            var names = ids.Select(IngredientName).Take(max + 1).ToList();
            if (names.Count <= max) return string.Join(", ", names);
            return string.Join(", ", names.Take(max)) + ", …";
        }

        private string T(string key, string fallback) => game.Config.Text(key, Language, fallback);
        private string F(string key, string fallback, params object[] args) => string.Format(T(key, fallback), args);
        private string IngredientName(string id) => game.Config.Text($"ingredient.{id}", Language, game.Config.Ingredient(id)?.displayName ?? id);
        private string RecipeName(string id) => game.Config.Text($"recipe.{id}", Language, game.Config.Recipe(id)?.displayName ?? id);
        private string RarityName(string id) => game.Config.Text($"rarity.{id}", Language, game.Config.Rarity(id)?.displayName ?? id);
        private string CategoryName(string id) => game.Config.Text($"category.{id}", Language, game.Config.Category(id)?.displayName ?? id);
        private string GroupName(string id) => game.Config.Text($"group.{id}", Language, game.Config.Group(id)?.displayName ?? id);
        private string TagName(string id) => game.Config.Text($"tag.{id}", Language, id);
        private string ContractName(string id) => game.Config.Text($"contract.{id}", Language, game.Config.ContractTemplate(id)?.displayName ?? id);
        private string RoleName(string role) => role switch
        {
            ContractRole.Basic => T("contract.role.basic", "Basic"),
            ContractRole.Specialist => T("contract.role.specialist", "Specialist"),
            ContractRole.Prestige => T("contract.role.prestige", "Prestige"),
            _ => role
        };
        private string LaboratoryName(PlayerLaboratoryState lab) => lab == null ? T("ui.label.laboratory", "Laboratory") : $"{T("ui.label.laboratory", "Laboratory")} {lab.id.Replace("lab_", "#")}";
        private string SourceName(string source) => source == LaboratoryJobType.Experiment ? T("ui.source.experiment", "experiment") : source == LaboratoryJobType.Production ? T("ui.source.production", "production") : source;
        private string ObjectiveName(string objective) => objective switch
        {
            ContractObjectiveType.Recipe => T("ui.objective.recipe", "Recipe"),
            ContractObjectiveType.Category => T("ui.objective.category", "Category"),
            ContractObjectiveType.Tag => T("ui.objective.tag", "Tag"),
            ContractObjectiveType.Rarity => T("ui.objective.rarity", "Rarity"),
            ContractObjectiveType.Ingredient => T("ui.objective.ingredient", "Ingredient"),
            ContractObjectiveType.Group => T("ui.objective.group", "Group"),
            ContractObjectiveType.Discover => T("ui.objective.discover", "Discover"),
            ContractObjectiveType.DistinctRecipes => T("ui.objective.distinct", "Distinct recipes"),
            ContractObjectiveType.RecipeMinRarity => T("ui.objective.recipe_quality", "Recipe quality"),
            ContractObjectiveType.ImproveRecord => T("ui.objective.record", "Improve record"),
            ContractObjectiveType.Source => T("ui.objective.source", "Source"),
            _ => objective
        };
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
