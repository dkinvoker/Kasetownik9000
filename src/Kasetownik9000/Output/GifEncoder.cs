namespace Kasetownik9000.Output;

/// <summary>
/// Prosty koder animowanych GIF-ów: wspólna paleta 256 kolorów (median cut z próbek klatek),
/// lekki dithering Bayera przeciw pasmom w gradientach, LZW i zapis tylko zmienionego fragmentu klatki.
/// </summary>
public sealed class GifEncoder
{
    private readonly Stream _out;
    private readonly int _w, _h;
    private readonly byte[] _palette; // 256 × RGB
    private readonly short[] _cache = new short[1 << 15];
    private readonly int[] _table = new int[4096 * 256];
    private int _generation;
    private byte[]? _previous;

    private static readonly int[] Bayer4 = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    public GifEncoder(Stream output, int width, int height, byte[] palette)
    {
        _out = output;
        _w = width;
        _h = height;
        _palette = palette;
        Array.Fill(_cache, (short)-1);

        Write("GIF89a"u8);
        WriteU16(width);
        WriteU16(height);
        _out.WriteByte(0xF7); // globalna paleta 256 kolorów
        _out.WriteByte(0);
        _out.WriteByte(0);
        _out.Write(palette);
        // NETSCAPE2.0 — zapętlanie w nieskończoność
        Write([0x21, 0xFF, 0x0B]);
        Write("NETSCAPE2.0"u8);
        Write([0x03, 0x01, 0x00, 0x00, 0x00]);
    }

    /// <summary>Dodaje klatkę (piksele BGRA, wiersze od góry). Opóźnienie w setnych sekundy.</summary>
    public void AddFrame(byte[] bgra, int delayCs)
    {
        var idx = new byte[_w * _h];
        for (int y = 0, i = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++, i++)
            {
                int d = Bayer4[((y & 3) << 2) | (x & 3)] - 8; // -8..7
                int b = Clamp(bgra[i * 4] + d / 2), g = Clamp(bgra[i * 4 + 1] + d / 2), r = Clamp(bgra[i * 4 + 2] + d / 2);
                idx[i] = Nearest(r, g, b);
            }
        }

        // tylko prostokąt, który różni się od poprzedniej klatki
        int left = 0, top = 0, right = _w - 1, bottom = _h - 1;
        if (_previous is not null)
        {
            left = _w; top = _h; right = -1; bottom = -1;
            for (int y = 0, i = 0; y < _h; y++)
                for (int x = 0; x < _w; x++, i++)
                {
                    if (idx[i] == _previous[i]) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            if (right < 0) { left = top = right = bottom = 0; }
        }
        _previous = idx;

        int rw = right - left + 1, rh = bottom - top + 1;
        var rect = new byte[rw * rh];
        for (int y = 0; y < rh; y++) Buffer.BlockCopy(idx, (top + y) * _w + left, rect, y * rw, rw);

        Write([0x21, 0xF9, 0x04, 0x04]); // rozszerzenie sterowania: nie usuwaj poprzedniej klatki
        WriteU16(delayCs);
        Write([0x00, 0x00]);
        _out.WriteByte(0x2C);
        WriteU16(left);
        WriteU16(top);
        WriteU16(rw);
        WriteU16(rh);
        _out.WriteByte(0);
        WriteLzw(rect);
    }

    public void Finish() => _out.WriteByte(0x3B);

