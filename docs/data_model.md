# Distillery Discovery - MVP Data Model

## Cel modelu danych

Model danych ma umożliwiać zbudowanie gry jako silnika kolekcjonersko-ekonomicznego, w którym cała zawartość jest definiowana przez konfigurację.

Kod gry powinien obsługiwać mechaniki, a dane powinny definiować:

- składniki,
- receptury,
- wagi wyników eksperymentów,
- rzadkości,
- dostawy,
- laboratoria,
- produkcję,
- kontrakty,
- progresję,
- monetyzację,
- sezony,
- motywy/reskiny.

Celem jest możliwość stworzenia drugiej gry o tej samej mechanice, ale z inną tematyką, przez podmianę danych i assetów.

---

# Założenia techniczne

Docelowa technologia: Unity.

Rekomendowany format konfiguracji MVP: JSON.

W Unity dane mogą być ładowane z:

- `StreamingAssets`,
- `Resources`,
- lokalnego katalogu konfiguracyjnego,
- później z Remote Config / CDN.

Na etapie MVP najprościej użyć JSON-ów w repozytorium.

Rekomendowana struktura:

```text
Assets/GameData/
  core/
    rarities.json
    currencies.json
    economy.json
  content/
    ingredients.json
    recipes.json
    recipe_outcomes.json
  systems/
    deliveries.json
    laboratories.json
    contracts.json
    mastery.json
    monetization.json
  seasons/
    season_001.json
  theme/
    theme.json
```

---

# Identyfikatory

Wszystkie encje powinny mieć stabilne tekstowe ID.

Przykłady:

```text
ingredient_malt_common
ingredient_honey_rare
recipe_beer_basic
recipe_mead_royal
rarity_common
rarity_rare
contract_daily_mead_001
lab_upgrade_speed_01
```

Zasady:

- ID nie powinny zawierać spacji.
- ID nie powinny być zmieniane po publikacji.
- Nazwy wyświetlane graczowi powinny być osobnym polem lokalizacyjnym.
- Dane zapisu gracza powinny odwoływać się do ID, nie do nazw.

---

# Główne encje

## Ingredient

Składnik zużywany w eksperymentach i produkcji.

Składnik może występować w różnych rzadkościach jako oddzielny obiekt albo jako wariant bazowego składnika.

Rekomendacja MVP: składnik i jego rzadkość jako jedna encja, z odwołaniem do `baseIngredientId`.

Przykład:

```json
{
  "id": "ingredient_honey_rare",
  "baseIngredientId": "honey",
  "displayNameKey": "ingredient.honey.rare.name",
  "descriptionKey": "ingredient.honey.rare.description",
  "rarityId": "rarity_rare",
  "tags": ["honey", "sweet", "natural"],
  "iconId": "icon_ingredient_honey_rare",
  "outcomeWeights": [
    { "recipeId": "recipe_mead_basic", "weight": 30 },
    { "recipeId": "recipe_mead_royal", "weight": 10 }
  ],
  "qualityBonus": 0.08,
  "discoveryBonus": 0.03,
  "enabled": true
}
```

Pola:

| Pole | Typ | Opis |
|---|---:|---|
| `id` | string | Stabilne ID składnika. |
| `baseIngredientId` | string | ID bazowego składnika, np. `honey`. |
| `displayNameKey` | string | Klucz lokalizacji nazwy. |
| `descriptionKey` | string | Klucz lokalizacji opisu. |
| `rarityId` | string | Rzadkość składnika. |
| `tags` | string[] | Tagi używane w kontraktach i sezonach. |
| `iconId` | string | ID assetu ikony. |
| `outcomeWeights` | array | Wagi receptur, do których składnik się przyczynia. |
| `qualityBonus` | number | Bonus do rzadkości/jakości wyniku. |
| `discoveryBonus` | number | Bonus do odkrywania nowych receptur. |
| `enabled` | bool | Czy składnik jest aktywny. |

Uwagi:

