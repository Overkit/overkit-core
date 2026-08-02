using Overkit.Contracts;
using Overkit.Host.Core;
using static Overkit.Host.Ui.NativeMethods;

namespace Overkit.Host.Ui;

/// <summary>
/// Fenêtre HUD (§2.2) : layered topmost click-through, réaffirmation TOPMOST
/// périodique, hotkey globale qui bascule vers un panneau interactif avec
/// restitution du focus au jeu. Le panneau WinUI 3 remplacera le placeholder
/// dans un jalon dédié — cette fenêtre restera la surface HUD.
/// </summary>
public sealed class HudWindow : Form
{
    private const int HotkeyId = 1;

    private readonly StateBus _bus;
    private readonly MapCalibration? _calibration;
    private readonly uint _hotkeyVk;
    private readonly string[] _gameProcessNames;
    private readonly Label _hudLabel;
    private readonly Panel _panel;
    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly System.Windows.Forms.Timer _topmostTimer;
    private readonly System.Windows.Forms.Timer _visibilityTimer;

    private bool _panelOpen;
    private IntPtr _previousForeground = IntPtr.Zero;
    private uint _gamePid;
    private DateTime _lastGameScan = DateTime.MinValue;

    public HudWindow(StateBus bus, MapCalibration? calibration, uint hotkeyVk, string[] gameProcessNames)
    {
        _bus = bus;
        _calibration = calibration;
        _hotkeyVk = hotkeyVk;
        _gameProcessNames = gameProcessNames;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen!.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;

        _hudLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Consolas", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 30, 46),
            Padding = new Padding(12),
            Location = new Point(40, 40),
            Text = "Overkit",
        };
        Controls.Add(_hudLabel);

        _panel = BuildPanelPlaceholder();
        Controls.Add(_panel);

        _renderTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _renderTimer.Tick += (_, _) => Render();
        _renderTimer.Start();

        _topmostTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _topmostTimer.Tick += (_, _) =>
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        _topmostTimer.Start();

        // Le HUD ne vit qu'avec le jeu : caché si le jeu est fermé ou en
        // arrière-plan (Alt-Tab), sauf panneau Overkit ouvert.
        _visibilityTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _visibilityTimer.Tick += (_, _) => UpdateVisibility();
        _visibilityTimer.Start();
    }

    private void UpdateVisibility()
    {
        var shouldShow = _panelOpen || IsGameForeground();
        if (shouldShow == Visible)
        {
            return;
        }
        if (shouldShow)
        {
            Visible = true; // ShowWithoutActivation : n'arrache pas le focus au jeu
        }
        else
        {
            if (_panelOpen)
            {
                ClosePanel(restoreFocus: false);
            }
            Visible = false;
        }
    }

    private bool IsGameForeground()
    {
        // Scan léger du process jeu toutes les 2 s ; entre deux scans, le PID
        // mémorisé suffit pour tester la fenêtre au premier plan.
        var now = DateTime.UtcNow;
        if ((now - _lastGameScan).TotalSeconds >= 2)
        {
            _lastGameScan = now;
            _gamePid = 0;
            foreach (var name in _gameProcessNames)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(name);
                foreach (var process in processes)
                {
                    if (_gamePid == 0)
                    {
                        _gamePid = (uint)process.Id;
                    }
                    process.Dispose();
                }
                if (_gamePid != 0)
                {
                    break;
                }
            }
        }
        if (_gamePid == 0)
        {
            return false;
        }

        GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundPid);
        if (foregroundPid == _gamePid)
        {
            return true;
        }
        // Le process du jeu a pu disparaître entre deux scans : re-vérifier au
        // prochain tick plutôt que de garder un HUD orphelin.
        if (foregroundPid != 0 && foregroundPid == (uint)Environment.ProcessId)
        {
            return true;
        }
        return false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RegisterHotKey(Handle, HotkeyId, MOD_NONE, _hotkeyVk))
        {
            _hudLabel.Text = "Overkit\n⚠ hotkey déjà prise par une autre application";
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotKey(Handle, HotkeyId);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            TogglePanel();
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Bascule du panneau depuis l'extérieur (icône de zone de notification).</summary>
    public void TogglePanelExternal() => TogglePanel();

    private void TogglePanel()
    {
        if (_panelOpen)
        {
            ClosePanel(restoreFocus: true);
        }
        else
        {
            OpenPanel();
        }
    }

    private void OpenPanel()
    {
        _panelOpen = true;
        _previousForeground = GetForegroundWindow();
        var exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
        SetWindowLong(Handle, GWL_EXSTYLE, exStyle & ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE));
        _panel.Visible = true;
        Visible = true;
        SetForegroundWindow(Handle);
        Activate();
    }

    private void ClosePanel(bool restoreFocus)
    {
        _panelOpen = false;
        _panel.Visible = false;
        var exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
        SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
        if (restoreFocus && _previousForeground != IntPtr.Zero)
        {
            SetForegroundWindow(_previousForeground);
        }
    }

    private void Render()
    {
        var snapshot = _bus.Current;

        // EXG-010 : bandeau discret, les vues statiques restent opérationnelles.
        var header = snapshot.Mode == ConnectionMode.Live
            ? $"Overkit — live (sonde v{snapshot.ProbeVersion ?? "?"})"
            : "Overkit — données live indisponibles";

        var lines = new List<string> { header };

        if (snapshot.World?.Time is { Status: FieldStatus.Ok } time)
        {
            lines.Add($"jour {time.Day} — {time.Hour:00}:{time.Minute:00}");
        }

        if (snapshot.Player is { Status: FieldStatus.Ok, Position: not null } player)
        {
            var p = player.Position;
            if (_calibration is not null)
            {
                var (mapX, mapY) = _calibration.WorldToMap(p.X, p.Y);
                lines.Add($"carte ({mapX:F0}, {mapY:F0})");
            }
            lines.Add($"monde X={p.X:N0} Y={p.Y:N0} Z={p.Z:N0}");
        }

        _hudLabel.Text = string.Join('\n', lines);
    }

    private Panel BuildPanelPlaceholder()
    {
        var panel = new Panel
        {
            Size = new Size(480, 240),
            BackColor = Color.FromArgb(24, 24, 37),
            Visible = false,
        };
        panel.Location = new Point(
            (Bounds.Width - panel.Width) / 2,
            (Bounds.Height - panel.Height) / 2);

        var title = new Label
        {
            Text = "Panneau Overkit",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(20, 20),
        };

        var body = new Label
        {
            Text = "Placeholder — le panneau WinUI 3 (vues Palbox, modules…)\n" +
                   "arrive dans un jalon dédié de la Phase 1.\n" +
                   "Même hotkey pour refermer et rendre le focus au jeu.",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.Gainsboro,
            AutoSize = true,
            Location = new Point(20, 64),
        };

        var quit = new Button
        {
            Text = "Quitter Overkit",
            Size = new Size(200, 34),
            Location = new Point(20, 170),
            ForeColor = Color.White,
        };
        quit.Click += (_, _) => Close();

        panel.Controls.AddRange([title, body, quit]);
        return panel;
    }
}
