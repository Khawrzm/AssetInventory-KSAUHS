using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using AssetInventory.Data;
using AssetInventory.Models;
using AssetInventory.Services;

namespace AssetInventory.UI;

public sealed class MainForm : Form
{
    // ── Data ─────────────────────────────────────────────────────────────
    private readonly AssetRepository _repo     = new();
    private List<Asset>              _all      = new();
    private List<Asset>              _filtered = new();
    private string?                  _statusFilter;
    private bool                     _suppressSave;   // blocks auto-save during grid rebind
    private bool                     _isArabic = false;
    private string T(string english, string arabic) => _isArabic ? arabic : english;

    // ── Cached GDI resources to prevent leaks ────────────────────────────
    private readonly Font _tagFont;
    private readonly Font _emojiFontLogo = new("Segoe UI Emoji", 17f);
    private readonly Font _subtitleFontLogo;
    private readonly Font _emojiFontKpi = new("Segoe UI Emoji", 10f);

    private readonly Font _fTitle;
    private readonly Font _fBody;
    private readonly Font _fSmall;
    private readonly Font _fGrid;
    private readonly Font _fBadge;
    private readonly Font _fBtn;
    private readonly Font _fKpiNum;
    private readonly Font _fKpiLbl;
    private readonly Font _fSidebar;
    private readonly Font _fSidebarSm;

    private readonly SolidBrush _gridSelectedBrush = new(Theme.GridSelected);
    private readonly SolidBrush _gridEvenBrush = new(Theme.GridEven);
    private readonly SolidBrush _gridOddBrush = new(Theme.GridOdd);
    private readonly SolidBrush _gridBorderBrush = new(Theme.GridBorder);
    private readonly SolidBrush _blueBrush = new(Theme.Blue);
    private readonly SolidBrush _sidebarBorderBrush = new(Theme.SidebarBorder);
    private readonly SolidBrush _toastBgBrush = new(Theme.SidebarActive);
    private readonly SolidBrush _kpiShadowBrush = new(Color.FromArgb(10, 0, 0, 0));

    // ── Controls ─────────────────────────────────────────────────────────
    private readonly DataGridView   _grid      = new();
    private readonly AnalyticsPanel _analytics = new();
    private readonly TextBox        _search    = new();
    private readonly Panel          _sidebar   = new();
    private readonly Panel          _kpiRow    = new();
    private readonly Panel          _gridView  = new();   // Assets tab content
    private readonly Label          _lblTotal  = new();
    private readonly Label          _lblShow   = new();
    private readonly Label          _lblSel    = new();

    // Sidebar nav
    private readonly List<(Button btn, string? filter, bool isView)> _nav = new();
    private bool _analyticsVisible;

