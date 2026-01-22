using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Logging;
using Avalonia.Media.Imaging;

namespace AsyncImageLoader.Loaders;

public class SmartRamImageLoader : SmartImageLoader {

    public static int Capacity = 10;

    private static readonly object _lruLock = new();

    private static readonly ConcurrentDictionary<string, byte[]?> _memoryCache = new();

    private static readonly ConcurrentDictionary<string, LinkedListNode<string>> _nodeCache = new();

    private static readonly LinkedList<string> _urls = new();

    private readonly ParametrizedLogger? _logger;

    public SmartRamImageLoader() {
        _logger = Logger.TryGet(LogEventLevel.Error, ImageLoader.AsyncImageLoaderLogArea);
    }

    protected void AddToCacheAndLRU(string url, byte[] bytes) {
        lock (_lruLock) {
            _memoryCache.TryGetValue(url, out var oldBitmap);

            if (_nodeCache.TryGetValue(url, out var existing))
                _urls.Remove(existing);

            var node = new LinkedListNode<string>(url);
            _urls.AddFirst(node);

            _nodeCache[url] = node;
            _memoryCache[url] = bytes;

            while (_memoryCache.Count > Capacity) {
                var last = _urls.Last;
                if (last == null)
                    break;

                RemoveFromCache(last.Value);
            }
        }
    }

    protected Task<Bitmap?> LoadCacheMemory(string url) {
        if (!_memoryCache.TryGetValue(url, out var bytes)) {
            return Task.FromResult<Bitmap?>(null);
        }

        lock (_lruLock) {
            if (_nodeCache.TryGetValue(url, out var node)) {
                _urls.Remove(node);
                _urls.AddFirst(node);
            }
        }

        try {
            using var memoryStream = new MemoryStream(bytes);
            var bitmap = new Bitmap(memoryStream);

            return Task.FromResult<Bitmap?>(bitmap);
        }
        catch (Exception e) {
            Debug.Print(
                "Failed to load bitmap from memory cache. Url: {Url}, Exception: {Exception}",
                url,
                e
            );

            RemoveFromCache(url);
            return Task.FromResult<Bitmap?>(null);
        }
    }

    protected override async Task<Bitmap?> LoadFromGlobalCache(string url)
                => await LoadCacheMemory(url);

    protected override Task SaveToGlobalCache(string url, byte[] imageBytes) {
        AddToCacheAndLRU(url, imageBytes);

        return Task.CompletedTask;
    }

    private void RemoveFromCache(string url) {
        lock (_lruLock) {
            if (_nodeCache.TryRemove(url, out var node)) {
                _urls.Remove(node);
            }

            _memoryCache.TryRemove(url, out var value);
        }
    }
}
