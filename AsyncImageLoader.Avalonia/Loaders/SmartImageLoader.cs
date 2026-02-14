using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
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
            using var response = await HttpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, url),
                HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var ms = new MemoryStream();

            var buffer = new byte[81920];
            int read;
            while ((read = await responseStream.ReadAsync(buffer, 0, buffer.Length)
                       .ConfigureAwait(false)) > 0)
            {
                ms.Write(buffer, 0, read);
            }

            return ms.ToArray();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            Logger.Value.Log(
                "Failed to resolve image from request with uri: {0}\nException: {1}",
                url, e);
            return null;
        }
    }

}