using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using AssetInventory.Models;

namespace AssetInventory.UI;

/// <summary>
/// لوحة تحليلات بصرية كاملة — رسم بياني دائري + أعمدة أفقية + إحصاءات.
/// </summary>
internal sealed class AnalyticsPanel : Panel
{
    private AssetStats?              _stats;
    private Dictionary<string, int>? _locations;
    private int                      _totalAssets;

    // Pre-allocated GDI+ resources to prevent memory leaks
    private readonly Font _titleFont;
    private readonly Font _smallBoldFont;
    private readonly Font _summaryValFont;
    private readonly Font _bodyFont;
    private readonly Font _sidebarSmFont;

    private readonly SolidBrush _shadowBrush = new(Color.FromArgb(12, 0, 0, 0));
    private readonly SolidBrush _whiteBrush = new(Color.White);
    private readonly SolidBrush _trackBrush = new(Color.FromArgb(241, 245, 249));
    private readonly SolidBrush _emptyRingBrush = new(Color.FromArgb(229, 231, 235));
    private readonly SolidBrush _summaryBgBrush = new(Color.FromArgb(15, 23, 42));
    private readonly SolidBrush _separatorBrush = new(Color.FromArgb(241, 245, 249));
    private readonly SolidBrush _dividerBrush = new(Color.FromArgb(40, 255, 255, 255));
    private readonly Pen _borderPen = new(Theme.Border, 1f);
    private readonly Pen _emptyRingPen = new(Theme.Border, 2f);

