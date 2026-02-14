using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace AsyncImageLoader.Memory.Services;

public sealed class BitmapStore {
    private readonly Dictionary<string, LinkedListNode<BitmapEntry>> _map = new();
    private readonly LinkedList<BitmapEntry> _lru = new();
    private readonly object _lock = new();
    
    public static BitmapStore Instance { get; } = new BitmapStore();
    
    private BitmapStore() { }

    public bool TryGet(string key, out BitmapEntry entry)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                MoveToFront(node);
                entry = node.Value;
                return true;
            }

            entry = null!;
            return false;
        }
    }

    public async Task<Bitmap?> GetOrAdd(string key, Func<Bitmap?> factory) {
        lock (_lock) {
            if (TryGet(key, out var entry))
                return entry.Bitmap;
        
            var bitmap = factory();
            
            if (bitmap == null)
                return bitmap;
        
            entry = new BitmapEntry(key, bitmap);
        
            Add(entry);

            return bitmap;
        }
    }

    public void Add(BitmapEntry entry)
    {
        lock (_lock)
        {
            if (_map.ContainsKey(entry.Key))
                return;

            var node = new LinkedListNode<BitmapEntry>(entry);
            _lru.AddFirst(node);
            _map[entry.Key] = node;
        }
    }

    public IEnumerable<BitmapEntry> EnumerateFromOldest()
    {
        lock (_lock)
        {
            return _lru.OrderBy(x => x.RefCount).ToList();
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            if (!_map.TryGetValue(key, out var node))
                return;

            _lru.Remove(node);
            _map.Remove(key);
        }
    }

    private void MoveToFront(LinkedListNode<BitmapEntry> node)
    {
        _lru.Remove(node);
        _lru.AddFirst(node);
    }
}

public sealed class BitmapEntry : IDisposable
{
    public string Key { get; }
    public Bitmap Bitmap { get; }

    private int _refCount;
    public int RefCount => _refCount;
    
    public DateTime LastReleased { get; private set; }

    public BitmapEntry(string key, Bitmap bitmap)
    {
        Key = key;
        Bitmap = bitmap;
    }

    public void Acquire()
    {
        Interlocked.Increment(ref _refCount);
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref _refCount) == 0)
            LastReleased = DateTime.UtcNow;
    }

    public void Dispose() {
        Bitmap.Dispose();
    }
}

public sealed class BitmapLease : IDisposable 
{
    private readonly BitmapEntry _entry;
    private int _disposed;

    public Bitmap? Bitmap
    {
        get
        {
            if (Volatile.Read(ref _disposed) == 1)
                return null;

            return _entry.Bitmap;
        }
    }


    public BitmapLease(BitmapEntry entry) {
        _entry = entry;
        _entry.Acquire();
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        
        _entry.Release();
    }
}