using System.Drawing;

namespace AssetInventory.UI;

internal static class Theme
{
    // ── Sidebar ──────────────────────────────────────────────────────────
    public static readonly Color SidebarBg       = Color.FromArgb(27,  28,  31);  // #1B1C1F
    public static readonly Color SidebarHover    = Color.FromArgb(39,  42,  47);  // #272A2F
    public static readonly Color SidebarActive   = Color.FromArgb(13,  164, 113); // #0DA471 (KSAU-HS Green)
    public static readonly Color SidebarText     = Color.FromArgb(148, 163, 184);
    public static readonly Color SidebarTextHi   = Color.White;
    public static readonly Color SidebarBorder   = Color.FromArgb(49,  54,  63);  // #31363F

    // ── Header ───────────────────────────────────────────────────────────
    public static readonly Color HeaderTop       = Color.FromArgb(39,  42,  47);  // #272A2F
    public static readonly Color HeaderBot       = Color.FromArgb(27,  28,  31);  // #1B1C1F

    // ── Content ──────────────────────────────────────────────────────────
    public static readonly Color ContentBg       = Color.FromArgb(27,  28,  31);  // #1B1C1F
    public static readonly Color CardBg          = Color.FromArgb(39,  42,  47);  // #272A2F
    public static readonly Color Border          = Color.FromArgb(49,  54,  63);  // #31363F
    public static readonly Color ToolbarBg       = Color.FromArgb(39,  42,  47);  // #272A2F

    // ── Text ─────────────────────────────────────────────────────────────
    public static readonly Color TextPrimary     = Color.FromArgb(227, 229, 234); // #E3E5EA
    public static readonly Color TextSecondary   = Color.FromArgb(148, 163, 184);
    public static readonly Color TextOnDark      = Color.White;
    public static readonly Color TextMuted       = Color.FromArgb(100, 116, 139);

    // ── Accents ──────────────────────────────────────────────────────────
    public static readonly Color Blue            = Color.FromArgb(59,  130, 246);
    public static readonly Color Green           = Color.FromArgb(13,  164, 113); // #0DA471 (KSAU-HS Green)
    public static readonly Color Yellow          = Color.FromArgb(234, 179, 8);
    public static readonly Color Red             = Color.FromArgb(239, 68,  68);
    public static readonly Color Cyan            = Color.FromArgb(6,   182, 212);
    public static readonly Color BlueHover       = Color.FromArgb(37,  99,  235);
    public static readonly Color RedHover        = Color.FromArgb(220, 38,  38);

    // ── Grid ─────────────────────────────────────────────────────────────
    public static readonly Color GridEven        = Color.FromArgb(27,  28,  31);  // #1B1C1F
    public static readonly Color GridOdd         = Color.FromArgb(33,  35,  39);  // Slightly lighter dark
    public static readonly Color GridSelected    = Color.FromArgb(13,  164, 113); // #0DA471 (KSAU-HS Green)
    public static readonly Color GridSelectedFg  = Color.White;
    public static readonly Color GridHeader      = Color.FromArgb(39,  42,  47);  // #272A2F
    public static readonly Color GridHeaderFg    = Color.FromArgb(227, 229, 234); // #E3E5EA
    public static readonly Color GridBorder      = Color.FromArgb(49,  54,  63);  // #31363F

    // ── Status badge ─────────────────────────────────────────────────────
    public static (Color bar, Color bg, Color fg) StatusStyle(string status) =>
        status.ToUpperInvariant() switch
        {
            "VERIFIED"    => (Color.FromArgb(13,  164, 113),
                              Color.FromArgb(20,  55,  40),  Color.FromArgb(13,  164, 113)),
            "PENDING"     => (Color.FromArgb(234, 179, 8),
                              Color.FromArgb(55,  45,  15),  Color.FromArgb(234, 179, 8)),
            "DISPOSED"    => (Color.FromArgb(239, 68,  68),
                              Color.FromArgb(55,  20,  20),  Color.FromArgb(239, 68,  68)),
            "TRANSFERRED" => (Color.FromArgb(59,  130, 246),
                              Color.FromArgb(20,  35,  55),  Color.FromArgb(59,  130, 246)),
            _             => (Color.FromArgb(148, 163, 184),
                              Color.FromArgb(35,  35,  35),  Color.FromArgb(148, 163, 184)),
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
