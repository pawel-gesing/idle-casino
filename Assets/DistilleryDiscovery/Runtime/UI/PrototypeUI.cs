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
        private enum FooterMode { Experiment, Production, Contract, Laboratory, None }

        private sealed class LocalizedBinding
        {
            public Text Text;
            public string Key;
            public string Fallback;
        }

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
        private Text statusText;
        private Text contentText;
        private Text selectionText;
        private Text currentIngredientText;
        private Text secondaryActionText;
        private Text primaryActionText;
        private GameObject settingsModal;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private FooterMode footerMode;
        private int currentIngredientIndex;
        private int currentRecipeIndex = -1;

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
            ShowState(T("ui.status.ready", "Ready. Receive a delivery to begin."));
        }

        private void Build()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            var canvasObject = Node("Canvas", transform, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasScaler = canvasObject.GetComponent<CanvasScaler>(); canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; canvasScaler.referenceResolution = new Vector2(1080, 1920); canvasScaler.matchWidthOrHeight = 0f;
            if (FindAnyObjectByType<EventSystem>() == null) Node("EventSystem", transform, typeof(EventSystem), typeof(StandaloneInputModule));
            safeAreaRoot = Node("SafeArea", canvasObject.transform, typeof(RectTransform)).GetComponent<RectTransform>();
            Stretch(safeAreaRoot.gameObject);
            var background = Panel("Background", safeAreaRoot, Ink); Stretch(background);

            var header = Panel("Header", background.transform, Plum); Rect(header, 0, .88f, 1, 1);
            var title = Label("DISTILLERY DISCOVERY", header.transform, 52, TextAnchor.MiddleCenter); Rect(title.gameObject, .04f, 0, .84f, 1);
            var settings = AddButton(header.transform, "⚙", OpenSettings, Gold, Ink); Rect(settings, .86f, .20f, .97f, .80f);

            var status = Panel("Status", background.transform, new Color(.14f, .12f, .18f)); Rect(status, 0, .81f, 1, .88f);
            statusText = Label("", status.transform, 29, TextAnchor.MiddleCenter); Stretch(statusText.gameObject, 20, 8);

            var nav = Panel("Menu", background.transform, new Color(.11f, .10f, .14f)); Rect(nav, 0, .60f, 1, .81f);
            navigationGrid = nav.AddComponent<GridLayoutGroup>(); navigationGrid.padding = new RectOffset(18, 18, 12, 12); navigationGrid.spacing = new Vector2(10, 8); navigationGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; navigationGrid.constraintCount = 3;
            AddLocalizedButton(nav.transform, "ui.nav.state", "STATE", () => ShowState());
            AddLocalizedButton(nav.transform, "ui.nav.ingredients", "INGREDIENTS", ShowIngredientInventory);
            AddLocalizedButton(nav.transform, "ui.nav.recipes", "RECIPES", ShowRecipeBook);
            AddLocalizedButton(nav.transform, "ui.nav.experiment", "EXPERIMENT", ShowExperiment);
            AddLocalizedButton(nav.transform, "ui.nav.production", "PRODUCTION", ShowProduction);
            AddLocalizedButton(nav.transform, "ui.nav.contracts", "CONTRACTS", ShowContracts);
            AddLocalizedButton(nav.transform, "ui.nav.delivery", "DELIVERY", ReceiveDelivery);
            AddLocalizedButton(nav.transform, "ui.nav.laboratory", "LABORATORY", ShowLaboratory);

            var scroll = Panel("Content", background.transform, new Color(.07f, .065f, .09f)); Rect(scroll, .025f, .16f, .975f, .59f);
            scroll.AddComponent<ScrollRect>();
            var viewport = Panel("Viewport", scroll.transform, Color.clear); Stretch(viewport); viewport.AddComponent<RectMask2D>();
            var body = Node("Text", viewport.transform, typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
            contentText = body.GetComponent<Text>(); contentText.font = font; contentText.fontSize = 32; contentText.color = Cream; contentText.alignment = TextAnchor.UpperLeft; contentText.supportRichText = true;
            var bodyRect = body.GetComponent<RectTransform>(); bodyRect.anchorMin = new Vector2(0, 1); bodyRect.anchorMax = new Vector2(1, 1); bodyRect.pivot = new Vector2(.5f, 1); bodyRect.offsetMin = new Vector2(28, 0); bodyRect.offsetMax = new Vector2(-28, 0);
            body.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrollRect = scroll.GetComponent<ScrollRect>(); scrollRect.viewport = viewport.GetComponent<RectTransform>(); scrollRect.content = bodyRect; scrollRect.horizontal = false;

            var footer = Panel("ActionBar", background.transform, Plum); Rect(footer, 0, 0, 1, .15f);
            selectionText = Label("", footer.transform, 27, TextAnchor.MiddleCenter); Rect(selectionText.gameObject, .02f, .69f, .98f, .98f);
            var previous = AddButton(footer.transform, "‹", () => ChangeIngredient(-1)); Rect(previous, .02f, .38f, .12f, .67f);
            currentIngredientText = Label("", footer.transform, 25, TextAnchor.MiddleCenter); Rect(currentIngredientText.gameObject, .13f, .38f, .60f, .67f);
            var next = AddButton(footer.transform, "›", () => ChangeIngredient(1)); Rect(next, .61f, .38f, .71f, .67f);
            var add = AddLocalizedButton(footer.transform, "ui.action.add", "ADD", AddCurrentIngredient, Gold, Ink); Rect(add, .73f, .38f, .98f, .67f);
            var secondary = AddButton(footer.transform, "—", DispatchSecondary); Rect(secondary, .02f, .05f, .30f, .33f); secondaryActionText = secondary.GetComponentInChildren<Text>();
            var primary = AddButton(footer.transform, "—", DispatchPrimary, Gold, Ink); Rect(primary, .32f, .05f, .98f, .33f); primaryActionText = primary.GetComponentInChildren<Text>();

            BuildSettingsModal();
            RefreshCurrentIngredient();
            ApplyMobileLayout();
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

        private void ApplyMobileLayout()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            var rawSafeArea = Screen.safeArea;
            var safeArea = rawSafeArea;
            const float portraitAspect = 9f / 16f;
            if (safeArea.height > 0f && safeArea.width / safeArea.height > portraitAspect)
            {
                var portraitWidth = safeArea.height * portraitAspect;
                safeArea.x += (safeArea.width - portraitWidth) * .5f;
                safeArea.width = portraitWidth;
            }
            canvasScaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;
            safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
            safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            safeAreaRoot.offsetMin = Vector2.zero; safeAreaRoot.offsetMax = Vector2.zero;
            lastSafeArea = rawSafeArea; lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            Canvas.ForceUpdateCanvases();
            var rect = navigationGrid.GetComponent<RectTransform>().rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            const int columns = 3;
            const int rows = 3;
            var width = (rect.width - navigationGrid.padding.horizontal - navigationGrid.spacing.x * (columns - 1)) / columns;
            var height = (rect.height - navigationGrid.padding.vertical - navigationGrid.spacing.y * (rows - 1)) / rows;
            navigationGrid.cellSize = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        private void ShowState(string message = null)
        {
            SetMode(FooterMode.None); SetStatus(message ?? T("ui.status.state", "Current player state"));
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.state", "PLAYER STATE")}</b></color>\n\n{T("ui.label.gold", "Gold")}: <b>{game.State.gold}</b>\n{T("ui.label.laboratory", "Laboratory")}: <b>{T("ui.label.level", "level")} {game.State.laboratoryLevel}</b>\n{T("ui.label.experiments", "Experiments")}: <b>{game.State.experimentsCompleted}</b>\n{T("ui.label.productions", "Productions")}: <b>{game.State.productionsCompleted}</b>\n{T("ui.label.ingredients", "Ingredients")}: <b>{game.State.inventory.Sum(x => Math.Max(0, x.amount))}</b>\n{T("ui.label.recipes", "Discovered recipes")}: <b>{game.State.recipes.Count}/{game.Config.Recipes.Count}</b> ({Completion():0.#}%)\n{T("ui.label.contracts", "Active contracts")}: <b>{game.State.activeContracts.Count}</b>";
        }

        private void ReceiveDelivery()
        {
            SetMode(FooterMode.None);
            var delivery = game.ReceiveDelivery();
            var lines = delivery.Items.Select(x => $"+{x.Value}  {IngredientName(x.Key)}");
            SetStatus(T("ui.status.delivery", "Delivery received"));
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.delivery", "NEW DELIVERY")}</b></color>\n\n" + string.Join("\n", lines);
            RefreshCurrentIngredient();
        }

        private void ShowIngredientInventory()
        {
            SetMode(FooterMode.None); SetStatus(T("ui.status.ingredients", "Ingredient storage"));
            var sb = new StringBuilder($"<color=#F2AD2E><b>{T("ui.heading.ingredients", "INGREDIENTS")}</b></color>\n\n");
            foreach (var item in game.Config.Ingredients)
            {
                var rarity = game.Config.Rarity(item.rarityId);
                sb.Append($"<color={rarity.colorHex}>●</color>  {IngredientName(item.id),-20}  <b>x{game.State.AmountOf(item.id)}</b>\n");
            }
            contentText.text = sb.ToString();
        }

        private void ShowExperiment()
        {
            SetMode(FooterMode.Experiment); SetStatus(T("ui.status.select_three", "Select exactly three ingredients"));
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.experiment", "EXPERIMENT")}</b></color>\n\n{T("ui.experiment.help", "Choose 3 ingredients. The recipe and rarity are rolled, then the product is sold automatically.")}";
            UpdateSelection();
        }

        private void ShowProduction()
        {
            var discovered = game.Config.Recipes.Where(x => game.State.RecipeState(x.id) != null).ToList();
            SetMode(FooterMode.Production);
            if (discovered.Count == 0)
            {
                currentRecipeIndex = -1; SetStatus(T("ui.status.discover_first", "Discover a recipe first")); contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.production", "PRODUCTION")}</b></color>\n\n{T("ui.production.none", "No discovered recipes.")}"; return;
            }
            currentRecipeIndex = (currentRecipeIndex + 1 + discovered.Count) % discovered.Count;
            var recipe = discovered[currentRecipeIndex];
            var valid = game.Config.Ingredients.Where(i => i.outcomeWeights.Any(x => x.recipeId == recipe.id && x.weight > 0)).Select(i => IngredientName(i.id));
            SetStatus(T("ui.status.production_cycle", "Press PRODUCTION again to select the next recipe"));
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.production", "PRODUCTION")}: {RecipeName(recipe.id)}</b></color>\n\n{T("ui.production.help", "Choose exactly 3 contributing ingredients. Duplicates are allowed.")}\n\n{T("ui.label.valid_ingredients", "Valid ingredients")}:\n• {string.Join("\n• ", valid)}\n\n{T("ui.production.guaranteed", "The recipe is guaranteed; only rarity is rolled. The product is sold automatically.")}";
            UpdateSelection();
        }

        private void ShowContracts()
        {
            SetMode(FooterMode.Contract); SetStatus(T("ui.status.contracts", "Three contracts are active at once"));
            var sb = new StringBuilder($"<color=#F2AD2E><b>{T("ui.heading.contracts", "ACTIVE CONTRACTS")}</b></color>\n\n");
            foreach (var state in game.State.activeContracts)
            {
                var contract = game.Config.Contract(state.contractId);
                var complete = state.progress >= contract.amount;
                sb.Append($"<b>{ContractName(contract.id)}</b>\n{DescribeRequirement(contract)}\n{T("ui.label.progress", "Progress")}: <b>{state.progress}/{contract.amount}</b> · {T("ui.label.reward", "Reward")}: <b>{contract.goldReward} {T("ui.label.gold_lower", "gold")}</b>\n");
                if (complete) sb.Append($"<color=#63D889><b>{T("ui.contract.ready", "READY TO CLAIM")}</b></color>\n");
                sb.Append("\n");
            }
            contentText.text = sb.ToString();
        }

        private void ShowLaboratory()
        {
            SetMode(FooterMode.Laboratory);
            var current = game.Config.LaboratoryLevel(game.State.laboratoryLevel);
            var next = game.Config.LaboratoryLevel(game.State.laboratoryLevel + 1);
            SetStatus(T("ui.status.laboratory", "Laboratory improves product rarity"));
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.laboratory", "LABORATORY")} · {T("ui.label.level", "LEVEL")} {current.level}</b></color>\n\n{T("ui.label.rarity_bonus", "Higher rarity bonus")}: <b>+{current.productQualityBonus:P0}</b>\n\n" + (next == null ? T("ui.laboratory.max", "Maximum level reached.") : $"{T("ui.label.next_level", "Next level")}: <b>{next.level}</b>\n{T("ui.label.new_bonus", "New bonus")}: <b>+{next.productQualityBonus:P0}</b>\n{T("ui.label.cost", "Cost")}: <b>{next.upgradeCost} {T("ui.label.gold_lower", "gold")}</b>");
        }

        private void ShowRecipeBook()
        {
            SetMode(FooterMode.None); SetStatus(F("ui.status.recipe_book", "Recipe book — {0:0.#}% complete", Completion()));
            var sb = new StringBuilder($"<color=#F2AD2E><b>{T("ui.heading.recipe_book", "RECIPE BOOK")}  {game.State.recipes.Count}/{game.Config.Recipes.Count}</b></color>\n\n");
            foreach (var recipe in game.Config.Recipes)
            {
                var state = game.State.RecipeState(recipe.id);
                if (state == null) sb.Append($"◇  ???\n    {T("ui.recipe.undiscovered", "Undiscovered recipe")}\n\n");
                else
                {
                    var rarity = game.Config.Rarity(state.highestProductRarityId);
                    var ingredients = string.Join(", ", state.revealedIngredientIds.Select(IngredientName));
                    sb.Append($"◆  <b>{RecipeName(recipe.id)}</b>\n    {T("ui.label.record", "Record")}: <color={rarity.colorHex}>{RarityName(rarity.id)}</color> · {T("ui.label.created", "Created")}: {state.timesCreated}\n    {T("ui.label.discovered_ingredients", "Discovered ingredients")}: {ingredients}\n\n");
                }
            }
            contentText.text = sb.ToString();
        }

        private void AddCurrentIngredient()
        {
            if (footerMode != FooterMode.Experiment && footerMode != FooterMode.Production) { SetStatus(T("ui.error.select_mode", "Ingredients are selected in experiment or production mode.")); return; }
            var required = footerMode == FooterMode.Experiment ? game.Config.Economy.ingredientsPerExperiment : game.Config.Economy.ingredientsPerProduction;
            var id = game.Config.Ingredients[currentIngredientIndex].id;
            if (selection.Count >= required) { SetStatus(T("ui.error.selection_full", "The required number of ingredients is already selected.")); return; }
            if (game.State.AmountOf(id) <= selection.Count(x => x == id)) { SetStatus(T("ui.error.no_ingredient", "No more of this ingredient.")); return; }
            selection.Add(id); UpdateSelection();
            if (footerMode == FooterMode.Experiment && selection.Count == required) ShowPreview();
        }

        private void ChangeIngredient(int direction)
        {
            currentIngredientIndex = (currentIngredientIndex + direction + game.Config.Ingredients.Count) % game.Config.Ingredients.Count;
            RefreshCurrentIngredient();
        }

        private void ShowPreview()
        {
            try
            {
                var sb = new StringBuilder($"<color=#F2AD2E><b>{T("ui.heading.outcomes", "POSSIBLE OUTCOMES")}</b></color>\n\n");
                foreach (var outcome in game.Preview(selection))
                {
                    var known = game.State.RecipeState(outcome.RecipeId) != null;
                    sb.Append($"{(known ? RecipeName(outcome.RecipeId) : "???"),-24} <b>{outcome.Probability:P1}</b>\n");
                }
                contentText.text = sb.ToString(); SetStatus(T("ui.status.preview", "Preview ready"));
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void DispatchPrimary()
        {
            try
            {
                switch (footerMode)
                {
                    case FooterMode.Experiment: RunExperiment(); break;
                    case FooterMode.Production: RunProduction(); break;
                    case FooterMode.Contract: ClaimCompletedContract(); break;
                    case FooterMode.Laboratory: game.UpgradeLaboratory(); ShowLaboratory(); break;
                }
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void DispatchSecondary()
        {
            if (footerMode == FooterMode.Experiment || footerMode == FooterMode.Production)
            {
                selection.Clear(); UpdateSelection();
            }
        }

        private void RunExperiment()
        {
            var result = game.RunExperiment(selection);
            var rarity = game.Config.Rarity(result.RarityId);
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.experiment_result", "EXPERIMENT RESULT")}</b></color>\n\n{T("ui.label.created_product", "Created")}: <b>{RecipeName(result.RecipeId)}</b>\n{T("ui.label.quality", "Quality")}: <color={rarity.colorHex}><b>{RarityName(rarity.id)}</b></color>\n{T("ui.label.auto_sale", "Automatic sale")}: <b>+{result.SaleValue} {T("ui.label.gold_lower", "gold")}</b>\n\n{(result.WasDiscovered ? T("ui.result.discovered", "NEW RECIPE DISCOVERED!") : result.RarityImproved ? T("ui.result.record", "NEW RARITY RECORD!") : T("ui.result.known", "Known recipe — record unchanged."))}";
            SetStatus(T("ui.status.auto_sold", "Product created and sold automatically")); selection.Clear(); UpdateSelection();
        }

        private void RunProduction()
        {
            var discovered = game.Config.Recipes.Where(x => game.State.RecipeState(x.id) != null).ToList();
            if (currentRecipeIndex < 0 || discovered.Count == 0) throw new InvalidOperationException(T("ui.error.choose_recipe", "Choose a discovered recipe."));
            var result = game.RunProduction(discovered[currentRecipeIndex].id, selection);
            var rarity = game.Config.Rarity(result.RarityId);
            contentText.text = $"<color=#F2AD2E><b>{T("ui.heading.production_result", "PRODUCTION COMPLETE")}</b></color>\n\n{T("ui.label.created_product", "Product")}: <b>{RecipeName(result.RecipeId)}</b>\n{T("ui.label.quality", "Rarity")}: <color={rarity.colorHex}>{RarityName(rarity.id)}</color>\n{T("ui.label.auto_sale", "Automatic sale")}: <b>+{result.SaleValue} {T("ui.label.gold_lower", "gold")}</b>";
            SetStatus(T("ui.status.auto_sold", "Product created and sold automatically")); selection.Clear(); UpdateSelection();
        }

        private void ClaimCompletedContract()
        {
            var completed = game.State.activeContracts.FirstOrDefault(x => x.progress >= game.Config.Contract(x.contractId).amount);
            if (completed == null) throw new InvalidOperationException(T("ui.error.no_completed_contract", "No completed contract to claim."));
            var result = game.ClaimContract(completed.contractId);
            ShowContracts(); SetStatus(F("ui.status.contract_claimed", "Reward claimed: +{0} gold", result.GoldEarned));
        }

        private string DescribeRequirement(ContractDefinition contract)
        {
            return contract.requirementType switch
            {
                ContractRequirementType.Recipe => F("ui.contract.recipe", "Create {0} × {1}", contract.amount, RecipeName(contract.targetId)),
                ContractRequirementType.Rarity => F("ui.contract.rarity", "Create {0} × {1} product", contract.amount, RarityName(contract.targetId)),
                ContractRequirementType.Category => F("ui.contract.category", "Create {0} × {1}", contract.amount, CategoryName(contract.targetId)),
                _ => contract.targetId
            };
        }

        private void SetMode(FooterMode mode)
        {
            footerMode = mode;
            secondaryActionText.text = mode is FooterMode.Experiment or FooterMode.Production ? T("ui.action.clear", "CLEAR") : "—";
            primaryActionText.text = mode switch
            {
                FooterMode.Experiment => T("ui.action.run_experiment", "RUN EXPERIMENT"),
                FooterMode.Production => T("ui.action.produce", "PRODUCE"),
                FooterMode.Contract => T("ui.action.claim", "CLAIM REWARD"),
                FooterMode.Laboratory => T("ui.action.upgrade", "UPGRADE LABORATORY"),
                _ => "—"
            };
            if (mode != FooterMode.Experiment && mode != FooterMode.Production) selection.Clear();
            UpdateSelection();
        }

        private void OpenSettings() => settingsModal.SetActive(true);
        private void CloseSettings() => settingsModal.SetActive(false);
        private void ToggleLanguage()
        {
            game.State.languageCode = Language == "pl" ? "en" : "pl";
            RefreshLocalizedBindings();
            ShowState(T("ui.status.language_changed", "Language changed"));
            settingsModal.SetActive(true);
        }

        private void SaveGame() { save.Save(game.State); CloseSettings(); ShowState(T("ui.status.saved", "Game saved locally")); }
        private void LoadGame() { try { game.ReplaceState(save.Load()); currentRecipeIndex = -1; RefreshLocalizedBindings(); CloseSettings(); ShowState(T("ui.status.loaded", "Local save loaded")); } catch (Exception ex) { SetStatus(ex.Message); } }
        private void ResetGame() { save.Reset(); game.ReplaceState(GameService.NewState(game.Config, Language)); currentRecipeIndex = -1; RefreshLocalizedBindings(); CloseSettings(); ShowState(T("ui.status.reset", "Save deleted and game reset")); }

        private string T(string key, string fallback) => game.Config.Text(key, Language, fallback);
        private string F(string key, string fallback, params object[] args) => string.Format(T(key, fallback), args);
        private string IngredientName(string id) => game.Config.Text($"ingredient.{id}", Language, game.Config.Ingredient(id)?.displayName ?? id);
        private string RecipeName(string id) => game.Config.Text($"recipe.{id}", Language, game.Config.Recipe(id)?.displayName ?? id);
        private string RarityName(string id) => game.Config.Text($"rarity.{id}", Language, game.Config.Rarity(id)?.displayName ?? id);
        private string CategoryName(string id) => game.Config.Text($"category.{id}", Language, game.Config.Category(id)?.displayName ?? id);
        private string ContractName(string id) => game.Config.Text($"contract.{id}", Language, game.Config.Contract(id)?.displayName ?? id);

        private void RefreshCurrentIngredient() { if (currentIngredientText != null && game.Config.Ingredients.Count > 0) { var item = game.Config.Ingredients[currentIngredientIndex]; currentIngredientText.text = $"{IngredientName(item.id)}  x{game.State.AmountOf(item.id)}"; } }
        private void UpdateSelection() { if (selectionText != null) selectionText.text = T("ui.label.selected", "Selected") + ": " + (selection.Count == 0 ? "—" : string.Join(" + ", selection.Select(IngredientName))) + $"  ({selection.Count}/3)"; RefreshCurrentIngredient(); }
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
