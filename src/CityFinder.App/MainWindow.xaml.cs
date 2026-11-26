using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using System.Windows.Threading;
using CityFinder.App.Services;
using CityFinder.App.ViewModels;

namespace CityFinder.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MapBridge _mapBridge;
    private readonly string? _startupFolder;

    public MainWindow(MainViewModel viewModel, string? startupFolder)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _startupFolder = startupFolder;
        DataContext = viewModel;

        _mapBridge = new MapBridge(MapView, Path.Combine(AppContext.BaseDirectory, "Map"));
        _viewModel.AttachMap(_mapBridge);
        _viewModel.FocusPhotoRequested += OnFocusPhotoRequested;

        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProc);
        }
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmGetMinMaxInfo = 0x0024;
        if (message != WmGetMinMaxInfo)
        {
            return IntPtr.Zero;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
        {
            return IntPtr.Zero;
        }

        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        info.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
        info.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
        info.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
        info.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
        Marshal.StructureToPtr(info, lParam, fDeleteOld: true);
        handled = true;
        return IntPtr.Zero;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var mapInitialization = _mapBridge.InitializeAsync();
            await _viewModel.InitializeAsync(_startupFolder);
            await mapInitialization;

            if (_mapBridge.IsReady)
            {
                _viewModel.SetMapReady();
                // Give the WebView2 control its final size before fitting the
                // markers once more. The initial map ready event can fire while
                // the panel still has its pre-layout dimensions.
                await Task.Delay(800);
                await _mapBridge.FitAllAsync();
            }
            else
            {
                _viewModel.SetMapUnavailable(_mapBridge.InitializationError ?? "raison inconnue");
            }
        }
        catch (Exception exception)
        {
            FileLog.Write("Initialisation de la fenêtre impossible.", exception);
            _viewModel.SetMapUnavailable(exception.Message);
        }
    }

    private void OnFocusPhotoRequested(PhotoViewModel photo)
    {
        Dispatcher.BeginInvoke(() =>
        {
            PhotoList.UpdateLayout();
            PhotoList.ScrollIntoView(photo);
        }, DispatcherPriority.Background);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (MaximizeGlyph is not null)
        {
            MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "❐" : "□";
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.FocusPhotoRequested -= OnFocusPhotoRequested;
        _viewModel.Dispose();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetFolder(e.Data) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var folder = TryGetFolder(e.Data);
        if (folder is not null)
        {
            _viewModel.OpenFolderPathCommand.Execute(folder);
        }
    }

    private static string? TryGetFolder(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop) ||
            data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return null;
        }

        return paths.FirstOrDefault(Directory.Exists);
    }

    private void CityBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: PhotoViewModel photo })
        {
            photo.NormalizeFields();
        }
    }

    private void Textbox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: PhotoViewModel photo })
        {
            photo.NormalizeFields();
        }
    }

    private void WebView2Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            FileLog.Write("Ouverture du lien WebView2 impossible.", exception);
        }

        e.Handled = true;
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
