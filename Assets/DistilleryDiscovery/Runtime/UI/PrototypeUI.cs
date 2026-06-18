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
        private enum FooterMode { Experiment, Production, Sale, Contract, Laboratory, None }

        private static readonly Color Ink = new(0.09f, 0.08f, 0.12f);
        private static readonly Color Plum = new(0.25f, 0.08f, 0.22f);
        private static readonly Color Gold = new(0.95f, 0.68f, 0.18f);
        private static readonly Color Cream = new(0.96f, 0.91f, 0.79f);
        private GameService game;
        private SaveService save;
        private Font font;
        private Text statusText;
        private Text contentText;
        private Text selectionText;
        private Text currentIngredientText;
        private Text secondaryActionText;
        private Text primaryActionText;
        private RectTransform safeAreaRoot;
        private GridLayoutGroup navigationGrid;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private readonly List<string> selection = new();
        private FooterMode footerMode;
        private int currentIngredientIndex;
        private int currentRecipeIndex = -1;
        private int currentProductIndex = -1;
        private int currentContractIndex = -1;

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
            ShowState("Laboratorium gotowe. Odbierz dostawę, aby zacząć.");
        }

        private void Build()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            var canvasObject = Node("Canvas", transform, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0f;
            if (FindFirstObjectByType<EventSystem>() == null) Node("EventSystem", transform, typeof(EventSystem), typeof(StandaloneInputModule));
            safeAreaRoot = Node("SafeArea", canvasObject.transform, typeof(RectTransform)).GetComponent<RectTransform>();
            Stretch(safeAreaRoot.gameObject);
            var background = Panel("Background", safeAreaRoot, Ink); Stretch(background);

            var header = Panel("Header", background.transform, Plum); Rect(header, 0, .88f, 1, 1);
            var title = Label("DISTILLERY DISCOVERY\n<color=#F2AD2E>EKONOMIA PROTOTYPU</color>", header.transform, 50, TextAnchor.MiddleCenter); Stretch(title.gameObject);
            var status = Panel("Status", background.transform, new Color(.14f, .12f, .18f)); Rect(status, 0, .81f, 1, .88f);
            statusText = Label("", status.transform, 29, TextAnchor.MiddleCenter); Stretch(statusText.gameObject, 20, 8);

            var nav = Panel("Menu", background.transform, new Color(.11f, .10f, .14f)); Rect(nav, 0, .60f, 1, .81f);
            navigationGrid = nav.AddComponent<GridLayoutGroup>(); navigationGrid.padding = new RectOffset(18, 18, 12, 12); navigationGrid.spacing = new Vector2(10, 8); navigationGrid.cellSize = new Vector2(338, 66); navigationGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; navigationGrid.constraintCount = 3;
            AddButton(nav.transform, "STAN", () => ShowState());
            AddButton(nav.transform, "DOSTAWA", ReceiveDelivery);
            AddButton(nav.transform, "SKŁADNIKI", ShowIngredientInventory);
            AddButton(nav.transform, "PRODUKTY", ShowProducts);
            AddButton(nav.transform, "EKSPERYMENT", ShowExperiment);
            AddButton(nav.transform, "PRODUKCJA", ShowProduction);
            AddButton(nav.transform, "SPRZEDAŻ", ShowSale);
            AddButton(nav.transform, "KONTRAKTY", ShowContracts);
            AddButton(nav.transform, "RECEPTURY", ShowRecipeBook);
            AddButton(nav.transform, "LABORATORIUM", ShowLaboratory);
            AddButton(nav.transform, "ZAPISZ", SaveGame);
            AddButton(nav.transform, "WCZYTAJ", LoadGame);
            AddButton(nav.transform, "RESET", ResetGame);
            AddButton(nav.transform, "WYJDŹ", Application.Quit);

            var scroll = Panel("Content", background.transform, new Color(.07f, .065f, .09f)); Rect(scroll, .025f, .16f, .975f, .59f);
            scroll.AddComponent<ScrollRect>();
            var viewport = Panel("Viewport", scroll.transform, Color.clear); Stretch(viewport); viewport.AddComponent<RectMask2D>();
            var body = Node("Text", viewport.transform, typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
            contentText = body.GetComponent<Text>(); contentText.font = font; contentText.fontSize = 32; contentText.color = Cream; contentText.alignment = TextAnchor.UpperLeft; contentText.supportRichText = true;
            var bodyRect = body.GetComponent<RectTransform>(); bodyRect.anchorMin = new Vector2(0, 1); bodyRect.anchorMax = new Vector2(1, 1); bodyRect.pivot = new Vector2(.5f, 1); bodyRect.offsetMin = new Vector2(28, 0); bodyRect.offsetMax = new Vector2(-28, 0);
            body.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrollRect = scroll.GetComponent<ScrollRect>(); scrollRect.viewport = viewport.GetComponent<RectTransform>(); scrollRect.content = bodyRect; scrollRect.horizontal = false;

            var footer = Panel("ActionBar", background.transform, Plum); Rect(footer, 0, 0, 1, .15f);
            selectionText = Label("Wybrane: —", footer.transform, 27, TextAnchor.MiddleCenter); Rect(selectionText.gameObject, .02f, .69f, .98f, .98f);
            var previous = AddButton(footer.transform, "‹", () => ChangeIngredient(-1)); Rect(previous, .02f, .38f, .12f, .67f);
            currentIngredientText = Label("", footer.transform, 25, TextAnchor.MiddleCenter); Rect(currentIngredientText.gameObject, .13f, .38f, .60f, .67f);
            var next = AddButton(footer.transform, "›", () => ChangeIngredient(1)); Rect(next, .61f, .38f, .71f, .67f);
            var add = AddButton(footer.transform, "DODAJ", AddCurrentIngredient, Gold, Ink); Rect(add, .73f, .38f, .98f, .67f);
            var secondary = AddButton(footer.transform, "WYCZYŚĆ", DispatchSecondary); Rect(secondary, .02f, .05f, .30f, .33f); secondaryActionText = secondary.GetComponentInChildren<Text>();
            var primary = AddButton(footer.transform, "URUCHOM", DispatchPrimary, Gold, Ink); Rect(primary, .32f, .05f, .98f, .33f); primaryActionText = primary.GetComponentInChildren<Text>();
            RefreshCurrentIngredient();
            ApplyMobileLayout();
        }

        private void ApplyMobileLayout()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safeArea = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
            safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (navigationGrid == null) return;
            Canvas.ForceUpdateCanvases();
            var rect = navigationGrid.GetComponent<RectTransform>().rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            const int columns = 3;
            const int rows = 5;
            var width = (rect.width - navigationGrid.padding.horizontal - navigationGrid.spacing.x * (columns - 1)) / columns;
            var height = (rect.height - navigationGrid.padding.vertical - navigationGrid.spacing.y * (rows - 1)) / rows;
            navigationGrid.cellSize = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        private void ShowState(string message = null)
        {
            SetMode(FooterMode.None);
            SetStatus(message ?? "Aktualny stan gracza");
            contentText.text = $"<color=#F2AD2E><b>STAN GRACZA</b></color>\n\nZłoto: <b>{game.State.gold}</b>\nLaboratorium: <b>poziom {game.State.laboratoryLevel}</b>\nEksperymenty: <b>{game.State.experimentsCompleted}</b>\nProdukcje: <b>{game.State.productionsCompleted}</b>\nSkładniki: <b>{game.State.inventory.Sum(x => Math.Max(0, x.amount))}</b>\nProdukty: <b>{game.State.products.Sum(x => x.amount)}</b>\nOdkryte receptury: <b>{game.State.recipes.Count}/{game.Config.Recipes.Count}</b> ({Completion():0.#}%)\n\nPętla ekonomii:\nDostawa → eksperyment → magazyn produktów → sprzedaż lub kontrakt → ulepszenie laboratorium → produkcja";
        }

        private void ReceiveDelivery()
        {
            SetMode(FooterMode.None);
            var delivery = game.ReceiveDelivery();
            var lines = delivery.Items.Select(x => $"+{x.Value}  {game.Config.Ingredient(x.Key).displayName}");
            SetStatus("Dostawa odebrana");
            contentText.text = "<color=#F2AD2E><b>NOWA DOSTAWA</b></color>\n\n" + string.Join("\n", lines);
            RefreshCurrentIngredient();
        }

        private void ShowIngredientInventory()
        {
            SetMode(FooterMode.None); SetStatus("Magazyn składników");
            var sb = new StringBuilder("<color=#F2AD2E><b>SKŁADNIKI</b></color>\n\n");
            foreach (var item in game.Config.Ingredients)
            {
                var rarity = game.Config.Rarity(item.rarityId);
                sb.Append($"<color={rarity.colorHex}>●</color>  {item.displayName,-20}  <b>x{game.State.AmountOf(item.id)}</b>\n");
            }
            contentText.text = sb.ToString();
        }

        private void ShowProducts()
        {
            SetMode(FooterMode.None); SetStatus("Magazyn produktów");
            var sb = new StringBuilder("<color=#F2AD2E><b>PRODUKTY</b></color>\n\n");
            if (game.State.products.Count == 0) sb.Append("Magazyn produktów jest pusty.");
            foreach (var product in game.State.products)
            {
                var rarity = game.Config.Rarity(product.rarityId);
                sb.Append($"<b>{game.Config.Recipe(product.recipeId).displayName}</b> · <color={rarity.colorHex}>{rarity.displayName}</color>\n  x{product.amount} · {product.saleValue} zł/szt.\n\n");
            }
            contentText.text = sb.ToString();
        }

        private void ShowExperiment()
        {
            SetMode(FooterMode.Experiment); SetStatus("Wybierz dokładnie trzy składniki");
            contentText.text = "<color=#F2AD2E><b>EKSPERYMENT</b></color>\n\nWybierz 3 składniki. Receptura i rzadkość zostaną wylosowane, a produkt trafi do magazynu.\n\nPo zebraniu trzech składników zobaczysz podgląd możliwych receptur.";
            UpdateSelection();
        }

        private void ShowProduction()
        {
            var discovered = game.Config.Recipes.Where(x => game.State.RecipeState(x.id) != null).ToList();
            SetMode(FooterMode.Production);
            if (discovered.Count == 0)
            {
                currentRecipeIndex = -1; SetStatus("Najpierw odkryj recepturę"); contentText.text = "<color=#F2AD2E><b>PRODUKCJA</b></color>\n\nBrak odkrytych receptur."; return;
            }
            currentRecipeIndex = (currentRecipeIndex + 1 + discovered.Count) % discovered.Count;
            var recipe = discovered[currentRecipeIndex];
            var valid = game.Config.Ingredients.Where(i => i.outcomeWeights.Any(x => x.recipeId == recipe.id && x.weight > 0)).Select(i => i.displayName);
            SetStatus("Ponowne kliknięcie PRODUKCJA wybiera następną recepturę");
            contentText.text = $"<color=#F2AD2E><b>PRODUKCJA: {recipe.displayName}</b></color>\n\nWybierz dokładnie 3 składniki wpływające na tę recepturę. Duplikaty są dozwolone.\n\nPoprawne składniki:\n• {string.Join("\n• ", valid)}\n\nRezultat receptury jest gwarantowany; losowana jest tylko rzadkość.";
            UpdateSelection();
        }

        private void ShowSale()
        {
            SetMode(FooterMode.Sale);
            if (game.State.products.Count == 0) { SetStatus("Brak produktów do sprzedaży"); contentText.text = "<color=#F2AD2E><b>SPRZEDAŻ</b></color>\n\nMagazyn jest pusty."; return; }
            currentProductIndex = (currentProductIndex + 1 + game.State.products.Count) % game.State.products.Count;
            var product = game.State.products[currentProductIndex];
            var rarity = game.Config.Rarity(product.rarityId);
            SetStatus("Ponowne kliknięcie SPRZEDAŻ wybiera następną pozycję");
            contentText.text = $"<color=#F2AD2E><b>SPRZEDAŻ</b></color>\n\nWybrano: <b>{game.Config.Recipe(product.recipeId).displayName}</b>\nRzadkość: <color={rarity.colorHex}>{rarity.displayName}</color>\nIlość: {product.amount}\nCena sztuki: <b>{product.saleValue} zł</b>\n\nMożesz sprzedać jedną sztukę albo cały magazyn.";
        }

        private void ShowContracts()
        {
            SetMode(FooterMode.Contract);
            if (game.State.activeContractIds.Count == 0) { SetStatus("Brak aktywnych kontraktów"); contentText.text = "<color=#F2AD2E><b>KONTRAKTY</b></color>\n\nBrak aktywnych kontraktów."; return; }
            currentContractIndex = (currentContractIndex + 1 + game.State.activeContractIds.Count) % game.State.activeContractIds.Count;
            var contract = game.Config.Contract(game.State.activeContractIds[currentContractIndex]);
            SetStatus("Ponowne kliknięcie KONTRAKTY wybiera następny kontrakt");
            contentText.text = $"<color=#F2AD2E><b>KONTRAKT: {contract.displayName}</b></color>\n\nWymaganie: <b>{DescribeRequirement(contract)}</b>\nNagroda: <b>{contract.goldReward} złota</b>\n\nRealizacja zużywa pasujące produkty.";
        }

        private void ShowLaboratory()
        {
            SetMode(FooterMode.Laboratory);
            var current = game.Config.LaboratoryLevel(game.State.laboratoryLevel);
            var next = game.Config.LaboratoryLevel(game.State.laboratoryLevel + 1);
            SetStatus("Laboratorium wpływa na losowanie rzadkości");
            contentText.text = $"<color=#F2AD2E><b>LABORATORIUM · POZIOM {current.level}</b></color>\n\nBonus do wyższej rzadkości: <b>+{current.productQualityBonus:P0}</b>\n\n" + (next == null ? "Osiągnięto maksymalny poziom." : $"Następny poziom: <b>{next.level}</b>\nNowy bonus: <b>+{next.productQualityBonus:P0}</b>\nKoszt: <b>{next.upgradeCost} złota</b>");
        }

        private void ShowRecipeBook()
        {
            SetMode(FooterMode.None); SetStatus($"Książka receptur — ukończono {Completion():0.#}%");
            var sb = new StringBuilder($"<color=#F2AD2E><b>KSIĄŻKA RECEPTUR  {game.State.recipes.Count}/{game.Config.Recipes.Count}</b></color>\n\n");
            foreach (var recipe in game.Config.Recipes)
            {
                var state = game.State.RecipeState(recipe.id);
                if (state == null) sb.Append("◇  ???\n    Nieodkryta receptura\n\n");
                else
                {
                    var rarity = game.Config.Rarity(state.highestProductRarityId);
                    var ingredients = string.Join(", ", state.revealedIngredientIds.Select(x => game.Config.Ingredient(x).displayName));
                    sb.Append($"◆  <b>{recipe.displayName}</b>\n    Rekord: <color={rarity.colorHex}>{rarity.displayName}</color> · Wytworzenia: {state.timesCreated}\n    Odkryte składniki: {ingredients}\n\n");
                }
            }
            contentText.text = sb.ToString();
        }

        private void AddCurrentIngredient()
        {
            if (footerMode != FooterMode.Experiment && footerMode != FooterMode.Production) { SetStatus("Składniki wybiera się w eksperymencie lub produkcji."); return; }
            var required = footerMode == FooterMode.Experiment ? game.Config.Economy.ingredientsPerExperiment : game.Config.Economy.ingredientsPerProduction;
            var id = game.Config.Ingredients[currentIngredientIndex].id;
            if (selection.Count >= required) { SetStatus("Wybrano już wymaganą liczbę składników."); return; }
            if (game.State.AmountOf(id) <= selection.Count(x => x == id)) { SetStatus("Brak kolejnej sztuki tego składnika."); return; }
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
                var sb = new StringBuilder("<color=#F2AD2E><b>MOŻLIWE WYNIKI</b></color>\n\n");
                foreach (var outcome in game.Preview(selection))
                {
                    var known = game.State.RecipeState(outcome.RecipeId) != null;
                    sb.Append($"{(known ? game.Config.Recipe(outcome.RecipeId).displayName : "???"),-24} <b>{outcome.Probability:P1}</b>\n");
                }
                contentText.text = sb.ToString(); SetStatus("Podgląd gotowy");
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
                    case FooterMode.Sale: SellSelected(); break;
                    case FooterMode.Contract: FulfillSelectedContract(); break;
                    case FooterMode.Laboratory: game.UpgradeLaboratory(); ShowLaboratory(); break;
                }
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void DispatchSecondary()
        {
            if (footerMode == FooterMode.Sale)
            {
                var result = game.SellAllProducts(); ShowState($"Sprzedano {result.ItemsSold} produktów za {result.GoldEarned} złota");
            }
            else if (footerMode == FooterMode.Experiment || footerMode == FooterMode.Production)
            {
                selection.Clear(); UpdateSelection();
            }
        }

        private void RunExperiment()
        {
            var result = game.RunExperiment(selection);
            var rarity = game.Config.Rarity(result.RarityId);
            contentText.text = $"<color=#F2AD2E><b>WYNIK EKSPERYMENTU</b></color>\n\nPowstało: <b>{game.Config.Recipe(result.RecipeId).displayName}</b>\nJakość: <color={rarity.colorHex}><b>{rarity.displayName}</b></color>\nWartość: <b>{result.SaleValue} zł</b>\n\nProdukt trafił do magazynu.\n{(result.WasDiscovered ? "NOWA RECEPTURA ODKRYTA!" : result.RarityImproved ? "NOWY REKORD RZADKOŚCI!" : "Znana receptura — rekord bez zmian.")}";
            SetStatus("Eksperyment zakończony — produkt czeka w magazynie"); selection.Clear(); UpdateSelection();
        }

        private void RunProduction()
        {
            var discovered = game.Config.Recipes.Where(x => game.State.RecipeState(x.id) != null).ToList();
            if (currentRecipeIndex < 0 || discovered.Count == 0) throw new InvalidOperationException("Wybierz odkrytą recepturę.");
            var result = game.RunProduction(discovered[currentRecipeIndex].id, selection);
            var rarity = game.Config.Rarity(result.RarityId);
            contentText.text = $"<color=#F2AD2E><b>PRODUKCJA ZAKOŃCZONA</b></color>\n\nProdukt: <b>{game.Config.Recipe(result.RecipeId).displayName}</b>\nRzadkość: <color={rarity.colorHex}>{rarity.displayName}</color>\nWartość: {result.SaleValue} zł\n\nProdukt dodano do magazynu.";
            SetStatus("Produkcja zakończona"); selection.Clear(); UpdateSelection();
        }

        private void SellSelected()
        {
            if (game.State.products.Count == 0) throw new InvalidOperationException("Brak produktów do sprzedaży.");
            currentProductIndex %= game.State.products.Count;
            var product = game.State.products[currentProductIndex];
            var result = game.SellProduct(product.recipeId, product.rarityId, product.saleValue);
            currentProductIndex = -1; ShowSale(); SetStatus($"Sprzedano 1 produkt za {result.GoldEarned} złota");
        }

        private void FulfillSelectedContract()
        {
            if (game.State.activeContractIds.Count == 0) throw new InvalidOperationException("Brak aktywnych kontraktów.");
            currentContractIndex %= game.State.activeContractIds.Count;
            var result = game.FulfillContract(game.State.activeContractIds[currentContractIndex]);
            currentContractIndex = -1; ShowContracts(); SetStatus($"Kontrakt zrealizowany: +{result.GoldEarned} złota");
        }

        private string DescribeRequirement(ContractDefinition contract)
        {
            return contract.requirementType switch
            {
                ContractRequirementType.Recipe => $"{contract.amount} × {game.Config.Recipe(contract.targetId).displayName}",
                ContractRequirementType.Rarity => $"{contract.amount} × produkt {game.Config.Rarity(contract.targetId).displayName}",
                ContractRequirementType.Category => $"{contract.amount} × {game.Config.Category(contract.targetId).displayName}",
                _ => contract.targetId
            };
        }

        private void SetMode(FooterMode mode)
        {
            footerMode = mode;
            secondaryActionText.text = mode == FooterMode.Sale ? "SPRZEDAJ WSZYSTKO" : mode is FooterMode.Experiment or FooterMode.Production ? "WYCZYŚĆ" : "—";
            primaryActionText.text = mode switch
            {
                FooterMode.Experiment => "URUCHOM EKSPERYMENT",
                FooterMode.Production => "WYPRODUKUJ",
                FooterMode.Sale => "SPRZEDAJ 1 SZTUKĘ",
                FooterMode.Contract => "ZREALIZUJ KONTRAKT",
                FooterMode.Laboratory => "ULEPSZ LABORATORIUM",
                _ => "—"
            };
            if (mode != FooterMode.Experiment && mode != FooterMode.Production) selection.Clear();
            UpdateSelection();
        }

        private void SaveGame() { save.Save(game.State); ShowState("Gra zapisana lokalnie"); }
        private void LoadGame() { try { game.ReplaceState(save.Load()); currentRecipeIndex = -1; ShowState("Wczytano lokalny zapis"); } catch (Exception ex) { SetStatus(ex.Message); } }
        private void ResetGame() { save.Reset(); game.ReplaceState(GameService.NewState(game.Config)); currentRecipeIndex = -1; ShowState("Zapis usunięty, stan gry zresetowany"); }
        private void RefreshCurrentIngredient() { if (currentIngredientText != null && game.Config.Ingredients.Count > 0) { var item = game.Config.Ingredients[currentIngredientIndex]; currentIngredientText.text = $"{item.displayName}  x{game.State.AmountOf(item.id)}"; } }
        private void UpdateSelection() { if (selectionText != null) selectionText.text = "Wybrane: " + (selection.Count == 0 ? "—" : string.Join(" + ", selection.Select(x => game.Config.Ingredient(x).displayName))) + $"  ({selection.Count}/3)"; RefreshCurrentIngredient(); }
        private float Completion() => game.Config.Recipes.Count == 0 ? 0 : 100f * game.State.recipes.Count / game.Config.Recipes.Count;
        private void SetStatus(string value) => statusText.text = value;

        private GameObject Node(string name, Transform parent, params Type[] components) { var node = new GameObject(name, components); node.transform.SetParent(parent, false); return node; }
        private GameObject Panel(string name, Transform parent, Color color) { var node = Node(name, parent, typeof(RectTransform), typeof(Image)); node.GetComponent<Image>().color = color; return node; }
        private Text Label(string value, Transform parent, int size, TextAnchor anchor) { var node = Node("Label", parent, typeof(RectTransform), typeof(Text)); var text = node.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = Cream; text.alignment = anchor; text.text = value; text.supportRichText = true; return text; }
        private GameObject AddButton(Transform parent, string label, Action action, Color? color = null, Color? textColor = null) { var node = Panel(label, parent, color ?? new Color(.28f, .20f, .31f)); node.AddComponent<Button>().onClick.AddListener(() => action()); var text = Label(label, node.transform, 24, TextAnchor.MiddleCenter); text.color = textColor ?? Cream; text.fontStyle = FontStyle.Bold; Stretch(text.gameObject, 5, 3); return node; }
        private static void Stretch(GameObject node, float x = 0, float y = 0) { var rect = node.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y); }
        private static void Rect(GameObject node, float xMin, float yMin, float xMax, float yMax) { var rect = node.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(xMin, yMin); rect.anchorMax = new Vector2(xMax, yMax); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    }
}