- Składnik nie musi przyczyniać się do każdej receptury.
- Składnik może być użyty w eksperymencie nawet wtedy, gdy nie daje wagi do części możliwych wyników.
- W produkcji składnik jest poprawny tylko wtedy, gdy daje dodatnią wagę do wybranej receptury albo spełnia osobne reguły produkcyjne receptury.

---

## Rarity

Rzadkości używane dla składników i produktów.

Można używać wspólnej tabeli rzadkości lub oddzielnych tabel dla składników i produktów.

Rekomendacja MVP: jedna tabela z polem `scope`.

Przykład:

```json
{
  "id": "rarity_epic",
  "displayNameKey": "rarity.epic.name",
  "scope": ["ingredient", "product"],
  "rank": 3,
  "colorHex": "#A855F7",
  "valueMultiplier": 4.0,
  "qualityScore": 40
}
```

Pola:

| Pole | Typ | Opis |
|---|---:|---|
| `id` | string | Stabilne ID rzadkości. |
| `displayNameKey` | string | Klucz lokalizacji. |
| `scope` | string[] | `ingredient`, `product` albo oba. |
| `rank` | int | Kolejność od najniższej do najwyższej. |
| `colorHex` | string | Kolor UI. |
| `valueMultiplier` | number | Mnożnik wartości sprzedaży. |
| `qualityScore` | number | Liczbowa wartość jakości do obliczeń. |

Przykładowe rzadkości:

```json
[
  { "id": "rarity_common", "displayNameKey": "rarity.common.name", "scope": ["ingredient", "product"], "rank": 1, "colorHex": "#B0B0B0", "valueMultiplier": 1.0, "qualityScore": 0 },
  { "id": "rarity_rare", "displayNameKey": "rarity.rare.name", "scope": ["ingredient", "product"], "rank": 2, "colorHex": "#3B82F6", "valueMultiplier": 2.0, "qualityScore": 15 },
  { "id": "rarity_epic", "displayNameKey": "rarity.epic.name", "scope": ["ingredient", "product"], "rank": 3, "colorHex": "#A855F7", "valueMultiplier": 4.0, "qualityScore": 40 },
  { "id": "rarity_legendary", "displayNameKey": "rarity.legendary.name", "scope": ["ingredient", "product"], "rank": 4, "colorHex": "#F59E0B", "valueMultiplier": 8.0, "qualityScore": 80 }
]
```

---

## Recipe

Receptura reprezentuje produkt, który może zostać odkryty w eksperymencie i później produkowany deterministycznie.

Przykład:

```json
{
  "id": "recipe_mead_royal",
  "displayNameKey": "recipe.mead_royal.name",
  "descriptionKey": "recipe.mead_royal.description",
  "categoryId": "category_mead",
  "tags": ["mead", "honey", "sarmatian"],
  "iconId": "icon_recipe_mead_royal",
  "baseValue": 250,
  "collectionRarityId": "rarity_epic",
  "minPlayerLevel": 1,
  "seasonId": null,
  "productionRules": {
    "requiredMatchingIngredients": 3,
    "allowDuplicates": true
  },
  "enabled": true
}
```

Pola:

| Pole | Typ | Opis |
|---|---:|---|
| `id` | string | Stabilne ID receptury. |
| `displayNameKey` | string | Klucz lokalizacji nazwy. |
| `descriptionKey` | string | Klucz lokalizacji opisu. |
| `categoryId` | string | Kategoria w książce receptur. |
| `tags` | string[] | Tagi używane przez kontrakty i bonusy. |
| `iconId` | string | ID assetu ikony. |
| `baseValue` | number | Bazowa wartość sprzedaży produktu. |
| `collectionRarityId` | string | Rzadkość kolekcjonerska receptury. |
| `minPlayerLevel` | int | Opcjonalna blokada progresji. W MVP może być ignorowana. |
| `seasonId` | string/null | ID sezonu, jeśli receptura jest sezonowa. |
| `productionRules` | object | Reguły produkcji deterministycznej. |
| `enabled` | bool | Czy receptura jest aktywna. |

