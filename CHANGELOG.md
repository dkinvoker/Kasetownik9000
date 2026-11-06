# Historia zmian

Numer wersji jest w jednym miejscu: `src/Directory.Build.props` (`<Version>`).
Widać go na pasku tytułu aplikacji, w oknie „?” i we właściwościach pliku exe.

## 1.9 — 2026-11-07

### Nowe
- **Zaznaczanie kilku elementów naraz.** Shift/Ctrl + klik dodaje element do zaznaczenia albo go usuwa,
  przeciągnięcie po pustym miejscu zaznacza prostokątem (z Shift/Ctrl — dokłada do zaznaczenia), Ctrl+A zaznacza
  wszystko, a na liście warstw działa Ctrl/Shift + klik. Zaznaczone elementy:
  - przesuwają się razem (myszką i strzałkami, z przyciąganiem całej ramki),
  - skalują się razem za narożniki wspólnej ramki — razem z czcionkami, grubością linii i zaokrągleniami,
  - obracają się razem wokół wspólnego środka (Shift — co 15°),
  - kopiują się, wklejają, duplikują, usuwają, blokują i ukrywają jednym ruchem.
- **Wyrównywanie do siebie** (lewa/prawa krawędź, góra/dół, środki) i **równe odstępy** w poziomie i w pionie —
  w nowym panelu „Zaznaczenie”. Całość można też wyśrodkować na karcie.
- **Grupy** (Ctrl+G, rozgrupowanie Ctrl+Shift+G). Klik na element grupy zaznacza całą grupę, nad ramką widać jej
  nazwę (do zmiany w panelu), a na liście warstw grupa ma swój wiersz z wciętymi elementami. Dwuklik w grupie
  wybiera jeden jej element (np. żeby zmienić tekst). Grupa zawsze leży w jednym miejscu stosu warstw;
  kopia całej grupy dostaje nową grupę „… (kopia)”.
- **Testy jednostkowe** (`src/Kasetownik9000.Tests`, `dotnet test`): 132 testy logiki — formaty i wszystkie style
  (m.in. czy napisy na naklejkach omijają okienko), wnętrza i kierunek odwracania, rozkład kart na arkuszu
  i druk dwustronny, grupy, kolejność warstw, wyrównywanie, skalowanie, pliki z poprzednich wersji, pola {…},
  kalkulator taśmy, dekodowanie schowka.

### Zmiany
- Przeciągnięcie po pustym miejscu płótna zaznacza teraz prostokątem zamiast przesuwać widok. Widok przesuwa
  się spacją + przeciągnięciem albo środkowym przyciskiem myszy (jak dotąd) i kółkiem.
- Skróty z Ctrl i Delete działają także wtedy, gdy kursor jest na liście warstw.

## 1.8 — 2026-11-06

### Nowe
- **Dwuklik na pliku `.kaseta` otwiera projekt.** Aplikacja przy starcie sama kojarzy pliki `.kaseta` ze sobą
  (tylko dla bieżącego użytkownika, bez uprawnień administratora) i daje im ikonę kasety. Z jednoplikowego exe
  pliki otwiera właśnie ten exe; po przeniesieniu go wystarczy raz go uruchomić. Skrypt odinstalowania usuwa
  skojarzenie.
- **Podgląd 3D bez kasety** — klawisz „KASETA” wyjmuje kasetę z pudełka. Od tyłu widać wtedy wnętrze karty
  (druga strona kartki: teksty, podziękowania) i złożone harmonijką wkładki 4- i 5-panelowej J-karty.
  Pudełko samo obraca się tyłem, jeśli jest co oglądać. Linia poleceń: `--cassette off`.
  Ściany karty mają teraz od spodu prawdziwe wnętrze także przy widocznej kasecie.
- **Podgląd 3D pokazuje kartę z aktywnej zakładki.** Przy kilku wersjach w jednym projekcie (np. J-card
  i J-card 4-panelowa) w pudełku jest ta, którą wybierzesz — zakładki zostają widoczne także w trybie 3D,
  a nazwa karty jest na wyświetlaczu. Kliknięcie naklejek zmienia naklejki na kasecie i zostawia oglądaną
  okładkę (i odwrotnie). Wcześniej zawsze była pierwsza J-karta z listy. W linii poleceń wybiera ją `--card N`.

