# Kasetownik 9000

Aplikacja do projektowania i drukowania okładek na kasety magnetofonowe — J-card, wkładki rozkładane
i naklejki na kasetę — z kalkulatorem taśmy i podglądem 3D. WinUI 3 + Win2D, skompilowana do
**Native AOT** (okno pojawia się w ułamku sekundy), z wyglądem prosto z magnetofonu z lat 80.

**Wersja 1.8** — co się zmieniało, opisuje [CHANGELOG.md](CHANGELOG.md), a pomysły na dalszy rozwój
[Developement.md](Developement.md).

![ikona](src/Kasetownik9000/Assets/AppIcon.png)

## Co potrafi

- **Projekt = cała kaseta**: w jednym pliku `.kaseta` kilka kart (np. J-card + naklejki A/B),
  przełączanych zakładkami nad płótnem. Wspólne dane albumu i lista utworów, wspólny wydruk.
- **Szablony formatów** (wymiary w mm):
  | Format | Szer. | Panele (od góry) |
  |---|---|---|
  | J-card (standard) | 101,6 | przód 65 · grzbiet 12,7 · klapka 15,9 |
  | J-card 4-panelowa | 101,6 | wkładka 65 · przód 65 · grzbiet 12,7 · klapka 15,9 |
  | J-card 5-panelowa | 101,6 | 2 × wkładka 65 · przód 65 · grzbiet 12,7 · klapka 15,9 |
  | J-card z długą klapką | 101,6 | przód 65 · grzbiet 12,7 · tył 65 |
  | Naklejki A/B | 89 | strona A 42 · strona B 42 (linia cięcia), w każdej okienko na szpule 60 × 16 |
  | Własny | dowolna | dowolne panele — dodawanie, usuwanie, zmiana wysokości |
- **Okienko na szpule w naklejkach**: otwór na trzpienie magnetofonu i podgląd taśmy widać w edytorze
  (przez otwór prześwituje kaseta), na wydruku jest białe z linią cięcia i napisem „wytnij”, a w 3D naklejka
  ma prawdziwy otwór. Wymiary jak na kasecie kompaktowej: 60 × 16 mm, środek 22 mm od górnej krawędzi
  naklejki, osie szpul 42,6 mm od siebie. Przycisk ◎◎ przy panelu naklejki wyłącza okienko (np. dla wąskiego
  paska nad okienkiem w formacie własnym).
- **Style startowe** (10): Retro paski, Minimal noir, Składanka (papier w linie + odręczne pismo), Synthwave,
  Wideo 80s, Chrom (Type II), Punk ksero, Kraft, Szwajcarski i pusta karta — każdy dla wszystkich paneli i naklejek.
- **Dane albumu i pola**: tytuł, wykonawca, rok, data, dopisek wpisujesz raz, a w tekstach używasz
  `{tytuł}`, `{wykonawca}`, `{rok}`, `{data}`, `{dopisek}`, `{kaseta}`, `{lista A}`, `{lista B}`,
  `{czas A}`, `{czas B}`, `{czas}` — wstawiają się same na wszystkich kartach („Wstaw pole” przy tekście).
- **Kalkulator taśmy**: C-46/C-60/C-90/C-120 albo własna długość, przerwy między utworami, import plików
  audio (tytuł, wykonawca i czas z tagów), automatyczny podział na strony A/B, liczniki stron z ostrzeżeniem
  „za długo”, ręczne przenoszenie, playlisty M3U. Pliki muzyczne można przeciągnąć prosto na płótno.
- **Elementy**: tekst, obraz, prostokąt, elipsa, linia, **paski retro** (gotowe palety: zachód słońca,
  tęcza 70s, neon 80s…), lista utworów.
- **Tekst**: dowolna czcionka systemowa, rozmiar w pt, kolor, pogrubienie/kursywa/WIELKIE LITERY,
  wyrównanie w poziomie i pionie, odstęp liter, interlinia, **automatyczne dopasowanie do ramki**
  (idealne na grzbiet), retro cień.
