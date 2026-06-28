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
/// هذا ما لا يستطيع Excel فعله تلقائياً بدون Pivot Table.
/// </summary>
internal sealed class AnalyticsPanel : Panel
{
    private AssetStats?              _stats;
    private Dictionary<string, int>? _locations;
    private int                      _totalAssets;

    public AnalyticsPanel()
    {
        DoubleBuffered = true;
        BackColor      = Theme.ContentBg;
        Dock           = DockStyle.Fill;
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

        int pad   = 20;
        int W     = ClientSize.Width  - pad * 2;
        int H     = ClientSize.Height - pad * 2;
        int leftW = (int)(W * 0.42f);
        int rightW= W - leftW - 16;

        // ── Left card: Donut chart ─────────────────────────────────────
        var leftRect = new Rectangle(pad, pad, leftW, H - 54);
        DrawCard(g, leftRect);
        DrawDonut(g, leftRect);

        // ── Right card: Location bars ──────────────────────────────────
        var rightRect = new Rectangle(pad + leftW + 16, pad, rightW, H - 54);
        DrawCard(g, rightRect);
        DrawLocationBars(g, rightRect);

        // ── Bottom summary bar ─────────────────────────────────────────
        var bottomRect = new Rectangle(pad, H - 26, W, 46);
        DrawSummary(g, bottomRect);
    }

