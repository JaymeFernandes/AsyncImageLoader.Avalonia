using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AsyncImageLoader.Avalonia.Demo.Pages;

public partial class AdvancedImageSafeMemoryPage : UserControl {
    public AdvancedImageSafeMemoryPage() {
        InitializeComponent();
    }
    
    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}