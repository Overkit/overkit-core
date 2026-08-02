using Overkit.Host.Core;
using Overkit.Host.Probe;
using Overkit.Host.Ui;

namespace Overkit.Host;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Journal minimal du squelette : fichier à côté de l'exécutable.
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
        var bus = new StateBus();

        // EXG-010 : démarrage sans Sonde = mode statique d'office ; la
        // connexion basculera en live dès que la Sonde répondra (EXG-011).
        using var probe = new ProbeConnection(new Uri(settings.ProbeUri), bus, Log);

        var hud = new HudWindow(bus, calibration, settings.PanelHotkeyVk, settings.GameProcessNames);
        using var tray = new TrayIcon(
            togglePanel: hud.TogglePanelExternal,
            openSettings: () => OpenSettingsFile(settings, Log),
            openLog: () => ShellOpen(logPath, Log),
            quit: hud.Close);

        Application.Run(hud);
        Log("--- Overkit Host arrêté ---");
    }

    /// <summary>Ouvre overkit.settings.json dans l'éditeur associé, en le créant avec les valeurs courantes s'il n'existe pas.</summary>
    private static void OpenSettingsFile(Core.HostSettings settings, Action<string> log)
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
