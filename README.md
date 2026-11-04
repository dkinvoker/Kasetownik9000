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
- Projekt zapisywany jako jeden plik `.kaseta` (JSON + osadzone obrazy).

## Uruchomienie

Gotowa wersja po zbudowaniu: `publish\win-x64\Kasetownik9000.exe` (cały folder można skopiować
na inny komputer z Windows 10 1809+ / Windows 11 — nic nie trzeba instalować).

## Budowanie

Wymagania: **.NET 10 SDK** oraz (tylko dla Native AOT) **Visual Studio Build Tools** z obciążeniem
„Programowanie aplikacji klasycznych w C++”.

```bash
dotnet build src/Kasetownik9000/Kasetownik9000.csproj
```

```bash
powershell -ExecutionPolicy Bypass -File publish.ps1
```

`publish.ps1` robi `dotnet publish -c Release -r win-x64` z Native AOT. Projekt zawiera obejście błędu
VS Build Tools 18, w którym linker AOT nie znajdował `vswhere.exe` — nie trzeba nic dopisywać do PATH.

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
```

Formaty: `jcard`, `jcard4`, `jcard5`, `jcard-long`, `labels`, `custom`.
Style: `retro`, `minimal`, `mixtape`, `empty`.

## Struktura

```
src/Kasetownik9000/
  Model/        dokument, elementy, formaty, style startowe (JSON z generatorem źródeł — AOT)
  Rendering/    CardRenderer (Win2D) — jeden renderer dla ekranu, PNG, PDF i druku; AssetStore
  Editor/       EditorView (płótno, uchwyty, przyciąganie, zoom), EditorSession (historia cofania)
  Output/       druk GDI (PrintDlg + StretchDIBits), PDF, rozkład na arkuszu, P/Invoke
  Controls/     ColorButton (paleta retro + próbnik), SectionHeader
  Themes/       Retro.xaml — kolory, klawisze magnetofonu, wyświetlacz LCD
tools/make-icon.ps1   generator ikony (kaseta) → Assets/AppIcon.ico + .png
```
