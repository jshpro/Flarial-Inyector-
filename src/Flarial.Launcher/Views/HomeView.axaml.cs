using Avalonia.Controls;
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
    }

    private void OnInitialized(object? sender, EventArgs e)
    {
    }

    private void SecretDot_Click(object? sender, RoutedEventArgs e)
    {
        if (!ReachPatcher.IsMinecraftRunning())
            return;

        if (VisualRoot is Window parentWindow)
            new ReachWindow().ShowDialog(parentWindow);
        else
            new ReachWindow().Show();
    }
}
