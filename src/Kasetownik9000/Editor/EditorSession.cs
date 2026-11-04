using System.Text.Json;
using Kasetownik9000.Model;
using Kasetownik9000.Rendering;

namespace Kasetownik9000.Editor;

[Flags]
public enum ChangeFlags
{
    None = 0,
    /// <summary>Trzeba przerysować płótno.</summary>
    Visual = 1,
    /// <summary>Zmieniła się lista elementów (kolejność, nazwy, dodanie/usunięcie).</summary>
    Layers = 2,
    /// <summary>Zmieniły się właściwości zaznaczonego elementu.</summary>
    Properties = 4,
    /// <summary>Zmieniły się ustawienia dokumentu (format, panele, tło).</summary>
    Document = 8,
    All = Visual | Layers | Properties | Document,
}

/// <summary>Stan otwartego projektu: dokument, obrazy, zaznaczenie, historia cofania.</summary>
public sealed class EditorSession
{
    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    private string? _lastKey;
    private DateTime _lastTime;
    private const int MaxHistory = 200;

    public CardDocument Doc { get; private set; }
    public AssetStore Assets { get; } = new();
    public string? FilePath { get; set; }
    public bool IsDirty { get; private set; }
    public CardElement? Selected { get; private set; }

    public event Action<ChangeFlags>? Changed;
    public event Action? SelectionChanged;
    public event Action? DirtyChanged;

    public EditorSession(CardDocument doc)
    {
        Doc = doc;
        Assets.BitmapLoaded += () => Changed?.Invoke(ChangeFlags.Visual);
    }

    public string DisplayName => FilePath is null ? "bez tytułu" : Path.GetFileNameWithoutExtension(FilePath);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    // ------------------------------------------------------------------ zaznaczenie

