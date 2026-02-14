using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncImageLoader.Memory.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AsyncImageLoader;

public partial class AdvancedImage : ContentControl {
    /// <summary>
    ///     Defines the <see cref="Loader" /> property.
    /// </summary>
    public static readonly StyledProperty<IAsyncImageLoader?> LoaderProperty =
        AvaloniaProperty.Register<AdvancedImage, IAsyncImageLoader?>(nameof(Loader));

    /// <summary>
    ///     Defines the <see cref="Source" /> property.
    /// </summary>
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<AdvancedImage, string?>(nameof(Source));
    
    /// <summary>
    ///     Defines the <see cref="FallbackImage" /> property.
    /// </summary>
    public static readonly StyledProperty<Bitmap?> FallbackImageProperty =
        AvaloniaProperty.Register<AdvancedImage, Bitmap?>(nameof(FallbackImage));

    /// <summary>
    ///     Defines the <see cref="ShouldLoaderChangeTriggerUpdate" /> property.
    /// </summary>
    public static readonly DirectProperty<AdvancedImage, bool> ShouldLoaderChangeTriggerUpdateProperty =
        AvaloniaProperty.RegisterDirect<AdvancedImage, bool>(
            nameof(ShouldLoaderChangeTriggerUpdate),
            image => image._shouldLoaderChangeTriggerUpdate,
            (image, b) => image._shouldLoaderChangeTriggerUpdate = b
        );

    /// <summary>
    ///     Defines the <see cref="IsLoading" /> property.
    /// </summary>
    public static readonly DirectProperty<AdvancedImage, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<AdvancedImage, bool>(
            nameof(IsLoading),
            image => image._isLoading);

    /// <summary>
    ///     Defines the <see cref="CurrentImage" /> property.
    /// </summary>
    public static readonly DirectProperty<AdvancedImage, IImage?> CurrentImageProperty =
        AvaloniaProperty.RegisterDirect<AdvancedImage, IImage?>(
            nameof(CurrentImage),
            image => image._currentImage);

    /// <summary>
    ///     Defines the <see cref="Stretch" /> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        Image.StretchProperty.AddOwner<AdvancedImage>();

    /// <summary>
    ///     Defines the <see cref="StretchDirection" /> property.
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        Image.StretchDirectionProperty.AddOwner<AdvancedImage>();
    
    private readonly Uri? _baseUri;

    private RoundedRect _cornerRadiusClip;

    private IImage? _currentImage;
    private bool _isCornerRadiusUsed;

    private bool _isLoading;
    
    private bool _shouldLoaderChangeTriggerUpdate;
    
    private CancellationTokenSource? _updateCancellationToken;
    private readonly ParametrizedLogger? _logger;
    
    private BitmapLease? _lease;
    private string? _source;

