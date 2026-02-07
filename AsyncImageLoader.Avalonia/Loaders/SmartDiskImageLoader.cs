using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace AsyncImageLoader.Loaders;

public class SmartDiskImageLoader : SmartRamImageLoader {
    
    private readonly string _cacheFolder;
    
    public SmartDiskImageLoader(string cacheFolder = "Cache/Images/") {
        _cacheFolder = cacheFolder;
    }
    
    protected override async Task<Bitmap?> LoadFromGlobalCache(string url) {
        var value = await LoadCacheMemory(url);
        
        if(value != null)
            return value;
        
        var path = Path.Combine(_cacheFolder, CreateMD5(url));

        return File.Exists(path) ? new Bitmap(path) : null;
    }
    
#if NETSTANDARD2_1
        protected sealed override async Task SaveToGlobalCache(string url, byte[] imageBytes) {
            AddToCacheAndLRU(url, imageBytes);

            var path = Path.Combine(_cacheFolder, CreateMD5(url));

            Directory.CreateDirectory(_cacheFolder);
            await File.WriteAllBytesAsync(path, imageBytes).ConfigureAwait(false);
        }
#else
    protected sealed override Task SaveToGlobalCache(string url, byte[] imageBytes) {
        
        AddToCacheAndLRU(url, imageBytes);
        
        var path = Path.Combine(_cacheFolder, CreateMD5(url));
        Directory.CreateDirectory(_cacheFolder);
        File.WriteAllBytes(path, imageBytes);
        
        return Task.CompletedTask;
    }
#endif
}