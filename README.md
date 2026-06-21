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

### Eksperyment, produkcja i czas

Gracz wybiera dokładnie 3 składniki. Uruchomienie eksperymentu lub produkcji natychmiast zużywa składniki i tworzy zapisywane zadanie w slocie laboratorium. Zadanie trwa przez czas z `economy.json`; receptura eksperymentu i rzadkość produktu nie są losowane przed końcem.

Po upływie czasu zadanie zmienia stan z `running` na `completed`, także podczas nieobecności. Gracz odbiera je ręcznie. Dopiero wtedy losowany jest wynik, aktualizowane są książka receptur, mistrzostwo i kontrakty oraz otwierane jest podsumowanie nagrody. Produkcja zawsze zwraca wybraną, wcześniej odkrytą recepturę.

### Dostawy czasowe

Darmowa dostawa odnawia się cyklicznie. Ekran dostawy pokazuje liczbę gotowych odbiorów i czas do następnego. Odbiór zużywa jeden zapisany odbiór. Podczas nieobecności dostawy kumulują się do `maxStoredFreeDeliveries`; nadmiar przepada.

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

Ekran laboratorium pokazuje wolne, aktywne i zakończone sloty oraz pozostały czas. Liczba początkowych slotów jest konfigurowalna. Ulepszenia nadal zwiększają jakość produktu; opcjonalny `laboratoryLevelTimeReduction` skraca czas za każdy poziom powyżej pierwszego.

### Offline progress i zegar debugowy

Wszystkie timestampy są zapisywane i porównywane w UTC. Po uruchomieniu lub wczytaniu gra nalicza dostawy, oznacza zakończone zadania i pokazuje krótkie podsumowanie zdarzeń offline. Wyniki zadań nigdy nie są automatycznie odbierane.

W ustawieniach są wyraźnie oznaczone przyciski prototypowe **DEBUG: +15 MIN** i **DEBUG: +1 H**. Przesuwają zegar dostarczany przez `AdjustableTimeProvider`, więc nie zmieniają zegara systemowego. Można je usunąć wraz z dwoma przyciskami w `PrototypeUI`; logika gry zależy wyłącznie od `ITimeProvider`.

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
- `economy.json` — wszystkie czasy, rozmiar i limit dostaw, liczba slotów oraz pozostałe parametry ekonomii,
- `laboratories.json` — poziomy, koszty i bonusy,
- `contracts.json` — wymagania i nagrody kontraktów,
- `mastery.json` — progi mistrzostwa receptur i bonusy rzadkości,
- `localization.json` — polskie oraz angielskie teksty UI i contentu.

Konfiguracja jest walidowana przy starcie, łącznie z dodatnimi czasami i limitami. Główne pola czasu to `freeDeliveryIntervalSeconds`, `experimentDurationSeconds`, `productionDurationSeconds`, `maxStoredFreeDeliveries`, `initialLaboratorySlots`, `laboratoryLevelTimeReduction` i opcjonalne `maxOfflineProgressSeconds`. Wszystkie wartości czasu są wyrażone w sekundach.

## Zapis stanu

Zapis wersji `6` przechowuje złoto, składniki, książkę receptur, laboratorium, kontrakty, nieodebrany wynik, timestamp dostaw, liczbę gotowych dostaw i zadania laboratoryjne wraz z wejściem oraz czasami UTC. Poziom mistrzostwa jest wyliczany z zapisanego licznika i konfiguracji.

Starsze zapisy są migrowane automatycznie. Brakujące pola czasu są inicjalizowane od chwili wczytania bez zmiany złota, magazynu, receptur ani mistrzostwa.

Plik zapisu: `Application.persistentDataPath/distillery_save.json`.

## Testy

W Unity: `Window → General → Test Runner → EditMode → Run All`.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode `
  -testResults TestResults.xml -logFile TestRun.log
```

Testy używają kontrolowanego `ITimeProvider` i obejmują konfigurację, dostawy przed/po interwale, limity offline, zużycie składników, zajętość slotów, ukończenie i odbiór obu typów zadań, odroczone losowanie, zapis/odczyt oraz migrację.

## Celowo poza zakresem

Reklamy, IAP, sezony, wiele laboratoriów, Firebase, Addressables, backend, konta, docelowa grafika i animacje. Nie ma synchronizacji czasu z serwerem ani ochrony przed zmianą zegara urządzenia. Mistrzostwo nie ma osobnego XP ani bonusu ceny.

Dokumenty projektowe znajdują się w `docs/`.
