using System.Drawing;

namespace AssetInventory.UI;

internal static class Theme
{
    // ── Sidebar ──────────────────────────────────────────────────────────
    public static readonly Color SidebarBg       = Color.FromArgb(10,  17,  32);
    public static readonly Color SidebarHover    = Color.FromArgb(22,  33,  55);
    public static readonly Color SidebarActive   = Color.FromArgb(30,  58, 138);
    public static readonly Color SidebarText     = Color.FromArgb(148, 163, 184);
    public static readonly Color SidebarTextHi   = Color.White;
    public static readonly Color SidebarBorder   = Color.FromArgb(30,  41,  59);

    // ── Header ───────────────────────────────────────────────────────────
    public static readonly Color HeaderTop       = Color.FromArgb(15,  23,  42);
    public static readonly Color HeaderBot       = Color.FromArgb(21,  36,  70);

    // ── Content ──────────────────────────────────────────────────────────
    public static readonly Color ContentBg       = Color.FromArgb(248, 250, 252);
    public static readonly Color CardBg          = Color.White;
    public static readonly Color Border          = Color.FromArgb(226, 232, 240);
    public static readonly Color ToolbarBg       = Color.White;

    // ── Text ─────────────────────────────────────────────────────────────
    public static readonly Color TextPrimary     = Color.FromArgb(15,  23,  42);
    public static readonly Color TextSecondary   = Color.FromArgb(100, 116, 139);
    public static readonly Color TextOnDark      = Color.White;
    public static readonly Color TextMuted       = Color.FromArgb(148, 163, 184);

    // ── Accents ──────────────────────────────────────────────────────────
    public static readonly Color Blue            = Color.FromArgb(59,  130, 246);
    public static readonly Color Green           = Color.FromArgb(34,  197, 94);
    public static readonly Color Yellow          = Color.FromArgb(234, 179, 8);
    public static readonly Color Red             = Color.FromArgb(239, 68,  68);
    public static readonly Color Cyan            = Color.FromArgb(6,   182, 212);
    public static readonly Color BlueHover       = Color.FromArgb(37,  99,  235);
    public static readonly Color RedHover        = Color.FromArgb(220, 38,  38);

    // ── Grid ─────────────────────────────────────────────────────────────
    public static readonly Color GridEven        = Color.White;
    public static readonly Color GridOdd         = Color.FromArgb(248, 250, 252);
    public static readonly Color GridSelected    = Color.FromArgb(219, 234, 254);
    public static readonly Color GridSelectedFg  = Color.FromArgb(30,  58, 138);
    public static readonly Color GridHeader      = Color.FromArgb(15,  23,  42);
    public static readonly Color GridHeaderFg    = Color.FromArgb(203, 213, 225);
    public static readonly Color GridBorder      = Color.FromArgb(241, 245, 249);

    // ── Status badge ─────────────────────────────────────────────────────
    public static (Color bar, Color bg, Color fg) StatusStyle(string status) =>
        status.ToUpperInvariant() switch
        {
            "VERIFIED"    => (Color.FromArgb(34,  197, 94),
                              Color.FromArgb(220, 252, 231), Color.FromArgb(21,  128, 61)),
            "PENDING"     => (Color.FromArgb(234, 179, 8),
                              Color.FromArgb(254, 249, 195), Color.FromArgb(133, 77,  14)),
            "DISPOSED"    => (Color.FromArgb(239, 68,  68),
                              Color.FromArgb(254, 226, 226), Color.FromArgb(185, 28,  28)),
            "TRANSFERRED" => (Color.FromArgb(59,  130, 246),
                              Color.FromArgb(219, 234, 254), Color.FromArgb(30,  64, 175)),
            _             => (Color.FromArgb(148, 163, 184),
                              Color.FromArgb(241, 245, 249), Color.FromArgb(71,  85, 105)),
        };

    // ── Fonts ─────────────────────────────────────────────────────────────
    public static readonly Font FTitle   = new("Segoe UI", 13f, FontStyle.Bold);
    public static readonly Font FBody    = new("Segoe UI", 9.5f);
    public static readonly Font FSmall   = new("Segoe UI", 8.5f);
    public static readonly Font FGrid    = new("Segoe UI", 9f);
    public static readonly Font FBadge   = new("Segoe UI", 7.5f, FontStyle.Bold);
    public static readonly Font FBtn     = new("Segoe UI", 9f);
    public static readonly Font FKpiNum  = new("Segoe UI", 22f, FontStyle.Bold);
    public static readonly Font FKpiLbl  = new("Segoe UI", 8f);
    public static readonly Font FSidebar = new("Segoe UI", 9.5f);
    public static readonly Font FSidebarSm = new("Segoe UI", 8f);
}
