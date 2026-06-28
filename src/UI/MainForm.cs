using System;
using System.Collections.Generic;
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

    // Cached GDI resources (never create fonts inside OnPaint)
    private static readonly Font _tagFont = new("Segoe UI", 9f, FontStyle.Bold);

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
        Font          = Theme.FBody;
        DoubleBuffered= true;
        KeyPreview    = true;

        BuildSidebar();
        BuildContent();
        BuildToast();
        WireKeys();
        ResumeLayout(false);

        Shown += (_, _) => RefreshAll();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SIDEBAR
    // ═══════════════════════════════════════════════════════════════════
    private void BuildSidebar()
    {
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
            e.Graphics.FillRectangle(new SolidBrush(Theme.Blue), 0, 0, 4, p.Height);

            TextRenderer.DrawText(e.Graphics, "📦", new Font("Segoe UI Emoji", 17f),
                new Rectangle(12, 0, 38, p.Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, "Asset Inventory", Theme.FTitle,
                new Rectangle(52, 4, p.Width - 56, p.Height - 18), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, "KSAUHS Enterprise",
                new Font("Segoe UI", 7.5f), new Rectangle(52, 0, p.Width - 56, p.Height + 10),
                Color.FromArgb(148, 163, 184),
                TextFormatFlags.Bottom | TextFormatFlags.Left);
        };

        // Nav flow
        var navFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true, BackColor = Theme.SidebarBg,
            Padding = new Padding(0, 10, 0, 0)
        };

        // View buttons
        Section(navFlow, "VIEWS");
        ViewBtn(navFlow, "  ◈  Assets",        isAnalytics: false, isDefault: true);
        ViewBtn(navFlow, "  📊  Analytics",     isAnalytics: true);

        // Filter buttons
        Section(navFlow, "FILTER BY STATUS");
        FilterBtn(navFlow, "  ◉  All Assets",    null,           isDefault: true);
        FilterBtn(navFlow, "  ✅  Verified",      "VERIFIED");
        FilterBtn(navFlow, "  ⏳  Pending",       "PENDING");
        FilterBtn(navFlow, "  🔄  Transferred",  "TRANSFERRED");
        FilterBtn(navFlow, "  🗑  Disposed",      "DISPOSED");

        // Actions
        Section(navFlow, "TOOLS");
        ActionBtn(navFlow, "  ↑  Import CSV",      OnImport);
        ActionBtn(navFlow, "  ↓  Export Excel",    OnExport);
        ActionBtn(navFlow, "  ↓  Export CSV",      OnExportCsv);

        // Footer
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Theme.SidebarBg };
        footer.Paint += (s, e) =>
        {
            var p = (Panel)s!;
            e.Graphics.FillRectangle(new SolidBrush(Theme.SidebarBorder), 0, 0, p.Width, 1);
            TextRenderer.DrawText(e.Graphics, "v3.0  ·  No Telemetry  ·  Offline",
                Theme.FSidebarSm, p.ClientRectangle, Color.FromArgb(71, 85, 105),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

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
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            AutoSize = false, Size = new Size(214, 26),
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(16, 0, 0, 2), BackColor = Theme.SidebarBg
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

    private static Button NavBtn(string text, bool active)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat,
            BackColor = active ? Theme.SidebarActive : Theme.SidebarBg,
            ForeColor = active ? Color.White : Theme.SidebarText,
            Font = Theme.FSidebar, Size = new Size(214, 38),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0), Cursor = Cursors.Hand
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
            Dock = DockStyle.Fill, ForeColor = Color.White, Font = Theme.FTitle,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            Text = "Asset Inventory  ·  KSAUHS Enterprise"
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
        _kpiRow.Dock      = DockStyle.Top;
        _kpiRow.Height    = 106;
        _kpiRow.BackColor = Theme.ContentBg;
        _kpiRow.Padding   = new Padding(16, 10, 16, 0);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
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
            g.FillRectangle(new SolidBrush(Color.FromArgb(10, 0, 0, 0)),
                new Rectangle(2, 2, p.Width - 1, p.Height - 1));
            // Card face
            using var path = Pill(new Rectangle(0, 0, p.Width - 2, p.Height - 2), 6);
            g.FillPath(Brushes.White, path);
            using var pen  = new Pen(Theme.Border, 1f);
            g.DrawPath(pen, path);
            // Left accent bar
            g.FillRectangle(new SolidBrush(accent), 0, 0, 4, p.Height - 2);
            // Icon bg
            g.FillEllipse(new SolidBrush(Color.FromArgb(18, accent.R, accent.G, accent.B)),
                p.Width - 38, 10, 24, 24);

            // Value
            string val = GetKpiVal((string)p.Tag!);
            TextRenderer.DrawText(g, val, Theme.FKpiNum,
                new Rectangle(12, 6, p.Width - 50, 38), Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // Label
            TextRenderer.DrawText(g, (string)p.Tag!, Theme.FKpiLbl,
                new Rectangle(12, 46, p.Width - 20, 20), Theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // Mini bar (percentage of total)
            if ((string)p.Tag! != "Total" && (_stats?.Total ?? 0) > 0)
            {
                int cnt = GetKpiCount((string)p.Tag!);
                double f = cnt * 1.0 / (_stats!.Total);
                int   bw = (int)((p.Width - 18) * f);
                g.FillRectangle(new SolidBrush(Color.FromArgb(25, accent.R, accent.G, accent.B)),
                    12, 70, p.Width - 20, 4);
                if (bw > 0) g.FillRectangle(new SolidBrush(accent), 12, 70, bw, 4);
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
            e.Graphics.FillRectangle(new SolidBrush(Theme.Border),
                0, ((Panel)s!).Height - 1, ((Panel)s!).Width, 1);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, Padding = new Padding(0, 8, 0, 8)
        };

        var btnAdd    = TBtn("＋  Add",           Theme.Blue,                    Color.White,  "Ctrl+N");
        var btnEdit   = TBtn("✎  Edit",           Color.FromArgb(30, 41, 59),   Color.White,  "F2");
        var btnDelete = TBtn("✕  Delete",         Theme.Red,                     Color.White,  "Del");
        var btnBulk   = TBtn("⚡  Bulk Status",   Color.FromArgb(109, 40, 217), Color.White,  "");
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
            Text = "🔍", Width = 28, Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleCenter, Font = Theme.FSmall
        };
        _search.BorderStyle     = BorderStyle.None;
        _search.Dock            = DockStyle.Fill;
        _search.BackColor       = Color.White;
        _search.Font            = Theme.FBody;
        _search.PlaceholderText = "Search tag, description, location, notes…  (Ctrl+F)";
        _search.TextChanged    += (_, _) => ApplyFilter();
        sBox.Controls.AddRange(new Control[] { _search, ico });

        var sep2       = Sep();
        var btnRefresh = TBtn("↺  Refresh", Color.FromArgb(51, 65, 85), Color.White, "F5");

        flow.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDelete, btnBulk, sep1, sBox, sep2, btnRefresh });
        tb.Controls.Add(flow);

        btnAdd.Click    += (_, _) => OnAdd();
        btnEdit.Click   += (_, _) => OnEdit();
        btnDelete.Click += (_, _) => OnDelete();
        btnBulk.Click   += (_, _) => OnBulkStatus();
        btnRefresh.Click += (_, _) => RefreshAll();

        return tb;
    }

    private static Button TBtn(string text, Color bg, Color fg, string tip)
    {
        var b = new Button
        {
            Text = text, BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat,
            Font = Theme.FBtn, Size = new Size(text.Length > 12 ? 140 : 108, 32),
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
        _grid.Font                        = Theme.FGrid;
        _grid.DefaultCellStyle.SelectionBackColor = Theme.GridSelected;
        _grid.DefaultCellStyle.SelectionForeColor = Theme.GridSelectedFg;
        _grid.DefaultCellStyle.Padding            = new Padding(0);
        _grid.ColumnHeadersDefaultCellStyle.BackColor          = Theme.GridHeader;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor          = Theme.GridHeaderFg;
        _grid.ColumnHeadersDefaultCellStyle.Font               = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.GridHeader;
        _grid.ColumnHeadersDefaultCellStyle.Padding            = new Padding(12, 0, 0, 0);
        _grid.EnableHeadersVisualStyles   = false;

        // TAG: read-only (primary key)
        var colTag = new DataGridViewTextBoxColumn
        {
            Name = "TagNumber",
            DataPropertyName = "TagNumber", HeaderText = "TAG NUMBER",
            MinimumWidth = 90, FillWeight = 80, ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        // DESC: editable
        var colDesc = new DataGridViewTextBoxColumn
        {
            Name = "AssetDescription",
            DataPropertyName = "AssetDescription", HeaderText = "DESCRIPTION",
            MinimumWidth = 180, FillWeight = 220,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        // LOCATION: editable
        var colLoc = new DataGridViewTextBoxColumn
        {
            Name = "MajorLoc",
            DataPropertyName = "MajorLoc", HeaderText = "LOCATION",
            MinimumWidth = 100, FillWeight = 100,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        // SUB-LOC: editable
        var colMinor = new DataGridViewTextBoxColumn
        {
            Name = "MinorLoc",
            DataPropertyName = "MinorLoc", HeaderText = "SUB-LOC",
            MinimumWidth = 80, FillWeight = 80,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        // STATUS: inline ComboBox
        var colStatus = new DataGridViewComboBoxColumn
        {
            Name = "Status",
            DataPropertyName = "Status", HeaderText = "STATUS",
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
            DataPropertyName = "Note", HeaderText = "NOTES",
            MinimumWidth = 120, FillWeight = 160,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        _grid.Columns.AddRange(colTag, colDesc, colLoc, colMinor, colStatus, colNote);

        _grid.CellPainting       += GridCell_Paint;
        _grid.SelectionChanged   += (_, _) => UpdateStatus();
        _grid.CellDoubleClick    += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex != 4) return; };
        _grid.RowValidated       += GridRow_Validated;   // ← auto-save on row leave
        _grid.DataError          += (_, e) => e.Cancel = true;
        _grid.ColumnHeaderMouseClick += GridHeader_Click;

        // Context menu
        var ctx = new ContextMenuStrip { Font = Theme.FBody };
        ctx.Items.Add("✎  فتح للتعديل الكامل", null, (_, _) => OnEdit());
        ctx.Items.Add("✕  حذف",                null, (_, _) => OnDelete());
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("⚡  تغيير حالة جماعي",  null, (_, _) => OnBulkStatus());
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("↓  Export Excel",        null, (_, _) => OnExport());
        ctx.Items.Add("↓  Export CSV",          null, (_, _) => OnExportCsv());
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
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false
        };
        void SL(Label l, int w)
        {
            l.ForeColor = Theme.TextMuted; l.Font = Theme.FSmall;
            l.AutoSize = false; l.Width = w; l.Height = 30;
            l.TextAlign = ContentAlignment.MiddleLeft; l.Padding = new Padding(4, 0, 0, 0);
        }
        SL(_lblTotal, 190); SL(_lblShow, 170); SL(_lblSel, 160);

        var s1 = new Panel { Width = 1, Height = 14, BackColor = Color.FromArgb(55, 65, 81), Margin = new Padding(4, 8, 4, 8) };
        var s2 = new Panel { Width = 1, Height = 14, BackColor = Color.FromArgb(55, 65, 81), Margin = new Padding(4, 8, 4, 8) };

        var hint = new Label
        {
            Text = "Ctrl+N  Add  |  F2  Edit  |  Del  Delete  |  Ctrl+F  Search  |  F5  Refresh  |  Tab  Next Cell",
            ForeColor = Color.FromArgb(55, 65, 81), Font = Theme.FSmall,
            Dock = DockStyle.Right, Width = 570, Height = 30,
            TextAlign = ContentAlignment.MiddleRight
        };

        flow.Controls.AddRange(new Control[] { _lblTotal, s1, _lblShow, s2, _lblSel });
        sb.Controls.AddRange(new Control[] { hint, flow });
        return sb;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GRID PAINTING  (badge for Status column, alternating rows, etc.)
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

        Color rowBg = selected ? Theme.GridSelected
                    : (e.RowIndex % 2 == 0 ? Theme.GridEven : Theme.GridOdd);

        e.Graphics.FillRectangle(new SolidBrush(rowBg), e.CellBounds);
        // Row separator
        e.Graphics.FillRectangle(new SolidBrush(Theme.GridBorder),
            e.CellBounds.X, e.CellBounds.Bottom - 1, e.CellBounds.Width, 1);
        // Blue accent bar on first col when selected
        if (e.ColumnIndex == 0 && selected)
            e.Graphics.FillRectangle(new SolidBrush(Theme.Blue),
                e.CellBounds.X, e.CellBounds.Y, 3, e.CellBounds.Height);

        if (isStatus && e.Value is string status && !string.IsNullOrEmpty(status))
        {
            DrawBadge(e.Graphics, e.CellBounds, status);
        }
        else
        {
            bool bold = e.ColumnIndex == 0;
            var  font = bold ? _tagFont : Theme.FGrid;   // cached — no GDI leak
            var  fg   = selected ? Theme.GridSelectedFg : Theme.TextPrimary;

            TextRenderer.DrawText(e.Graphics, e.Value?.ToString() ?? "", font,
                new Rectangle(e.CellBounds.X + 12, e.CellBounds.Y,
                              e.CellBounds.Width - 16, e.CellBounds.Height),
                fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        e.Handled = true;
    }

    private static void DrawBadge(Graphics g, Rectangle cell, string status)
    {
        var (_, bg, fg) = Theme.StatusStyle(status);
        var font        = Theme.FBadge;
        var sz          = TextRenderer.MeasureText(status, font);
        int bw          = sz.Width + 18;
        int bh          = 20;
        int bx          = cell.X + (cell.Width  - bw) / 2;
        int by          = cell.Y + (cell.Height - bh) / 2;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Pill(new Rectangle(bx, by, bw, bh), 10);
        g.FillPath(new SolidBrush(bg), path);
        TextRenderer.DrawText(g, status, font,
            new Rectangle(bx, by, bw, bh), fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
        var prop = typeof(Asset).GetProperty(_grid.Columns[e.ColumnIndex].DataPropertyName ?? "");
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

    // ═══════════════════════════════════════════════════════════════════
    //  AUTO-SAVE  (inline editing → save when leaving row)
    // ═══════════════════════════════════════════════════════════════════
    private void GridRow_Validated(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressSave || e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;

        // Read current values from the row's cells
        var row   = _grid.Rows[e.RowIndex];
        var asset = _filtered[e.RowIndex];

        string Get(string col) =>
            row.Cells[_grid.Columns[col]?.Index ?? -1]?.Value?.ToString()?.Trim() ?? "";

        asset.AssetDescription = Get("AssetDescription");
        asset.MajorLoc         = Get("MajorLoc");
        asset.MinorLoc         = Get("MinorLoc");
        asset.Status           = Get("Status").ToUpperInvariant() switch
        {
            "VERIFIED"    => "VERIFIED",
            "DISPOSED"    => "DISPOSED",
            "TRANSFERRED" => "TRANSFERRED",
            _             => "PENDING"
        };
        asset.Note     = Get("Note");
        asset.DataHash = Core.IntegrityGuard.CalculateRecordHash(asset.TagNumber, asset.MajorLoc);

        if (!Core.AssetValidator.Validate(asset, out var err))
        {
            Toast($"⚠  {err}", Theme.Yellow);
            return;
        }

        try
        {
            _repo.Save(asset);
            // Refresh stats + KPI silently
            _stats    = _repo.GetStats();
            _locStats = _repo.GetLocationStats();
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
        _toastLabel.Font      = Theme.FBody;
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
    private void RefreshAll()
    {
        try
        {
            _all      = _repo.GetAll();
            _stats    = _repo.GetStats();
            _locStats = _repo.GetLocationStats();
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
        var bs = new BindingSource { DataSource = _filtered };
        _grid.DataSource = bs;
        if (_grid.Columns["DataHash"] is { } hc) hc.Visible = false;
        _suppressSave = false;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _lblTotal.Text = $"Total: {_all.Count} assets";
        _lblShow.Text  = $"Showing: {_filtered.Count}";
        _lblSel.Text   = $"Selected: {_grid.SelectedRows.Count}";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CRUD
    // ═══════════════════════════════════════════════════════════════════
    private void OnAdd()
    {
        using var dlg = new AssetDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { _repo.Save(dlg.Result); RefreshAll(); Toast("✓  تمت الإضافة بنجاح", Theme.Green); }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private void OnEdit()
    {
        var a = PickOne();
        if (a == null) { Toast("⚠  اختر أصلاً أولاً", Theme.Yellow); return; }
        using var dlg = new AssetDialog(a);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { _repo.Save(dlg.Result); RefreshAll(); Toast("✓  تم حفظ التعديلات", Theme.Blue); }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private void OnDelete()
    {
        var tags = SelectedTags();
        if (tags.Count == 0) { Toast("⚠  اختر أصولاً أولاً", Theme.Yellow); return; }

        if (MessageBox.Show(
                tags.Count == 1 ? $"حذف الأصل [{tags[0]}] نهائياً؟"
                                : $"حذف {tags.Count} أصول نهائياً؟",
                "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            != DialogResult.Yes) return;

        try
        {
            foreach (var t in tags) _repo.Delete(t);
            RefreshAll();
            Toast($"✓  تم حذف {tags.Count} أصل", Theme.Red);
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private void OnBulkStatus()
    {
        var tags = SelectedTags();
        if (tags.Count == 0) { Toast("⚠  اختر أصولاً أولاً", Theme.Yellow); return; }

        using var f = new Form
        {
            Text = "تغيير جماعي", Size = new Size(300, 158),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, BackColor = Color.White
        };
        var lbl = new Label { Text = "الحالة الجديدة:", Dock = DockStyle.Top, Height = 30, Padding = new Padding(20, 8, 0, 0), ForeColor = Theme.TextSecondary, Font = Theme.FSmall };
        var cb  = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, Height = 30, Margin = new Padding(20, 0, 20, 8) };
        cb.Items.AddRange(new object[] { "PENDING", "VERIFIED", "DISPOSED", "TRANSFERRED" });
        cb.SelectedIndex = 0;
        var btn = new Button { Text = $"تطبيق على {tags.Count} أصول", Dock = DockStyle.Bottom, Height = 40, BackColor = Color.FromArgb(109, 40, 217), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        btn.FlatAppearance.BorderSize = 0;
        f.Controls.AddRange(new Control[] { btn, cb, lbl });
        f.AcceptButton = btn;

        if (f.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _repo.BulkSetStatus(tags, cb.SelectedItem!.ToString()!);
            RefreshAll();
            Toast($"✓  تم تغيير حالة {tags.Count} أصول → {cb.SelectedItem}", Color.FromArgb(109, 40, 217));
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private void OnImport()
    {
        using var ofd = new OpenFileDialog { Filter = "CSV Files|*.csv", Title = "استيراد CSV" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ImportService.ImportFromCsv(ofd.FileName, new DatabaseService());
            RefreshAll();
            Toast($"✓  استيراد: {System.IO.Path.GetFileName(ofd.FileName)}", Theme.Green);
        }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private void OnExport()
    {
        using var sfd = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"AssetInventory_{DateTime.Now:yyyyMMdd}.xlsx" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        try { ExcelExportService.ExportToXlsx(_filtered, sfd.FileName); Toast($"✓  Excel: {System.IO.Path.GetFileName(sfd.FileName)}", Theme.Green); }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
    }

    private void OnExportCsv()
    {
        using var sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"AssetInventory_{DateTime.Now:yyyyMMdd}.csv" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        try { ExportService.ExportToCsv(_filtered, sfd.FileName); Toast($"✓  CSV: {System.IO.Path.GetFileName(sfd.FileName)}", Theme.Green); }
        catch (Exception ex) { Toast($"✕  {ex.Message}", Theme.Red); }
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
