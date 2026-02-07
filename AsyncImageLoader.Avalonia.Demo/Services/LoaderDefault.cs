using AsyncImageLoader.Loaders;

namespace AsyncImageLoader.Avalonia.Demo.Services;

public class LoaderDefault {
    public static IAsyncImageLoader Instance = new SmartRamImageLoader();
}