    // Toast
    private readonly Panel  _toast      = new();
    private readonly Label  _toastLabel = new();
    private readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 2800 };

    // Stats cache
    private AssetStats?              _stats;
    private Dictionary<string, int>? _locStats;

    public MainForm()
    {
        SuspendLayout();
        Text          = "Asset Inventory — KSAUHS";
        Size          = new Size(1300, 780);
        MinimumSize   = new Size(1000, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor     = Theme.ContentBg;
        DoubleBuffered= true;
        KeyPreview    = true;

        // Resolve bilingual-safe font family to prevent vertical clipping
        string ff = GetBilingualFontFamily();
        _fTitle = new Font(ff, 13f, FontStyle.Bold);
        _fBody = new Font(ff, 9.5f);
        _fSmall = new Font(ff, 8.5f);
        _fGrid = new Font(ff, 9f);
        _fBadge = new Font(ff, 7.5f, FontStyle.Bold);
        _fBtn = new Font(ff, 9f);
        _fKpiNum = new Font(ff, 22f, FontStyle.Bold);
        _fKpiLbl = new Font(ff, 8f);
        _fSidebar = new Font(ff, 9.5f);
        _fSidebarSm = new Font(ff, 8f);
        
        _tagFont = new Font(ff, 9f, FontStyle.Bold);
        _subtitleFontLogo = new Font(ff, 7.5f);

        Font = _fBody;

        RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = _isArabic;

        // Setup Virtual Mode for DataGridView to prevent UI freeze and achieve O(1) performance
        _grid.VirtualMode = true;
        _grid.CellValueNeeded += Grid_CellValueNeeded;
        _grid.CellValuePushed += Grid_CellValuePushed;
        _grid.CellPainting += GridCell_Paint;
        _grid.SelectionChanged += (_, _) => UpdateStatus();
        _grid.RowValidated += GridRow_Validated;
        _grid.DataError += (_, e) => e.Cancel = true;
        _grid.ColumnHeaderMouseClick += GridHeader_Click;

        BuildSidebar();
        BuildContent();
        BuildToast();
        WireKeys();
        ResumeLayout(false);

        Shown += (_, _) => RefreshAll();
    }

    private static string GetBilingualFontFamily()
    {
        try
        {
            using var font1 = new Font("Segoe UI Arabic", 9f);
            if (font1.Name == "Segoe UI Arabic") return "Segoe UI Arabic";
        }
        catch { }
        try
        {
            using var font2 = new Font("Tahoma", 9f);
            if (font2.Name == "Tahoma") return "Tahoma";
        }
        catch { }
        return "Segoe UI";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tagFont?.Dispose();
            _emojiFontLogo?.Dispose();
            _subtitleFontLogo?.Dispose();
            _emojiFontKpi?.Dispose();

            _fTitle?.Dispose();
            _fBody?.Dispose();
            _fSmall?.Dispose();
            _fGrid?.Dispose();
            _fBadge?.Dispose();
            _fBtn?.Dispose();
            _fKpiNum?.Dispose();
            _fKpiLbl?.Dispose();
            _fSidebar?.Dispose();
            _fSidebarSm?.Dispose();

            _gridSelectedBrush?.Dispose();
            _gridEvenBrush?.Dispose();
            _gridOddBrush?.Dispose();
            _gridBorderBrush?.Dispose();
            _blueBrush?.Dispose();
            _sidebarBorderBrush?.Dispose();
            _toastBgBrush?.Dispose();
            _kpiShadowBrush?.Dispose();

            _toastTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SIDEBAR
    // ═══════════════════════════════════════════════════════════════════
    private void BuildSidebar()
    {
        _sidebar.Controls.Clear();
        _sidebar.Dock      = DockStyle.Left;
        _sidebar.Width     = 214;
        _sidebar.BackColor = Theme.SidebarBg;

        // Logo
        var logo = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.SidebarBg };
        logo.Paint += (s, e) =>
        {
            var p = (Panel)s!;
            using var br = new LinearGradientBrush(p.ClientRectangle,
                Color.FromArgb(30, 58, 138), Color.FromArgb(10, 17, 32), LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(br, p.ClientRectangle);
            e.Graphics.FillRectangle(_blueBrush, 0, 0, 4, p.Height);

            int emojiX = _isArabic ? p.Width - 42 : 12;
            int textX  = _isArabic ? 12 : 52;
            var align  = _isArabic ? TextFormatFlags.Right : TextFormatFlags.Left;

            TextRenderer.DrawText(e.Graphics, "📦", _emojiFontLogo,
                new Rectangle(emojiX, 0, 38, p.Height), Color.White,
                TextFormatFlags.VerticalCenter | align);
            TextRenderer.DrawText(e.Graphics, T("Asset Inventory", "جرد الممتلكات"), _fTitle,
                new Rectangle(textX, 4, p.Width - 64, p.Height - 18), Color.White,
                TextFormatFlags.VerticalCenter | align);
            TextRenderer.DrawText(e.Graphics, T("KSAUHS Enterprise", "جامعة الملك سعود"),
                _subtitleFontLogo, new Rectangle(textX, 0, p.Width - 64, p.Height + 10),
                Color.FromArgb(148, 163, 184),
                TextFormatFlags.Bottom | align);
        };

        // Nav flow
        var navFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true, BackColor = Theme.SidebarBg,
            Padding = new Padding(0, 10, 0, 0)
        };

        // View buttons
        Section(navFlow, T("VIEWS", "العروض"));
        ViewBtn(navFlow, T("  ◈  Assets", "  ◈  الأصول"),        isAnalytics: false, isDefault: true);
        ViewBtn(navFlow, T("  📊  Analytics", "  📊  التحليلات"),     isAnalytics: true);

        // Filter buttons
        Section(navFlow, T("FILTER BY STATUS", "تصفية حسب الحالة"));
        FilterBtn(navFlow, T("  ◉  All Assets", "  ◉  كل الأصول"),    null,           isDefault: true);
        FilterBtn(navFlow, T("  ✅  Verified", "  ✅  المحققة"),      "VERIFIED");
        FilterBtn(navFlow, T("  ⏳  Pending", "  ⏳  المعلقة"),       "PENDING");
        FilterBtn(navFlow, T("  🔄  Transferred", "  🔄  المنقولة"),  "TRANSFERRED");
        FilterBtn(navFlow, T("  🗑  Disposed", "  🗑  المستبعدة"),      "DISPOSED");

        // Actions
        Section(navFlow, T("TOOLS", "الأدوات"));
        ActionBtn(navFlow, T("  ↑  Import CSV", "  ↑  استيراد CSV"),      OnImport);
        ActionBtn(navFlow, T("  ↓  Export Excel", "  ↓  تصدير Excel"),    OnExport);
        ActionBtn(navFlow, T("  ↓  Export CSV", "  ↓  تصدير CSV"),      OnExportCsv);

        // Footer
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Theme.SidebarBg };
        footer.Paint += (s, e) =>
        {
            var p = (Panel)s!;
            e.Graphics.FillRectangle(_sidebarBorderBrush, 0, 0, p.Width, 1);
        };
        var btnLang = new Button
        {
            Text = _isArabic ? "🌐 Switch to English" : "🌐 التحويل للعربية",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = _fSidebarSm,
            Cursor = Cursors.Hand
        };
        btnLang.FlatAppearance.BorderSize = 0;
        btnLang.FlatAppearance.MouseOverBackColor = Theme.SidebarHover;
        btnLang.Click += (s, e) => ToggleLanguage();
        footer.Controls.Add(btnLang);

        var navScroll = new Panel { Dock = DockStyle.Fill, BackColor = Theme.SidebarBg, AutoScroll = true };
        navScroll.Controls.Add(navFlow);
        _sidebar.Controls.AddRange(new Control[] { navScroll, footer, logo });
        Controls.Add(_sidebar);
    }

    private void Section(FlowLayoutPanel f, string title)
    {
        f.Controls.Add(new Label
        {
            Text = title, ForeColor = Color.FromArgb(55, 65, 81),
            Font = new Font(_fBody.FontFamily, 7.5f, FontStyle.Bold),
            AutoSize = false, Size = new Size(214, 26),
            TextAlign = _isArabic ? ContentAlignment.BottomRight : ContentAlignment.BottomLeft,
            Padding = _isArabic ? new Padding(0, 0, 16, 2) : new Padding(16, 0, 0, 2),
            BackColor = Theme.SidebarBg
        });
    }

    private void ViewBtn(FlowLayoutPanel f, string text, bool isAnalytics, bool isDefault = false)
    {
        var btn = NavBtn(text, isDefault);
        btn.Click += (_, _) =>
        {
            SetAnalyticsView(isAnalytics);
            HighlightNav(btn);
        };
        _nav.Add((btn, null, true));
        f.Controls.Add(btn);
    }

    private void FilterBtn(FlowLayoutPanel f, string text, string? filter, bool isDefault = false)
    {
        var btn = NavBtn(text, false);
        btn.Click += (_, _) =>
        {
            if (_analyticsVisible) SetAnalyticsView(false);
            _statusFilter = filter;
            ApplyFilter();
            HighlightNav(btn);
        };
        _nav.Add((btn, filter, false));
        f.Controls.Add(btn);
    }

    private void ActionBtn(FlowLayoutPanel f, string text, Action action)
    {
        var btn = NavBtn(text, false);
        btn.Click += (_, _) => action();
        f.Controls.Add(btn);
    }

    private Button NavBtn(string text, bool active)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat,
            BackColor = active ? Theme.SidebarActive : Theme.SidebarBg,
            ForeColor = active ? Color.White : Theme.SidebarText,
            Font = _fSidebar, Size = new Size(214, 38),
            TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
            Padding = _isArabic ? new Padding(0, 0, 8, 0) : new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize         = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.SidebarHover;
        return b;
    }

    private void HighlightNav(Button active)
    {
        foreach (var (b, _, _) in _nav)
        {
            b.BackColor = Theme.SidebarBg;
            b.ForeColor = Theme.SidebarText;
        }
        active.BackColor = Theme.SidebarActive;
        active.ForeColor = Color.White;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CONTENT AREA
    // ═══════════════════════════════════════════════════════════════════
    private void BuildContent()
    {
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.ContentBg };

        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Theme.HeaderTop };
        header.Paint += (s, e) =>
        {
            var p = (Panel)s!;
            using var br = new LinearGradientBrush(p.ClientRectangle,
                Theme.HeaderTop, Theme.HeaderBot, LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(br, p.ClientRectangle);
        };
        var hLbl = new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.White, Font = _fTitle,
            TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
            Padding = _isArabic ? new Padding(0, 0, 20, 0) : new Padding(20, 0, 0, 0),
            Text = T("Asset Inventory  ·  KSAUHS Enterprise", "نظام جرد الممتلكات  ·  جامعة الملك سعود")
        };
        header.Controls.Add(hLbl);

        // KPI row
        BuildKpiRow();

        // Grid view
        BuildGridView();

        // Analytics panel (hidden initially)
        _analytics.Dock    = DockStyle.Fill;
        _analytics.Visible = false;

        // Status bar
        var sb = BuildStatusBar();

        content.Controls.AddRange(new Control[] { _analytics, _gridView, sb, _kpiRow, header });
        Controls.Add(content);
    }

    // ── KPI Cards ────────────────────────────────────────────────────────
    private void BuildKpiRow()
    {
        _kpiRow.Controls.Clear();
        _kpiRow.Dock      = DockStyle.Top;
        _kpiRow.Height    = 106;
        _kpiRow.BackColor = Theme.ContentBg;
        _kpiRow.Padding   = new Padding(16, 10, 16, 0);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = _isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            WrapContents = false, BackColor = Color.Transparent
        };

        var cards = new (string tag, string icon, Color accent)[]
        {
            ("Total",       "📋", Theme.Blue),
            ("Verified",    "✅", Theme.Green),
            ("Pending",     "⏳", Theme.Yellow),
            ("Transferred", "🔄", Theme.Cyan),
            ("Disposed",    "🗑", Theme.Red),
        };
        foreach (var (tag, icon, color) in cards)
            flow.Controls.Add(MakeKpiCard(tag, icon, color));

        _kpiRow.Controls.Add(flow);
    }

    private Panel MakeKpiCard(string tag, string icon, Color accent)
    {
        var card = new Panel
        {
            Size = new Size(170, 82), BackColor = Color.White,
            Margin = new Padding(0, 0, 10, 0), Tag = tag, Cursor = Cursors.Hand
        };
        card.Paint += (s, e) =>
        {
            var p  = (Panel)s!;
            var g  = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Shadow
            g.FillRectangle(_kpiShadowBrush, new Rectangle(2, 2, p.Width - 1, p.Height - 1));
            // Card face
            using var path = Pill(new Rectangle(0, 0, p.Width - 2, p.Height - 2), 6);
            g.FillPath(Brushes.White, path);
            using var pen  = new Pen(Theme.Border, 1f);
            g.DrawPath(pen, path);

            bool isRtl = _isArabic;

            // Accent bar placement
            using (var accentBrush = new SolidBrush(accent))
            {
                if (isRtl)
                    g.FillRectangle(accentBrush, p.Width - 6, 0, 4, p.Height - 2);
                else
                    g.FillRectangle(accentBrush, 0, 0, 4, p.Height - 2);
            }

            // Icon bg & Emoji
            int iconX = isRtl ? 14 : p.Width - 38;
            using (var iconBgBrush = new SolidBrush(Color.FromArgb(18, accent.R, accent.G, accent.B)))
            {
                g.FillEllipse(iconBgBrush, iconX, 10, 24, 24);
            }

            TextRenderer.DrawText(g, icon, _emojiFontKpi,
                new Rectangle(iconX, 10, 24, 24), Color.Empty,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Value
            string val = GetKpiVal((string)p.Tag!);
            int textX = isRtl ? 38 : 12;
            var align = isRtl ? TextFormatFlags.Right : TextFormatFlags.Left;
            TextRenderer.DrawText(g, val, _fKpiNum,
                new Rectangle(textX, 6, p.Width - 50, 38), Theme.TextPrimary,
                align | TextFormatFlags.VerticalCenter);

            // Label
            string labelText = (string)p.Tag! switch
            {
                "Total"       => T("Total", "الإجمالي"),
                "Verified"    => T("Verified", "المحققة"),
                "Pending"     => T("Pending", "المعلقة"),
                "Transferred" => T("Transferred", "المنقولة"),
                "Disposed"    => T("Disposed", "المستبعدة"),
                _             => (string)p.Tag!
            };
            TextRenderer.DrawText(g, labelText, _fKpiLbl,
                new Rectangle(textX, 46, p.Width - 50, 20), Theme.TextSecondary,
                align | TextFormatFlags.VerticalCenter);

            // Mini bar (percentage of total)
            if ((string)p.Tag! != "Total" && (_stats?.Total ?? 0) > 0)
            {
                int cnt = GetKpiCount((string)p.Tag!);
                double f = cnt * 1.0 / (_stats!.Total);
                int   bw = (int)((p.Width - 24) * f);
                using (var bgBarBrush = new SolidBrush(Color.FromArgb(25, accent.R, accent.G, accent.B)))
                using (var fgBarBrush = new SolidBrush(accent))
                {
                    g.FillRectangle(bgBarBrush, 12, 70, p.Width - 24, 4);
                    if (bw > 0)
                    {
                        int barX = isRtl ? p.Width - 12 - bw : 12;
                        g.FillRectangle(fgBarBrush, barX, 70, bw, 4);
                    }
                }
            }
        };
        return card;
    }

    private string GetKpiVal(string tag) => tag switch
    {
        "Total"       => (_stats?.Total       ?? 0).ToString(),
        "Verified"    => (_stats?.Verified    ?? 0).ToString(),
        "Pending"     => (_stats?.Pending     ?? 0).ToString(),
        "Disposed"    => (_stats?.Disposed    ?? 0).ToString(),
        "Transferred" => (_stats?.Transferred ?? 0).ToString(),
        _             => "0"
    };

    private int GetKpiCount(string tag) => tag switch
    {
        "Verified"    => _stats?.Verified    ?? 0,
        "Pending"     => _stats?.Pending     ?? 0,
        "Disposed"    => _stats?.Disposed    ?? 0,
        "Transferred" => _stats?.Transferred ?? 0,
        _             => 0
    };

    // ── Grid View ────────────────────────────────────────────────────────
    private void BuildGridView()
    {
        _gridView.Dock      = DockStyle.Fill;
        _gridView.BackColor = Theme.ContentBg;

        var toolbar  = BuildToolbar();
        var topSep   = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };
        BuildGrid();

        _gridView.Controls.AddRange(new Control[] { _grid, topSep, toolbar });
    }

    // ── Toolbar ──────────────────────────────────────────────────────────
    private Panel BuildToolbar()
    {
        var tb = new Panel
        {
            Dock = DockStyle.Top, Height = 50, BackColor = Color.White,
            Padding = new Padding(16, 0, 16, 0)
        };
        tb.Paint += (s, e) =>
            e.Graphics.FillRectangle(_gridBorderBrush,
                0, ((Panel)s!).Height - 1, ((Panel)s!).Width, 1);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = _isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            WrapContents = false, Padding = new Padding(0, 8, 0, 8)
        };

        var btnAdd    = TBtn(T("＋  Add", "＋  إضافة"),           Theme.Blue,                    Color.White,  "Ctrl+N");
        var btnEdit   = TBtn(T("✎  Edit", "✎  تعديل"),           Color.FromArgb(30, 41, 59),   Color.White,  "F2");
        var btnDelete = TBtn(T("✕  Delete", "✕  حذف"),         Theme.Red,                     Color.White,  "Del");
        var btnBulk   = TBtn(T("⚡  Bulk Status", "⚡  حالة جماعية"),   Color.FromArgb(109, 40, 217), Color.White,  "");
        var sep1      = Sep();

        // Search
        var sBox = new Panel { Size = new Size(290, 32), BackColor = Color.White };
        sBox.Paint += (s, e) =>
        {
            using var p = new Pen(Theme.Border, 1f);
            e.Graphics.DrawRectangle(p, 0, 0, ((Panel)s!).Width - 1, ((Panel)s!).Height - 1);
        };
        var ico = new Label
        {
            Text = "🔍", Width = 28, Dock = _isArabic ? DockStyle.Right : DockStyle.Left,
            TextAlign = ContentAlignment.MiddleCenter, Font = _fSmall
        };
        _search.BorderStyle     = BorderStyle.None;
        _search.Dock            = DockStyle.Fill;
        _search.BackColor       = Color.White;
        _search.Font            = _fBody;
        _search.PlaceholderText = T("Search tag, description, location, notes…  (Ctrl+F)", "ابحث برقم الأصل، الوصف، الموقع… (Ctrl+F)");
        _search.TextChanged    += (_, _) => ApplyFilter();
        sBox.Controls.AddRange(new Control[] { _search, ico });

        var sep2       = Sep();
        var btnRefresh = TBtn(T("↺  Refresh", "↺  تحديث"), Color.FromArgb(51, 65, 85), Color.White, "F5");

        flow.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDelete, btnBulk, sep1, sBox, sep2, btnRefresh });
        tb.Controls.Add(flow);

        btnAdd.Click    += (_, _) => OnAdd();
        btnEdit.Click   += (_, _) => OnEdit();
        btnDelete.Click += (_, _) => OnDelete();
        btnBulk.Click   += (_, _) => OnBulkStatus();
        btnRefresh.Click += (_, _) => RefreshAll();

        return tb;
    }

    private Button TBtn(string text, Color bg, Color fg, string tip)
    {
        var b = new Button
        {
            Text = text, BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat,
            Font = _fBtn, Size = new Size(text.Length > 12 ? 140 : 108, 32),
            Margin = new Padding(0, 0, 4, 0), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize         = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(bg, 0.08f);
        if (!string.IsNullOrEmpty(tip)) new ToolTip().SetToolTip(b, tip);
        return b;
    }

    private static Panel Sep() =>
        new() { Width = 1, BackColor = Theme.Border, Margin = new Padding(4, 8, 4, 8) };

    // ── Grid ─────────────────────────────────────────────────────────────
    private void BuildGrid()
    {
        _grid.Columns.Clear();
        _grid.Dock                        = DockStyle.Fill;
        _grid.BackgroundColor             = Theme.ContentBg;
        _grid.BorderStyle                 = BorderStyle.None;
        _grid.CellBorderStyle             = DataGridViewCellBorderStyle.None;
        _grid.GridColor                   = Theme.GridBorder;
        _grid.SelectionMode               = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect                 = true;
        _grid.ReadOnly                    = false;           // ← inline editing
        _grid.EditMode                    = DataGridViewEditMode.EditOnKeystroke;
        _grid.AllowUserToAddRows          = false;
        _grid.AllowUserToDeleteRows       = false;
        _grid.AllowUserToResizeRows       = false;
        _grid.RowHeadersVisible           = false;
        _grid.AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight         = 40;
        _grid.RowTemplate.Height          = 38;
        _grid.Font                        = _fGrid;
        _grid.DefaultCellStyle.SelectionBackColor = Theme.GridSelected;
        _grid.DefaultCellStyle.SelectionForeColor = Theme.GridSelectedFg;
        _grid.DefaultCellStyle.Padding            = new Padding(0);
        _grid.ColumnHeadersDefaultCellStyle.BackColor          = Theme.GridHeader;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor          = Theme.GridHeaderFg;
        _grid.ColumnHeadersDefaultCellStyle.Font               = new Font(_fBody.FontFamily, 8.5f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.GridHeader;
        _grid.ColumnHeadersDefaultCellStyle.Padding            = _isArabic ? new Padding(0, 0, 12, 0) : new Padding(12, 0, 0, 0);
        _grid.EnableHeadersVisualStyles   = false;
        _grid.RightToLeft                 = _isArabic ? RightToLeft.Yes : RightToLeft.No;

        // TAG: read-only (primary key)
        var colTag = new DataGridViewTextBoxColumn
        {
            Name = "TagNumber",
            DataPropertyName = "TagNumber", HeaderText = T("TAG NUMBER", "رقم الأصل"),
            MinimumWidth = 90, FillWeight = 80, ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        // DESC: editable
        var colDesc = new DataGridViewTextBoxColumn
        {
            Name = "AssetDescription",
            DataPropertyName = "AssetDescription", HeaderText = T("DESCRIPTION", "الوصف"),
            MinimumWidth = 180, FillWeight = 220,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        // LOCATION: editable
        var colLoc = new DataGridViewTextBoxColumn
        {
            Name = "MajorLoc",
            DataPropertyName = "MajorLoc", HeaderText = T("LOCATION", "الموقع الرئيسي"),
            MinimumWidth = 100, FillWeight = 100,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        // SUB-LOC: editable
        var colMinor = new DataGridViewTextBoxColumn
        {
            Name = "MinorLoc",
            DataPropertyName = "MinorLoc", HeaderText = T("SUB-LOC", "الموقع الفرعي"),
            MinimumWidth = 80, FillWeight = 80,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        // STATUS: inline ComboBox
        var colStatus = new DataGridViewComboBoxColumn
        {
            Name = "Status",
            DataPropertyName = "Status", HeaderText = T("STATUS", "الحالة"),
            MinimumWidth = 110, FillWeight = 100,
            DataSource   = new[] { "PENDING", "VERIFIED", "DISPOSED", "TRANSFERRED" },
            FlatStyle    = FlatStyle.Flat,
            SortMode     = DataGridViewColumnSortMode.Programmatic,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing
        };

        // NOTES: editable
        var colNote = new DataGridViewTextBoxColumn
        {
            Name = "Note",
            DataPropertyName = "Note", HeaderText = T("NOTES", "الملاحظات"),
            MinimumWidth = 120, FillWeight = 160,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        _grid.Columns.AddRange(colTag, colDesc, colLoc, colMinor, colStatus, colNote);

        // Context menu
        var ctx = new ContextMenuStrip { Font = _fBody, RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No };
        ctx.Items.Add(T("✎  Open Full Edit", "✎  فتح للتعديل الكامل"), null, (_, _) => OnEdit());
        ctx.Items.Add(T("✕  Delete", "✕  حذف"),                null, (_, _) => OnDelete());
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add(T("⚡  Bulk Status Update", "⚡  تغيير حالة جماعي"),  null, (_, _) => OnBulkStatus());
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add(T("↓  Export Excel", "↓  تصدير Excel"),        null, (_, _) => OnExport());
        ctx.Items.Add(T("↓  Export CSV", "↓  تصدير CSV"),          null, (_, _) => OnExportCsv());
        _grid.ContextMenuStrip = ctx;
    }

    // ── Status bar ───────────────────────────────────────────────────────
    private Panel BuildStatusBar()
    {
        var sb = new Panel
        {
            Dock = DockStyle.Bottom, Height = 30,
            BackColor = Theme.SidebarBg, Padding = new Padding(16, 0, 16, 0)
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = _isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, WrapContents = false
        };
        void SL(Label l, int w)
        {
            l.ForeColor = Theme.TextMuted; l.Font = _fSmall;
            l.AutoSize = false; l.Width = w; l.Height = 30;
            l.TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            l.Padding = _isArabic ? new Padding(0, 0, 4, 0) : new Padding(4, 0, 0, 0);
        }
        SL(_lblTotal, 190); SL(_lblShow, 170); SL(_lblSel, 160);

        var s1 = new Panel { Width = 1, Height = 14, BackColor = Color.FromArgb(55, 65, 81), Margin = new Padding(4, 8, 4, 8) };
        var s2 = new Panel { Width = 1, Height = 14, BackColor = Color.FromArgb(55, 65, 81), Margin = new Padding(4, 8, 4, 8) };

        var hint = new Label
        {
            Text = T("Ctrl+N  Add  |  F2  Edit  |  Del  Delete  |  Ctrl+F  Search  |  F5  Refresh  |  Tab  Next Cell",
                     "Ctrl+N  إضافة  |  F2  تعديل  |  Del  حذف  |  Ctrl+F  بحث  |  F5  تحديث  |  Tab  الحقل التالي"),
            ForeColor = Color.FromArgb(55, 65, 81), Font = _fSmall,
            Dock = _isArabic ? DockStyle.Left : DockStyle.Right, Width = 570, Height = 30,
            TextAlign = _isArabic ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight
        };

        flow.Controls.AddRange(new Control[] { _lblTotal, s1, _lblShow, s2, _lblSel });
        sb.Controls.AddRange(new Control[] { hint, flow });
        return sb;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GRID PAINTING
    // ═══════════════════════════════════════════════════════════════════
    private void GridCell_Paint(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics is null) return;

        bool isStatus  = e.ColumnIndex == _grid.Columns["Status"]?.Index;
        bool selected  = _grid.Rows[e.RowIndex].Selected;
        bool editing   = _grid.IsCurrentCellInEditMode
                      && _grid.CurrentCell?.RowIndex    == e.RowIndex
                      && _grid.CurrentCell?.ColumnIndex == e.ColumnIndex;

        // If the status cell is actively being edited → let ComboBox render itself
        if (isStatus && editing)
        {
            e.Handled = false;
            return;
        }

        Brush rowBgBrush = selected ? _gridSelectedBrush
                     : (e.RowIndex % 2 == 0 ? _gridEvenBrush : _gridOddBrush);

        e.Graphics.FillRectangle(rowBgBrush, e.CellBounds);
        // Row separator
        e.Graphics.FillRectangle(_gridBorderBrush,
            e.CellBounds.X, e.CellBounds.Bottom - 1, e.CellBounds.Width, 1);
        // Blue accent bar on first col when selected
        if (e.ColumnIndex == 0 && selected)
            e.Graphics.FillRectangle(_blueBrush,
                e.CellBounds.X, e.CellBounds.Y, 3, e.CellBounds.Height);

        if (isStatus && e.Value is string status && !string.IsNullOrEmpty(status))
        {
            DrawBadge(e.Graphics, e.CellBounds, status);
        }
        else
        {
            bool bold = e.ColumnIndex == 0;
            var  font = bold ? _tagFont : _fGrid;
            var  fg   = selected ? Theme.GridSelectedFg : Theme.TextPrimary;

            TextRenderer.DrawText(e.Graphics, e.Value?.ToString() ?? "", font,
                new Rectangle(e.CellBounds.X + 12, e.CellBounds.Y,
                              e.CellBounds.Width - 16, e.CellBounds.Height),
                fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        e.Handled = true;
    }

    private void DrawBadge(Graphics g, Rectangle cell, string status)
    {
        var (_, bg, fg) = Theme.StatusStyle(status);
        var font        = Theme.FBadge;
        var sz          = TextRenderer.MeasureText(status, font);
        int bw          = sz.Width + 18;
        int bh          = 20;
        int bx          = cell.X + (cell.Width  - bw) / 2;
        int by          = cell.Y + (cell.Height - bh) / 2;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Pill(new Rectangle(bx, by, bw, bh), 10))
        using (var badgeBgBrush = new SolidBrush(bg))
        {
            g.FillPath(badgeBgBrush, path);
            TextRenderer.DrawText(g, status, font,
                new Rectangle(bx, by, bw, bh), fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private static GraphicsPath Pill(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private void GridHeader_Click(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0) return;
        var propName = _grid.Columns[e.ColumnIndex].DataPropertyName ?? "";
        var prop = typeof(Asset).GetProperty(propName);
        if (prop == null) return;

        bool asc = _grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection != SortOrder.Ascending;
        _filtered = asc
            ? _filtered.OrderBy(a => prop.GetValue(a)?.ToString() ?? "").ToList()
            : _filtered.OrderByDescending(a => prop.GetValue(a)?.ToString() ?? "").ToList();

        foreach (DataGridViewColumn c in _grid.Columns)
            c.HeaderCell.SortGlyphDirection = SortOrder.None;
        _grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection =
            asc ? SortOrder.Ascending : SortOrder.Descending;

        BindGrid();
    }

    // ── Grid Virtual Mode Events ──────────────────────────────────────────
    private void Grid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
        var asset = _filtered[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => asset.TagNumber,
            1 => asset.AssetDescription,
            2 => asset.MajorLoc,
            3 => asset.MinorLoc,
            4 => asset.Status,
            5 => asset.Note,
            _ => null
        };
    }

    private void Grid_CellValuePushed(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
        var asset = _filtered[e.RowIndex];
        var val = e.Value?.ToString() ?? "";
        switch (e.ColumnIndex)
        {
            case 1:
                asset.AssetDescription = val;
                break;
            case 2:
                asset.MajorLoc = val;
                break;
            case 3:
                asset.MinorLoc = val;
                break;
            case 4:
                asset.Status = val.ToUpperInvariant() switch
                {
                    "VERIFIED" => "VERIFIED",
                    "DISPOSED" => "DISPOSED",
                    "TRANSFERRED" => "TRANSFERRED",
                    _ => "PENDING"
                };
                break;
            case 5:
                asset.Note = val;
                break;
        }
        asset.DataHash = Core.IntegrityGuard.CalculateRecordHash(asset.TagNumber, asset.MajorLoc);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTO-SAVE  (inline editing → save when leaving row)
    // ═══════════════════════════════════════════════════════════════════
    private async void GridRow_Validated(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressSave || e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;

        var asset = _filtered[e.RowIndex];

        if (!Core.AssetValidator.Validate(asset, out var err))
        {
            Toast($"⚠  {err}", Theme.Yellow);
            return;
        }

        try
        {
            await Task.Run(() => _repo.Save(asset));
            
            // Refresh stats + KPI silently and asynchronously
            var stats = await Task.Run(() => _repo.GetStats());
            var locStats = await Task.Run(() => _repo.GetLocationStats());
            _stats = stats;
            _locStats = locStats;
            _kpiRow.Invalidate(true);
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  VIEW SWITCHING
    // ═══════════════════════════════════════════════════════════════════
    private void SetAnalyticsView(bool show)
    {
        _analyticsVisible     = show;
        _gridView.Visible     = !show;
        _analytics.Visible    = show;

        if (show && _stats != null && _locStats != null)
            _analytics.Refresh(_stats, _locStats);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TOAST
    // ═══════════════════════════════════════════════════════════════════
    private void BuildToast()
    {
        _toast.Size      = new Size(340, 44);
        _toast.BackColor = Theme.SidebarActive;
        _toast.Visible   = false;
        _toastLabel.Dock      = DockStyle.Fill;
        _toastLabel.ForeColor = Color.White;
        _toastLabel.Font      = _fBody;
        _toastLabel.TextAlign = ContentAlignment.MiddleLeft;
        _toastLabel.Padding   = new Padding(14, 0, 0, 0);
        _toast.Controls.Add(_toastLabel);
        _toastTimer.Tick += (_, _) => { _toast.Visible = false; _toastTimer.Stop(); };
        Controls.Add(_toast);
        _toast.BringToFront();
        SizeChanged += (_, _) => PosToast();
    }

    private void PosToast() =>
        _toast.Location = new Point(ClientSize.Width - _toast.Width - 24,
                                    ClientSize.Height - _toast.Height - 44);

    private void Toast(string msg, Color? color = null)
    {
        _toast.BackColor = color ?? Theme.SidebarActive;
        _toastLabel.Text = msg;
        PosToast();
        _toast.Visible = true;
        _toast.BringToFront();
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  KEYBOARD
    // ═══════════════════════════════════════════════════════════════════
    private void WireKeys()
    {
        KeyDown += (_, e) =>
        {
            if (_grid.IsCurrentCellInEditMode) return;
            switch (e.KeyCode)
            {
                case Keys.N when e.Control:             e.SuppressKeyPress = true; OnAdd();         break;
                case Keys.F2:                           OnEdit();                                   break;
                case Keys.Delete when !_search.Focused: OnDelete();                                break;
                case Keys.F when e.Control:             e.SuppressKeyPress = true; _search.Focus();break;
                case Keys.F5:                           RefreshAll();                               break;
                case Keys.Escape:                       _search.Clear();                            break;
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DATA
    // ═══════════════════════════════════════════════════════════════════
    private async void RefreshAll()
    {
        try
        {
            var all = await Task.Run(() => _repo.GetAll());
            var stats = await Task.Run(() => _repo.GetStats());
            var locStats = await Task.Run(() => _repo.GetLocationStats());

            _all = all;
            _stats = stats;
            _locStats = locStats;

            _kpiRow.Invalidate(true);
            ApplyFilter();
            if (_analyticsVisible) _analytics.Refresh(_stats, _locStats);
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private void ApplyFilter()
    {
        var q = _search.Text.Trim().ToLowerInvariant();
        _filtered = _all
            .Where(a => string.IsNullOrEmpty(_statusFilter) || a.Status == _statusFilter)
            .Where(a => string.IsNullOrEmpty(q) ||
                        a.TagNumber.ToLowerInvariant().Contains(q)        ||
                        a.AssetDescription.ToLowerInvariant().Contains(q) ||
                        a.MajorLoc.ToLowerInvariant().Contains(q)         ||
                        a.Note.ToLowerInvariant().Contains(q))
            .ToList();
        BindGrid();
    }

    private void BindGrid()
    {
        _suppressSave = true;
        _grid.DataSource = null; // Unbind data source
        _grid.RowCount = _filtered.Count;
        _grid.Invalidate();
        _suppressSave = false;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _lblTotal.Text = T($"Total: {_all.Count} assets", $"الإجمالي: {_all.Count} أصول");
        _lblShow.Text  = T($"Showing: {_filtered.Count}", $"المعروض: {_filtered.Count}");
        _lblSel.Text   = T($"Selected: {_grid.SelectedRows.Count}", $"المحدد: {_grid.SelectedRows.Count}");
    }

    private void ToggleLanguage()
    {
        _isArabic = !_isArabic;
        RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = _isArabic;

        // Clear and rebuild layout
        Controls.Clear();
        _nav.Clear();
        _sidebar.Controls.Clear();
        _kpiRow.Controls.Clear();
        _gridView.Controls.Clear();

        SuspendLayout();

        BuildSidebar();
        BuildContent();
        BuildToast();

        ResumeLayout(true);
        RefreshAll();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CRUD
    // ═══════════════════════════════════════════════════════════════════
    private async void OnAdd()
    {
        using var dlg = new AssetDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try 
        { 
            await Task.Run(() => _repo.Save(dlg.Result)); 
            RefreshAll(); 
            Toast(T("✓  Added successfully", "✓  تمت الإضافة بنجاح"), Theme.Green); 
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private async void OnEdit()
    {
        var a = PickOne();
        if (a == null) { Toast(T("⚠  Select an asset first", "⚠  اختر أصلاً أولاً"), Theme.Yellow); return; }
        using var dlg = new AssetDialog(a);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try 
        { 
            await Task.Run(() => _repo.Save(dlg.Result)); 
            RefreshAll(); 
            Toast(T("✓  Changes saved", "✓  تم حفظ التعديلات"), Theme.Blue); 
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private async void OnDelete()
    {
        var tags = SelectedTags();
        if (tags.Count == 0) { Toast(T("⚠  Select assets first", "⚠  اختر أصولاً أولاً"), Theme.Yellow); return; }

        string confirmMsg = tags.Count == 1 
            ? T($"Delete asset [{tags[0]}] permanently?", $"حذف الأصل [{tags[0]}] نهائياً؟")
            : T($"Delete {tags.Count} assets permanently?", $"حذف {tags.Count} أصول نهائياً؟");

        if (MessageBox.Show(confirmMsg, T("Confirm", "تأكيد"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            await Task.Run(() =>
            {
                foreach (var t in tags) _repo.Delete(t);
            });
            RefreshAll();
            Toast(T($"✓  Deleted {tags.Count} assets", $"✓  تم حذف {tags.Count} أصل"), Theme.Red);
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private async void OnBulkStatus()
    {
        var tags = SelectedTags();
        if (tags.Count == 0) { Toast(T("⚠  Select assets first", "⚠  اختر أصولاً أولاً"), Theme.Yellow); return; }

        using var f = new Form
        {
            Text = T("Bulk Update", "تغيير جماعي"), Size = new Size(300, 158),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, BackColor = Color.White,
            RightToLeft = RightToLeft,
            RightToLeftLayout = RightToLeftLayout
        };
        var lbl = new Label { Text = T("New Status:", "الحالة الجديدة:"), Dock = DockStyle.Top, Height = 30, Padding = new Padding(20, 8, 20, 0), ForeColor = Theme.TextSecondary, Font = _fSmall };
        var cb  = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, Height = 30, Margin = new Padding(20, 0, 20, 8) };
        cb.Items.AddRange(new object[] { "PENDING", "VERIFIED", "DISPOSED", "TRANSFERRED" });
        cb.SelectedIndex = 0;
        var btn = new Button { Text = T($"Apply to {tags.Count} assets", $"تطبيق على {tags.Count} أصول"), Dock = DockStyle.Bottom, Height = 40, BackColor = Color.FromArgb(109, 40, 217), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        btn.FlatAppearance.BorderSize = 0;
        f.Controls.AddRange(new Control[] { btn, cb, lbl });
        f.AcceptButton = btn;

        if (f.ShowDialog(this) != DialogResult.OK) return;
        Cursor = Cursors.WaitCursor;
        try
        {
            string newStatus = cb.SelectedItem!.ToString()!;
            await Task.Run(() => _repo.BulkSetStatus(tags, newStatus));
            RefreshAll();
            Toast(T($"✓  Status changed for {tags.Count} assets → {newStatus}", $"✓  تم تغيير حالة {tags.Count} أصول → {newStatus}"), Color.FromArgb(109, 40, 217));
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
        finally { Cursor = Cursors.Default; }
    }

    private async void OnImport()
    {
        using var ofd = new OpenFileDialog { Filter = "CSV Files|*.csv", Title = T("Import CSV", "استيراد CSV") };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        Cursor = Cursors.WaitCursor;
        try
        {
            string filePath = ofd.FileName;
            await Task.Run(() => ImportService.ImportFromCsv(filePath, new DatabaseService()));
            RefreshAll();
            Toast(T($"Imported: {System.IO.Path.GetFileName(filePath)}", $"✓  استيراد: {System.IO.Path.GetFileName(filePath)}"), Theme.Green);
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
        finally { Cursor = Cursors.Default; }
    }

    private async void OnExport()
    {
        using var sfd = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"AssetInventory_{DateTime.Now:yyyyMMdd}.xlsx" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        Cursor = Cursors.WaitCursor;
        try
        {
            string filePath = sfd.FileName;
            var list = new List<Asset>(_filtered);
            await Task.Run(() => ExcelExportService.ExportToXlsx(list, filePath));
            Toast(T($"Exported Excel: {System.IO.Path.GetFileName(filePath)}", $"✓  Excel: {System.IO.Path.GetFileName(filePath)}"), Theme.Green);
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
        finally { Cursor = Cursors.Default; }
    }

    private async void OnExportCsv()
    {
        using var sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"AssetInventory_{DateTime.Now:yyyyMMdd}.csv" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        Cursor = Cursors.WaitCursor;
        try
        {
            string filePath = sfd.FileName;
            var list = new List<Asset>(_filtered);
            await Task.Run(() => ExportService.ExportToCsv(list, filePath));
            Toast(T($"Exported CSV: {System.IO.Path.GetFileName(filePath)}", $"✓  CSV: {System.IO.Path.GetFileName(filePath)}"), Theme.Green);
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
        finally { Cursor = Cursors.Default; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private Asset? PickOne()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        int i = _grid.SelectedRows[0].Index;
        return i >= 0 && i < _filtered.Count ? _filtered[i] : null;
    }

    private List<string> SelectedTags() =>
        _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.Index).Where(i => i >= 0 && i < _filtered.Count)
            .Select(i => _filtered[i].TagNumber).ToList();
}
