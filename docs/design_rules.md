# Distillery Discovery — zasady projektu v2

## Pętla gry

`dostawa → wybór laboratorium i 3 składników → eksperyment albo produkcja → timed job → ręczny odbiór → pending reward → kontrakty i laboratorium`

Eksperyment odkrywa receptury i pozostaje ważonym losowaniem. Produkcja powtarza odkrytą recepturę deterministycznie. Obie ścieżki zachowują te same timery, offline progress, jakość produktu, mastery, magazyn produktów i claim nagród.

## Content

- Używamy wyłącznie 22 ugruntowanych składników gorzelniczych z `ingredients.json`.
- Grupy `grain`, `sweetener`, `fruit`, `herb`, `spice` są danymi konfiguracyjnymi.
- Receptura ma co najmniej jedną obowiązkową klauzulę i dodatnią wagę bazową.
- Wybrane neutralne składniki są dozwolone; wszystkie wymagania nadal muszą być spełnione.
- Bonus wyniku działa raz na obecność składnika albo grupy, nie raz na sztukę.
- Epic ma najwyżej 5, a legendary najwyżej 3 bezpośrednie skojarzenia z recepturami. Ogólne klauzule grupowe nie liczą się do limitu.
- Typowy nowy content ma być dodawalny przez JSON bez zmian w C#.

## Eksperyment i podgląd

Podgląd pokazuje wyłącznie kwalifikujące się receptury i normalizuje ich wagi do 100%. Tożsamość nieodkrytej receptury pozostaje ukryta. Pusta pula blokuje start przed zużyciem inventory. Wynik nie jest losowany podczas startu ani końca timera, lecz dopiero przy claimie.

## Produkcja

Produkcja wymaga odkrytej receptury i dokładnie trzech składników spełniających jej klauzule. UI pokazuje wymagania receptury oraz wszystkie kanoniczne składniki, ponieważ składnik neutralny może legalnie wypełnić wolny slot. Wynikowa receptura jest gwarantowana; jakość nadal jest losowana.

## Dostawy i jakość

`DeliveryPool.rolls` określa liczbę losowań. Dostawy są częstsze i mniejsze: bazowo co 20 minut, a licznik kolejnej rusza dopiero po odebraniu gotowej. Common podtrzymuje częste eksperymenty, rare pochodzi głównie z dostaw i kontraktów, epic głównie z kontraktów z małą szansą dostawy, a legendary głównie z prestige z minimalną szansą dostawy.

Jakość produktu zależy od rzadkości/bonusu składników, wybranego laboratorium i mastery. Gracz zaczyna z jednym laboratorium, może kupować kolejne i ulepszać każde niezależnie; ceny kolejnych laboratoriów używają progów kosztów kolejnych poziomów. Mastery poprawia wagi wyższej jakości, nie gwarantuje konkretnego poziomu i nie dodaje osobnego mnożnika sprzedaży.

## Kontrakty

Zawsze aktywny jest jeden kontrakt każdej roli:

- `basic`: szeroki, niezawodny cel i nagrody common,
- `specialist`: odkryte receptury, kategorie, tagi, składniki i grupy; główne nagrody rare,
- `prestige`: jakość produktu, minimalna jakość receptury, różne receptury, odkrycia, rekordy i epic; ma kontrolowaną szansę legendary.

Generator rozwiązuje cele tylko z dostępnego contentu i najwyższego poziomu posiadanego laboratorium. Dokładna produkcja używa odkrytych receptur; jawny kontrakt odkrycia może wskazać nieodkrytą recepturę. Aktywne cele nie mogą się duplikować ani mieć identycznego typu i targetu. Postęp distinct zapisuje widziane ID.

Pełny `ProductionEvent` jest jedynym wejściem postępu kontraktów, dzięki czemu pojedynczy claim i `Collect All` naliczają identycznie. Sprzedaż i ukończone kontrakty są wypłacane wyłącznie przez pending-result claim. Jeden darmowy reroll na 24-godzinny cykl chroni przed blokadą.

## Trwałość i czas

Wszystkie czasy są UTC. Timed jobs i dostawy są aktualizowane po powrocie, ale produkty nie odbierają się automatycznie. Save v8 przechowuje cele kontraktów i refresh. Migracja nie wypłaca starych niepoprawnych kontraktów i nie dubluje pending rewards.

## UI i lokalizacja

Każda encja gracza ma PL/EN. Książka ukrywa nazwę oraz wymagania nieodkrytej receptury, a po odkryciu pokazuje kategorię, obowiązkowe klauzule, rekord i mastery. Kontrakty pokazują rolę, rozwiązany cel, postęp, distinct progress, minimalną jakość i nagrodę. Lista 175 receptur używa jednego tekstowego widoku prototypu zamiast 175 ciężkich obiektów UI.

## Poza zakresem

Brak IAP, reklam, backendu, sezonów, finalnej grafiki, ochrony zegara i nowych niezwiązanych mechanik progresji.
