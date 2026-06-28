using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using AssetInventory.Models;

namespace AssetInventory.Services;

public static class ExcelExportService
{
    public static void ExportToXlsx(List<Asset> assets, string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Asset Inventory");

        // ── Title row ────────────────────────────────────────────────────
        var titleCell = ws.Cell(1, 1);
        titleCell.Value = $"KSAUHS — Asset Inventory Report   |   {DateTime.Now:dd MMM yyyy}";
        titleCell.Style.Font.Bold      = true;
        titleCell.Style.Font.FontSize  = 14;
        titleCell.Style.Font.FontColor = XLColor.FromArgb(15, 23, 42);
        ws.Range(1, 1, 1, 7).Merge();
        ws.Row(1).Height = 28;

        ws.Row(2).Height = 6; // spacer

        // ── Column headers ───────────────────────────────────────────────
        string[] headers = { "#", "TAG NUMBER", "DESCRIPTION", "LOCATION", "SUB-LOC", "STATUS", "NOTES" };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(3, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold      = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(15, 23, 42);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder  = XLBorderStyleValues.Thin;
            cell.Style.Border.BottomBorderColor = XLColor.FromArgb(59, 130, 246);
        }
        ws.Row(3).Height = 22;

        // ── Data rows ─────────────────────────────────────────────────────
        for (int i = 0; i < assets.Count; i++)
        {
            var a   = assets[i];
            int row = i + 4;
            bool odd = i % 2 == 1;

            var rowBg = odd
                ? XLColor.FromArgb(248, 250, 252)
                : XLColor.White;

            void WriteCell(int col, object val, bool centered = false)
            {
                var cell = ws.Cell(row, col);
                string strVal = val?.ToString() ?? "";

                // Prevent formula injection in Excel by escaping leading triggers
                char[] triggerChars = { '=', '+', '-', '@', '\t', '\r' };
                if (strVal.Length > 0 && System.Array.Exists(triggerChars, c => strVal[0] == c))
                {
                    strVal = "'" + strVal;
                }

                cell.Value = strVal;
                cell.Style.Fill.BackgroundColor = rowBg;
                cell.Style.Font.FontColor = XLColor.FromArgb(15, 23, 42);
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                cell.Style.Border.BottomBorderColor = XLColor.FromArgb(226, 232, 240);
                if (centered)
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            WriteCell(1, i + 1, centered: true);
            WriteCell(2, a.TagNumber);
            WriteCell(3, a.AssetDescription);
            WriteCell(4, a.MajorLoc);
            WriteCell(5, a.MinorLoc);
            WriteCell(7, a.Note);

            // Status cell with badge-like coloring
            var statusCell = ws.Cell(row, 6);
            statusCell.Value = a.Status;
            statusCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            statusCell.Style.Font.Bold = true;
            statusCell.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            statusCell.Style.Border.BottomBorderColor = XLColor.FromArgb(226, 232, 240);

            statusCell.Style.Fill.BackgroundColor = a.Status.ToUpperInvariant() switch
            {
                "VERIFIED"    => XLColor.FromArgb(220, 252, 231),
                "PENDING"     => XLColor.FromArgb(254, 249, 195),
                "DISPOSED"    => XLColor.FromArgb(254, 226, 226),
                "TRANSFERRED" => XLColor.FromArgb(219, 234, 254),
                _             => XLColor.FromArgb(241, 245, 249)
            };
            statusCell.Style.Font.FontColor = a.Status.ToUpperInvariant() switch
            {
                "VERIFIED"    => XLColor.FromArgb(21,  128, 61),
                "PENDING"     => XLColor.FromArgb(133, 77,  14),
                "DISPOSED"    => XLColor.FromArgb(185, 28,  28),
                "TRANSFERRED" => XLColor.FromArgb(30,  64, 175),
                _             => XLColor.FromArgb(71,  85, 105)
            };
        }

        // ── Summary row ──────────────────────────────────────────────────
        int summaryRow = assets.Count + 5;
        var sumCell = ws.Cell(summaryRow, 1);
        sumCell.Value = $"Total: {assets.Count} assets  |  Exported: {DateTime.Now:dd/MM/yyyy HH:mm}";
        sumCell.Style.Font.Italic    = true;
        sumCell.Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);
        ws.Range(summaryRow, 1, summaryRow, 7).Merge();

        // ── Column widths ─────────────────────────────────────────────────
        ws.Column(1).Width  = 6;
        ws.Column(2).Width  = 16;
        ws.Column(3).Width  = 42;
        ws.Column(4).Width  = 18;
        ws.Column(5).Width  = 16;
        ws.Column(6).Width  = 14;
        ws.Column(7).Width  = 28;

        // Freeze header rows
        ws.SheetView.FreezeRows(3);

        // Auto-filter
        ws.Range(3, 1, 3 + assets.Count, 7).SetAutoFilter();

        wb.SaveAs(filePath);
    }
}
