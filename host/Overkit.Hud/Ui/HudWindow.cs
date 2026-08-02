using Overkit.Contracts;
using Overkit.Host.Core;
using static Overkit.Host.Ui.NativeMethods;

namespace Overkit.Host.Ui;

/// <summary>
/// Fenêtre HUD (§2.2) : layered topmost click-through, réaffirmation TOPMOST
/// périodique, visible uniquement quand le jeu est au premier plan. La hotkey
/// globale délègue l'ouverture du panneau (WinUI 3) au callback fourni.
/// </summary>
public sealed class HudWindow : Form
{
    private const int HotkeyId = 1;

    private readonly StateBus _bus;
    private readonly MapCalibration? _calibration;
    private readonly uint _hotkeyVk;
    private readonly string[] _gameProcessNames;
    private readonly Label _hudLabel;
    private readonly Action _togglePanel;
    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly System.Windows.Forms.Timer _topmostTimer;
    private readonly System.Windows.Forms.Timer _visibilityTimer;

    private uint _gamePid;
    private DateTime _lastGameScan = DateTime.MinValue;

    public HudWindow(StateBus bus, MapCalibration? calibration, uint hotkeyVk, string[] gameProcessNames, Action togglePanel)
    {
        _bus = bus;
        _togglePanel = togglePanel;
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

        // HUD discret : petite pastille translucide aux coins arrondis, en
        // haut à gauche, avec l'essentiel seulement. L'exhaustif ira au panneau.
        Opacity = 0.92;
        _hudLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(230, 230, 240),
            BackColor = Color.FromArgb(24, 24, 34),
            Padding = new Padding(10, 6, 10, 6),
            Location = new Point(24, 24),
            Text = "Overkit",
        };
        _hudLabel.SizeChanged += (_, _) =>
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            const int r = 12;
            var b = new Rectangle(0, 0, _hudLabel.Width, _hudLabel.Height);
            path.AddArc(b.X, b.Y, r, r, 180, 90);
            path.AddArc(b.Right - r, b.Y, r, r, 270, 90);
            path.AddArc(b.Right - r, b.Bottom - r, r, r, 0, 90);
            path.AddArc(b.X, b.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            _hudLabel.Region = new Region(path);
        };
        Controls.Add(_hudLabel);

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
        var shouldShow = IsGameForeground();
        if (shouldShow == Visible)
        {
            return;
        }
        Visible = shouldShow; // ShowWithoutActivation : n'arrache pas le focus au jeu
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
            _togglePanel();
            return;
        }
        base.WndProc(ref m);
    }

    private void Render()
    {
        var snapshot = _bus.Current;

        // Ligne 1 : état. ● live / ○ statique (EXG-010, bandeau discret).
        var parts = new List<string>
        {
            snapshot.Mode == ConnectionMode.Live ? "● Overkit" : "○ Overkit — hors ligne",
        };

        // Ligne 2 : l'essentiel en une ligne — heure, position carte, palbox.
        var info = new List<string>();
        if (snapshot.World?.Time is { Status: FieldStatus.Ok } time)
        {
            info.Add($"J{time.Day}  {time.Hour:00}:{time.Minute:00}");
        }
        if (snapshot.Player is { Status: FieldStatus.Ok, Position: not null } player && _calibration is not null)
        {
            var (mapX, mapY) = _calibration.WorldToMap(player.Position.X, player.Position.Y);
            info.Add($"({mapX:F0}, {mapY:F0})");
        }
        if (snapshot.Palbox is { Pals: not null } palbox &&
            palbox.Status is FieldStatus.Ok or FieldStatus.Degraded)
        {
            info.Add(palbox.Status == FieldStatus.Degraded && palbox.Owned_count is > 0
                ? $"{palbox.Pals.Count}/{palbox.Owned_count} pals*"
                : $"{palbox.Pals.Count} pals");
        }
        if (info.Count > 0)
        {
            parts.Add(string.Join("   ", info));
        }

        _hudLabel.Text = string.Join('\n', parts);
    }

}
