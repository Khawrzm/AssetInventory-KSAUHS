using System;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using AssetInventory.Services;

namespace AssetInventory.UI;

[ComVisible(true)]
public class FinancialBridge
{
    public string GetSovereignData()
    {
        try
        {
            using var engine = new PivotEngine();
            return engine.GetExecutivePivotJson();
        }
        catch (Exception ex)
        {
            return $"[{{\"Error\": {JsonSerializer.Serialize(ex.Message)}}}]";
        }
    }
}

public partial class ExecutiveDashboard : Form
{
    private WebView2 _webView = null!;

    public ExecutiveDashboard()
    {
        this.Text = "📈 Executive BI & Financial Analytics";
        this.Size = new System.Drawing.Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Theme.ContentBg;
        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        _webView = new WebView2 { Dock = DockStyle.Fill };
        this.Controls.Add(_webView);

        try
        {
            await _webView.EnsureCoreWebView2Async();
            
            string uiFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "UI");
            if (!Directory.Exists(uiFolder))
            {
                uiFolder = Path.Combine(Application.StartupPath, "UI");
            }

            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "sovereign.assets",
                uiFolder,
                CoreWebView2HostResourceAccessKind.Allow
            );

            _webView.CoreWebView2.AddHostObjectToScript("bridge", new FinancialBridge());
            _webView.Source = new Uri("https://sovereign.assets/univer_executive.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2 component: {ex.Message}", "WebView2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
