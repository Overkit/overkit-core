using System.Drawing.Drawing2D;

namespace Overkit.Host.Ui;

/// <summary>
/// Icône de zone de notification : le point de contrôle permanent du host,
/// visible même quand le HUD est caché (jeu fermé ou en arrière-plan).
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly IntPtr _iconHandle;

    public TrayIcon(Action togglePanel, Action openSettings, Action openLog, Action quit)
    {
        var (icon, handle) = CreateIcon();
        _iconHandle = handle;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Panneau Overkit\tF6", null, (_, _) => togglePanel());
        menu.Items.Add("Paramètres…", null, (_, _) => openSettings());
        menu.Items.Add("Ouvrir le journal", null, (_, _) => openLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter Overkit", null, (_, _) => quit());

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Overkit — All-in-One Overlay for Palworld",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => togglePanel();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
        }
    }

    /// <summary>
    /// Icône « O » dessinée à la volée — remplacée par un vrai asset quand le
    /// projet aura son identité visuelle.
    /// </summary>
    private static (Icon Icon, IntPtr Handle) CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var background = new SolidBrush(Color.FromArgb(30, 30, 46));
            g.FillEllipse(background, 0, 0, 31, 31);
            using var ring = new Pen(Color.FromArgb(137, 180, 250), 4f);
            g.DrawEllipse(ring, 7, 7, 17, 17);
        }
        var handle = bitmap.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }
}
