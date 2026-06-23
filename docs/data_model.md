# Distillery Discovery — model danych Content Expansion v2

## Zasady

Runtime implementuje mechaniki, a JSON-y w `Assets/Resources/GameData/` definiują content. Wszystkie odwołania używają stabilnych tekstowych ID. Nazwy gracza mają kompletne wpisy PL/EN w `localization.json`.

Aktualny content zawiera 22 składniki, 5 grup, 175 receptur, 7 kategorii oraz 36 szablonów kontraktów.

## IngredientGroup i Ingredient

```json
{ "id": "grain", "displayName": "Grains" }
```

```json
{
  "id": "ingredient_barley_common",
  "baseIngredientId": "barley",
  "displayName": "Barley",
  "groupId": "grain",
  "rarityId": "rarity_common",
  "sourceRule": "delivery",
  "qualityBonus": 0.01,
  "enabled": true
}
```

Składnik należy do najwyżej jednej grupy. `sourceRule` dokumentuje dostępność; walidator wymaga wpisu w dostawach, chyba że ustawiono jawne `contracts_only`. Składniki nie przechowują wag wyników.

## Recipe

```json
{
  "id": "recipe_001_table_beer",
  "categoryId": "beer",
  "tags": ["beer", "grain", "hoppy"],
  "baseValue": 40,
  "collectionRarityId": "rarity_common",
  "baseWeight": 100,
  "requirements": [
    { "type": "group", "groupId": "grain", "count": 1 }
  ],
  "weightBonuses": [
    { "ingredientId": "ingredient_barley_common", "weight": 20 },
    { "ingredientId": "ingredient_hops_rare", "weight": 15 }
  ],
  "enabled": true
}
```

Obsługiwane obowiązkowe klauzule:

- `ingredient`: konkretny składnik i liczba sztuk,
- `group`: liczba sztuk należących do grupy,
- `distinct_group`: liczba różnych składników z grupy,
- `any_of`: łączna liczba sztuk z listy konkretnych składników.

Klauzule są łączone przez AND. Waga kwalifikującej się receptury wynosi `baseWeight + suma pasujących weightBonuses`. Bonus składnika lub grupy jest naliczany raz niezależnie od duplikatów. Neutralny składnik nie unieważnia receptury.

`RecipeOutcomeResolver` jest czystą wspólną ścieżką dla podglądu, końcowego losowania eksperymentu i walidacji produkcji. Produkcja dodatkowo wymaga odkrytej receptury. Dokładnie trzy składniki są zużywane dopiero po poprawnej walidacji.

## Rzadkość produktu i ekonomia

Losowanie jakości następuje po wybraniu receptury. Bazowe wagi z `economy.json` są modyfikowane przez średnią jakość składników, poziom wybranego laboratorium i mastery. Cena to wartość receptury pomnożona przez mnożnik rzadkości i bonus jakości składników.

`DeliveryPool.rolls` jest jedynym autorytatywnym polem liczby losowań dostawy. Darmowa dostawa trwa 20 minut, po odebraniu gotowej dostawy licznik następnej rusza od chwili odbioru. Każdy wpis ma dodatnią wagę i zakres ilości. Zwykła pula zawiera cały kanoniczny zestaw, ale epic ma małą, a legendary bardzo małą wagę; ich główne źródło stanowią odpowiednio kontrakty specialist/prestige.

## ContractTemplate i ActiveContractState

Szablon definiuje rolę (`basic`, `specialist`, `prestige`), tier, wagę wyboru, typ celu, selektor/fiksowany cel, zakres ilości, ograniczenia jakości/źródła/laboratorium i nagrody.

Obsługiwane cele:

- `produce_recipe`, `produce_category`, `produce_tag`, `produce_rarity`,
- `use_ingredient`, `use_group`,
- `discover_recipes`, `distinct_recipes`,
- `recipe_min_rarity`, `improve_record`, `produce_source`.

Wygenerowany `ActiveContractState` zapisuje `instanceId`, `templateId`, rolę, rozwiązany cel, wymaganą ilość, postęp, złoto, źródło/minimalną jakość, czas utworzenia i `seenRecipeIds` dla celów distinct. Ponowne wczytanie nie losuje celu od nowa.

Nagrody składnikowe są ważoną pulą selektorów:

- dokładny `ingredient`,
- losowy składnik z `group`,
- losowy składnik o `rarity`.

Nagroda i złoto trafiają do gracza wyłącznie przez claim nieodebranego wyniku. Dostępny jest zapisywany darmowy reroll oraz odświeżenie czasowe.

## ProductionEvent

Każdy odebrany produkt tworzy niemutowalne zdarzenie zawierające:

- recepturę i jakość produktu,
- kategorię i tagi receptury,
- wybrane ID składników i wynikające grupy,
- źródło `experiment` albo `production`,
- informację o nowym odkryciu i poprawie rekordu.

Ta sama metoda aktualizuje kontrakty przy pojedynczym odbiorze i `Collect All`.

## PlayerState v8

Zapis v9 przechowuje złoto, inventory, produkty, postęp receptur/mastery, listę laboratoriów z poziomami, timed jobs z wybranym laboratorium, dostawy, język, pending result i pełne wygenerowane kontrakty. Wynik i jego losowanie pozostają odroczone do claimu.

Migracja v7:

- zachowuje złoto, istniejące kanoniczne składniki, receptury, produkty, laboratorium, język i czasy,
- mapuje dawny słód na jęczmień i dawny bursztyn na kanoniczny legendary,
- usuwa nieistniejący content,
- regeneruje stare kontrakty bez wypłacania ich nagród,
- zachowuje pending product/sale gold, ale usuwa niepoprawny progres starego kontraktu, aby nie dublować wypłaty.

## Walidacja

`ConfigValidator` sprawdza unikalność i referencje, kompletność PL/EN, grupy i rzadkości składników, klauzule i bonusy, możliwość ułożenia każdej receptury w trzech slotach, limity bezpośrednich skojarzeń epic/legendary, wartości i wagi, dostawy, progresję laboratoriów/mastery oraz zgodność szablonów, celów i nagród kontraktów.