    // ═══════════════════════════════════════════════════════════════
    //  CARD BACKGROUND
    // ═══════════════════════════════════════════════════════════════
    private static void DrawCard(Graphics g, Rectangle r)
    {
        // Shadow
        g.FillRectangle(new SolidBrush(Color.FromArgb(12, 0, 0, 0)),
            new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height));
        // Card
        using var path = Rounded(r, 8);
        g.FillPath(Brushes.White, path);
        using var pen = new Pen(Theme.Border, 1f);
        g.DrawPath(pen, path);
    }

    // ═══════════════════════════════════════════════════════════════
    //  DONUT CHART
    // ═══════════════════════════════════════════════════════════════
    private void DrawDonut(Graphics g, Rectangle card)
    {
        var stats = _stats!;

        // Title
        TextRenderer.DrawText(g, "Inventory Health",
            new Font("Segoe UI", 11f, FontStyle.Bold),
            new Rectangle(card.X + 16, card.Y + 14, card.Width - 32, 28),
            Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        // Donut dimensions
        int donutSize = Math.Min(card.Width - 40, 200);
        int cx        = card.X + (card.Width - donutSize) / 2;
        int cy        = card.Y + 52;
        var donutRect = new Rectangle(cx, cy, donutSize, donutSize);

        // Segments
        var segments = new (string label, int count, Color color)[]
        {
            ("Verified",    stats.Verified,    Theme.Green),
            ("Pending",     stats.Pending,     Theme.Yellow),
            ("Disposed",    stats.Disposed,    Theme.Red),
            ("Transferred", stats.Transferred, Theme.Cyan),
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
                g.FillPie(new SolidBrush(color), segRect, start + 1f, sweep - 2f);
                start += sweep;
            }

            // Gray ring for zero-count segments
            if (stats.Total == 0)
                g.FillEllipse(new SolidBrush(Color.FromArgb(229, 231, 235)), donutRect);

            // Donut hole
            int holeSize  = (int)(donutSize * 0.6f);
            int holeOffset = (donutSize - holeSize) / 2;
            var holeRect  = new Rectangle(cx + holeOffset, cy + holeOffset, holeSize, holeSize);
            g.FillEllipse(Brushes.White, holeRect);

            // Center text
            double pct = stats.VerifiedPct;
            var bigFont = new Font("Segoe UI", donutSize * 0.16f, FontStyle.Bold);
            var smFont  = new Font("Segoe UI", donutSize * 0.07f);
            TextRenderer.DrawText(g, $"{pct:0}%", bigFont, holeRect, Theme.TextPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            var smRect = new Rectangle(holeRect.X, holeRect.Y + (int)(holeSize * 0.35f), holeSize, (int)(holeSize * 0.2f));
            TextRenderer.DrawText(g, "Verified", smFont, smRect, Theme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else
        {
            // Empty state
            g.DrawEllipse(new Pen(Theme.Border, 2f), donutRect);
            TextRenderer.DrawText(g, "لا توجد بيانات",
                Theme.FBody, donutRect, Theme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // Legend
        int legendY = cy + donutSize + 16;
        int legendX = card.X + 16;
        int legW    = card.Width - 32;
        int legH    = Math.Max(0, card.Bottom - legendY - 12);

        DrawLegend(g, segments, stats.Total, new Rectangle(legendX, legendY, legW, legH));
    }

    private static void DrawLegend(Graphics g,
        (string label, int count, Color color)[] items,
        int total,
        Rectangle area)
    {
        int itemH = 24;
        int y     = area.Y;

        foreach (var (label, count, color) in items)
        {
            if (y + itemH > area.Bottom) break;

            // Color dot
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(new SolidBrush(color), area.X, y + 6, 12, 12);
            g.SmoothingMode = SmoothingMode.None;

            // Label
            TextRenderer.DrawText(g, label, Theme.FSmall,
                new Rectangle(area.X + 18, y, area.Width / 2 - 10, itemH),
                Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // Count
            TextRenderer.DrawText(g, count.ToString(), new Font("Segoe UI", 8.5f, FontStyle.Bold),
                new Rectangle(area.Right - 70, y, 35, itemH),
                Theme.TextPrimary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            // Percentage
            double pct = total > 0 ? count * 100.0 / total : 0;
            TextRenderer.DrawText(g, $"{pct:0.0}%", Theme.FSmall,
                new Rectangle(area.Right - 34, y, 34, itemH),
                Theme.TextSecondary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            y += itemH;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  LOCATION BAR CHART
    // ═══════════════════════════════════════════════════════════════
    private void DrawLocationBars(Graphics g, Rectangle card)
    {
        TextRenderer.DrawText(g, "Top Locations  —  asset distribution",
            new Font("Segoe UI", 11f, FontStyle.Bold),
            new Rectangle(card.X + 16, card.Y + 14, card.Width - 32, 28),
            Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        if (_locations is null || _locations.Count == 0)
        {
            TextRenderer.DrawText(g, "لا توجد بيانات موقع", Theme.FBody,
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
        int barAreaX = card.X + 16 + labelW;
        int barAreaW = card.Width - 32 - labelW - 50;

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
            TextRenderer.DrawText(g, displayLoc, Theme.FSmall,
                new Rectangle(card.X + 16, rowY, labelW - 6, rowH),
                Theme.TextSecondary,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Background track
            g.FillRectangle(new SolidBrush(Color.FromArgb(241, 245, 249)),
                barAreaX, barY, barAreaW, barH);

            // Bar fill with gradient
            if (barW > 0)
            {
                using var barBrush = new LinearGradientBrush(
                    new Rectangle(barAreaX, barY, barW + 1, barH),
                    barColor, Color.FromArgb(Math.Max(0, barColor.R - 40),
                                             Math.Max(0, barColor.G - 40),
                                             Math.Max(0, barColor.B - 40)),
                    LinearGradientMode.Horizontal);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = Rounded(new Rectangle(barAreaX, barY, barW, barH), 4);
                g.FillPath(barBrush, path);
                g.SmoothingMode = SmoothingMode.None;
            }

            // Count value
            TextRenderer.DrawText(g, count.ToString(),
                new Font("Segoe UI", 8.5f, FontStyle.Bold),
                new Rectangle(barAreaX + barAreaW + 4, rowY, 40, rowH),
                Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // Row separator
            if (i < top.Count - 1)
                g.FillRectangle(new SolidBrush(Color.FromArgb(241, 245, 249)),
                    card.X + 8, rowY + rowH - 1, card.Width - 16, 1);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUMMARY BAR
    // ═══════════════════════════════════════════════════════════════
    private void DrawSummary(Graphics g, Rectangle area)
    {
        if (_stats is null) return;

        int locCount  = _locations?.Count ?? 0;
        double avgLoc = locCount > 0 ? Math.Round(_totalAssets * 1.0 / locCount, 1) : 0;
        string topLoc = _locations?.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "—";

        var stats = new (string label, string value)[]
        {
            ("Total Assets",     _stats.Total.ToString()),
            ("Verified",         $"{_stats.Verified}  ({_stats.VerifiedPct:0.0}%)"),
            ("Pending",          $"{_stats.Pending}"),
            ("Locations",        locCount.ToString()),
            ("Avg / Location",   avgLoc.ToString("0.0")),
            ("Top Location",     topLoc.Length > 16 ? topLoc[..15] + "…" : topLoc),
        };

        using var cardBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var path      = Rounded(area, 8);
        g.FillPath(cardBrush, path);

        int itemW = area.Width / stats.Length;
        for (int i = 0; i < stats.Length; i++)
        {
            var (label, value) = stats[i];
            int x = area.X + i * itemW;

            TextRenderer.DrawText(g, value,
                new Font("Segoe UI", 12f, FontStyle.Bold),
                new Rectangle(x, area.Y + 2, itemW, 26),
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, label, Theme.FSidebarSm,
                new Rectangle(x, area.Y + 26, itemW, 18),
                Color.FromArgb(148, 163, 184),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (i > 0)
                g.FillRectangle(new SolidBrush(Color.FromArgb(40, 255, 255, 255)),
                    x, area.Y + 8, 1, 30);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════
    private void DrawEmpty(Graphics g)
    {
        TextRenderer.DrawText(g, "لا توجد بيانات بعد — أضف أصولاً أولاً", Theme.FTitle,
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
