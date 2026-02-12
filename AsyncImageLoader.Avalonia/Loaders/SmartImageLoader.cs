using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AsyncImageLoader.Loaders;

public class SmartImageLoader : BaseWebImageLoader, ICoordinatedImageLoader
{
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _loadingTasks = new();

    protected override async Task<byte[]?> LoadDataFromExternalAsync(string url)
    {
        var task = _loadingTasks.GetOrAdd(url, GetImageFromExternalAsync);

        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            _loadingTasks.TryRemove(url, out _);
        }
    }

    private async Task<byte[]?> GetImageFromExternalAsync(string url)
    {
        try
        {
            return await HttpClient.GetByteArrayAsync(url).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.Value.Log("Failed to resolve image from request with uri: {0}\nException: {1}",
                url,
                e);

            return null;
        }
    }
}