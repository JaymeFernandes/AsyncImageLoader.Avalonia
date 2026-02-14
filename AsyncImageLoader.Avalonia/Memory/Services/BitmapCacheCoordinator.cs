using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncImageLoader.Memory.Interfaces;
using Avalonia.Media.Imaging;

namespace AsyncImageLoader.Memory.Services;

public class BitmapCacheCoordinator : IDisposable 
{
    private IBitmapEvictionPolicy _policy;
    private readonly CancellationTokenSource _cts = new();

    public BitmapCacheCoordinator(IBitmapEvictionPolicy policy) {
        _policy = policy;
        _ = CleanupLoop(_cts.Token);
    }

    public async Task<BitmapLease?> GetOrAdd(string key, Func<Task<Bitmap>> factory) {
        if (BitmapStore.Instance.TryGet(key, out var result))
            return new BitmapLease(result);
        
        var bitmap = await factory();
        var entry = new BitmapEntry(key, bitmap);
        
        BitmapStore.Instance.Add(entry);
        
        return new BitmapLease(entry);
    }
    
    private async Task CleanupLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), token);
            
            foreach (var entry in BitmapStore.Instance.EnumerateFromOldest())
            {
                if(entry.RefCount > 0)
                    break;
                
                if (!_policy.ShouldEvict(entry))
                    continue;
                
                BitmapStore.Instance.Remove(entry.Key);
                
                entry.Dispose();
            }
        }
    }
    
    public void Dispose() {
        _cts.Cancel();
    }
}