Uwagi:

- `collectionRarityId` opisuje rzadkość samej receptury w albumie.
- `productRarityId` jest wynikiem konkretnego eksperymentu lub produkcji.
- Receptura może być rzadka jako wpis kolekcjonerski, ale produkt może wypaść w różnych jakościach.

---

## Recipe Outcome Weights

Wagi wyników mogą być przechowywane bezpośrednio w składnikach albo w osobnej tabeli.

Rekomendacja MVP: w składnikach, bo łatwiej edytować zachowanie każdego składnika.

Alternatywny format globalny:

```json
{
  "ingredientId": "ingredient_malt_common",
  "recipeId": "recipe_beer_basic",
  "weight": 20
}
```

Zasada eksperymentu:

1. Gracz wybiera składniki.
2. System sumuje wagi wszystkich wybranych składników dla każdej receptury.
3. Receptury o wadze `0` nie mogą wypaść.
4. Wynik jest losowany proporcjonalnie do sum wag.

Przykład:

```json
{
  "selectedIngredients": [
    "ingredient_malt_common",
    "ingredient_hops_common",
    "ingredient_honey_common"
  ],
  "calculatedWeights": [
    { "recipeId": "recipe_beer_basic", "weight": 30 },
    { "recipeId": "recipe_whisky_basic", "weight": 20 },
    { "recipeId": "recipe_mead_basic", "weight": 30 }
  ]
}
```

---

## Product Result

Produkt wynikowy nie musi być osobną konfiguracją. Może być runtime’owym wynikiem receptury i rzadkości.

Przykład obiektu runtime:

```json
{
  "recipeId": "recipe_mead_royal",
  "productRarityId": "rarity_epic",
  "saleValue": 1000,
  "createdAt": "2026-06-18T12:00:00Z",
  "source": "experiment"
}
```

Produkt może być:

- automatycznie sprzedawany,
- przechowywany krótko do kontraktów,
- zużywany przy realizacji kontraktów.

Rekomendacja MVP: produkty mogą być przechowywane w prostym magazynie, ponieważ kontrakty wymagają konkretnych typów produktów.

---

# System eksperymentów

## ExperimentConfig

Globalna konfiguracja eksperymentów.

Przykład:

```json
{
  "ingredientsPerExperiment": 3,
  "allowDuplicateIngredients": true,
  "dailyInstantExperiments": 10,
  "timedExperimentDurationSeconds": 3600,
  "previewMode": "top_results_plus_unknown",
  "maxPreviewResults": 5,
  "showExactPercentages": false
}
```

Pola:

| Pole | Typ | Opis |
|---|---:|---|
| `ingredientsPerExperiment` | int | Liczba składników zużywana w eksperymencie. |
| `allowDuplicateIngredients` | bool | Czy można użyć np. 3x słód. |
| `dailyInstantExperiments` | int | Liczba natychmiastowych eksperymentów dziennie. |
| `timedExperimentDurationSeconds` | int | Czas trwania zwykłego eksperymentu. |
| `previewMode` | string | Sposób prezentacji szans graczowi. |
| `maxPreviewResults` | int | Ile wyników pokazywać w podglądzie. |
| `showExactPercentages` | bool | Czy pokazywać dokładne procenty. |

---

## Product Rarity Roll

Rzadkość wyniku powinna być liczona osobno po wylosowaniu receptury.

Konfiguracja bazowa:

```json
{
  "baseProductRarityWeights": [
    { "rarityId": "rarity_common", "weight": 7000 },
    { "rarityId": "rarity_rare", "weight": 2200 },
    { "rarityId": "rarity_epic", "weight": 700 },
    { "rarityId": "rarity_legendary", "weight": 100 }
  ],
  "ingredientQualityInfluence": 1.0,
  "laboratoryQualityInfluence": 1.0,
  "masteryQualityInfluence": 1.0
}
```

Źródła bonusów do jakości:

