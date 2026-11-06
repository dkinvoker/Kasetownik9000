using System.Text;
using System.Text.Json;

namespace Kasetownik9000.Model;

public enum LayerOrder { Front, Forward, Backward, Back }

public enum AlignMode { Left, CenterX, Right, Top, CenterY, Bottom }

/// <summary>
/// Operacje na kilku elementach naraz: grupy, kolejność warstw, wyrównywanie, rozmieszczanie, duplikowanie,
/// skalowanie i obrót zestawu. Czyste funkcje na modelu (bez UI i historii cofania) — łatwo je testować.
/// </summary>
public static class ElementOps
{
    // ------------------------------------------------------------------ grupy

    /// <summary>Elementy tej samej grupy co <paramref name="e"/> (w kolejności warstw), albo sam element.</summary>
    public static List<CardElement> GroupMembers(CardDocument doc, CardElement e) =>
        e.GroupId is null ? [e] : doc.Elements.Where(x => x.GroupId == e.GroupId).ToList();

    /// <summary>Dopełnia zaznaczenie do całych grup; wynik w kolejności warstw (od spodu).</summary>
    public static List<CardElement> ExpandToGroups(CardDocument doc, IEnumerable<CardElement> elements)
    {
        var groups = elements.Select(e => e.GroupId).Where(g => g is not null).ToHashSet();
        var set = elements.ToHashSet();
        return doc.Elements.Where(e => set.Contains(e) || (e.GroupId is not null && groups.Contains(e.GroupId))).ToList();
    }

    /// <summary>W kolejności warstw (od spodu) — tylko elementy, które są na karcie.</summary>
    public static List<CardElement> InLayerOrder(CardDocument doc, IEnumerable<CardElement> elements)
    {
        var set = elements.ToHashSet();
        return doc.Elements.Where(set.Contains).ToList();
    }

    /// <summary>Czy zaznaczenie to dokładnie jedna cała grupa.</summary>
    public static ElementGroup? WholeGroup(CardDocument doc, IReadOnlyCollection<CardElement> selection)
    {
        if (selection.Count < 2) return null;
        var id = selection.First().GroupId;
        if (id is null || selection.Any(e => e.GroupId != id)) return null;
        return doc.Elements.Count(e => e.GroupId == id) == selection.Count ? doc.FindGroup(id) : null;
    }

    public static string UniqueGroupName(CardDocument doc, string baseName = "Grupa")
    {
        for (int n = 1; ; n++)
        {
            var name = $"{baseName} {n}";
            if (!doc.Groups.Exists(g => g.Name == name)) return name;
        }
    }

    /// <summary>Łączy elementy w nową grupę (co najmniej dwa). Wcześniejsze grupy tych elementów są rozwiązywane.</summary>
    public static ElementGroup? Group(CardDocument doc, IEnumerable<CardElement> elements)
    {
        var list = InLayerOrder(doc, elements);
        if (list.Count < 2) return null;
        var group = new ElementGroup { Name = UniqueGroupName(doc) };
        doc.Groups.Add(group);
        foreach (var e in list) e.GroupId = group.Id;
        doc.NormalizeGroups();
        return group;
    }

    /// <summary>Wyjmuje elementy z ich grup (puste i jednoelementowe grupy znikają).</summary>
    public static void Ungroup(CardDocument doc, IEnumerable<CardElement> elements)
    {
        foreach (var e in elements) e.GroupId = null;
        doc.NormalizeGroups();
    }

    // ------------------------------------------------------------------ dodawanie / usuwanie / kopie

    public static CardElement Clone(CardElement e)
    {
        var json = JsonSerializer.Serialize(e, ModelJson.Default.CardElement);
        var copy = JsonSerializer.Deserialize(json, ModelJson.Default.CardElement)!;
        copy.Id = CardElement.NewId();
        return copy;
    }

    public static string UniqueName(CardDocument doc, string baseName)
    {
        int n = 1;
        string name;
        do name = n == 1 ? baseName : $"{baseName} {n}"; while (doc.Elements.Exists(e => e.Name == name) && ++n < 1000);
        return name;
    }

    public static void Delete(CardDocument doc, IEnumerable<CardElement> elements)
    {
        foreach (var e in elements.ToList()) doc.Elements.Remove(e);
        doc.NormalizeGroups();
    }

