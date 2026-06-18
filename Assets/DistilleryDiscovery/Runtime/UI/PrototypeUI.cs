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
        private readonly List<string> selection = new();
        private Text currentIngredientText;
        private int currentIngredientIndex;

        public void Initialize(GameService gameService, SaveService saveService)
        {
            game = gameService; save = saveService;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build(); ShowState("Laboratorium gotowe. Odbierz dostawę, aby zacząć.");
        }

        private void Build()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            var canvasObject = Node("Canvas", transform, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null) Node("EventSystem", transform, typeof(EventSystem), typeof(StandaloneInputModule));
            var background = Panel("Background", canvasObject.transform, Ink); Stretch(background);

            var header = Panel("Header", background.transform, Plum); Rect(header, 0, .86f, 1, 1);
            var title = Label("DISTILLERY DISCOVERY\n<color=#F2AD2E>PROTOTYP LABORATORIUM</color>", header.transform, 54, TextAnchor.MiddleCenter); Stretch(title.gameObject);

            var status = Panel("Status", background.transform, new Color(.14f, .12f, .18f)); Rect(status, 0, .79f, 1, .86f);
            statusText = Label("", status.transform, 31, TextAnchor.MiddleCenter); Stretch(statusText.gameObject, 20, 8);

            var nav = Panel("Menu", background.transform, new Color(.11f, .10f, .14f)); Rect(nav, 0, .60f, 1, .79f);
            var grid = nav.AddComponent<GridLayoutGroup>(); grid.padding = new RectOffset(18, 18, 16, 16); grid.spacing = new Vector2(12, 10); grid.cellSize = new Vector2(250, 72); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            AddButton(nav.transform, "STAN", () => ShowState());
            AddButton(nav.transform, "DOSTAWA", ReceiveDelivery);
            AddButton(nav.transform, "MAGAZYN", ShowInventory);
            AddButton(nav.transform, "EKSPERYMENT", ShowExperiment);
            AddButton(nav.transform, "RECEPTURY", ShowRecipeBook);
            AddButton(nav.transform, "ZAPISZ", SaveGame);
            AddButton(nav.transform, "WCZYTAJ", LoadGame);
            AddButton(nav.transform, "RESET", ResetGame);
            AddButton(nav.transform, "WYJDŹ", Application.Quit);

            var scroll = Panel("Content", background.transform, new Color(.07f, .065f, .09f)); Rect(scroll, .025f, .16f, .975f, .59f);
            scroll.AddComponent<ScrollRect>();
            var viewport = Panel("Viewport", scroll.transform, Color.clear); Stretch(viewport); viewport.AddComponent<RectMask2D>();
            var body = Node("Text", viewport.transform, typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
            contentText = body.GetComponent<Text>(); contentText.font = font; contentText.fontSize = 35; contentText.color = Cream; contentText.alignment = TextAnchor.UpperLeft; contentText.supportRichText = true;
            var bodyRect = body.GetComponent<RectTransform>(); bodyRect.anchorMin = new Vector2(0, 1); bodyRect.anchorMax = new Vector2(1, 1); bodyRect.pivot = new Vector2(.5f, 1); bodyRect.offsetMin = new Vector2(28, 0); bodyRect.offsetMax = new Vector2(-28, 0);
            body.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrollRect = scroll.GetComponent<ScrollRect>(); scrollRect.viewport = viewport.GetComponent<RectTransform>(); scrollRect.content = bodyRect; scrollRect.horizontal = false;

            var footer = Panel("ExperimentBar", background.transform, Plum); Rect(footer, 0, 0, 1, .15f);
            selectionText = Label("Wybrane: —", footer.transform, 29, TextAnchor.MiddleCenter); Rect(selectionText.gameObject, .02f, .69f, .98f, .98f);
            var previous = AddButton(footer.transform, "‹", () => ChangeIngredient(-1)); Rect(previous, .02f, .38f, .12f, .67f);
            currentIngredientText = Label("", footer.transform, 27, TextAnchor.MiddleCenter); Rect(currentIngredientText.gameObject, .13f, .38f, .60f, .67f);
            var next = AddButton(footer.transform, "›", () => ChangeIngredient(1)); Rect(next, .61f, .38f, .71f, .67f);
            var add = AddButton(footer.transform, "DODAJ", AddCurrentIngredient, Gold, Ink); Rect(add, .73f, .38f, .98f, .67f);
            var clear = AddButton(footer.transform, "WYCZYŚĆ", ClearSelection); Rect(clear, .02f, .05f, .30f, .33f);
            var run = AddButton(footer.transform, "URUCHOM EKSPERYMENT", RunExperiment, Gold, Ink); Rect(run, .32f, .05f, .98f, .33f);
            RefreshCurrentIngredient();
        }

        private void ShowState(string message = null)
        {
            SetStatus(message ?? "Aktualny stan gracza");
            contentText.text = $"<color=#F2AD2E><b>STAN GRACZA</b></color>\n\nZłoto: <b>{game.State.gold}</b>\nEksperymenty: <b>{game.State.experimentsCompleted}</b>\nSkładniki w magazynie: <b>{game.State.inventory.Sum(x => Math.Max(0, x.amount))}</b>\nOdkryte receptury: <b>{game.State.recipes.Count}/{game.Config.Recipes.Count}</b> ({Completion():0.#}%)\n\nPętla MVP:\nDostawa → wybór 3 składników → podgląd szans → eksperyment → odkrycie → sprzedaż";
        }

        private void ReceiveDelivery()
        {
            var delivery = game.ReceiveDelivery();
            var lines = delivery.Items.Select(x => $"+{x.Value}  {game.Config.Ingredient(x.Key).displayName}");
            SetStatus("Dostawa odebrana"); contentText.text = "<color=#F2AD2E><b>NOWA DOSTAWA</b></color>\n\n" + string.Join("\n", lines) + "\n\nSkładniki dodano do magazynu.";
        }

        private void ShowInventory()
        {
            SetStatus("Magazyn składników");
            var sb = new StringBuilder("<color=#F2AD2E><b>MAGAZYN</b></color>\n\n");
            foreach (var item in game.Config.Ingredients)
            {
                var rarity = game.Config.Rarity(item.rarityId);
                sb.Append($"<color={rarity.colorHex}>●</color>  {item.displayName,-20}  <b>x{game.State.AmountOf(item.id)}</b>\n");
            }
            contentText.text = sb.ToString();
        }

        private void ShowExperiment()
        {
            SetStatus("Klikaj składniki, aby wybrać dokładnie trzy");
            var sb = new StringBuilder("<color=#F2AD2E><b>PLAN EKSPERYMENTU</b></color>\n\n");
            foreach (var item in game.Config.Ingredients)
                sb.Append($"<color=#E7C678>●</color>  {item.displayName}  x{game.State.AmountOf(item.id)}\n");
            sb.Append("\nWybierz składnik strzałkami na dole i naciśnij DODAJ. Duplikaty są dozwolone.");
            contentText.text = sb.ToString();
            UpdateSelection();
        }

        private void AddCurrentIngredient() => AddIngredient(game.Config.Ingredients[currentIngredientIndex].id);
        private void ChangeIngredient(int direction)
        {
            currentIngredientIndex = (currentIngredientIndex + direction + game.Config.Ingredients.Count) % game.Config.Ingredients.Count;
            RefreshCurrentIngredient();
        }
        private void RefreshCurrentIngredient()
        {
            if (currentIngredientText == null || game.Config.Ingredients.Count == 0) return;
            var item = game.Config.Ingredients[currentIngredientIndex];
            currentIngredientText.text = $"{item.displayName}  x{game.State.AmountOf(item.id)}";
        }
        private void AddIngredient(string id)
        {
            if (selection.Count >= game.Config.Economy.ingredientsPerExperiment) { SetStatus("Masz już trzy składniki — wyczyść wybór albo uruchom eksperyment."); return; }
            if (game.State.AmountOf(id) <= selection.Count(x => x == id)) { SetStatus("Brak kolejnej sztuki tego składnika."); return; }
            selection.Add(id); UpdateSelection(); RefreshCurrentIngredient();
            if (selection.Count == game.Config.Economy.ingredientsPerExperiment) ShowPreview();
        }

        private void ShowPreview()
        {
            try
            {
                var sb = new StringBuilder("<color=#F2AD2E><b>PRZEWIDYWANE WYNIKI</b></color>\n\n");
                foreach (var outcome in game.Preview(selection))
                {
                    var known = game.State.RecipeState(outcome.RecipeId) != null;
                    var name = known ? game.Config.Recipe(outcome.RecipeId).displayName : "???";
                    sb.Append($"{name,-24} <b>{outcome.Probability:P1}</b>\n");
                }
                sb.Append("\nRzadkość produktu jest losowana osobno."); contentText.text = sb.ToString(); SetStatus("Podgląd gotowy — możesz uruchomić eksperyment");
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void RunExperiment()
        {
            try
            {
                var result = game.RunExperiment(selection);
                var recipe = game.Config.Recipe(result.RecipeId); var rarity = game.Config.Rarity(result.RarityId);
                contentText.text = $"<color=#F2AD2E><b>WYNIK EKSPERYMENTU</b></color>\n\nPowstało: <b>{recipe.displayName}</b>\nJakość: <color={rarity.colorHex}><b>{rarity.displayName}</b></color>\nSprzedaż: <b>+{result.SaleValue} złota</b>\n\n{(result.WasDiscovered ? "NOWA RECEPTURA ODKRYTA!" : result.RarityImproved ? "NOWY REKORD RZADKOŚCI!" : "Znana receptura — rekord bez zmian.")}";
                SetStatus("Eksperyment rozstrzygnięty i produkt sprzedany"); ClearSelection(false);
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void ShowRecipeBook()
        {
            SetStatus($"Książka receptur — ukończono {Completion():0.#}%");
            var sb = new StringBuilder($"<color=#F2AD2E><b>KSIĄŻKA RECEPTUR  {game.State.recipes.Count}/{game.Config.Recipes.Count}</b></color>\n\n");
            foreach (var recipe in game.Config.Recipes)
            {
                var state = game.State.RecipeState(recipe.id);
                if (state == null) sb.Append("◇  ???\n    Nieodkryta receptura\n\n");
                else
                {
                    var rarity = game.Config.Rarity(state.highestProductRarityId);
                    var ingredients = string.Join(", ", state.revealedIngredientIds.Select(x => game.Config.Ingredient(x).displayName));
                    sb.Append($"◆  <b>{recipe.displayName}</b>\n    Rekord: <color={rarity.colorHex}>{rarity.displayName}</color> · Próby: {state.timesCreated}\n    Odkryte składniki: {ingredients}\n\n");
                }
            }
            contentText.text = sb.ToString();
        }

        private void SaveGame() { save.Save(game.State); ShowState("Gra zapisana lokalnie"); }
        private void LoadGame() { try { game.ReplaceState(save.Load()); ClearSelection(false); ShowState("Wczytano lokalny zapis"); } catch (Exception ex) { SetStatus(ex.Message); } }
        private void ResetGame() { save.Reset(); game.ReplaceState(GameService.NewState(game.Config)); ClearSelection(false); ShowState("Zapis usunięty, stan gry zresetowany"); }
        private void ClearSelection() => ClearSelection(true);
        private void ClearSelection(bool show) { selection.Clear(); UpdateSelection(); if (show) ShowExperiment(); }
        private void UpdateSelection() { selectionText.text = "Wybrane: " + (selection.Count == 0 ? "—" : string.Join(" + ", selection.Select(x => game.Config.Ingredient(x).displayName))) + $"  ({selection.Count}/3)"; RefreshCurrentIngredient(); }
        private float Completion() => game.Config.Recipes.Count == 0 ? 0 : 100f * game.State.recipes.Count / game.Config.Recipes.Count;
        private void SetStatus(string value) => statusText.text = value;

        private GameObject Node(string name, Transform parent, params Type[] components) { var node = new GameObject(name, components); node.transform.SetParent(parent, false); return node; }
        private GameObject Panel(string name, Transform parent, Color color) { var node = Node(name, parent, typeof(RectTransform), typeof(Image)); node.GetComponent<Image>().color = color; return node; }
        private Text Label(string value, Transform parent, int size, TextAnchor anchor) { var node = Node("Label", parent, typeof(RectTransform), typeof(Text)); var text = node.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = Cream; text.alignment = anchor; text.text = value; text.supportRichText = true; return text; }
        private GameObject AddButton(Transform parent, string label, Action action, Color? color = null, Color? textColor = null)
        {
            var node = Panel(label, parent, color ?? new Color(.28f, .20f, .31f)); node.AddComponent<Button>().onClick.AddListener(() => action());
            var text = Label(label, node.transform, 26, TextAnchor.MiddleCenter); text.color = textColor ?? Cream; text.fontStyle = FontStyle.Bold; Stretch(text.gameObject, 5, 3); return node;
        }
        private static void Stretch(GameObject node, float x = 0, float y = 0) { var rect = node.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y); }
        private static void Rect(GameObject node, float xMin, float yMin, float xMax, float yMax) { var rect = node.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(xMin, yMin); rect.anchorMax = new Vector2(xMax, yMax); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    }

}
