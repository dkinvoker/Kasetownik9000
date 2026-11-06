namespace Kasetownik9000.Tests;

/// <summary>Pomocnicze konstruktory danych do testów.</summary>
internal static class TestData
{
    public static ShapeElement Box(string name, double x, double y, double w = 10, double h = 10) =>
        new() { Name = name, X = x, Y = y, Width = w, Height = h };

    public static TextElement Text(string name, double x, double y, double w = 20, double h = 8, double pt = 12) =>
        new() { Name = name, X = x, Y = y, Width = w, Height = h, FontSize = pt };

    /// <summary>J-card z elementami A…E (od spodu).</summary>
    public static CardDocument DocWith(params CardElement[] elements)
    {
        var doc = CardFormats.Get("jcard").CreateDocument();
        doc.Elements.AddRange(elements);
        return doc;
    }

    public static string Names(CardDocument doc) => string.Join(",", doc.Elements.Select(e => e.Name));
}