- rzadkość składników,
- bonusy składników,
- poziom laboratorium,
- ulepszenia laboratorium,
- mistrzostwo receptury,
- eventy/sezony.

---

# System produkcji

Produkcja służy do deterministycznego wytwarzania znanych produktów.

## ProductionConfig

```json
{
  "ingredientsPerProduction": 3,
  "allowDuplicateIngredients": true,
  "baseProductionDurationSeconds": 1800,
  "autoSellByDefault": true,
  "allowStoreForContracts": true
}
```

Zasady:

- Gracz wybiera odkrytą recepturę.
- Gracz wybiera składniki.
- Każdy składnik musi mieć dodatnią wagę dla wybranej receptury albo spełniać regułę receptury.
- Produkt zawsze jest wybraną recepturą.
- Rzadkość produktu jest losowana na podstawie jakości składników i bonusów.
- Produkt może być sprzedany albo użyty w kontraktach.

---

# Książka receptur

Książka receptur zawiera stan kolekcji gracza.

## PlayerRecipeState

Runtime/save data, nie konfiguracja.

```json
{
  "recipeId": "recipe_mead_royal",
  "discovered": true,
  "firstDiscoveredAt": "2026-06-18T12:00:00Z",
  "highestProductRarityId": "rarity_epic",
  "timesCreated": 17,
  "masteryLevel": 3,
  "masteryXp": 45,
  "revealedIngredientIds": [
    "ingredient_honey_common",
    "ingredient_plum_common",
    "ingredient_amber_rare"
  ]
}
```

Zasady:

- Nieodkryte receptury są widoczne jako placeholdery.
- Placeholder pokazuje miejsce w kolekcji, ale nie pokazuje nazwy.
- Po odkryciu receptury nazwa i opis zostają ujawnione.
- Składniki użyte przy odkryciu zostają zapisane jako odkryte dla tej receptury.
- Najwyższa uzyskana rzadkość produktu jest aktualizowana tylko przy lepszym wyniku.

---

# Mastery receptur

Mastery wzmacnia sens powtarzania znanych receptur.

## MasteryConfig

```json
{
  "levels": [
    { "level": 1, "requiredXp": 0, "qualityBonus": 0.00, "saleValueBonus": 0.00 },
    { "level": 2, "requiredXp": 10, "qualityBonus": 0.02, "saleValueBonus": 0.02 },
    { "level": 3, "requiredXp": 30, "qualityBonus": 0.04, "saleValueBonus": 0.04 },
    { "level": 4, "requiredXp": 60, "qualityBonus": 0.06, "saleValueBonus": 0.06 },
    { "level": 5, "requiredXp": 100, "qualityBonus": 0.10, "saleValueBonus": 0.10 }
  ]
}
```

Źródła mastery XP:

- eksperyment zakończony daną recepturą,
- produkcja danej receptury,
- kontrakty wymagające danej receptury.

---

# Dostawy

## DeliveryConfig

```json
{
  "baseDelivery": {
    "id": "delivery_base",
    "intervalSeconds": 7200,
    "maxStoredDeliveries": 1,
    "rolls": 6,
    "poolId": "pool_base_ingredients"
  },
  "dailyChoice": {
    "freeChoicesPerDay": 1,
    "premiumChoiceCost": 25
  }
}
```

## DeliveryPool

```json
{
  "id": "pool_base_ingredients",
  "entries": [
    { "ingredientId": "ingredient_malt_common", "weight": 100, "minAmount": 1, "maxAmount": 3 },
    { "ingredientId": "ingredient_hops_common", "weight": 80, "minAmount": 1, "maxAmount": 2 },
    { "ingredientId": "ingredient_honey_common", "weight": 50, "minAmount": 1, "maxAmount": 2 },
    { "ingredientId": "ingredient_amber_rare", "weight": 10, "minAmount": 1, "maxAmount": 1 }
  ]
}
```

Zasady:

