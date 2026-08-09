// Flarial.Launcher/Views/ReachWindow.axaml.cs
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flarial.Launcher.SystemTuning;

namespace Flarial.Launcher.Views;

public partial class ReachWindow : Window
{
    private static ReachWindow? _current;

    public ReachWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _current = null;
    }

    public static void ShowOrActivate(Window? owner)
    {
        if (_current != null)
        {
            if (!_current.IsVisible)
                _current.Show();
            _current.Activate();
            _current.BringIntoView();
            return;
        }

        _current = new ReachWindow();
        if (owner != null)
            _current.Show(owner);
        else
            _current.Show();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!float.TryParse(ReachTextBox.Text, out float reach) || reach <= 0f || reach > 7.0f)
        {
            StatusText.Text = "Valor inválido (1.0 - 7.0)";
            return;
        }

        ApplyButton.IsEnabled = false;
        StatusText.Text = "Aplicando...";

        try
        {
            bool success = await Task.Run(() => ReachPatcher.ApplyReach(reach));
            StatusText.Text = success ? "Reach inyectado!" : "Error: Minecraft no encontrado o firma no hallada.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ApplyButton.IsEnabled = true;
        }
    }
}