// Flarial.Launcher/Views/ReachWindow.axaml.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Flarial.Launcher.SystemTuning;

namespace Flarial.Launcher.Views;

public partial class ReachWindow : Window
{
    private static ReachWindow? _current;

    // Hotkey
    private const int HOTKEY_ID = 9001;
    private const uint VK_F8 = 0x77;
    private const uint MOD_NOREPEAT = 0x4000;

    // Win32 para la ventana oculta de mensajes
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle,
        [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
        uint dwStyle, int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern uint GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_DESTROY = 0x0002;

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private static readonly IntPtr HWND_MESSAGE_VAL = new IntPtr(-3);

    private Thread? _messageThread;
    private IntPtr _messageWindow = IntPtr.Zero;
    private CancellationTokenSource _cts = new();
    private bool _hotkeyRegistered = false;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    public ReachWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
        StartMessagePump();
    }

    private void StartMessagePump()
    {
        _messageThread = new Thread(() =>
        {
            IntPtr hInstance = GetModuleHandleW(null);
            _messageWindow = CreateWindowExW(
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                "STATIC",
                "",
                WS_POPUP,
                0, 0, 0, 0,
                HWND_MESSAGE_VAL,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero);

            if (_messageWindow == IntPtr.Zero) return;

            _hotkeyRegistered = RegisterHotKey(_messageWindow, HOTKEY_ID, MOD_NOREPEAT, VK_F8);
            if (!_hotkeyRegistered) { DestroyWindow(_messageWindow); return; }

            MSG msg;
            while (_messageWindow != IntPtr.Zero)
            {
                if (GetMessageW(out msg, IntPtr.Zero, 0, 0) > 0)
                {
                    if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == HOTKEY_ID)
                    {
                        Dispatcher.UIThread.Post(ToggleVisibility);
                    }
                    else if (msg.message == WM_DESTROY)
                    {
                        break;
                    }
                    TranslateMessage(ref msg);
                    DispatchMessageW(ref msg);
                }
                else
                {
                    break;
                }
            }

            if (_messageWindow != IntPtr.Zero)
            {
                UnregisterHotKey(_messageWindow, HOTKEY_ID);
                DestroyWindow(_messageWindow);
                _messageWindow = IntPtr.Zero;
            }
        });

        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.IsBackground = true;
        _messageThread.Start();
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
            Hide();
        else
        {
            Show();
            Activate();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_messageWindow != IntPtr.Zero)
        {
            PostQuitMessage(0);
            _messageThread?.Join(2000);
        }
        _cts.Cancel();
        _current = null;
    }

    public static void ShowOrActivate(Window? owner)
    {
        if (_current != null && _current.IsVisible)
        {
            _current.Activate();
            _current.BringIntoView();
            return;
        }

        _current = new ReachWindow();
        if (owner != null)
            _current.Show(owner);
        else
            _current.Show(); // sin propietario, ventana independiente
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
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