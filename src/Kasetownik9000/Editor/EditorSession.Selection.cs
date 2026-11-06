using Kasetownik9000.Model;

namespace Kasetownik9000.Editor;

/// <summary>Zaznaczenie (jeden albo wiele elementów) i operacje na nim — z historią cofania.</summary>
public sealed partial class EditorSession
{
    private readonly List<CardElement> _selection = [];

    /// <summary>Zaznaczone elementy (w kolejności warstw, od spodu).</summary>
    public IReadOnlyList<CardElement> Selection => _selection;

    /// <summary>Jedyny zaznaczony element — do panelu właściwości; przy kilku zaznaczonych null.</summary>
    public CardElement? Selected => _selection.Count == 1 ? _selection[0] : null;

    public bool HasSelection => _selection.Count > 0;

    public bool IsSelected(CardElement e) => _selection.Contains(e);

    /// <summary>Zaznaczona jest dokładnie jedna cała grupa (wtedy panel pokazuje jej nazwę).</summary>
    public ElementGroup? SelectedGroup => ElementOps.WholeGroup(Doc, _selection);

    /// <summary>Zaznacza jeden element (null = nic).</summary>
    public void Select(CardElement? e) => SetSelection(e is null ? [] : [e]);

    /// <summary>Zaznacza element razem z jego grupą — tak działa kliknięcie na płótnie.</summary>
    public void SelectWithGroup(CardElement e) => SetSelection(ElementOps.GroupMembers(Doc, e));

    /// <summary>Shift/Ctrl + klik: dodaje element (z grupą) do zaznaczenia albo go z niego usuwa.</summary>
    public void ToggleWithGroup(CardElement e)
    {
        var members = ElementOps.GroupMembers(Doc, e);
        var next = _selection.ToList();
        if (members.All(_selection.Contains)) next.RemoveAll(members.Contains);
        else next.AddRange(members.Where(m => !next.Contains(m)));
        SetSelection(next);
    }

    public void SelectAll() => SetSelection(Doc.Elements.Where(e => !e.Hidden));

    public void SetSelection(IEnumerable<CardElement> elements)
    {
        var next = ElementOps.InLayerOrder(Doc, elements);
        if (next.SequenceEqual(_selection)) return;
        _selection.Clear();
        _selection.AddRange(next);
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.Visual | ChangeFlags.Properties);
    }

    /// <summary>Zaznaczone elementy, którymi wolno ruszać (bez zablokowanych).</summary>
    public List<CardElement> MovableSelection() => _selection.Where(e => !e.Locked).ToList();

    // ------------------------------------------------------------------ operacje

    private void AfterStructureChange(IEnumerable<CardElement>? select = null)
    {
        var next = select is null ? _selection.Where(Doc.Elements.Contains).ToList() : ElementOps.InLayerOrder(Doc, select);
        _selection.Clear();
        _selection.AddRange(next);
        SelectionChanged?.Invoke();
        Changed?.Invoke(ChangeFlags.All);
    }

    public void DeleteSelected()
    {
        if (_selection.Count == 0) return;
        BeginEdit();
        int idx = Doc.Elements.IndexOf(_selection[0]);
        ElementOps.Delete(Doc, _selection);
        // po usunięciu jednego elementu zaznacz sąsiada (wygodne przy kasowaniu kilku po kolei)
        var next = _selection.Count == 1 && Doc.Elements.Count > 0 ? [Doc.Elements[Math.Clamp(idx - 1, 0, Doc.Elements.Count - 1)]] : new List<CardElement>();
        AfterStructureChange(next);
    }

    public void DuplicateSelected()
    {
        if (_selection.Count == 0) return;
        BeginEdit();
        var copies = ElementOps.Duplicate(Doc, _selection, 3, 3);
        AfterStructureChange(copies);
    }

    /// <summary>Wkleja elementy (nowe id, nazwy i grupy; przesunięte) na wierzch i je zaznacza.</summary>
    public void PasteElements(IReadOnlyList<CardElement> source, IReadOnlyList<ElementGroup>? groups, double dx, double dy)
    {
        if (source.Count == 0) return;
        BeginEdit();
        var copies = ElementOps.Prepare(Doc, source, groups, dx, dy);
        Doc.Elements.AddRange(copies);
        Doc.NormalizeGroups();
        AfterStructureChange(copies);
    }

    public void Reorder(LayerOrder order)
    {
        if (_selection.Count == 0) return;
        BeginEdit();
        ElementOps.Reorder(Doc, _selection, order);
        AfterStructureChange();
    }

    public void GroupSelected()
    {
        if (_selection.Count < 2) return;
        BeginEdit();
        ElementOps.Group(Doc, _selection);
        AfterStructureChange();
    }

    public void UngroupSelected()
    {
        if (!_selection.Any(e => e.GroupId is not null)) return;
        BeginEdit();
        ElementOps.Ungroup(Doc, _selection);
        AfterStructureChange();
    }

    public void RenameGroup(ElementGroup group, string name)
    {
        if (group.Name == name) return;
        Edit("group-name:" + group.Id, () => group.Name = name, ChangeFlags.Layers | ChangeFlags.Properties);
    }

    public void AlignSelected(AlignMode mode)
    {
        if (_selection.Count < 2) return;
        Edit(null, () => ElementOps.Align(Doc, _selection, mode), ChangeFlags.Visual | ChangeFlags.Properties);
    }

    public void DistributeSelected(bool horizontal)
    {
        if (_selection.Count < 3) return;
        Edit(null, () => ElementOps.Distribute(Doc, _selection, horizontal), ChangeFlags.Visual | ChangeFlags.Properties);
    }

    /// <summary>Zmiana na wszystkich zaznaczonych (np. blokada, ukrycie, przesunięcie strzałkami).</summary>
    public void EditSelection(string? key, Action<CardElement> change, ChangeFlags flags = ChangeFlags.Visual | ChangeFlags.Properties)
    {
        if (_selection.Count == 0) return;
        Edit(key, () => { foreach (var e in _selection) change(e); }, flags);
    }
}
