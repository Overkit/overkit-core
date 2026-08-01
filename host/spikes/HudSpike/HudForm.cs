using static HudSpike.NativeMethods;

namespace HudSpike;

/// <summary>
/// Spike Phase 0 : fenêtre plein écran topmost en deux modes.
/// Mode HUD : click-through total (WS_EX_TRANSPARENT | WS_EX_NOACTIVATE), widget passif.
/// Mode panneau (F6) : la fenêtre redevient interactive, prend le focus, puis le
/// restitue à la fenêtre précédente (le jeu) à la fermeture.
/// </summary>
public sealed class HudForm : Form
{
    private const int HotkeyId = 1;

    private readonly Label _hudLabel;
    private readonly Panel _panel;
    private readonly System.Windows.Forms.Timer _dataTimer;
    private readonly System.Windows.Forms.Timer _topmostTimer;

    private bool _panelOpen;
    private IntPtr _previousForeground = IntPtr.Zero;
    private long _tick;

    public HudForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen!.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        // Tout pixel Magenta est invisible ET laisse passer les clics (hit-test).
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;

        _hudLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Consolas", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 30, 46),
            Padding = new Padding(12),
            Location = new Point(40, 40),
            Text = "Overkit HUD",
        };
        Controls.Add(_hudLabel);

        _panel = BuildPanel();
        Controls.Add(_panel);

        _dataTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _dataTimer.Tick += (_, _) => UpdateHud();
        _dataTimer.Start();

        // Le jeu peut repasser devant après un alt-tab système : on réaffirme
        // TOPMOST périodiquement sans voler le focus.
        _topmostTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _topmostTimer.Tick += (_, _) =>
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        _topmostTimer.Start();
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
        if (!RegisterHotKey(Handle, HotkeyId, MOD_NONE, VK_F6))
            _hudLabel.Text = "Overkit HUD\n⚠ F6 déjà pris par une autre application";
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

    private void TogglePanel()
    {
        _panelOpen = !_panelOpen;
        var exStyle = GetWindowLong(Handle, GWL_EXSTYLE);

        if (_panelOpen)
        {
            _previousForeground = GetForegroundWindow();
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle & ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE));
            _panel.Visible = true;
            SetForegroundWindow(Handle);
            Activate();
        }
        else
        {
            _panel.Visible = false;
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
            if (_previousForeground != IntPtr.Zero)
                SetForegroundWindow(_previousForeground);
        }
    }

    private void UpdateHud()
    {
        _tick++;
        var mode = _panelOpen ? "PANNEAU (interactif)" : "HUD (click-through)";
        _hudLabel.Text =
            $"Overkit HUD — spike Phase 0\n" +
            $"{DateTime.Now:HH:mm:ss}  tick {_tick}\n" +
            $"mode : {mode}\n" +
            $"F6 : ouvrir/fermer le panneau";
    }

    private Panel BuildPanel()
    {
        var panel = new Panel
        {
            Size = new Size(420, 260),
            BackColor = Color.FromArgb(24, 24, 37),
            Visible = false,
        };
        panel.Location = new Point(
            (Bounds.Width - panel.Width) / 2,
            (Bounds.Height - panel.Height) / 2);

        var title = new Label
        {
            Text = "Panneau Overkit (spike)",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(20, 20),
        };

        var body = new Label
        {
            Text = "Cette fenêtre a le focus : le jeu ne reçoit plus\n" +
                   "les entrées. F6 pour refermer et rendre le focus.",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.Gainsboro,
            AutoSize = true,
            Location = new Point(20, 70),
        };

        var testButton = new Button
        {
            Text = "Bouton cliquable (test focus)",
            Size = new Size(220, 36),
            Location = new Point(20, 140),
            ForeColor = Color.White,
        };
        testButton.Click += (_, _) => testButton.Text = $"Cliqué à {DateTime.Now:HH:mm:ss}";

        var quitButton = new Button
        {
            Text = "Quitter le spike",
            Size = new Size(220, 36),
            Location = new Point(20, 190),
            ForeColor = Color.White,
        };
        quitButton.Click += (_, _) => Close();

        panel.Controls.AddRange([title, body, testButton, quitButton]);
        return panel;
    }
}