- **Obraz**: wypełnij/zmieść/rozciągnij, filtry czarno-biały i sepia, **odbicie lustrzane** w poziomie
  i w pionie; obrazy są osadzane w pliku projektu. Można je przeciągnąć z Eksploratora albo wkleić ze schowka.
- **Edycja myszką**: przeciąganie, uchwyty skalowania i obrotu, przyciąganie do krawędzi paneli,
  środka karty i innych elementów (Alt wyłącza), Shift = proporcje / linia prosta / obrót co 15°.
- Warstwy (kolejność, blokada, ukrywanie), cofnij/ponów, kopiuj/wklej (także między oknami — skopiowany
  obraz wkleisz też np. w Paincie), duplikuj.
- **Druk 1:1** przez systemowe okno drukowania, ze znacznikami cięcia i zgięcia: **cały komplet na jednym
  arkuszu** (np. J-card + naklejki A/B na A4) albo kopie jednej karty. Spad (domyślnie 1 mm) chroni przed
  białymi brzegami.
- **Wnętrze karty i druk dwustronny**: „+ KARTA” → „Wnętrze…” dodaje drugą stronę kartki (np. na teksty
  piosenek i podziękowania) jako osobną zakładkę z tymi samymi panelami. Projektuje się ją tak, jak widać
  kartkę obróconą **góra ↔ dół** — tak wnętrze przodu widać po otwarciu pudełka (J-karta obraca się na zawiasie
  przy grzbiecie), więc panele idą w odwrotnej kolejności; można też przełączyć na lewa ↔ prawa. Przy druku
  wnętrze trafia na odwrocie, dokładnie za swoją kartą. Drukarka
  z dupleksem drukuje obie strony sama, bez dupleksu — najpierw przody, potem po przełożeniu kartek tyły.
  Do wyboru obracanie kartki wzdłuż dłuższej albo krótszej krawędzi. **Wyrównanie stron**: kartka testowa
  (miarki z przodu, krzyżyk z tyłu) — pod światło widać, o ile tył „ucieka”, a wpisana korekta działa przy
  każdym druku.
- **Kalibracja drukarki**: strona testowa z miarkami — po wpisaniu pomiarów wydruki są korygowane
  (wiele drukarek drukuje 98–99% rozmiaru).
- **Eksport PDF** (A4/Letter, rzeczywisty rozmiar, 600 DPI, wiele arkuszy, przy wnętrzach — strony przód/tył
  na przemian) i **PNG** (300/600 DPI).
- **Podgląd 3D** (klawisz „3D PODGLĄD” albo F3): J-karta złożona wokół kasety w przezroczystym pudełku,
  na kasecie Twoje naklejki, obracanie myszą, widoki przód / 3/4 / grzbiet / tył, automatyczny obrót,
  pudełko i okładkę można ukryć, zapis **mockupu PNG** i **animacji GIF**. Odświeża się na żywo.
  Klawisz **KASETA** wyjmuje kasetę z pudełka: widać wtedy od tyłu wnętrze karty (druga strona kartki)
  i złożone wkładki 4- i 5-panelowej J-karty. Gdy projekt ma kilka wersji okładki albo naklejek, w pudełku
  jest ta z aktywnej zakładki — zakładki są widoczne także w podglądzie 3D.
- **Dwuklik na pliku `.kaseta`** w Eksploratorze otwiera projekt (pliki mają ikonę kasety). Skojarzenie
  zapisuje się samo przy uruchomieniu aplikacji — tylko dla Twojego konta, bez uprawnień administratora.

## Uruchomienie i rozdawanie

Po zbudowaniu (`publish.ps1`) są dwie wersje — obie działają bez instalowania czegokolwiek
na Windows 10 2004+ / Windows 11:

