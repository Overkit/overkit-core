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

        Application.Run(new HudWindow(bus, calibration, settings.PanelHotkeyVk));
        Log("--- Overkit Host arrêté ---");
    }
}
