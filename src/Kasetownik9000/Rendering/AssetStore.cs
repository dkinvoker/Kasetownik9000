using System.Security.Cryptography;
using Microsoft.Graphics.Canvas;
using Windows.Storage.Streams;

namespace Kasetownik9000.Rendering;

/// <summary>Obrazy osadzone w projekcie (bajty) + pamięć podręczna bitmap Win2D.</summary>
public sealed class AssetStore
{
    private readonly Dictionary<string, byte[]> _data = [];
    private readonly Dictionary<string, CanvasBitmap> _bitmaps = [];
    private readonly Dictionary<string, Task<CanvasBitmap?>> _loading = [];
    private readonly HashSet<string> _failed = [];

    public event Action? BitmapLoaded;

    public static string ComputeId(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..20].ToLowerInvariant();

    public string Add(byte[] bytes)
    {
        var id = ComputeId(bytes);
        _data[id] = bytes;
        return id;
    }

    public void Put(string id, byte[] bytes) => _data[id] = bytes;

    /// <summary>Rejestruje już zdekodowaną bitmapę (żeby nie dekodować obrazu drugi raz).</summary>
    public void SetBitmap(string id, CanvasBitmap bitmap)
    {
        _bitmaps[id] = bitmap;
        _failed.Remove(id);
    }

    public byte[]? GetBytes(string id) => _data.GetValueOrDefault(id);

    public bool HasFailed(string id) => _failed.Contains(id);

    /// <summary>Zwraca bitmapę, jeśli jest gotowa; w przeciwnym razie zaczyna ładowanie w tle.</summary>
    public CanvasBitmap? TryGetBitmap(string id)
    {
        if (_bitmaps.TryGetValue(id, out var bmp)) return bmp;
        if (!_loading.ContainsKey(id) && _data.ContainsKey(id) && !_failed.Contains(id))
        {
            _loading[id] = LoadAsync(id, notify: true);
        }
        return null;
    }

    public async Task EnsureLoadedAsync(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            if (_bitmaps.ContainsKey(id) || !_data.ContainsKey(id) || _failed.Contains(id)) continue;
            if (!_loading.TryGetValue(id, out var task))
                _loading[id] = task = LoadAsync(id, notify: false);
            await task;
        }
    }

    private async Task<CanvasBitmap?> LoadAsync(string id, bool notify)
    {
        try
        {
            var bmp = await DecodeAsync(_data[id]);
            _bitmaps[id] = bmp;
            if (notify) BitmapLoaded?.Invoke();
            return bmp;
        }
        catch
        {
            _failed.Add(id);
            return null;
        }
        finally
        {
            _loading.Remove(id);
        }
    }

    public static async Task<CanvasBitmap> DecodeAsync(byte[] bytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        return await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream);
    }
}
