using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flarial.Launcher.SystemTuning;
using Flarial.Launcher.Views;
using System;

namespace Flarial.Launcher.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        Focusable = true;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
    }

    private void OnInitialized(object? sender, EventArgs e)
    {
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            SecretDotButton.Opacity = 0.3;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            SecretDotButton.Opacity = 0.05;
        }
    }

    private void SecretDot_Click(object? sender, RoutedEventArgs e)
    {
        if (!ReachPatcher.IsMinecraftRunning())
            return;

        Window? parentWindow = TopLevel.GetTopLevel(this) as Window;
        ReachWindow.ShowOrActivate(parentWindow);
    }
}