    public void Select(CardElement? e)
    {
        if (ReferenceEquals(Selected, e)) return;
        Selected = e;
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.Visual | ChangeFlags.Properties);
    }

    // ------------------------------------------------------------------ edycja i historia

    /// <summary>
    /// Wywołaj PRZED zmianą modelu. Kolejne wywołania z tym samym kluczem (w krótkim czasie)
    /// łączą się w jeden krok cofania (np. pisanie tekstu, przeciąganie suwaka).
    /// </summary>
    public void BeginEdit(string? coalesceKey = null)
    {
        var now = DateTime.UtcNow;
        bool merge = coalesceKey is not null && coalesceKey == _lastKey && (now - _lastTime).TotalSeconds < 2;
        _lastKey = coalesceKey;
        _lastTime = now;
        if (!merge)
        {
            _undo.Add(Snapshot());
            if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
            _redo.Clear();
        }
        SetDirty(true);
    }

    /// <summary>Zamyka scalanie — następna edycja zawsze tworzy nowy krok.</summary>
    public void BreakCoalescing() => _lastKey = null;

    public void NotifyChanged(ChangeFlags flags) => Changed?.Invoke(flags);

    public void Edit(string? key, Action change, ChangeFlags flags = ChangeFlags.Visual | ChangeFlags.Properties)
    {
        BeginEdit(key);
        change();
        NotifyChanged(flags);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Add(Snapshot());
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
        _lastKey = null;
        SetDirty(true);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Add(Snapshot());
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
        _lastKey = null;
        SetDirty(true);
    }

    private string Snapshot() => JsonSerializer.Serialize(Doc, ModelJson.Default.CardDocument);

    private void Restore(string json)
    {
        var selId = Selected?.Id;
        Doc = JsonSerializer.Deserialize(json, ModelJson.Default.CardDocument) ?? Doc;
        Selected = Doc.FindElement(selId);
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
    }

    public void SetDirty(bool dirty)
    {
        if (IsDirty == dirty) return;
        IsDirty = dirty;
        DirtyChanged?.Invoke();
    }

    /// <summary>Zastępuje cały dokument (np. zmiana formatu) jako jeden krok cofania.</summary>
    public void ReplaceDocument(CardDocument doc)
    {
        BeginEdit();
        var selId = Selected?.Id;
        Doc = doc;
        Selected = Doc.FindElement(selId);
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
    }

    // ------------------------------------------------------------------ operacje na elementach

    public void AddElement(CardElement e) => AddElements([e]);

    /// <summary>Dodaje elementy jako jeden krok cofania i zaznacza pierwszy z nich.</summary>
    public void AddElements(IReadOnlyList<CardElement> elements)
    {
        if (elements.Count == 0) return;
        BeginEdit();
        foreach (var e in elements)
        {
            if (string.IsNullOrEmpty(e.Name)) e.Name = UniqueName(e.KindName);
            Doc.Elements.Add(e);
        }
        Selected = elements[0];
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
    }

    public string UniqueName(string baseName)
    {
        int n = 1;
        string name;
        do name = n == 1 ? baseName : $"{baseName} {n}"; while (Doc.Elements.Exists(e => e.Name == name) && ++n < 1000);
        return name;
    }

    public void DeleteSelected()
    {
        if (Selected is null) return;
        BeginEdit();
        int idx = Doc.Elements.IndexOf(Selected);
        Doc.Elements.Remove(Selected);
        Selected = Doc.Elements.Count == 0 ? null : Doc.Elements[Math.Clamp(idx - 1, 0, Doc.Elements.Count - 1)];
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
    }

    public CardElement CloneElement(CardElement e)
    {
        var json = JsonSerializer.Serialize(e, ModelJson.Default.CardElement);
        var copy = JsonSerializer.Deserialize(json, ModelJson.Default.CardElement)!;
        copy.Id = CardElement.NewId();
        return copy;
    }

    public void DuplicateSelected()
    {
        if (Selected is null) return;
        var copy = CloneElement(Selected);
        copy.X += 3;
        copy.Y += 3;
        copy.Locked = false;
        copy.Name = UniqueName(Selected.Name.Length > 0 ? Selected.Name : copy.KindName);
        BeginEdit();
        Doc.Elements.Insert(Doc.Elements.IndexOf(Selected) + 1, copy);
        Selected = copy;
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
    }

    public enum Order { Front, Forward, Backward, Back }

    public void Reorder(Order order)
    {
        var e = Selected;
        if (e is null) return;
        var list = Doc.Elements;
        int i = list.IndexOf(e);
        int target = order switch
        {
            Order.Front => list.Count - 1,
            Order.Forward => Math.Min(list.Count - 1, i + 1),
            Order.Backward => Math.Max(0, i - 1),
            _ => 0,
        };
        if (target == i) return;
        BeginEdit();
        list.RemoveAt(i);
        list.Insert(target, e);
        Changed?.Invoke(ChangeFlags.Visual | ChangeFlags.Layers);
    }

    // ------------------------------------------------------------------ zapis / odczyt

    public async Task SaveAsync(string path)
    {
        var file = new KasetaFile { Card = Doc };
        foreach (var id in UsedAssetIds())
        {
            var bytes = Assets.GetBytes(id);
            if (bytes is not null) file.Assets[id] = Convert.ToBase64String(bytes);
        }
        var tmp = path + ".tmp";
        await using (var fs = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(fs, file, ModelJson.Default.KasetaFile);
        }
        File.Move(tmp, path, overwrite: true);
        FilePath = path;
        _lastKey = null;
        SetDirty(false);
    }

    public static async Task<EditorSession> LoadAsync(string path)
    {
        await using var fs = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync(fs, ModelJson.Default.KasetaFile)
                   ?? throw new InvalidDataException("Pusty plik projektu.");
        if (file.Card.Panels.Count == 0) throw new InvalidDataException("Plik nie zawiera karty.");
        var s = new EditorSession(file.Card) { FilePath = path };
        foreach (var (id, b64) in file.Assets)
            s.Assets.Put(id, Convert.FromBase64String(b64));
        return s;
    }

    public IEnumerable<string> UsedAssetIds() =>
        Doc.Elements.OfType<ImageElement>().Select(i => i.AssetId).Where(id => id.Length > 0).Distinct();
}