    /// <summary>
    /// Kopie elementów (z przesunięciem) wstawione nad najwyższym z nich, w tej samej kolejności.
    /// Grupa skopiowana w całości dostaje nową grupę „… (kopia)”; kopia części grupy jest bez grupy.
    /// </summary>
    public static List<CardElement> Duplicate(CardDocument doc, IEnumerable<CardElement> elements, double dx, double dy)
    {
        var list = InLayerOrder(doc, elements);
        if (list.Count == 0) return [];
        var copies = Prepare(doc, list, doc.Groups, dx, dy);
        int at = doc.Elements.IndexOf(list[^1]) + 1;
        doc.Elements.InsertRange(at, copies);
        doc.NormalizeGroups();
        return copies;
    }

    /// <summary>
    /// Przygotowuje elementy do wstawienia (wklejanie, duplikowanie): nowe id, unikalne nazwy, odblokowane,
    /// przesunięte; grupy skopiowane w całości dostają nowe grupy (dodane do karty), resztę grup się pomija.
    /// </summary>
    public static List<CardElement> Prepare(CardDocument doc, IReadOnlyList<CardElement> source,
        IReadOnlyList<ElementGroup>? sourceGroups, double dx, double dy, bool copyNames = true)
    {
        var copies = new List<CardElement>(source.Count);
        var groupMap = new Dictionary<string, string?>();
        foreach (var e in source)
        {
            var c = Clone(e);
            c.X += dx;
            c.Y += dy;
            c.Locked = false;
            if (e.GroupId is { } gid)
            {
                if (!groupMap.TryGetValue(gid, out var newId))
                {
                    // nowa grupa tylko wtedy, gdy kopiujemy co najmniej dwa jej elementy
                    newId = source.Count(x => x.GroupId == gid) >= 2 ? CardElement.NewId() : null;
                    if (newId is not null)
                    {
                        var name = sourceGroups?.FirstOrDefault(g => g.Id == gid)?.Name;
                        doc.Groups.Add(new ElementGroup { Id = newId, Name = name is null ? UniqueGroupName(doc) : $"{name} (kopia)" });
                    }
                    groupMap[gid] = newId;
                }
                c.GroupId = newId;
            }
            copies.Add(c);
        }
        foreach (var c in copies)
        {
            var baseName = string.IsNullOrWhiteSpace(c.Name) ? c.KindName : c.Name;
            if (copyNames) c.Name = UniqueName(doc, baseName);
            doc.Elements.Add(c); // chwilowo — żeby kolejne nazwy były unikalne także względem siebie
        }
        foreach (var c in copies) doc.Elements.Remove(c);
        return copies;
    }

    // ------------------------------------------------------------------ kolejność warstw

    /// <summary>
    /// Przesuwa zaznaczenie w stosie warstw. Całe grupy poruszają się jak jeden element (nie rozrywamy ich);
    /// gdy zaznaczona jest część jednej grupy — kolejność zmienia się tylko wewnątrz tej grupy.
    /// </summary>
    public static void Reorder(CardDocument doc, IEnumerable<CardElement> selection, LayerOrder order)
    {
        var sel = selection.ToHashSet();
        if (sel.Count == 0) return;
        var all = doc.Elements;

        // zaznaczenie wewnątrz jednej grupy (np. jeden jej element) — przestawiamy tylko w obrębie grupy
        var gid = sel.First().GroupId;
        if (gid is not null && sel.All(e => e.GroupId == gid) && all.Count(e => e.GroupId == gid) > sel.Count)
        {
            int first = all.FindIndex(e => e.GroupId == gid);
            var members = all.Where(e => e.GroupId == gid).Select(e => new List<CardElement> { e }).ToList();
            var reordered = ReorderUnits(members, u => sel.Contains(u[0]), order);
            all.RemoveRange(first, members.Count);
            all.InsertRange(first, reordered.SelectMany(u => u));
            return;
        }

        // jednostki: cała grupa albo pojedynczy element
        var units = new List<List<CardElement>>();
        foreach (var e in all)
        {
            if (e.GroupId is not null && units.Count > 0 && units[^1][0].GroupId == e.GroupId) units[^1].Add(e);
            else units.Add([e]);
        }
        var result = ReorderUnits(units, u => u.Any(sel.Contains), order);
        all.Clear();
        all.AddRange(result.SelectMany(u => u));
    }