- Dostawy zawierają losową zawartość.
- Częstsze składniki mają większe wagi.
- Rzadkie składniki mają mniejsze wagi.
- Gracz może zamówić większą dostawę za złoto.
- Gracz nie ma gwarancji otrzymania konkretnego składnika z losowej dostawy.
- Raz dziennie może wybrać jeden konkretny składnik za darmo.
- Dodatkowy wybór może kosztować walutę premium.

---

# Laboratoria

## LaboratoryConfig

```json
{
  "baseLaboratories": 1,
  "baseSlotsPerLaboratory": 1,
  "maxLaboratories": 5,
  "upgrades": [
    {
      "id": "lab_upgrade_speed_01",
      "displayNameKey": "lab.upgrade.speed.01.name",
      "level": 1,
      "cost": { "currencyId": "currency_gold", "amount": 500 },
      "effects": [
        { "type": "experiment_time_multiplier", "value": 0.95 }
      ]
    },
    {
      "id": "lab_upgrade_quality_01",
      "displayNameKey": "lab.upgrade.quality.01.name",
      "level": 2,
      "cost": { "currencyId": "currency_gold", "amount": 1000 },
      "effects": [
        { "type": "product_quality_bonus", "value": 0.03 }
      ]
    },
    {
      "id": "lab_upgrade_slot_01",
      "displayNameKey": "lab.upgrade.slot.01.name",
      "level": 3,
      "cost": { "currencyId": "currency_gold", "amount": 2500 },
      "effects": [
        { "type": "additional_slot", "value": 1 }
      ]
    }
  ],
  "newLaboratoryCosts": [
    { "laboratoryIndex": 2, "cost": { "currencyId": "currency_gold", "amount": 5000 } },
    { "laboratoryIndex": 3, "cost": { "currencyId": "currency_gold", "amount": 20000 } }
  ]
}
```

Efekty laboratoriów:

- `experiment_time_multiplier`,
- `production_time_multiplier`,
- `product_quality_bonus`,
- `discovery_bonus`,
- `additional_slot`,
- `sale_value_bonus`.

---

# Kontrakty

Kontrakty nadają sens produktom i prowadzą gracza do konkretnych aktywności.

## Contract

```json
{
  "id": "contract_daily_mead_001",
  "type": "daily",
  "displayNameKey": "contract.daily.mead.001.name",
  "descriptionKey": "contract.daily.mead.001.description",
  "requirements": [
    {
      "type": "deliver_product_with_tag",
      "tag": "mead",
      "minProductRarityId": "rarity_common",
      "amount": 3
    }
  ],
  "rewards": [
    { "type": "currency", "currencyId": "currency_gold", "amount": 500 },
    { "type": "currency", "currencyId": "currency_premium", "amount": 5 }
  ],
  "weight": 100,
  "enabled": true
}
```

Typy wymagań:

- `deliver_recipe`,
- `deliver_product_with_tag`,
- `deliver_product_with_min_rarity`,
- `produce_count`,
- `discover_recipe_count`,
- `use_ingredient_tag`,
- `complete_experiment_count`.

Typy nagród:

- `currency`,
- `ingredient`,
- `delivery`,
- `premium_currency`,
- `season_points`.

---

# Waluty

## Currency

```json
[
  {
    "id": "currency_gold",
    "displayNameKey": "currency.gold.name",
    "type": "soft",
    "iconId": "icon_currency_gold"
  },
  {
    "id": "currency_premium",
    "displayNameKey": "currency.premium.name",
    "type": "hard",
    "iconId": "icon_currency_premium"
  }
]
```

---

# Monetyzacja

Na poziomie danych warto zdefiniować tylko dostępne akcje, ceny i limity.

## MonetizationConfig

