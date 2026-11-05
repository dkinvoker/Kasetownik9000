# Historia zmian

Numer wersji jest w jednym miejscu: `src/Directory.Build.props` (`<Version>`).
Widać go na pasku tytułu aplikacji, w oknie „?” i we właściwościach pliku exe.

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
