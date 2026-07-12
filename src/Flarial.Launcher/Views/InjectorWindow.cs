using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Flarial.Launcher.SystemTuning;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Launcher.Views;

public class InjectorWindow : Window
{
    private TextBlock _statusText;
    private ProgressBar _injectionProgress;
    private Button _injectButton;

    public InjectorWindow()
    {
        Title = "Injector";
        Width = 420;
        Height = 250;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        ShowInTaskbar = false;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;

        var app = Application.Current;
        Color accent = app?.FindResource("SystemAccentColor") is Color acc ? acc : Color.FromRgb(0, 160, 255);
        Color text = app?.FindResource("SystemBaseHighColor") is Color txt ? txt : Colors.White;

        var mainBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(240, 18, 20, 26)),
            CornerRadius = new CornerRadius(14),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1.5),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0, OffsetY = 8, Blur = 24, Spread = 0,
                Color = Color.FromArgb(120, 0, 0, 0)
            }),
            Padding = new Thickness(0)
        };

        // Cabecera
        var titleBar = new Border
        {
            Height = 46,
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = "Injector",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(22, 0, 0, 0)
            }
        };
        titleBar.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        // Estado
        _statusText = new TextBlock
        {
            Text = "Ready",
            FontSize = 14,
            Foreground = new SolidColorBrush(text),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 4)
        };

        // Barra de progreso
        _injectionProgress = new ProgressBar
        {
            IsIndeterminate = true,
            IsVisible = false,
            Height = 5,
            Foreground = new SolidColorBrush(accent),
            Background = new SolidColorBrush(Color.FromRgb(50, 52, 60)),
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0)
        };

        // Botones
        _injectButton = new Button
        {
            Content = "  Inject  ",
            Background = new SolidColorBrush(accent),
            Foreground = Brushes.White,
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(32, 10),
            Margin = new Thickness(0, 16, 6, 0)
        };
        _injectButton.Click += InjectButton_Click;

        var closeButton = new Button
        {
            Content = "  Close  ",
            Background = new SolidColorBrush(Color.FromRgb(70, 72, 80)),
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(32, 10),
            Margin = new Thickness(6, 16, 0, 0)
        };
        closeButton.Click += (s, e) => Close();

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _injectButton, closeButton }
        };

        // Firma
        var signature = new TextBlock
        {
            Text = "Made by Josh",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var centralStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _statusText, _injectionProgress, buttonPanel, signature }
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children = { titleBar, centralStack }
        };
        Grid.SetRow(titleBar, 0);
        Grid.SetRow(centralStack, 1);

        mainBorder.Child = grid;
        Content = mainBorder;
    }

    private async void InjectButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _injectButton.IsEnabled = false;
        _statusText.Text = "Downloading...";
        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200));
        _injectionProgress.IsVisible = true;

        try
        {
            await Task.Run(() =>
            {
                SystemMemoryOptimizer.StartOptimization();
                while (SystemMemoryOptimizer.Status == SystemMemoryOptimizer.OptimizationStatus.Downloading ||
                       SystemMemoryOptimizer.Status == SystemMemoryOptimizer.OptimizationStatus.Optimizing)
                {
                    Thread.Sleep(200);
                }
            });

            _statusText.Text = SystemMemoryOptimizer.StatusMessage;
            if (SystemMemoryOptimizer.Status == SystemMemoryOptimizer.OptimizationStatus.Completed)
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 220, 100));
            else
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80));
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Error: {ex.Message}";
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80));
        }
        finally
        {
            _injectionProgress.IsVisible = false;
            _injectButton.IsEnabled = true;
        }
    }
}