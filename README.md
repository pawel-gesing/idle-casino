# Distillery Discovery — Unity MVP

Mobilny prototyp gry kolekcjonersko-ekonomicznej w Unity 6 LTS.

`dostawa → eksperyment → odkrycie i automatyczna sprzedaż → kontrakty → ulepszenie laboratorium → produkcja znanych receptur`

## Uruchomienie

- Unity `6000.4.10f1` (Unity 6 LTS),
- Android Build Support jest potrzebny tylko do budowania APK.

1. Dodaj repozytorium jako projekt w Unity Hub.
2. Otwórz `Assets/Scenes/Main.unity`.
3. Naciśnij **Play**.

Interfejs działa w orientacji pionowej, skaluje się do szerokości urządzenia i respektuje `Screen.safeArea`. W poziomym Game View wyświetla pionową ramkę 9:16.

## Rozgrywka

### Eksperyment

Gracz wybiera dokładnie 3 składniki. Ich wagi definiują możliwe receptury. Receptura i rzadkość są losowane, książka receptur zostaje zaktualizowana, a produkt automatycznie sprzedany. Wytworzenie produktu aktualizuje wszystkie pasujące kontrakty.

### Produkcja

Produkcja wymaga odkrytej receptury i dokładnie 3 składników mających na nią dodatni wpływ. Receptura wyniku jest gwarantowana, a rzadkość zależy od składników i laboratorium. Produkt jest automatycznie sprzedawany i zaliczany do kontraktów.

### Kontrakty

Jednocześnie aktywne są 3 różne kontrakty losowane z puli. Mogą wymagać:

- konkretnej receptury,
- konkretnej rzadkości,
- produktu z określonej kategorii.

Eksperyment i produkcja zwiększają postęp. Ukończony kontrakt pozostaje aktywny do naciśnięcia **Odbierz nagrodę**. Po odbiorze złota natychmiast losowany jest następny kontrakt, bez duplikatów w aktywnej trójce.

### Laboratorium

Jedno laboratorium ma 5 konfigurowalnych poziomów z bonusami rzadkości 0–20%. Koszty wejścia na poziomy 2–5 wynoszą odpowiednio 1500, 4000, 9000 i 18000 złota.

## Interfejs i ustawienia

Główna nawigacja zawiera tylko funkcje rozgrywki:

- rząd 1: Stan, Składniki, Receptury,
- rząd 2: Eksperyment, Produkcja, Kontrakty,
- rząd 3: Dostawa, Laboratorium.

Koło zębate na górnej wstążce otwiera modal ustawień z zapisem, wczytywaniem, resetem, wyjściem i zmianą języka. Dostępne są polski oraz angielski. Przy pierwszym uruchomieniu wybierany jest język systemowy, a pozostałe języki używają angielskiego jako fallbacku. Wybór jest zapisywany.

## Dane gry

Konfiguracja znajduje się w `Assets/Resources/GameData/`:

- `rarities.json` — rzadkości i mnożniki ceny,
- `ingredients.json` — składniki i wpływy na receptury,
- `recipes.json` — receptury i wartości bazowe,
- `categories.json` — kategorie receptur,
- `economy.json` — parametry eksperymentu, produkcji i dostaw,
- `laboratories.json` — poziomy, koszty i bonusy,
- `contracts.json` — wymagania i nagrody kontraktów,
- `localization.json` — polskie oraz angielskie teksty UI i contentu.

Konfiguracja jest walidowana przy starcie, łącznie z referencjami kontraktów i brakującymi tłumaczeniami contentu.

## Zapis stanu

Zapis wersji `3` przechowuje złoto, składniki, książkę receptur, poziom laboratorium, liczniki, język oraz aktywne kontrakty z postępem.

Zapis wersji 2 jest migrowany automatycznie. Produkty pozostałe w starym magazynie zostają spieniężone, a stare kontrakty otrzymują początkowy postęp 0.

Plik zapisu: `Application.persistentDataPath/distillery_save.json`.

## Testy

W Unity: `Window → General → Test Runner → EditMode → Run All`.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode `
  -testResults TestResults.xml -logFile TestRun.log
```

Testy obejmują eksperymenty, produkcję, automatyczną sprzedaż, trzy typy kontraktów, obowiązkowy odbiór nagrody, rotację kontraktów, laboratorium, migrację i zapis języka.

## Celowo poza zakresem

Timery czasu rzeczywistego, reklamy, IAP, sezony, wiele laboratoriów, Firebase, Addressables, backend, konta, mastery, docelowa grafika i animacje.

Dokumenty projektowe znajdują się w `docs/`.
