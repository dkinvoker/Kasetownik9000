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
    /// <summary>Zmieniły się ustawienia karty (format, panele, tło).</summary>
    Document = 8,
    /// <summary>Zmieniła się lista kart albo aktywna karta.</summary>
    Cards = 16,
    /// <summary>Zmieniły się dane albumu lub plan taśmy.</summary>
    Project = 32,
    All = Visual | Layers | Properties | Document | Cards | Project,
}

/// <summary>Stan otwartego projektu: karty, dane albumu, obrazy, zaznaczenie, historia cofania.</summary>
public sealed class EditorSession
{
    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    private string? _lastKey;
    private DateTime _lastTime;
    private const int MaxHistory = 200;

    public CardProject Project { get; private set; }
    public int ActiveIndex { get; private set; }
    public AssetStore Assets { get; } = new();
    public string? FilePath { get; set; }
    public bool IsDirty { get; private set; }
    public CardElement? Selected { get; private set; }

    /// <summary>Aktywna (edytowana) karta.</summary>
    public CardDocument Doc => Project.Cards[ActiveIndex];

    public TextResolver Resolver { get; private set; }

    public event Action<ChangeFlags>? Changed;
    public event Action? SelectionChanged;
    public event Action? DirtyChanged;

    public EditorSession(CardProject project)
    {
        if (project.Cards.Count == 0) project.Cards.Add(CardFormats.Get("jcard").CreateDocument());
        Project = project;
        Resolver = new TextResolver(project);
        Assets.BitmapLoaded += () => Changed?.Invoke(ChangeFlags.Visual);
    }

    public string DisplayName => FilePath is null ? "bez tytułu" : Path.GetFileNameWithoutExtension(FilePath);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    // ------------------------------------------------------------------ zaznaczenie i karty

    public void Select(CardElement? e)
    {
        if (ReferenceEquals(Selected, e)) return;
        Selected = e;
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.Visual | ChangeFlags.Properties);
    }

    public void SelectCard(int index)
    {
        index = Math.Clamp(index, 0, Project.Cards.Count - 1);
        if (index == ActiveIndex) return;
        ActiveIndex = index;
        Selected = null;
        _lastKey = null;
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
    }

    public void AddCard(CardDocument card)
    {
        BeginEdit();
        Project.Cards.Add(card);
        ActiveIndex = Project.Cards.Count - 1;
        Selected = null;
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
    }

    public void RemoveCard(int index)
    {
        if (Project.Cards.Count <= 1 || index < 0 || index >= Project.Cards.Count) return;
        BeginEdit();
        Project.Cards.RemoveAt(index);
        ActiveIndex = Math.Clamp(ActiveIndex >= index ? ActiveIndex - 1 : ActiveIndex, 0, Project.Cards.Count - 1);
        Selected = null;
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
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

    /// <summary>Zmiana danych albumu / planu taśmy (wpływa na teksty na wszystkich kartach).</summary>
    public void EditProject(string? key, Action<CardProject> change) =>
        Edit(key is null ? null : "project:" + key, () => change(Project), ChangeFlags.Visual | ChangeFlags.Project);

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

    private string Snapshot() => JsonSerializer.Serialize(Project, ModelJson.Default.CardProject);

    private void Restore(string json)
    {
        var selId = Selected?.Id;
        Project = JsonSerializer.Deserialize(json, ModelJson.Default.CardProject) ?? Project;
        Resolver = new TextResolver(Project);
        ActiveIndex = Math.Clamp(ActiveIndex, 0, Project.Cards.Count - 1);
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
        var file = new KasetaFile { Project = Project, AppVersion = AppInfo.Version };
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

        if (file.Project is not { Cards.Count: > 0 } project)
            throw new InvalidDataException("To nie jest projekt Kasetownika 1.5+ (pliki z wersji 1.0 nie są już obsługiwane) albo plik jest uszkodzony.");

        var s = new EditorSession(project) { FilePath = path };
        foreach (var (id, b64) in file.Assets)
            s.Assets.Put(id, Convert.FromBase64String(b64));
        return s;
    }

    /// <summary>Obrazy używane na wszystkich kartach projektu.</summary>
    public IEnumerable<string> UsedAssetIds() =>
        Project.Cards.SelectMany(c => c.Elements).OfType<ImageElement>().Select(i => i.AssetId).Where(id => id.Length > 0).Distinct();
}