    private static List<List<CardElement>> ReorderUnits(List<List<CardElement>> units, Func<List<CardElement>, bool> isSelected, LayerOrder order)
    {
        var list = units.ToList();
        switch (order)
        {
            case LayerOrder.Front:
                return [.. list.Where(u => !isSelected(u)), .. list.Where(isSelected)];
            case LayerOrder.Back:
                return [.. list.Where(isSelected), .. list.Where(u => !isSelected(u))];
            case LayerOrder.Forward:
                for (int i = list.Count - 2; i >= 0; i--)
                    if (isSelected(list[i]) && !isSelected(list[i + 1])) (list[i], list[i + 1]) = (list[i + 1], list[i]);
                return list;
            default:
                for (int i = 1; i < list.Count; i++)
                    if (isSelected(list[i]) && !isSelected(list[i - 1])) (list[i], list[i - 1]) = (list[i - 1], list[i]);
                return list;
        }
    }

    // ------------------------------------------------------------------ geometria zestawu

    /// <summary>Prostokąt otaczający element po obrocie (w mm).</summary>
    public static MmRect Bounds(CardElement e)
    {
        double a = e.Rotation * Math.PI / 180, c = Math.Abs(Math.Cos(a)), s = Math.Abs(Math.Sin(a));
        double w = e.Width * c + e.Height * s, h = e.Width * s + e.Height * c;
        return new MmRect(e.CenterX - w / 2, e.CenterY - h / 2, w, h);
    }

    /// <summary>Prostokąt otaczający kilka elementów.</summary>
    public static MmRect Bounds(IEnumerable<CardElement> elements)
    {
        double l = double.MaxValue, t = double.MaxValue, r = double.MinValue, b = double.MinValue;
        foreach (var e in elements)
        {
            var bb = Bounds(e);
            l = Math.Min(l, bb.X); t = Math.Min(t, bb.Y);
            r = Math.Max(r, bb.Right); b = Math.Max(b, bb.Bottom);
        }
        return l > r ? default : new MmRect(l, t, r - l, b - t);
    }

    /// <summary>
    /// Jednostki do wyrównywania/rozmieszczania: całe grupy zachowują swój wewnętrzny układ. Wyjątek: zaznaczona
    /// jest jedna grupa — wtedy wyrównuje się jej elementy względem siebie.
    /// </summary>
    public static List<List<CardElement>> Units(CardDocument doc, IReadOnlyCollection<CardElement> selection)
    {
        var ordered = InLayerOrder(doc, selection).Where(e => !e.Locked).ToList();
        if (WholeGroup(doc, selection) is not null) return ordered.Select(e => new List<CardElement> { e }).ToList();
        var units = new List<List<CardElement>>();
        foreach (var e in ordered)
        {
            var u = e.GroupId is null ? null : units.Find(x => x[0].GroupId == e.GroupId);
            if (u is null) units.Add([e]); else u.Add(e);
        }
        return units;
    }

    public static void Translate(IEnumerable<CardElement> elements, double dx, double dy)
    {
        foreach (var e in elements)
        {
            e.X = Math.Round(e.X + dx, 2);
            e.Y = Math.Round(e.Y + dy, 2);
        }
    }

    /// <summary>Wyrównuje jednostki zaznaczenia do wspólnej krawędzi / środka prostokąta otaczającego.</summary>
    public static void Align(CardDocument doc, IReadOnlyCollection<CardElement> selection, AlignMode mode)
    {
        var units = Units(doc, selection);
        if (units.Count < 2) return;
        var all = Bounds(units.SelectMany(u => u));
        foreach (var u in units)
        {
            var b = Bounds(u);
            double dx = mode switch
            {
                AlignMode.Left => all.X - b.X,
                AlignMode.CenterX => all.CenterX - b.CenterX,
                AlignMode.Right => all.Right - b.Right,
                _ => 0,
            };
            double dy = mode switch
            {
                AlignMode.Top => all.Y - b.Y,
                AlignMode.CenterY => all.CenterY - b.CenterY,
                AlignMode.Bottom => all.Bottom - b.Bottom,
                _ => 0,
            };
            Translate(u, dx, dy);
        }
    }