- **`dist\Kasetownik9000.exe` — jeden plik (~29 MB)**, najlepszy do podania komuś dalej.
  W środku jest spakowana cała aplikacja; przy pierwszym uruchomieniu rozpakowuje się
  do `%LOCALAPPDATA%\Kasetownik9000\app\<wersja>` (ok. 2–3 s), kolejne starty są natychmiastowe.
  Nowsza wersja exe sama rozpakuje się obok i usunie starą.
  **Odinstalowanie:** po pierwszym uruchomieniu obok exe pojawia się `Kasetownik9000 - odinstaluj.cmd` —
  dwuklik, „T” i folder w `%LOCALAPPDATA%` znika razem z ustawieniami i skojarzeniem plików `.kaseta`
  (skrypt sprawdza, czy aplikacja nie jest otwarta, nie rusza projektów ani wydruków i na koniec usuwa sam
  siebie). Potem wystarczy skasować exe.
  Pliki `.kaseta` otwiera dwuklikiem ten exe, który był uruchomiony ostatnio — po przeniesieniu go w inne
  miejsce wystarczy raz go uruchomić.
- **`publish\win-x64\`** — ten sam program jako zwykły folder (exe + biblioteki Windows App SDK).

> Plik nie jest podpisany certyfikatem, więc Windows SmartScreen przy pierwszym uruchomieniu
> pobranego pliku pokaże ostrzeżenie — „Więcej informacji” → „Uruchom mimo to”.

WinUI 3 nie da się skompilować do jednego „gołego” exe: Windows App SDK ładuje ~240 natywnych
bibliotek i plików zasobów, które muszą leżeć na dysku obok programu — dlatego wersja jednoplikowa
to mały starter (też Native AOT), który je rozpakowuje ([src/Kasetownik9000.Launcher](src/Kasetownik9000.Launcher)).

## Budowanie

Wymagania: **.NET 10 SDK** oraz (tylko dla Native AOT) **Visual Studio Build Tools** z obciążeniem
„Programowanie aplikacji klasycznych w C++”.

```bash
dotnet build src/Kasetownik9000/Kasetownik9000.csproj
```

```bash
powershell -ExecutionPolicy Bypass -File publish.ps1
```

`publish.ps1` publikuje aplikację (`dotnet publish -c Release -r win-x64`, Native AOT) do `publish\win-x64`,
a potem buduje z niej jednoplikowy `dist\Kasetownik9000.exe` (opcja `-NoSingleFile` pomija ten krok,
`-Arch arm64` buduje wersję ARM — wymaga narzędzi C++ dla ARM64). W `src/Directory.Build.targets` jest
obejście błędu VS Build Tools 18, w którym linker AOT nie znajdował `vswhere.exe` — nie trzeba nic
dopisywać do PATH. Numer wersji zmienia się w `src/Directory.Build.props`.

## Jak wydrukować dobrą okładkę

1. Najpierw raz **skalibruj drukarkę** (DRUKUJ → „Kalibracja…”): wydrukuj stronę testową, zmierz miarki
   i odległość krzyżyka od krawędzi kartki, wpisz wyniki.
2. Drukuj z aplikacji (DRUKUJ) — karty idą w skali 1:1. Z PDF-a drukuj w skali **100% /
   „Rzeczywisty rozmiar”**, nigdy „Dopasuj do strony”.
3. Najlepszy jest papier 160–200 g/m² (matowy lub satynowy).
4. Wytnij wzdłuż znaczników cięcia (narożniki), zegnij wzdłuż znaczników zgięcia przy krawędziach —
   najlepiej po wcześniejszym nagnieceniu linii tępą stroną noża i linijką.
5. W naklejkach wytnij białe okienko („wytnij”) nożykiem przy linijce, zanim przykleisz naklejkę na kasetę —
   magnetofon musi mieć dostęp do szpul.
6. Druk dwustronny (wnętrze karty): za pierwszym razem wydrukuj kartkę testową w „Wyrównanie stron…”
   (w oknie drukowania) — od razu widać, jak włożyć kartkę przy druku ręcznym i o ile przesuwa się tył.
   Spad na wnętrzu też jest — milimetr przesunięcia nie zostawi białego paska.

## Skróty klawiszowe

| Skrót | Akcja |
|---|---|
| Ctrl+N / O / S / Shift+S | nowy / otwórz / zapisz / zapisz jako |
| Ctrl+P · Ctrl+E | drukuj · eksport PNG |
| F3 · Esc | podgląd 3D · powrót do edycji |
| Ctrl+Z · Ctrl+Y | cofnij · ponów |
| Ctrl+C / X / V / D | kopiuj / wytnij / wklej / duplikuj |
| Del · Esc | usuń · odznacz |
| Strzałki (Shift) | przesuń o 0,5 mm (5 mm) |
| Ctrl+kółko · Ctrl+0 · Ctrl+1 | zoom · dopasuj · rozmiar rzeczywisty |
| Spacja + przeciągnij / środkowy przycisk | przesuwanie widoku |

## Linia poleceń

```bash
Kasetownik9000.exe projekt.kaseta
Kasetownik9000.exe projekt.kaseta --export-pdf komplet.pdf
Kasetownik9000.exe projekt.kaseta --card 1 --fill --export-pdf dwie-jkarty.pdf
Kasetownik9000.exe --format jcard4 --starter mixtape --komplet --tracks "C:\Muzyka\Składanka" --tape 60 --export-kaseta skladanka.kaseta
Kasetownik9000.exe projekt.kaseta --export-3d mockup.png --yaw -28 --pitch 24 --case on --cover on
Kasetownik9000.exe projekt.kaseta --export-3d wnetrze.png --yaw -22 --pitch 160 --cassette off
Kasetownik9000.exe projekt.kaseta --export-gif obrot.gif
Kasetownik9000.exe projekt.kaseta --print "Nazwa drukarki"
Kasetownik9000.exe --print-calibration "Nazwa drukarki"
Kasetownik9000.exe --format jcard4 --starter kraft --komplet --inside --export-pdf dwustronnie.pdf
Kasetownik9000.exe --print-duplex-test "Nazwa drukarki"
```

Formaty: `jcard`, `jcard4`, `jcard5`, `jcard-long`, `labels`, `custom`.
Style: `retro`, `minimal`, `mixtape`, `synthwave`, `video`, `chrome`, `punk`, `kraft`, `swiss`, `empty`.
PDF i druk obejmują cały komplet kart, chyba że podasz `--card N` albo `--fill`.
`--inside` dodaje do nowego projektu wnętrze J-karty; wnętrza idą na odwrocie arkusza (`--no-duplex` je pomija,
`--duplex-short` = obracanie kartki wzdłuż krótszej krawędzi).

## Struktura

```
src/Kasetownik9000/
  Model/        projekt i karty, elementy, formaty, okienko naklejki (LabelWindow), style startowe (Starters,
                StartersMore), pola {…} (TextFields), plan taśmy i import audio (TapeImport),
                ustawienia (kalibracja) — JSON z generatorem źródeł (AOT)
  Rendering/    CardRenderer (Win2D) — jeden renderer dla ekranu, PNG, PDF, druku i 3D; AssetStore
  Editor/       EditorView (płótno, uchwyty, przyciąganie, zoom), EditorSession (projekt, karty, cofanie),
                Preview3DView (podgląd 3D: tekstury paneli + Transform3DEffect w perspektywie, eksport GIF)
  Output/       druk GDI (PrintDlg + StretchDIBits, kalibracja), PDF, rozkład kart na arkuszach, GifEncoder
  Controls/     ColorButton (paleta retro + próbnik), SectionHeader
  Themes/       Retro.xaml — kolory, klawisze magnetofonu, wyświetlacz LCD
  MainWindow.*  okno: właściwości, pliki i druk, kalkulator taśmy (MainWindow.Tape.cs)
src/Kasetownik9000.Launcher/   jednoplikowy starter (osadzony zip aplikacji, rozpakowanie raz na wersję)
src/Directory.Build.props      numer wersji
tools/make-icon.ps1            generator ikony (kaseta) → Assets/AppIcon.ico + .png
```