    public AnalyticsPanel()
    {
        DoubleBuffered = true;
        BackColor      = Theme.ContentBg;
        Dock           = DockStyle.Fill;

        // Resolve bilingual-safe font family to prevent vertical clipping
        string ff = GetBilingualFontFamily();
        _titleFont = new Font(ff, 11f, FontStyle.Bold);
        _smallBoldFont = new Font(ff, 8.5f, FontStyle.Bold);
        _summaryValFont = new Font(ff, 12f, FontStyle.Bold);
        _bodyFont = new Font(ff, 9.5f);
        _sidebarSmFont = new Font(ff, 8f);
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
            _titleFont.Dispose();
            _smallBoldFont.Dispose();
            _summaryValFont.Dispose();
            _bodyFont.Dispose();
            _sidebarSmFont.Dispose();

            _shadowBrush.Dispose();
            _whiteBrush.Dispose();
            _trackBrush.Dispose();
            _emptyRingBrush.Dispose();
            _summaryBgBrush.Dispose();
            _separatorBrush.Dispose();
            _dividerBrush.Dispose();
            _borderPen.Dispose();
            _emptyRingPen.Dispose();
        }
        base.Dispose(disposing);
    }

    public void Refresh(AssetStats stats, Dictionary<string, int> locations)
    {
        _stats       = stats;
        _locations   = locations;
        _totalAssets = stats.Total;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_stats is null) { DrawEmpty(e.Graphics); return; }

        var g = e.Graphics;
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.TextRenderingHint  = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        bool isRtl = RightToLeft == RightToLeft.Yes;

        int pad   = 20;
        int W     = ClientSize.Width  - pad * 2;
        int H     = ClientSize.Height - pad * 2;
        int leftW = (int)(W * 0.42f);
        int rightW= W - leftW - 16;

        Rectangle leftRect;
        Rectangle rightRect;

        if (isRtl)
        {
            // Swap card positions for RTL layout
            leftRect = new Rectangle(pad + rightW + 16, pad, leftW, H - 54);
            rightRect = new Rectangle(pad, pad, rightW, H - 54);
        }
        else
        {
            leftRect = new Rectangle(pad, pad, leftW, H - 54);
            rightRect = new Rectangle(pad + leftW + 16, pad, rightW, H - 54);
        }

        // ── Card: Donut chart ──────────────────────────────────────────
        DrawCard(g, leftRect);
        DrawDonut(g, leftRect);

        // ── Card: Location bars ─────────────────────────────────────────
        DrawCard(g, rightRect);
        DrawLocationBars(g, rightRect);

        // ── Bottom summary bar ─────────────────────────────────────────
        var bottomRect = new Rectangle(pad, H - 26, W, 46);
        DrawSummary(g, bottomRect);
    }

    // ═══════════════════════════════════════════════════════════════
    //  CARD BACKGROUND
    // ═══════════════════════════════════════════════════════════════
    private void DrawCard(Graphics g, Rectangle r)
    {
        // Shadow
        g.FillRectangle(_shadowBrush, new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height));
        // Card
        using var path = Rounded(r, 8);
        g.FillPath(_whiteBrush, path);
        g.DrawPath(_borderPen, path);
    }

    // ═══════════════════════════════════════════════════════════════
    //  DONUT CHART
    // ═══════════════════════════════════════════════════════════════
    private void DrawDonut(Graphics g, Rectangle card)
    {
        var stats = _stats!;
        bool isRtl = RightToLeft == RightToLeft.Yes;
        var align = isRtl ? TextFormatFlags.Right : TextFormatFlags.Left;

        // Title
        string title = isRtl ? "صحة المخزون" : "Inventory Health";
        TextRenderer.DrawText(g, title,
            _titleFont,
            new Rectangle(card.X + 16, card.Y + 14, card.Width - 32, 28),
            Theme.TextPrimary, align | TextFormatFlags.VerticalCenter);

        // Donut dimensions
        int donutSize = Math.Min(card.Width - 40, 200);
        int cx        = card.X + (card.Width - donutSize) / 2;
        int cy        = card.Y + 52;
        var donutRect = new Rectangle(cx, cy, donutSize, donutSize);

        // Segments
        var segments = new (string label, int count, Color color)[]
        {
            (isRtl ? "محققة" : "Verified",    stats.Verified,    Theme.Green),
            (isRtl ? "معلقة" : "Pending",     stats.Pending,     Theme.Yellow),
            (isRtl ? "مستبعدة" : "Disposed",    stats.Disposed,    Theme.Red),
            (isRtl ? "منقولة" : "Transferred", stats.Transferred, Theme.Cyan),
        };

        if (stats.Total > 0)
        {
            float start = -90f;
            foreach (var (_, count, color) in segments)
            {
                float sweep = count * 360f / stats.Total;
                if (sweep < 0.5f) { start += sweep; continue; }

                // Outer arc (slightly inset for gap)
                var segRect = new Rectangle(donutRect.X + 3, donutRect.Y + 3,
                                             donutRect.Width - 6, donutRect.Height - 6);
                using (var brush = new SolidBrush(color))
                {
                    g.FillPie(brush, segRect, start + 1f, sweep - 2f);
                }
                start += sweep;
            }

            // Gray ring for zero-count segments
            if (stats.Total == 0)
                g.FillEllipse(_emptyRingBrush, donutRect);

            // Donut hole
            int holeSize  = (int)(donutSize * 0.6f);
            int holeOffset = (donutSize - holeSize) / 2;
            var holeRect  = new Rectangle(cx + holeOffset, cy + holeOffset, holeSize, holeSize);
            g.FillEllipse(_whiteBrush, holeRect);

            // Center text
            double pct = stats.VerifiedPct;
            using (var bigFont = new Font(_titleFont.FontFamily, donutSize * 0.16f, FontStyle.Bold))
            using (var smFont  = new Font(_titleFont.FontFamily, donutSize * 0.07f))
            {
                TextRenderer.DrawText(g, $"{pct:0}%", bigFont, holeRect, Theme.TextPrimary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                var smRect = new Rectangle(holeRect.X, holeRect.Y + (int)(holeSize * 0.35f), holeSize, (int)(holeSize * 0.2f));
                string verStr = isRtl ? "محققة" : "Verified";
                TextRenderer.DrawText(g, verStr, smFont, smRect, Theme.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
        else
        {
            // Empty state
            g.DrawEllipse(_emptyRingPen, donutRect);
            string emptyStr = isRtl ? "لا توجد بيانات" : "No data available";
            TextRenderer.DrawText(g, emptyStr,
                _bodyFont, donutRect, Theme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // Legend
        int legendY = cy + donutSize + 16;
        int legendX = card.X + 16;
        int legW    = card.Width - 32;
        int legH    = Math.Max(0, card.Bottom - legendY - 12);

        DrawLegend(g, segments, stats.Total, new Rectangle(legendX, legendY, legW, legH));
    }

    private void DrawLegend(Graphics g,
        (string label, int count, Color color)[] items,
        int total,
        Rectangle area)
    {
        bool isRtl = RightToLeft == RightToLeft.Yes;
        int itemH = 24;
        int y     = area.Y;

        foreach (var (label, count, color) in items)
        {
            if (y + itemH > area.Bottom) break;

            int dotX, labelX, labelW, countX, pctX;
            var labelAlign = isRtl ? TextFormatFlags.Right : TextFormatFlags.Left;
            var numAlign = isRtl ? TextFormatFlags.Left : TextFormatFlags.Right;

            if (isRtl)
            {
                dotX = area.Right - 12;
                labelX = area.X + area.Width / 2 - 10;
                labelW = area.Width / 2;
                countX = area.X + 34;
                pctX = area.X;
            }
            else
            {
                dotX = area.X;
                labelX = area.X + 18;
                labelW = area.Width / 2 - 10;
                countX = area.Right - 70;
                pctX = area.Right - 34;
            }

            // Color dot
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, dotX, y + 6, 12, 12);
            }
            g.SmoothingMode = SmoothingMode.None;

            // Label
            TextRenderer.DrawText(g, label, _smallBoldFont,
                new Rectangle(labelX, y, labelW, itemH),
                Theme.TextPrimary, labelAlign | TextFormatFlags.VerticalCenter);

            // Count
            TextRenderer.DrawText(g, count.ToString(), _smallBoldFont,
                new Rectangle(countX, y, 35, itemH),
                Theme.TextPrimary, numAlign | TextFormatFlags.VerticalCenter);

            // Percentage
            double pct = total > 0 ? count * 100.0 / total : 0;
            TextRenderer.DrawText(g, $"{pct:0.0}%", _smallBoldFont,
                new Rectangle(pctX, y, 34, itemH),
                Theme.TextSecondary, numAlign | TextFormatFlags.VerticalCenter);

            y += itemH;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  LOCATION BAR CHART
    // ═══════════════════════════════════════════════════════════════
    private void DrawLocationBars(Graphics g, Rectangle card)
    {
        bool isRtl = RightToLeft == RightToLeft.Yes;
        var align = isRtl ? TextFormatFlags.Right : TextFormatFlags.Left;
        string title = isRtl ? "أهم المواقع — توزيع الأصول" : "Top Locations  —  asset distribution";

        TextRenderer.DrawText(g, title,
            _titleFont,
            new Rectangle(card.X + 16, card.Y + 14, card.Width - 32, 28),
            Theme.TextPrimary, align | TextFormatFlags.VerticalCenter);

        if (_locations is null || _locations.Count == 0)
        {
            string noLoc = isRtl ? "لا توجد بيانات موقع" : "No location data available";
            TextRenderer.DrawText(g, noLoc, _bodyFont,
                card, Theme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var top      = _locations.OrderByDescending(x => x.Value).Take(10).ToList();
        int maxCount = top.Max(x => x.Value);

        int dataTop  = card.Y + 50;
        int dataH    = card.Height - 70;
        int rowH     = dataH / top.Count;
        int barH     = Math.Max(10, (int)(rowH * 0.55f));
        int labelW   = 110;

        int barAreaX, barAreaW, countX, labelX;
        var labelTextAlign = isRtl ? TextFormatFlags.Left : TextFormatFlags.Right;
        var countTextAlign = isRtl ? TextFormatFlags.Right : TextFormatFlags.Left;

        if (isRtl)
        {
            labelX = card.Right - 16 - labelW;
            barAreaX = card.X + 50;
            barAreaW = card.Width - 32 - labelW - 50;
            countX = card.X + 16;
        }
        else
        {
            labelX = card.X + 16;
            barAreaX = card.X + 16 + labelW;
            barAreaW = card.Width - 32 - labelW - 50;
            countX = barAreaX + barAreaW + 4;
        }

        // Color gradient for bars
        Color[] barColors =
        {
            Color.FromArgb(59,  130, 246),
            Color.FromArgb(99,  102, 241),
            Color.FromArgb(168, 85,  247),
            Color.FromArgb(236, 72,  153),
            Color.FromArgb(239, 68,  68),
            Color.FromArgb(234, 179, 8),
            Color.FromArgb(34,  197, 94),
            Color.FromArgb(6,   182, 212),
            Color.FromArgb(59,  130, 246),
            Color.FromArgb(99,  102, 241),
        };

        for (int i = 0; i < top.Count; i++)
        {
            var (loc, count) = (top[i].Key, top[i].Value);
            int rowY      = dataTop + i * rowH;
            int barY      = rowY + (rowH - barH) / 2;
            float pct     = maxCount > 0 ? count * 1f / maxCount : 0;
            int   barW    = (int)(barAreaW * pct);
            var   barColor= barColors[i % barColors.Length];

            // Label (truncate if too long)
            string displayLoc = loc.Length > 14 ? loc[..13] + "…" : loc;
            TextRenderer.DrawText(g, displayLoc, _smallBoldFont,
                new Rectangle(labelX, rowY, labelW - 6, rowH),
                Theme.TextSecondary,
                labelTextAlign | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Background track
            g.FillRectangle(_trackBrush, barAreaX, barY, barAreaW, barH);

            // Bar fill with gradient
            if (barW > 0)
            {
                int drawBarX = isRtl ? (barAreaX + barAreaW - barW) : barAreaX;

                using var barBrush = new LinearGradientBrush(
                    new Rectangle(drawBarX, barY, barW + 1, barH),
                    isRtl ? Color.FromArgb(Math.Max(0, barColor.R - 40), Math.Max(0, barColor.G - 40), Math.Max(0, barColor.B - 40)) : barColor,
                    isRtl ? barColor : Color.FromArgb(Math.Max(0, barColor.R - 40), Math.Max(0, barColor.G - 40), Math.Max(0, barColor.B - 40)),
                    LinearGradientMode.Horizontal);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = Rounded(new Rectangle(drawBarX, barY, barW, barH), 4);
                g.FillPath(barBrush, path);
                g.SmoothingMode = SmoothingMode.None;
            }

            // Count value
            TextRenderer.DrawText(g, count.ToString(),
                _smallBoldFont,
                new Rectangle(countX, rowY, 40, rowH),
                Theme.TextPrimary, countTextAlign | TextFormatFlags.VerticalCenter);

            // Row separator
            if (i < top.Count - 1)
                g.FillRectangle(_separatorBrush, card.X + 8, rowY + rowH - 1, card.Width - 16, 1);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUMMARY BAR
    // ═══════════════════════════════════════════════════════════════
    private void DrawSummary(Graphics g, Rectangle area)
    {
        if (_stats is null) return;
        bool isRtl = RightToLeft == RightToLeft.Yes;

        int locCount  = _locations?.Count ?? 0;
        double avgLoc = locCount > 0 ? Math.Round(_totalAssets * 1.0 / locCount, 1) : 0;
        string topLoc = _locations?.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "—";

        var stats = new (string label, string value)[]
        {
            (isRtl ? "إجمالي الأصول" : "Total Assets",     _stats.Total.ToString()),
            (isRtl ? "الأصول المحققة" : "Verified",         $"{_stats.Verified}  ({_stats.VerifiedPct:0.0}%)"),
            (isRtl ? "الأصول المعلقة" : "Pending",          $"{_stats.Pending}"),
            (isRtl ? "المواقع" : "Locations",        locCount.ToString()),
            (isRtl ? "معدل الموقع" : "Avg / Location",   avgLoc.ToString("0.0")),
            (isRtl ? "الموقع الأبرز" : "Top Location",     topLoc.Length > 16 ? topLoc[..15] + "…" : topLoc),
        };

        using var path = Rounded(area, 8);
        g.FillPath(_summaryBgBrush, path);

        int itemW = area.Width / stats.Length;
        for (int i = 0; i < stats.Length; i++)
        {
            int index = isRtl ? (stats.Length - 1 - i) : i;
            var (label, value) = stats[index];
            int x = area.X + i * itemW;

            TextRenderer.DrawText(g, value,
                _summaryValFont,
                new Rectangle(x, area.Y + 2, itemW, 26),
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, label, _sidebarSmFont,
                new Rectangle(x, area.Y + 26, itemW, 18),
                Color.FromArgb(148, 163, 184),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (i > 0)
                g.FillRectangle(_dividerBrush, x, area.Y + 8, 1, 30);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════
    private void DrawEmpty(Graphics g)
    {
        bool isRtl = RightToLeft == RightToLeft.Yes;
        string emptyStr = isRtl ? "لا توجد بيانات بعد — أضف أصولاً أولاً" : "No data available yet — please add assets first";
        TextRenderer.DrawText(g, emptyStr, _titleFont,
            ClientRectangle, Theme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
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
}