### Zmiany
- **Wnętrze karty ogląda się po obróceniu góra ↔ dół.** J-karta obraca się w pudełku na zawiasie przy grzbiecie,
  więc tak właśnie widać wnętrze przodu po otwarciu pudełka (i tak samo czyta się klapkę z tyłu). W 1.7 wnętrze
  było odwracane lewa ↔ prawa — po otwarciu pudełka tekst byłby do góry nogami. Nowe wnętrza mają panele
  w odwrotnej kolejności (klapka u góry, przód na dole), a druk dwustronny uwzględnia to sam.
  Wnętrza z 1.7 otwierają się bez zmian (lewa ↔ prawa); kierunek zmienisz w panelu karty („Wnętrze ogląda się
  po obróceniu kartki”) — elementy przechodzą razem ze swoim panelem.

## 1.7 — 2026-11-06

### Nowe
- **Wnętrze karty i druk dwustronny.** „+ KARTA” → „Wnętrze karty…” (albo prawy przycisk na zakładce) dodaje
  drugą stronę kartki jako zakładkę „J-card · wnętrze”, od razu z miejscem na teksty piosenek i podziękowania.
  Wnętrze ma zawsze wymiary i panele swojej karty. Projektuje się je tak, jak widać kartkę po odwróceniu
  (lewa ↔ prawa), a przy druku trafia na odwrocie arkusza, dokładnie za swoją kartą.
  - Drukarka z dupleksem dostaje jedno zadanie (przód, tył…); bez dupleksu albo na życzenie — najpierw przody,
    potem okno „przełóż kartki” i tyły. Gdy drukarka nie umie drukować dwustronnie, aplikacja sama przechodzi
    na tryb ręczny.
  - Obracanie kartki wzdłuż dłuższej krawędzi (jak strona w książce) albo krótszej (jak kalendarz) —
    zapamiętywane razem z trybem.
  - **Wyrównanie stron:** kartka testowa z miarkami z przodu i krzyżykiem z tyłu; pod światło widać, o ile tył
    jest przesunięty, a wpisana korekta działa przy każdym druku dwustronnym.
  - PDF: po każdym arkuszu jego odwrocie. Linia poleceń: `--inside`, `--no-duplex`, `--duplex-short`,
    `--print-duplex-test`.
- **Odbicie lustrzane obrazów** — w poziomie i w pionie (przyciski w panelu „Obraz” i w menu pod prawym przyciskiem).

### Poprawki
- **Kopiuj/wklej elementów (Ctrl+C, Ctrl+V) nie działało.** Windows oddawał skopiowany element jako strumień
  bajtów, a nie tekst, więc wklejanie po cichu nic nie robiło (dla tekstów wklejał się tylko goły tekst bez
  formatowania). Teraz wkleja się pełny element razem z obrazem, jak przy Ctrl+D. Kolejne wklejenia układają się
  kaskadą, a skopiowany obraz można też wkleić w innym programie (np. w Paincie).

## 1.6.1 — 2026-11-05

### Poprawki
- **Okienko na szpule w naklejkach A/B.** Wcześniej było widać je tylko w 3D, więc na wydruku tekst potrafił
  wpaść w miejsce, które i tak trzeba wyciąć (np. wykonawca w stylu Kraft), a samo okienko trzeba było wycinać na oko.
  Teraz ten sam otwór 60 × 16 mm (szpule co 42,6 mm, środek 22 mm od górnej krawędzi naklejki) jest:
  - w edytorze — prześwituje przez niego kaseta, obrys w kolorze linii cięcia,
  - na wydruku, w PDF i PNG — białe pole bez farby z cienką linią cięcia i napisem „wytnij”,
  - w podglądzie 3D — naklejka ma prawdziwy otwór, przez który widać obudowę, okienko z taśmą i szpule,
  - na miniaturach w oknie „Nowa kaseta”.
- Wszystkie style startowe układają naklejki wokół okienka: tytuł nad nim, wykonawca pod nim, litera strony
  (i długość kasety albo rok) po bokach.
- Przycisk ◎◎ przy panelu naklejki (Karta → Panele) wyłącza okienko, np. dla wąskiego paska naklejanego nad okienkiem.

Projekty zapisane wcześniej same dostaną okienko na naklejkach. Ich elementy zostają tam, gdzie były,
więc warto zerknąć, czy coś nie wpada w otwór.

## 1.6 — 2026-11-05

