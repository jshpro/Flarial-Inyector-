using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flarial.Launcher.SystemTuning;
using System;

namespace Flarial.Launcher.Views;

public partial class ReachWindow : Window
{
    public ReachWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
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
            bool success = await System.Threading.Tasks.Task.Run(() =>
                ReachPatcher.ApplyReach(reach));

            StatusText.Text = success ? "Reach Activo " : "Error: Minecraft no encontrado";
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

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}