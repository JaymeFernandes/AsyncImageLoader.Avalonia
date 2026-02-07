using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AsyncImageLoader.Avalonia.Demo.ViewModels;
using AsyncImageLoader.Avalonia.Demo.Views;
using AsyncImageLoader.Loaders;

namespace AsyncImageLoader.Avalonia.Demo;

public class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new MainWindow {
                DataContext = new MainWindowViewModel(),
            };
            desktop.MainWindow.AttachDevTools();
        }

        ImageLoader.AsyncImageLoader = new SmartRamImageLoader();

        base.OnFrameworkInitializationCompleted();
    }
}