    static AdvancedImage() {
        AffectsRender<AdvancedImage>(CurrentImageProperty, StretchProperty, StretchDirectionProperty,
            CornerRadiusProperty);
        AffectsMeasure<AdvancedImage>(CurrentImageProperty, StretchProperty, StretchDirectionProperty);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AdvancedImage" /> class.
    /// </summary>
    /// <param name="baseUri">The base URL for the XAML context.</param>
    public AdvancedImage(Uri? baseUri) {
        _baseUri = baseUri;
        _logger = Logger.TryGet(LogEventLevel.Error, ImageLoader.AsyncImageLoaderLogArea);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AdvancedImage" /> class.
    /// </summary>
    /// <param name="serviceProvider">The XAML service provider.</param>
    public AdvancedImage(IServiceProvider serviceProvider)
        : this((serviceProvider.GetService(typeof(IUriContext)) as IUriContext)?.BaseUri) {
    }

    /// <summary>
    ///     Gets or sets the URI for image that will be displayed.
    /// </summary>
    public IAsyncImageLoader? Loader {
        get => GetValue(LoaderProperty);
        set => SetValue(LoaderProperty, value);
    }

    /// <summary>
    ///     Gets or sets the URI for image that will be displayed.
    /// </summary>
    public string? Source {
        get => GetValue(SourceProperty);
        set 
        {
            SetValue(SourceProperty, value);
            _source = value;
        }
    }

    /// <summary>
    ///     Gets or sets the value controlling whether the image should be reloaded after changing the loader.
    /// </summary>
    public bool ShouldLoaderChangeTriggerUpdate {
        get => _shouldLoaderChangeTriggerUpdate;
        set => SetAndRaise(ShouldLoaderChangeTriggerUpdateProperty, ref _shouldLoaderChangeTriggerUpdate, value);
    }
    
    /// <summary>
    ///     Gets or sets the Bitmap for Fallback image that will be displayed if the Source image isn't loaded.
    /// </summary>
    public Bitmap? FallbackImage {
        get => GetValue(FallbackImageProperty);
        set => SetValue(FallbackImageProperty, value);
    }

    /// <summary>
    ///     Gets a value indicating is image currently is loading state.
    /// </summary>
    public bool IsLoading {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    /// <summary>
    ///     Gets a currently loaded IImage.
    /// </summary>
    public IImage? CurrentImage {
        get => _currentImage;
        set => SetAndRaise(CurrentImageProperty, ref _currentImage, value);
    }

    /// <summary>
    ///     Gets or sets a value controlling how the image will be stretched.
    /// </summary>
    public Stretch Stretch {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value controlling in what direction the image will be stretched.
    /// </summary>
    public StretchDirection StretchDirection {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        if (change.Property == SourceProperty)
            _ = UpdateImage(change.GetNewValue<string>(), Loader);
        else if (change.Property == LoaderProperty && ShouldLoaderChangeTriggerUpdate)
            _ = UpdateImage(change.GetNewValue<string>(), Loader);
        else if (change.Property == CurrentImageProperty)
            ClearSourceIfUserProvideImage();
        else if (change.Property == CornerRadiusProperty)
            UpdateCornerRadius(change.GetNewValue<CornerRadius>());
        else if (change.Property == BoundsProperty && CornerRadius != default) UpdateCornerRadius(CornerRadius);
        else if (change.Property == FallbackImageProperty && Source == null)
            _ = UpdateImage(null, null);
        
        base.OnPropertyChanged(change);
    }

    private void ClearSourceIfUserProvideImage() {
        if (CurrentImage is not null and not ImageWrapper) {
            // User provided image himself
            Source = null;
        }
    }

    private async Task UpdateImage(string? source, IAsyncImageLoader? loader) {
        _source = source;
        
        var cts = ReplaceCts(ref _updateCancellationToken);

        if (source is null && CurrentImage is not ImageWrapper)
            return;

        IsLoading = true;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        BitmapLease? lease;

        try
        {
            lease = await LoadImageInternalAsync(source, loader, storage, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            return;
        }
        finally
        {
            cts.Dispose();
        }

        if (cts.IsCancellationRequested)
            return;

        if (CurrentImage is ImageWrapper wrapper)
            wrapper.Dispose();

        CurrentImage = lease is null ? null : new ImageWrapper(lease);

        IsLoading = false;
    }


    
    private async Task<BitmapLease?> LoadImageInternalAsync(
        string? source,
        IAsyncImageLoader? loader,
        IStorageProvider? storage,
        CancellationToken token)
    {
        async Task<Bitmap?> Load()
        {
            token.ThrowIfCancellationRequested();

            var uri = new Uri(source, UriKind.RelativeOrAbsolute);

            if (AssetLoader.Exists(uri, _baseUri))
            {
                using var stream = AssetLoader.Open(uri, _baseUri);

                token.ThrowIfCancellationRequested();
                return new Bitmap(stream);
            }

            if (loader is IAdvancedAsyncImageLoader advanced)
            {
                token.ThrowIfCancellationRequested();

                return await advanced
                    .ProvideImageAsync(source, storage)
                    .ConfigureAwait(false);
            }

            token.ThrowIfCancellationRequested();

            return await loader
                .ProvideImageAsync(source)
                .ConfigureAwait(false);
        }
        
        token.ThrowIfCancellationRequested();

        loader ??= ImageLoader.AsyncImageLoader;

        if (source == null)
            return null;
        
        BitmapLease? lease = null;

        try
        {
            if (loader is ICoordinatedImageLoader)
            {
                lease = await ImageLoader.BitmapCacheEvictionManager
                    .GetOrAdd(source, Load);
            }
            else
            {
                var entry = new BitmapEntry(source, await Load());
                lease = new BitmapLease(entry);
            }

            token.ThrowIfCancellationRequested();
            return lease;
        }
        catch (TaskCanceledException)
        {
            lease?.Dispose();
            throw;
        }
        catch
        {
            lease?.Dispose();
            throw;
        }
    }
    
    private void UpdateCornerRadius(CornerRadius radius) {
        _isCornerRadiusUsed = radius != default;
        _cornerRadiusClip = new RoundedRect(new Rect(0, 0, Bounds.Width, Bounds.Height), radius);
    }

    /// <summary>
    ///     Renders the control.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public override void Render(DrawingContext context) {
        var source = CurrentImage;

        if (source != null && Bounds is { Width: > 0, Height: > 0 }) {
            var viewPort = new Rect(Bounds.Size);
            var sourceSize = source.Size;

            var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
            var scaledSize = sourceSize * scale;
            var destRect = viewPort
                .CenterRect(new Rect(scaledSize))
                .Intersect(viewPort);
            var sourceRect = new Rect(sourceSize)
                .CenterRect(new Rect(destRect.Size / scale));

            DrawingContext.PushedState? pushedState =
                _isCornerRadiusUsed ? context.PushClip(_cornerRadiusClip) : null;
            context.DrawImage(source, sourceRect, destRect);
            pushedState?.Dispose();
        }
        else {
            base.Render(context);
        }
    }

    /// <summary>
    ///     Measures the control.
    /// </summary>
    /// <param name="availableSize">The available size.</param>
    /// <returns>The desired size of the control.</returns>
    protected override Size MeasureOverride(Size availableSize) {
        return CurrentImage != null
            ? Stretch.CalculateSize(availableSize, CurrentImage.Size, StretchDirection)
            : base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize) {
        return CurrentImage != null
            ? Stretch.CalculateSize(finalSize, CurrentImage.Size)
            : base.ArrangeOverride(finalSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        if (CurrentImage is null)
            _ = Dispatcher.UIThread.InvokeAsync(HandleAttachedAsync);
        
        
        base.OnAttachedToVisualTree(e);
    }

    private async Task HandleAttachedAsync()
        => await UpdateImage(Source, Loader);

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        ReleaseImage();
        base.OnDetachedFromVisualTree(e);
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        ReleaseImage();
        base.OnDataContextChanged(e);
    }
    
    private void ReleaseImage()
    {
        if (CurrentImage is ImageWrapper wrapper)
        {
            wrapper.Dispose();
            CurrentImage = null;
        }
    }
    
    private static CancellationTokenSource ReplaceCts(ref CancellationTokenSource? field)
    {
        var newCts = new CancellationTokenSource();
        var old = Interlocked.Exchange(ref field, newCts);

        if (old != null)
        {
            try { old.Cancel(); }
            catch { }
            old.Dispose();
        }

        return newCts;
    }

    public sealed class ImageWrapper : IImage, IDisposable
    {
        private BitmapLease _lease;
        private bool _disposed = false;

        private Size _size;
        
        public bool IsDisponse => _disposed;

        public ImageWrapper(BitmapLease lease)
        {
            _lease = lease;
            
            _size = new Size(lease.Bitmap.Size.Width, lease.Bitmap.Size.Height );
            
        }

        ~ImageWrapper() {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            _lease?.Dispose();
        }

        public Size Size => _size;

        public void Draw(DrawingContext context, Rect s, Rect d) 
            => ((IImage)_lease?.Bitmap).Draw(context, s, d);
    }
}