```json
{
  "rewardedAds": [
    {
      "id": "ad_extra_delivery",
      "placementId": "rewarded_extra_delivery",
      "dailyLimit": 5,
      "reward": { "type": "delivery", "deliveryId": "delivery_ad_bonus" }
    },
    {
      "id": "ad_speed_up_experiment",
      "placementId": "rewarded_speed_up_experiment",
      "dailyLimit": 10,
      "reward": { "type": "time_reduction", "seconds": 1800 }
    },
    {
      "id": "ad_double_sale",
      "placementId": "rewarded_double_sale",
      "dailyLimit": 10,
      "reward": { "type": "sale_multiplier", "value": 2.0 }
    }
  ],
  "premiumActions": [
    {
      "id": "premium_choose_extra_ingredient",
      "cost": { "currencyId": "currency_premium", "amount": 25 },
      "effect": { "type": "ingredient_choice", "amount": 1 }
    },
    {
      "id": "premium_instant_delivery",
      "cost": { "currencyId": "currency_premium", "amount": 50 },
      "effect": { "type": "delivery", "deliveryId": "delivery_premium" }
    }
  ]
}
```

Zasady MVP:

- Nie sprzedawać bezpośrednio receptur.
- Nie gwarantować konkretnych rzadkich wyników za pieniądze.
- Monetyzacja powinna zwiększać liczbę prób, skracać czas albo dawać wygodę.

---

# Sezony

## Season

```json
{
  "id": "season_001_ghost_forest",
  "displayNameKey": "season.001.name",
  "startAt": "2026-07-01T00:00:00Z",
  "endAt": "2026-07-31T23:59:59Z",
  "ingredientIds": [
    "ingredient_ghost_mushroom_common",
    "ingredient_moon_moss_rare",
    "ingredient_ancestor_ash_epic"
  ],
  "recipeIds": [
    "recipe_ghost_mead",
    "recipe_moonshine_of_whispers"
  ],
  "contractIds": [
    "contract_season_ghost_001"
  ],
  "deliveryPoolIds": [
    "pool_season_ghost_forest"
  ],
  "enabled": true
}
```

Sezon może aktywować:

- składniki,
- receptury,
- kontrakty,
- specjalne dostawy,
- specjalne nagrody,
- przepustkę sezonową.

---

# Motyw / reskin

## ThemeConfig

ThemeConfig pozwala przenieść mechanikę do innej gry.

```json
{
  "id": "theme_winged_barrel_distillery",
  "displayName": "Winged Barrel Distillery",
  "terminology": {
    "ingredient": "Ingredient",
    "recipe": "Recipe",
    "product": "Product",
    "laboratory": "Laboratory",
    "experiment": "Experiment",
    "production": "Production",
    "contract": "Contract"
  },
  "assetPrefix": "wb_distillery",
  "localizationTable": "localization_wb_distillery"
}
```

Przykład reskinu dla dzieci:

```json
{
  "id": "theme_creature_lab",
  "displayName": "Creature Lab",
  "terminology": {
    "ingredient": "Material",
    "recipe": "Creature",
    "product": "Creature Variant",
    "laboratory": "Workshop",
    "experiment": "Mixing",
    "production": "Hatching",
    "contract": "Quest"
  },
  "assetPrefix": "creature_lab",
  "localizationTable": "localization_creature_lab"
}
```

---

# Save Data

Minimalny zapis gracza.

```json
{
  "version": 1,
  "currencies": [
    { "currencyId": "currency_gold", "amount": 12500 },
    { "currencyId": "currency_premium", "amount": 120 }
  ],
  "inventory": [
    { "ingredientId": "ingredient_malt_common", "amount": 12 },
    { "ingredientId": "ingredient_honey_rare", "amount": 2 }
  ],
  "recipes": [
    {
      "recipeId": "recipe_mead_royal",
      "discovered": true,
      "highestProductRarityId": "rarity_epic",
      "timesCreated": 17,
      "masteryLevel": 3,
      "masteryXp": 45,
      "revealedIngredientIds": ["ingredient_honey_common", "ingredient_plum_common"]
    }
  ],
  "laboratories": [
    {
      "id": "player_lab_001",
      "level": 3,
      "slotCount": 2,
      "activeJobs": []
    }
  ],
  "deliveryState": {
    "lastBaseDeliveryAt": "2026-06-18T10:00:00Z",
    "storedBaseDeliveries": 0,
    "dailyIngredientChoiceUsedAt": "2026-06-18T08:00:00Z"
  },
  "instantExperimentState": {
    "date": "2026-06-18",
    "used": 4
  },
  "contracts": {
    "activeContractIds": ["contract_daily_mead_001"],
    "completedContractIds": []
  }
}
```

