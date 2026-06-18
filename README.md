# Distillery Discovery — Unity MVP

Tekstowo-tabelkowy prototyp mobilnej gry kolekcjonerskiej. Implementuje pełną pętlę MVP:

`dostawa → wybór 3 składników → podgląd szans → eksperyment → odkrycie receptury → automatyczna sprzedaż → progres kolekcji`

## Wymagania i uruchomienie

- Unity `6000.4.10f1` (Unity 6 LTS),
- moduł Android Build Support jest potrzebny wyłącznie do budowania APK.

1. Dodaj główny folder repozytorium jako projekt w Unity Hub.
2. Otwórz projekt w Unity 6 LTS.
3. Otwórz `Assets/Scenes/Main.unity` i naciśnij **Play**. Prototyp uruchamia się automatycznie i buduje szczątkowy interfejs uGUI w runtime.
4. Dla Androida wybierz `File → Build Profiles → Android`, przełącz platformę i wykonaj build. Scena startowa jest już dodana do ustawień buildu, orientacja to portrait, a minimalna wersja Androida to API 26.

Zapis gry trafia do `Application.persistentDataPath/distillery_save.json`. Przycisk **RESET** usuwa plik i tworzy świeży stan.

## Testy

W Unity otwórz `Window → General → Test Runner`, wybierz **EditMode** i uruchom **Run All**.

Z wiersza poleceń (dostosuj ścieżkę Unity, jeśli potrzebne):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode `
  -testResults TestResults.xml -logFile TestRun.log -quit
```

## Dane gry

Content znajduje się w `Assets/Resources/GameData/`:

- `rarities.json` — rzadkości składników i produktów, mnożniki ceny i jakość,
- `ingredients.json` — składniki oraz ich wpływy na receptury,
- `recipes.json` — receptury, kategorie i wartości bazowe,
- `economy.json` — startowa ekonomia, losowanie jakości oraz pula dostaw.

Konfiguracja jest walidowana przy starcie. Błędne ID, odwołania, wagi lub puste pule zatrzymują uruchomienie czytelnym wyjątkiem.

### Dodanie składnika

1. Dodaj obiekt do tablicy `ingredients` w `ingredients.json` z niezmiennym, unikalnym `id`.
2. Wskaż istniejące `rarityId` i co najmniej jedną dodatnią wagę `outcomeWeights`.
3. Dodaj składnik do `entries` wybranej puli w `economy.json`, jeśli ma wypadać z dostaw.

### Dodanie receptury

1. Dodaj obiekt do tablicy `recipes` w `recipes.json`.
2. Użyj istniejącej rzadkości kolekcjonerskiej i dodatniej wartości bazowej.
3. Dodaj jej dodatnią wagę do co najmniej jednego składnika w `ingredients.json`.

## Architektura

- `Runtime/Model` — serializowalne definicje konfiguracji i zapisu,
- `Runtime/Config` — ładowanie oraz walidacja JSON,
- `Runtime/Services` — testowalna logika gry i zapis lokalny,
- `Runtime/UI` — prosty, generowany programowo interfejs uGUI,
- `Tests/EditMode` — testy najważniejszych reguł.

## Celowo poza zakresem

Docelowa grafika, animacje, prawdziwe timery, produkcja znanych receptur, laboratoria i ulepszenia, kontrakty, mastery, sezony, reklamy, IAP, backend, konta, Firebase/Remote Config, Addressables oraz iOS. Te systemy nie są potrzebne do walidacji pierwszej pętli MVP.

Dokumenty projektowe znajdują się w `docs/`.