### Nowe
- **Sześć nowych stylów startowych** (razem dziesięć), każdy z wersją dla przodu, grzbietu, klapki, wkładek
  i naklejek A/B:
  - **Synthwave** — neonowy zachód słońca z „pociętym” słońcem nad siatką zbiegającą się na horyzoncie,
  - **Wideo 80s** — czarne pudełko i tęczowe skośne pasy jak na kasetach wideo z wypożyczalni,
  - **Chrom (Type II)** — metaliczne srebro, grafitowy pas z tytułem i czerwony akcent,
  - **Punk ksero** — przekrzywione napisy, kawałki taśmy, maszynopis i pieczątka DIY,
  - **Kraft** — papier pakowy, zawieszka z dziurką i sznurkiem, pieczątka z rokiem,
  - **Szwajcarski** — czerwona kolumna biegnąca przez wszystkie panele, czarna typografia, siatka.
- W oknie „Nowa kaseta” lista stylów jest zwięzła (same nazwy), a opis wybranego stylu pojawia się pod podglądem.
- Elementy czysto tłowe w nowych stylach (siatka, metaliczne pasy) są od razu zablokowane, żeby nie łapały
  się pod mysz przy klikaniu w napisy — odblokujesz je w warstwach.

### Usunięte
- Obsługa plików projektu z wersji 1.0. Taki plik pokazuje teraz czytelny komunikat zamiast konwersji.

## 1.5 — 2026-11-05

### Nowe
- **Projekt = cała kaseta.** Jeden plik `.kaseta` może mieć kilka kart: J-card, naklejki A/B, własne formaty.
  Przełączasz je zakładkami nad płótnem („+ KARTA” dodaje nową w stylu projektu; prawy przycisk na zakładce:
  zmiana nazwy, usunięcie). Okno „Nowy” od razu proponuje komplet: J-card + naklejki.
- **Dane albumu i pola w tekstach.** Tytuł, wykonawca, rok, data nagrania i dopisek wpisujesz raz (panel „Album”),
  a w tekstach używasz pól `{tytuł}`, `{wykonawca}`, `{rok}`, `{data}`, `{dopisek}`, `{kaseta}`,
  `{lista A}`, `{lista B}`, `{czas A}`, `{czas B}`, `{czas}` — podstawiają się na wszystkich kartach.
  Przycisk „Wstaw pole” przy edycji tekstu. Style startowe korzystają z pól.
- **Kalkulator taśmy.** Wybór kasety (C-46, C-60, C-90, C-120 albo własna długość) i przerwy między utworami,
  import plików audio (MP3, FLAC, M4A, WAV, WMA, OGG…) z tytułem, wykonawcą i czasem z tagów, automatyczny podział
  na strony A/B (z zachowaniem kolejności), ręczne przenoszenie i edycja, liczniki stron z ostrzeżeniem
  „za długo”, playlisty M3U dla każdej strony. Pliki muzyczne można przeciągnąć na płótno albo do okna kalkulatora.
  Lista utworów na okładce aktualizuje się sama (z czasami, wykonawcami, numeracją 1. albo A1.).
- **Komplet na arkuszu.** Druk i PDF układają wszystkie karty projektu na jednym arkuszu (albo kilku,
  jeśli się nie mieszczą) — np. J-card i naklejki A/B na jednej kartce A4. Do wyboru: komplet albo bieżąca karta.
- **Kalibracja drukarki.** Strona testowa z miarkami 100 mm i krzyżykiem; po wpisaniu pomiarów każdy wydruk
  jest korygowany (skala i przesunięcie). W PDF korektę można włączyć opcjonalnie.
- **Podgląd 3D:** kaseta w pudełku nosi prawdziwe naklejki z projektu; klawisz „OKŁADKA” chowa J-kartę;
  **„ZAPISZ GIF”** nagrywa animację obrotu (własny koder GIF ze wspólną paletą — bez pasm w gradientach).
- Numer wersji na pasku tytułu i we właściwościach pliku.
- Linia poleceń: `--komplet`, `--card`, `--tracks`, `--tape`, `--export-gif`, `--cover`, `--calibrated`,
  `--print-calibration`.

### Zmiany
- Przezroczyste pudełko w 3D przylega ciaśniej do karty (ok. 1,2 mm luzu zamiast ~2 mm).
- Pliki projektu w formacie 2. Projekty z wersji 1.0 otwierają się bez problemu
  (wersja 1.0 nie otworzy plików zapisanych w 1.5).

## 1.0 — 2026-11-04

Pierwsza wersja: edytor okładek (J-card 3/4/5-panelowa, z długą klapką, naklejki A/B, własny format),
style startowe, tekst/obraz/kształty/paski retro, warstwy, cofanie, przyciąganie, druk 1:1 ze znacznikami,
eksport PDF i PNG, podgląd 3D z mockupem PNG, Native AOT, jednoplikowy exe ze skryptem odinstalowania.