---

# Walidacja danych

Przed uruchomieniem gry konfiguracja powinna być walidowana.

Walidacje obowiązkowe:

- Każde ID jest unikalne.
- Każde odwołanie wskazuje istniejącą encję.
- Każda aktywna receptura ma przynajmniej jeden składnik, który daje do niej wagę.
- Każdy aktywny składnik ma poprawną rzadkość.
- Każdy pool dostaw ma przynajmniej jeden wpis.
- Wagi w dostawach są dodatnie.
- Wagi outcome’ów są dodatnie.
- Każdy kontrakt ma poprawne wymagania i nagrody.
- Każdy koszt używa istniejącej waluty.
- Każdy poziom mastery ma rosnące wymagania XP.
- Każdy sezon wskazuje istniejące składniki, receptury i kontrakty.

Walidacje ostrzegawcze:

- Receptura jest bardzo trudna do odkrycia.
- Składnik nie prowadzi do żadnej receptury.
- Kontrakt wymaga receptury niedostępnej w danym sezonie.
- Dostawa może generować składniki nieużywane przez żadną aktywną recepturę.
- Koszt ulepszenia jest zerowy.

---

# Minimalny zestaw danych MVP

Na pierwszy prototyp wystarczy:

- 4 rzadkości,
- 8-10 składników,
- 25-35 receptur,
- 1 bazowy pool dostaw,
- 1 płatny pool dostaw,
- 1 premium pool dostaw,
- 10-15 kontraktów,
- 10 poziomów mastery,
- 10-20 ulepszeń laboratorium,
- 1 sezon testowy opcjonalnie.

Na test komercyjny lepiej celować w:

- 10-15 składników,
- 40-60 receptur,
- 20-30 kontraktów,
- 20-30 poziomów laboratorium,
- 1 prostą przepustkę sezonową.

---

# Priorytet implementacji modelu

Kolejność wdrożenia:

1. `Rarity`
2. `Currency`
3. `Ingredient`
4. `Recipe`
5. Outcome calculation
6. Product rarity roll
7. Player inventory
8. Player recipe state
9. Deliveries
10. Laboratory slots
11. Experiment jobs
12. Production jobs
13. Product storage/sale
14. Contracts
15. Mastery
16. Monetization hooks
17. Seasons
18. ThemeConfig

---

# Najważniejsze decyzje projektowe

Ustalone:

- Składniki są ograniczone i zużywane.
- Dostawy przychodzą cyklicznie i mają losową zawartość.
- Gracz może zamawiać większe dostawy za złoto.
- Gracz raz dziennie wybiera jeden konkretny składnik za darmo.
- Dodatkowe wybory mogą kosztować walutę premium.
- Eksperymenty służą do odkrywania receptur.
- Produkcja służy do deterministycznego wytwarzania odkrytych receptur.
- Receptura po odkryciu pokazuje składniki użyte do jej odkrycia.
- Użycie tych samych składników nie gwarantuje tego samego wyniku w eksperymencie.
- Produkcja gwarantuje wybraną recepturę, jeśli użyto poprawnych składników.
- Książka receptur pokazuje placeholdery nieodkrytych receptur bez nazw.
- Książka zapisuje najwyższą rzadkość uzyskaną dla każdej receptury.
- Laboratoria są głównym systemem progresji.
- Receptury mogą mieć mastery zwiększające szanse na lepszą rzadkość.
- Content musi być w pełni konfigurowalny.

Otwarte, ale nieblokujące:

- konkretne czasy,
- konkretne ceny,
- konkretne szanse,
- liczba receptur na start,
- docelowy styl UI,
- finalne nazewnictwo walut,
- szczegóły sezonów i przepustki.
