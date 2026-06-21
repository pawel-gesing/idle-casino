# Distillery Discovery — Unity MVP

Mobilny prototyp gry kolekcjonersko-ekonomicznej w Unity 6 LTS.

`dostawa → wybór składników → eksperyment/produkcja → podsumowanie → odbiór nagród → kontrakty i rozwój laboratorium`

## Uruchomienie

- Unity `6000.4.10f1` (Unity 6 LTS),
- Android Build Support jest potrzebny tylko do budowania APK.

1. Dodaj repozytorium jako projekt w Unity Hub.
2. Otwórz `Assets/Scenes/Main.unity`.
3. Naciśnij **Play**.

Interfejs działa w orientacji pionowej, skaluje się do szerokości urządzenia i respektuje `Screen.safeArea`. W poziomym Game View wyświetla pionową ramkę 9:16.

## Rozgrywka

### Eksperyment

Gracz wybiera dokładnie 3 składniki z kafelków pokazujących dostępne ilości. Kliknięcie rezerwuje sztukę, a **Wyczyść** zwraca całą rezerwację. Składniki są faktycznie zużywane dopiero przy uruchomieniu. Receptura i rzadkość są losowane, książka receptur zostaje zaktualizowana, a produkt trafia do podsumowania z nagrodą w złocie.

### Produkcja i mistrzostwo

Produkcja zaczyna się od kafelkowej listy odkrytych receptur. Po wyborze wyświetlane są wyłącznie składniki mające dodatni wpływ na tę recepturę. Gracz rezerwuje dowolną poprawną konfigurację 3 składników. Receptura wyniku jest gwarantowana, a rzadkość zależy od składników, laboratorium i mistrzostwa receptury.

Każdy produkt uzyskany z eksperymentu lub produkcji zwiększa licznik danej receptury. Progi mistrzostwa są konfigurowalne: Czeladnik (1), Rzemieślnik (10), Mistrz (30) i Arcymistrz (100). Poziomy dają odpowiednio 0%, 5%, 10% i 20% bonusu do wag wyższych rzadkości; nie zwiększają ceny sprzedaży i nie gwarantują najwyższej jakości. Książka receptur pokazuje licznik, poziom i brakującą liczbę wytworzeń albo informację o poziomie maksymalnym.

### Odbiór wyniku

Eksperyment i produkcja tworzą zapisywany, blokujący ekran podsumowania. Pokazuje on produkt, rzadkość, nagrodę sprzedaży oraz wszystkie zmiany postępu kontraktów. Złoto ze sprzedaży i bonusy ukończonych kontraktów trafiają na konto dopiero po naciśnięciu **Odbierz**. Do tego czasu nie można rozpocząć kolejnej akcji rozgrywki.

### Kontrakty

Jednocześnie aktywne są 3 różne kontrakty losowane z puli. Mogą wymagać:

- konkretnej receptury,
- konkretnej rzadkości,
- produktu z określonej kategorii.

Eksperyment i produkcja zwiększają postęp. Jeżeli wynik ukończy jeden lub kilka kontraktów, wszystkie są pokazane w podsumowaniu i wypłacane wspólnym przyciskiem **Odbierz**. Nagroda obejmuje złoto oraz skonfigurowane składniki w losowej ilości min–max; ekran odbioru pokazuje faktycznie otrzymane ilości. Kontrakty są głównym źródłem rzadkich składników — ich częstotliwość w zwykłych dostawach nie została zwiększona. Następnie ukończone kontrakty są automatycznie zastępowane, bez duplikatów w aktywnej trójce.

### Laboratorium

Jedno laboratorium ma 5 konfigurowalnych poziomów z bonusami rzadkości 0–20%. Koszty wejścia na poziomy 2–5 wynoszą odpowiednio 1500, 4000, 9000 i 18000 złota.

## Interfejs i ustawienia

Górna wstążka stale pokazuje złoto, liczbę odkrytych receptur i liczbę składników. Kafelki są skrótami odpowiednio do Stanu, Książki receptur i Magazynu składników.

Główna nawigacja zawiera tylko funkcje rozgrywki:

- rząd 1: Eksperyment, Produkcja, Kontrakty,
- rząd 2: Dostawa, Laboratorium.

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
- `mastery.json` — progi mistrzostwa receptur i bonusy rzadkości,
- `localization.json` — polskie oraz angielskie teksty UI i contentu.

Konfiguracja jest walidowana przy starcie, łącznie z referencjami kontraktów i brakującymi tłumaczeniami contentu.

## Zapis stanu

Zapis wersji `5` przechowuje złoto, składniki (w tym nagrody kontraktowe), książkę receptur z licznikami wytworzeń, poziom laboratorium, język, aktywne kontrakty z postępem oraz nieodebrany wynik. Poziom mistrzostwa jest wyliczany z zapisanego licznika i konfiguracji.

Zapis wersji 2 jest migrowany automatycznie. Produkty pozostałe w starym magazynie zostają spieniężone, a stare kontrakty otrzymują początkowy postęp 0.

Plik zapisu: `Application.persistentDataPath/distillery_save.json`.

## Testy

W Unity: `Window → General → Test Runner → EditMode → Run All`.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode `
  -testResults TestResults.xml -logFile TestRun.log
```

Testy obejmują eksperymenty, produkcję, złoto i składnikowe nagrody kontraktów, mistrzostwo oraz jego wpływ na rzadkość, oczekujące nagrody, rotację kontraktów, laboratorium, migrację i zapis stanu.

## Celowo poza zakresem

Timery i postęp idle/offline, reklamy, IAP, sezony, wiele laboratoriów, Firebase, Addressables, backend, konta, docelowa grafika i animacje. Mistrzostwo działa przy produkcji oraz przy eksperymencie zakończonym daną recepturą; nie ma osobnego XP ani bonusu ceny.

Dokumenty projektowe znajdują się w `docs/`.
