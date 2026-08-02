using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Overkit.Host.Core;
using Overkit.Host.Probe;
using Overkit.Host.Ui;

namespace Overkit.Host;

/// <summary>
/// Composition du host (§2.2) : le thread principal porte l'app WinUI 3 et le
/// panneau ; le HUD click-through (WinForms) et l'icône de zone de
/// notification vivent sur leur propre thread STA avec leur message pump.
/// </summary>
public partial class App : Application
{
    private DispatcherQueue _dispatcher = null!;
    private StateBus _bus = null!;
    private ProbeConnection? _probe;
    private PanelWindow? _panel;
    private HudWindow? _hud;
    private IntPtr _previousForeground;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        var logPath = Path.Combine(AppContext.BaseDirectory, "overkit.log");
        void Log(string message)
        {
            try
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Le journal ne doit jamais faire tomber le host.
            }
        }

        Log("--- Overkit Host démarre ---");
        var settings = HostSettings.Load(Log);
        var calibration = MapCalibration.TryLoad(Log);
        var refData = RefData.Load(Log);
        _bus = new StateBus();

        // EXG-010 : démarrage sans Sonde = mode statique d'office ; bascule
        // live automatique dès que la Sonde répond (EXG-011).
        _probe = new ProbeConnection(new Uri(settings.ProbeUri), _bus, Log);

        _panel = new PanelWindow(_bus, refData);

        var hudThread = new Thread(() =>
        {
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
            System.Windows.Forms.Application.EnableVisualStyles();

            _hud = new HudWindow(_bus, calibration, settings.PanelHotkeyVk, settings.GameProcessNames, TogglePanel);
            using var tray = new TrayIcon(
                togglePanel: TogglePanel,
                openSettings: () => OpenSettingsFile(settings, Log),
                openLog: () => ShellOpen(logPath, Log),
                quit: Quit);

            System.Windows.Forms.Application.Run(_hud);

            // Fermeture du HUD = arrêt du host.
            Log("--- Overkit Host arrêté ---");
            _probe?.Dispose();
            _dispatcher.TryEnqueue(() =>
            {
                _panel?.Close();
                Exit();
            });
        })
        {
            IsBackground = false,
            Name = "Overkit HUD",
        };
        hudThread.SetApartmentState(ApartmentState.STA);
        hudThread.Start();
    }

    /// <summary>Bascule du panneau — appelable depuis n'importe quel thread (hotkey, tray).</summary>
    private void TogglePanel()
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (_panel is null)
            {
                return;
            }
            if (_panel.AppWindow.IsVisible)
            {
                _panel.AppWindow.Hide();
                if (_previousForeground != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(_previousForeground);
                }
            }
            else
            {
                _previousForeground = NativeMethods.GetForegroundWindow();
                _panel.AppWindow.Show();
                _panel.Activate();
                // §2.2 : à l'ouverture, le panneau libère le curseur que le
                // jeu confine en borderless.
                NativeMethods.ClipCursor(IntPtr.Zero);
            }
        });
    }

    private void Quit()
    {
        _hud?.BeginInvoke(() => _hud.Close());
    }

    private static void OpenSettingsFile(HostSettings settings, Action<string> log)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "overkit.settings.json");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(settings,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        ShellOpen(path, log);
    }

    private static void ShellOpen(string path, Action<string> log)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            log($"Ouverture de {path} impossible : {ex.Message}");
        }
    }
}