    /// <summary>Rozmieszcza jednostki (co najmniej trzy) tak, by odstępy między nimi były równe.</summary>
    public static void Distribute(CardDocument doc, IReadOnlyCollection<CardElement> selection, bool horizontal)
    {
        var units = Units(doc, selection);
        if (units.Count < 3) return;
        var boxes = units.Select(u => (Unit: u, Box: Bounds(u)))
                         .OrderBy(x => horizontal ? x.Box.CenterX : x.Box.CenterY).ToList();
        double start = horizontal ? boxes[0].Box.X : boxes[0].Box.Y;
        double end = horizontal ? boxes[^1].Box.Right : boxes[^1].Box.Bottom;
        double sizes = boxes.Sum(x => horizontal ? x.Box.W : x.Box.H);
        double gap = (end - start - sizes) / (boxes.Count - 1);
        double pos = start;
        foreach (var (unit, box) in boxes)
        {
            double d = pos - (horizontal ? box.X : box.Y);
            if (horizontal) Translate(unit, d, 0); else Translate(unit, 0, d);
            pos += (horizontal ? box.W : box.H) + gap;
        }
    }

    /// <summary>
    /// Skalowanie zestawu względem punktu <paramref name="anchorX"/>/<paramref name="anchorY"/>: położenie, rozmiar,
    /// a także czcionki, grubości linii, zaokrąglenia, odstępy — tak, by grupa wyglądała jak powiększona całość.
    /// Liczone od stanu początkowego (<paramref name="start"/>), żeby przeciąganie nie kumulowało błędów.
    /// </summary>
    public static void ApplyScale(CardElement target, CardElement start, double anchorX, double anchorY, double s)
    {
        double cx = anchorX + (start.CenterX - anchorX) * s, cy = anchorY + (start.CenterY - anchorY) * s;
        target.Width = Math.Round(start.Width * s, 2);
        target.Height = Math.Round(start.Height * s, 2);
        target.X = Math.Round(cx - target.Width / 2, 2);
        target.Y = Math.Round(cy - target.Height / 2, 2);
        switch (target, start)
        {
            case (TextElement t, TextElement t0):
                t.FontSize = Math.Round(t0.FontSize * s, 2);
                t.ShadowOffset = Math.Round(t0.ShadowOffset * s, 3);
                break;
            case (ShapeElement sh, ShapeElement sh0):
                sh.StrokeWidth = Math.Round(sh0.StrokeWidth * s, 3);
                sh.CornerRadius = Math.Round(sh0.CornerRadius * s, 3);
                break;
            case (StripesElement st, StripesElement st0):
                st.Gap = Math.Round(st0.Gap * s, 3);
                break;
        }
    }

    /// <summary>Obrót elementu wokół wspólnego środka zestawu (o <paramref name="degrees"/> od stanu początkowego).</summary>
    public static void ApplyRotation(CardElement target, CardElement start, double centerX, double centerY, double degrees)
    {
        double a = degrees * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        double dx = start.CenterX - centerX, dy = start.CenterY - centerY;
        double cx = centerX + dx * cos - dy * sin, cy = centerY + dx * sin + dy * cos;
        target.X = Math.Round(cx - start.Width / 2, 2);
        target.Y = Math.Round(cy - start.Height / 2, 2);
        double rot = (start.Rotation + degrees) % 360;
        if (rot > 180) rot -= 360;
        if (rot <= -180) rot += 360;
        target.Rotation = Math.Round(rot, 2);
    }

    // ------------------------------------------------------------------ schowek

    /// <summary>
    /// Dane własnego formatu ze schowka jako tekst. Windows oddaje je jako bajty: UTF-8 (nasz zapis)
    /// albo UTF-16 (gdy ktoś zapisał string). JSON zaczyna się od '{', więc w UTF-16 drugi bajt to zero.
    /// </summary>
    public static string DecodeClipboardText(byte[] bytes)
    {
        var text = bytes.Length >= 2 && bytes[1] == 0 ? Encoding.Unicode.GetString(bytes) : Encoding.UTF8.GetString(bytes);
        return text.TrimStart('﻿').TrimEnd('\0');
    }
}