    private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    private byte Nearest(int r, int g, int b)
    {
        int key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
        var cached = _cache[key];
        if (cached >= 0) return (byte)cached;
        int cr = (r & 0xF8) | 4, cg = (g & 0xF8) | 4, cb = (b & 0xF8) | 4;
        int best = 0, bestDist = int.MaxValue;
        for (int i = 0; i < 256; i++)
        {
            int dr = _palette[i * 3] - cr, dg = _palette[i * 3 + 1] - cg, db = _palette[i * 3 + 2] - cb;
            int dist = dr * dr * 3 + dg * dg * 4 + db * db * 2;
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        _cache[key] = (short)best;
        return (byte)best;
    }

    // ------------------------------------------------------------------ LZW (wg sprawdzonego schematu z gif.h)

    private void WriteLzw(byte[] data)
    {
        const int minCodeSize = 8, clear = 256;
        _out.WriteByte(minCodeSize);
        var bits = new SubBlockWriter(_out);
        var table = _table;
        int generation = ++_generation; // zamiast czyścić 4 MB tablicy — nowy "numer pokolenia" wpisów
        int codeSize = minCodeSize + 1, maxCode = clear + 1;

        bits.Write(clear, codeSize);
        int cur = data[0];
        for (int i = 1; i < data.Length; i++)
        {
            int next = data[i];
            int key = (cur << 8) | next;
            int v = table[key];
            if (v >> 12 == generation)
            {
                cur = v & 0xFFF;
                continue;
            }
            bits.Write(cur, codeSize);
            table[key] = (generation << 12) | ++maxCode;
            if (maxCode >= 1 << codeSize) codeSize++;
            if (maxCode == 4095)
            {
                bits.Write(clear, codeSize);
                generation = ++_generation;
                codeSize = minCodeSize + 1;
                maxCode = clear + 1;
            }
            cur = next;
        }
        bits.Write(cur, codeSize);
        bits.Write(clear + 1, codeSize); // koniec danych
        bits.Flush();
        _out.WriteByte(0);
    }

    private sealed class SubBlockWriter(Stream s)
    {
        private readonly byte[] _block = new byte[255];
        private int _count, _acc, _accBits;

        public void Write(int code, int size)
        {
            _acc |= code << _accBits;
            _accBits += size;
            while (_accBits >= 8)
            {
                Push((byte)(_acc & 0xFF));
                _acc >>= 8;
                _accBits -= 8;
            }
        }

        private void Push(byte b)
        {
            _block[_count++] = b;
            if (_count == 255) Emit();
        }

        private void Emit()
        {
            if (_count == 0) return;
            s.WriteByte((byte)_count);
            s.Write(_block, 0, _count);
            _count = 0;
        }

        public void Flush()
        {
            if (_accBits > 0) Push((byte)(_acc & 0xFF));
            _acc = _accBits = 0;
            Emit();
        }
    }

    private void Write(ReadOnlySpan<byte> bytes) => _out.Write(bytes);

    private void WriteU16(int v)
    {
        _out.WriteByte((byte)(v & 0xFF));
        _out.WriteByte((byte)(v >> 8));
    }

    // ------------------------------------------------------------------ paleta (median cut)

    private sealed class Box
    {
        public int Start, End, Axis, Range;
    }

    /// <summary>Wylicza paletę 256 kolorów z próbek (BGRA) kilku klatek.</summary>
    public static byte[] BuildPalette(IEnumerable<byte[]> frames, int step = 3)
    {
        var px = new List<int>();
        foreach (var f in frames)
            for (int i = 0; i + 3 < f.Length; i += 4 * step)
                px.Add((f[i + 2] << 16) | (f[i + 1] << 8) | f[i]);
        if (px.Count == 0) px.Add(0);

        var arr = px.ToArray();
        var boxes = new List<Box> { Measure(arr, 0, arr.Length) };
        while (boxes.Count < 256)
        {
            Box? pick = null;
            long bestScore = 0;
            foreach (var b in boxes)
            {
                long score = (long)(b.End - b.Start) * b.Range;
                if (b.End - b.Start > 1 && b.Range > 0 && score > bestScore) { bestScore = score; pick = b; }
            }
            if (pick is null) break;
            int shift = pick.Axis * 8;
            Array.Sort(arr, pick.Start, pick.End - pick.Start, Comparer<int>.Create((a, b) => ((a >> shift) & 0xFF) - ((b >> shift) & 0xFF)));
            int mid = (pick.Start + pick.End) / 2;
            boxes.Remove(pick);
            boxes.Add(Measure(arr, pick.Start, mid));
            boxes.Add(Measure(arr, mid, pick.End));
        }

        var palette = new byte[256 * 3];
        for (int i = 0; i < boxes.Count; i++)
        {
            long r = 0, g = 0, b = 0;
            var box = boxes[i];
            for (int j = box.Start; j < box.End; j++)
            {
                r += (arr[j] >> 16) & 0xFF;
                g += (arr[j] >> 8) & 0xFF;
                b += arr[j] & 0xFF;
            }
            int n = Math.Max(1, box.End - box.Start);
            palette[i * 3] = (byte)(r / n);
            palette[i * 3 + 1] = (byte)(g / n);
            palette[i * 3 + 2] = (byte)(b / n);
        }
        return palette;
    }

    private static Box Measure(int[] arr, int start, int end)
    {
        int rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
        for (int i = start; i < end; i++)
        {
            int r = (arr[i] >> 16) & 0xFF, g = (arr[i] >> 8) & 0xFF, b = arr[i] & 0xFF;
            if (r < rMin) rMin = r;
            if (r > rMax) rMax = r;
            if (g < gMin) gMin = g;
            if (g > gMax) gMax = g;
            if (b < bMin) bMin = b;
            if (b > bMax) bMax = b;
        }
        int rr = rMax - rMin, gr = gMax - gMin, br = bMax - bMin;
        // oś o największym rozrzucie (zielony liczony mocniej — oko jest na niego czulsze)
        if (gr * 4 >= rr * 3 && gr * 4 >= br * 2 * 2) return new Box { Start = start, End = end, Axis = 1, Range = gr };
        return rr * 3 >= br * 2
            ? new Box { Start = start, End = end, Axis = 2, Range = rr }
            : new Box { Start = start, End = end, Axis = 0, Range = br };
    }
}
