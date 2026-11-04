# Kasetownik 9000

Aplikacja do projektowania i drukowania okładek na kasety magnetofonowe — J-card, wkładki rozkładane
i naklejki na kasetę. WinUI 3 + Win2D, skompilowana do **Native AOT** (okno pojawia się w ułamku sekundy),
z wyglądem prosto z magnetofonu z lat 80.

![ikona](src/Kasetownik9000/Assets/AppIcon.png)

## Co potrafi

- **Szablony formatów** (wymiary w mm):
  | Format | Szer. | Panele (od góry) |
  |---|---|---|
  | J-card (standard) | 101,6 | przód 65 · grzbiet 12,7 · klapka 15,9 |
  | J-card 4-panelowa | 101,6 | wkładka 65 · przód 65 · grzbiet 12,7 · klapka 15,9 |
  | J-card 5-panelowa | 101,6 | 2 × wkładka 65 · przód 65 · grzbiet 12,7 · klapka 15,9 |
  | J-card z długą klapką | 101,6 | przód 65 · grzbiet 12,7 · tył 65 |
  | Naklejki A/B | 89 | strona A 42 · strona B 42 (linia cięcia) |
  | Własny | dowolna | dowolne panele — dodawanie, usuwanie, zmiana wysokości |
- **Style startowe**: Retro paski, Minimal noir, Składanka (papier w linie + odręczne pismo), pusta karta.
- **Elementy**: tekst, obraz, prostokąt, elipsa, linia, **paski retro** (gotowe palety: zachód słońca,
  tęcza 70s, neon 80s…), lista utworów.
- **Tekst**: dowolna czcionka systemowa, rozmiar w pt, kolor, pogrubienie/kursywa/WIELKIE LITERY,
  wyrównanie w poziomie i pionie, odstęp liter, interlinia, **automatyczne dopasowanie do ramki**
  (idealne na grzbiet), retro cień.
- **Obraz**: wypełnij/zmieść/rozciągnij, filtry czarno-biały i sepia; obrazy są osadzane w pliku projektu.
  Można je przeciągnąć z Eksploratora albo wkleić ze schowka.
- **Edycja myszką**: przeciąganie, uchwyty skalowania i obrotu, przyciąganie do krawędzi paneli,
  środka karty i innych elementów (Alt wyłącza), Shift = proporcje / linia prosta / obrót co 15°.
- Warstwy (kolejność, blokada, ukrywanie), cofnij/ponów, kopiuj/wklej (także między oknami), duplikuj.
- **Druk 1:1** przez systemowe okno drukowania, ze znacznikami cięcia i zgięcia, opcjonalnie
  kilka kopii na arkuszu (np. 2 J-karty na A4). Spad (domyślnie 1 mm) chroni przed białymi brzegami.
- **Eksport PDF** (A4/Letter, rzeczywisty rozmiar, 600 DPI) i **PNG** (300/600 DPI, opcjonalnie ze spadem).
- **Podgląd 3D** (klawisz „3D PODGLĄD” albo F3): J-karta złożona wokół kasety w przezroczystym pudełku,
  obracanie myszą, widoki przód / 3/4 / grzbiet / tył, automatyczny obrót, pudełko do ukrycia
  i zapis mockupu do PNG (2400 × 1600). Odświeża się na żywo podczas edycji w panelu właściwości.
  Dla naklejek A/B pokazuje samą kasetę z etykietami.
- Projekt zapisywany jako jeden plik `.kaseta` (JSON + osadzone obrazy).

## Uruchomienie i rozdawanie

Po zbudowaniu (`publish.ps1`) są dwie wersje — obie działają bez instalowania czegokolwiek
na Windows 10 2004+ / Windows 11:

- **`dist\Kasetownik9000.exe` — jeden plik (~29 MB)**, najlepszy do podania komuś dalej.
  W środku jest spakowana cała aplikacja; przy pierwszym uruchomieniu rozpakowuje się
  do `%LOCALAPPDATA%\Kasetownik9000\app\<wersja>` (ok. 2–3 s), kolejne starty są natychmiastowe.
  Nowsza wersja exe sama rozpakuje się obok i usunie starą.
  **Odinstalowanie:** po pierwszym uruchomieniu obok exe pojawia się `Kasetownik9000 - odinstaluj.cmd` —
  dwuklik, „T” i folder w `%LOCALAPPDATA%` znika (skrypt sprawdza, czy aplikacja nie jest otwarta,
  nie rusza projektów ani wydruków i na koniec usuwa sam siebie). Potem wystarczy skasować exe.
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
dopisywać do PATH.

## Jak wydrukować dobrą okładkę

1. Drukuj z aplikacji (DRUKUJ) — karta idzie w skali 1:1. Z PDF-a drukuj w skali **100% /
   „Rzeczywisty rozmiar”**, nigdy „Dopasuj do strony”.
2. Najlepszy jest papier 160–200 g/m² (matowy lub satynowy).
3. Wytnij wzdłuż znaczników cięcia (narożniki), zegnij wzdłuż znaczników zgięcia przy krawędziach —
   najlepiej po wcześniejszym nagnieceniu linii tępą stroną noża i linijką.

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
Kasetownik9000.exe projekt.kaseta --export-pdf okladka.pdf --fill
Kasetownik9000.exe --format jcard4 --starter mixtape --export-png podglad.png --dpi 600
Kasetownik9000.exe projekt.kaseta --print "Nazwa drukarki"
Kasetownik9000.exe projekt.kaseta --export-3d mockup.png --yaw -28 --pitch 24 --case on
```

Formaty: `jcard`, `jcard4`, `jcard5`, `jcard-long`, `labels`, `custom`.
Style: `retro`, `minimal`, `mixtape`, `empty`.

## Struktura

```
src/Kasetownik9000/
  Model/        dokument, elementy, formaty, style startowe (JSON z generatorem źródeł — AOT)
  Rendering/    CardRenderer (Win2D) — jeden renderer dla ekranu, PNG, PDF i druku; AssetStore
  Editor/       EditorView (płótno, uchwyty, przyciąganie, zoom), EditorSession (historia cofania),
                Preview3DView (podgląd 3D: tekstury paneli + Transform3DEffect w perspektywie)
  Output/       druk GDI (PrintDlg + StretchDIBits), PDF, rozkład na arkuszu, P/Invoke
  Controls/     ColorButton (paleta retro + próbnik), SectionHeader
  Themes/       Retro.xaml — kolory, klawisze magnetofonu, wyświetlacz LCD
src/Kasetownik9000.Launcher/   jednoplikowy starter (osadzony zip aplikacji, rozpakowanie raz na wersję)
tools/make-icon.ps1   generator ikony (kaseta) → Assets/AppIcon.ico + .png
```
