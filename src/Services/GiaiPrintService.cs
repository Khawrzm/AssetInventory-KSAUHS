using System;
using System.Drawing;
using System.Drawing.Printing;

namespace AssetInventory.Services;

public class GiaiPrintService
{
    private string _tagNumber;
    private string _description;
    private string _location;

    /// <summary>
    /// Instantiates print document and triggers direct thermal printing using GS1 GIAI formatting.
    /// </summary>
    public void PrintAssetTag(string tagNumber, string description, string location)
    {
        _tagNumber = tagNumber;
        _description = description;
        _location = location;

        PrintDocument pd = new PrintDocument();
        pd.PrintPage += new PrintPageEventHandler(RenderGiaiLabel);
        
        // Bypassing native dialogs for sub-millisecond hardware trigger
        pd.Print();
    }

    private void RenderGiaiLabel(object sender, PrintPageEventArgs ev)
    {
        Graphics g = ev.Graphics;
        
        Font titleFont = new Font("Arial", 10, FontStyle.Bold);
        Font boldFont = new Font("Arial", 9, FontStyle.Bold);
        Font regFont = new Font("Arial", 8);

        // Render header
        g.DrawString("KSAU-HS LOGISTICS", titleFont, Brushes.Black, 10, 10);
        
        // Formatted strictly as: (8004) {TagNumber} (GS1 GIAI standard)
        string giaiText = $"(8004){_tagNumber}";
        g.DrawString(giaiText, boldFont, Brushes.Black, 10, 28);

        // Native 1D barcode visual simulation
        int startX = 10;
        int startY = 46;
        int height = 28;
        int width = 120;
        
        for (int i = 0; i < width; i += 4)
        {
            int thickness = ((i % 3) == 0) ? 2 : 1;
            g.FillRectangle(Brushes.Black, startX + i, startY, thickness, height);
        }

        // Draw description and location below the barcode
        g.DrawString($"Desc: {Truncate(_description, 28)}", regFont, Brushes.Black, 10, 80);
        g.DrawString($"Loc: {_location}", regFont, Brushes.Black, 10, 95);
    }

    private string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
    }
}
