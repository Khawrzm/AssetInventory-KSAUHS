using System;
using System.Windows.Forms;
using AssetInventory.UI;

// ── تعطيل أي telemetry على مستوى البيئة ───────────────────────────────
Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT",  "1");
Environment.SetEnvironmentVariable("DOTNET_TELEMETRY_OPTOUT",       "1");
Environment.SetEnvironmentVariable("DOTNET_NOLOGO",                  "1");

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
Application.Run(new MainForm());
