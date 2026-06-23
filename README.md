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

Pula eksperymentu zawiera wyłącznie receptury, których wszystkie obowiązkowe klauzule spełnia wybrana trójka. Wynikowa waga to dodatnia waga bazowa receptury plus opcjonalne premie składnika lub grupy, naliczane najwyżej raz. Podgląd i końcowe losowanie używają tego samego czystego algorytmu; pusty wybór jest odrzucany przed zużyciem składników.

### Dostawy czasowe

Darmowa dostawa odnawia się cyklicznie. Ekran dostawy pokazuje liczbę gotowych odbiorów i czas do następnego. Odbiór zużywa jeden zapisany odbiór. Podczas nieobecności dostawy kumulują się do `maxStoredFreeDeliveries`; nadmiar przepada.

### Produkcja i mistrzostwo

Produkcja zaczyna się od kafelkowej listy odkrytych receptur. Każda receptura pokazuje obowiązkowe klauzule: konkretny składnik, grupę, różne składniki z grupy albo zestaw `any_of`. Gracz rezerwuje dokładnie 3 składniki; dodatkowe neutralne składniki i duplikaty są dozwolone, o ile klauzule są spełnione. Receptura wyniku jest gwarantowana, a rzadkość zależy od składników, laboratorium i mistrzostwa receptury.

Każdy produkt uzyskany z eksperymentu lub produkcji zwiększa licznik danej receptury. Progi mistrzostwa są konfigurowalne: Czeladnik (1), Rzemieślnik (10), Mistrz (30) i Arcymistrz (100). Poziomy dają odpowiednio 0%, 5%, 10% i 20% bonusu do wag wyższych rzadkości; nie zwiększają ceny sprzedaży i nie gwarantują najwyższej jakości. Książka receptur pokazuje licznik, poziom i brakującą liczbę wytworzeń albo informację o poziomie maksymalnym.

### Odbiór wyniku

Eksperyment i produkcja tworzą zapisywany, blokujący ekran podsumowania. Pokazuje on produkt, rzadkość, nagrodę sprzedaży oraz wszystkie zmiany postępu kontraktów. Złoto ze sprzedaży i bonusy ukończonych kontraktów trafiają na konto dopiero po naciśnięciu **Odbierz**. Do tego czasu nie można rozpocząć kolejnej akcji rozgrywki.

Ekran laboratorium udostępnia również **Collect All**. Akcja rozlicza wszystkie gotowe eksperymenty i produkcje, pomija zadania w toku, dodaje produkty do magazynu, aktualizuje księgę receptur oraz mistrzostwo i pokazuje łączne liczby zadań, produktów, odkryć, rekordów rzadkości i uzyskanego złota.

### Kontrakty

Jednocześnie aktywne są 3 kontrakty generowane z szablonów: `basic`, `specialist` i `prestige`. Cele obejmują recepturę, kategorię, tag, jakość produktu, użycie składnika lub grupy, odkrycia, różne receptury, minimalną jakość konkretnej receptury, poprawę rekordu i źródło produktu.

Eksperyment i produkcja emitują identyczne pełne zdarzenie zawierające recepturę, jakość, kategorię, tagi, składniki, grupy, źródło, odkrycie i poprawę rekordu. Jeżeli wynik ukończy jeden lub kilka kontraktów, wszystkie są pokazane w podsumowaniu i wypłacane przy **Odbierz**. Nagrody obejmują złoto i ważone składniki wybierane bezpośrednio, według grupy lub rzadkości. Ukończone kontrakty są zastępowane bez duplikatów; dostępny jest jeden darmowy reroll na cykl i bezpieczny odświeżający cykl 24 h.

### Laboratorium

Gracz zaczyna z jednym laboratorium, może kupować kolejne i ulepszać każde niezależnie. Przy starcie eksperymentu lub produkcji wybierane jest wolne laboratorium; jego poziom określa sloty, mnożniki czasu i bonus jakości dla danego zadania. Ceny kolejnych laboratoriów są mapowane na koszty kolejnych poziomów z `laboratories.json`.

Ekran laboratorium dzieli zadania na aktywne eksperymenty, aktywne produkcje i gotowe do odbioru. Każdy kafelek pokazuje typ, składniki, docelową recepturę produkcji, pozostały czas i status; gotowy kafelek działa jako przycisk pojedynczego odbioru.

Poziom każdego laboratorium niezależnie określa liczbę slotów eksperymentów i produkcji oraz mnożniki czasu obu typów zadań. Ulepszenia nadal zwiększają jakość produktu. Wszystkie te wartości oraz koszt następnego poziomu pochodzą z `laboratories.json`.

### Offline progress i zegar debugowy

Wszystkie timestampy są zapisywane i porównywane w UTC. Po uruchomieniu lub wczytaniu gra pokazuje czas nieobecności, liczbę skumulowanych dostaw, liczbę zadań ukończonych offline oraz osobne liczby gotowych eksperymentów i produkcji. Wyniki zadań nigdy nie są automatycznie odbierane.

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
- `ingredient_groups.json` — 5 grup składników,
- `ingredients.json` — 22 składniki, rzadkości, grupy i źródła,
- `recipes.json` — 175 receptur, wymagania, bazowe wagi, bonusy, kategorie, tagi i wartości,
- `categories.json` — kategorie receptur,
- `economy.json` — wszystkie czasy, rozmiar i limit dostaw oraz pozostałe parametry ekonomii,
- `laboratories.json` — poziomy, koszty, bonusy jakości, sloty eksperymentów/produkcji i ich mnożniki czasu,
- `contracts.json` — 36 szablonów kontraktów v2 i ważone nagrody,
- `mastery.json` — progi mistrzostwa receptur i bonusy rzadkości,
- `localization.json` — polskie oraz angielskie teksty UI i contentu.

Konfiguracja jest walidowana przy starcie, łącznie z dodatnimi czasami, kosztami, slotami i mnożnikami. Bazowe czasy w sekundach znajdują się w `economy.json`; darmowa dostawa trwa 20 minut i rusza dopiero po odebraniu poprzedniej gotowej dostawy. Pola `experimentSlots`, `productionSlots`, `experimentTimeMultiplier` i `productionTimeMultiplier` są definiowane dla każdego poziomu w `laboratories.json`. Opcjonalny `maxOfflineProgressSeconds` ogranicza naliczanie offline.

## Zapis stanu

Zapis wersji `9` przechowuje złoto, poprawne składniki, produkty, książkę receptur, listę laboratoriów z poziomami, wygenerowane cele i role kontraktów, zbiory różnych receptur, stan rerolla/odświeżenia, nieodebrany wynik, dostawy i zadania laboratoryjne wraz z wybranym laboratorium, wejściem oraz czasami UTC. Zapisy v7/v8 są migrowane: zachowany zostaje poprawny postęp, a nieaktualne kontrakty są bezpiecznie generowane ponownie.

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

Reklamy, IAP, sezony, wiele laboratoriów, Firebase, Addressables, backend, konta, docelowa grafika i animacje. Nie ma synchronizacji czasu z serwerem ani ochrony przed zmianą zegara urządzenia. Mistrzostwo nie ma osobnego XP ani bonusu ceny. Prototypowe podsumowania i kafelki zadań używają tekstowego UI bez finalnej oprawy.

Dokumenty projektowe znajdują się w `docs/`.
