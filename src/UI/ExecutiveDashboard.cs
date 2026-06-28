using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using AssetInventory.Services;

namespace AssetInventory.UI;

public partial class ExecutiveDashboard : Form
{
    private WebView2 _webView = null!;

    public ExecutiveDashboard()
    {
        this.Text = "📈 Executive BI & Financial Analytics";
        this.Size = new System.Drawing.Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Theme.ContentBg;
        
        // Register Load event for non-blocking asynchronous WebView2 initialization
        this.Load += new EventHandler(ExecutiveDashboard_Load);
    }

    private async void ExecutiveDashboard_Load(object? sender, EventArgs e)
    {
        _webView = new WebView2 { Dock = DockStyle.Fill };
        this.Controls.Add(_webView);

        try
        {
            // Asynchronously initialize the WebView2 control core
            await _webView.EnsureCoreWebView2Async(null);
            
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

            // Using postWebMessage is 100% stable in .NET 8 and bypasses COM interop crashes
            _webView.NavigationCompleted += WebView_NavigationCompleted;
            _webView.Source = new Uri("https://sovereign.assets/univer_executive.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2 component: {ex.Message}", "WebView2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            var engine = new PivotEngine();
            string dataJson = engine.GetExecutivePivotJson();
            _webView.CoreWebView2.PostWebMessageAsJson(dataJson);
        }
        catch (Exception ex)
        {
            _webView.CoreWebView2.PostWebMessageAsJson($"[{{\"Error\": {JsonSerializer.Serialize(ex.Message)}}}]");
        }
    }
}
