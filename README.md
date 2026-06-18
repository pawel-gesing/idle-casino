# Distillery Discovery — Unity MVP

Tekstowo-tabelkowy prototyp mobilnej gry kolekcjonersko-ekonomicznej w Unity 6 LTS.

Aktualna pętla gry:

`dostawa → eksperyment → odkrycie receptury i produkt → sprzedaż lub kontrakt → ulepszenie laboratorium → produkcja znanych receptur`

## Wymagania i uruchomienie

- Unity `6000.4.10f1` (Unity 6 LTS),
- Android Build Support tylko do budowania APK.

1. Dodaj główny folder repozytorium jako projekt w Unity Hub.
2. Otwórz `Assets/Scenes/Main.unity` i naciśnij **Play**.
3. Prototyp automatycznie buduje prosty interfejs uGUI w runtime.

Interfejs jest przeznaczony dla urządzeń mobilnych w orientacji pionowej. Skaluje się względem szerokości ekranu i respektuje `Screen.safeArea`, więc przyciski oraz treść nie wchodzą pod notch, wycięcia ani systemowe obszary ekranu.

Zapis trafia do `Application.persistentDataPath/distillery_save.json`. Przycisk **RESET** usuwa zapis i tworzy nowy stan.

## Systemy rozgrywki

### Eksperyment

Gracz wybiera dokładnie 3 składniki. Ich konfigurowalne wagi wyznaczają możliwe receptury, a wynik jest losowany. Receptura trafia do książki lub aktualizuje najlepszą osiągniętą rzadkość. Powstały produkt jest odkładany w magazynie — eksperyment nie sprzedaje go automatycznie.

### Produkcja

Produkcja działa wyłącznie dla odkrytych receptur. Gracz wybiera recepturę i dokładnie 3 składniki; każdy musi mieć dodatni wpływ na wybraną recepturę. Duplikaty są dozwolone. Receptura produktu jest gwarantowana, losowana jest tylko rzadkość zależna od jakości składników i poziomu laboratorium.

### Produkty i sprzedaż

Produkty są grupowane według receptury, rzadkości i wartości sprzedaży. Ekran sprzedaży pozwala sprzedać jedną sztukę wybranej pozycji albo cały magazyn. Złoto jest przyznawane dopiero podczas sprzedaży.

### Kontrakty

Aktywne kontrakty pochodzą z danych i obsługują trzy wymagania:

- konkretną recepturę,
- konkretną rzadkość produktu,
- kategorię receptury.

Realizacja kontraktu atomowo zużywa wymaganą liczbę pasujących produktów i przyznaje złoto. Niespełniony kontrakt nie zmienia magazynu.

### Laboratorium

Gracz ma jedno laboratorium z poziomami zdefiniowanymi w konfiguracji. Ulepszenie pobiera złoto i zwiększa wagę wyższych rzadkości produktu. Prototyp zawiera 5 poziomów z bonusami od 0% do 20%.

## Dane gry

Konfiguracja znajduje się w `Assets/Resources/GameData/`:

- `rarities.json` — rzadkości, mnożniki ceny i jakość,
- `ingredients.json` — składniki oraz wpływy na receptury,
- `recipes.json` — receptury i wartości bazowe,
- `categories.json` — kategorie receptur używane m.in. przez kontrakty,
- `economy.json` — parametry eksperymentu, produkcji, losowania i dostaw,
- `laboratories.json` — poziomy, koszty i bonusy laboratorium,
- `contracts.json` — wymagania i złote nagrody kontraktów.

Konfiguracja jest walidowana przy starcie: sprawdzane są unikalne ID, referencje, dodatnie wagi i wartości, ciągłość poziomów laboratorium oraz poprawność kontraktów.

## Zapis stanu

Wersja zapisu `2` przechowuje:

- złoto i magazyn składników,
- książkę receptur,
- pogrupowane produkty,
- poziom laboratorium,
- aktywne kontrakty,
- liczniki eksperymentów i produkcji.

Starszy zapis prototypu jest normalizowany przy wczytaniu i otrzymuje początkowe kontrakty oraz laboratorium poziomu 1.

## Testy

W Unity otwórz `Window → General → Test Runner`, wybierz **EditMode** i uruchom **Run All**.

Z wiersza poleceń:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode `
  -testResults TestResults.xml -logFile TestRun.log -quit
```

Testy obejmują eksperymenty, produkcję, magazyn produktów, sprzedaż, wszystkie reguły kontraktów, koszt i wpływ laboratorium, dostawy, konfigurację oraz zapis ekonomii.

## Architektura

- `Runtime/Model` — serializowalne definicje konfiguracji i zapisu,
- `Runtime/Config` — ładowanie oraz walidacja JSON,
- `Runtime/Services` — testowalna logika ekonomii i zapis lokalny,
- `Runtime/UI` — prosty interfejs uGUI generowany programowo,
- `Tests/EditMode` — testy reguł rozgrywki.

## Celowo poza zakresem

Timery czasu rzeczywistego, reklamy, IAP, sezony, wiele laboratoriów, Firebase, Addressables, backend, konta, docelowa grafika, animacje i mastery receptur.

Dokumenty projektowe znajdują się w `docs/`.
