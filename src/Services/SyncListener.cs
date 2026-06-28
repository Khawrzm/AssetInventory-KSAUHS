using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AssetInventory.Core;
using AssetInventory.Models;
using AssetInventory.Data;

namespace AssetInventory.Services
{
    public sealed class SyncListener
    {
        private static readonly SyncListener _instance = new();
        public static SyncListener Instance => _instance;

        private HttpListener? _listener;
        private bool _isRunning;
        private readonly AssetRepository _repo = new();

        public event Action<Asset>? ScanReceived;
        public int Port { get; private set; } = 8080;
        public List<string> BoundAddresses { get; } = new();

        private SyncListener() { }

        public void Start(int port = 8080)
        {
            if (_isRunning) return;
            Port = port;
            BoundAddresses.Clear();

            try
            {
                _listener = new HttpListener();
                
                // Add loopback
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                BoundAddresses.Add($"http://127.0.0.1:{Port}");

                // Query and bind to all local active IPv4 addresses to allow Wi-Fi intranet sync
                var localIPs = GetLocalIPv4Addresses();
                foreach (var ip in localIPs)
                {
                    string prefix = $"http://{ip}:{Port}/";
                    _listener.Prefixes.Add(prefix);
                    BoundAddresses.Add($"http://{ip}:{Port}");
                }

                _listener.Start();
                _isRunning = true;
                
                // Start listener loop in thread pool
                Task.Run(() => ListenLoop());
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _listener = null;
                throw new InvalidOperationException($"Failed to start local sync server on port {Port}: {ex.Message}", ex);
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
            finally
            {
                _listener = null;
            }
        }

        public bool IsRunning => _isRunning;

        private async Task ListenLoop()
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(ctx));
                }
                catch
                {
                    // Listener stopped or errored out
                    break;
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;

            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-Sync-Token");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = (int)HttpStatusCode.OK;
                res.Close();
                return;
            }

            try
            {
                string path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "";
                if (path == "/api/v1/status" && req.HttpMethod == "GET")
                {
                    var stats = _repo.GetStats();
                    var payload = new
                    {
                        status = "active",
                        device = Environment.MachineName,
                        total_assets = stats.Total,
                        verified_assets = stats.Verified
                    };
                    SendJson(res, HttpStatusCode.OK, payload);
                }
                else if (path == "/api/v1/scan" && req.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                    string body = await reader.ReadToEndAsync();
                    var scan = JsonSerializer.Deserialize<ScanPayload>(body);

                    if (scan == null || string.IsNullOrWhiteSpace(scan.TagNumber))
                    {
                        SendJson(res, HttpStatusCode.BadRequest, new { error = "Invalid payload or missing TagNumber" });
                        return;
                    }

                    // Sanitize tag to protect against SQL injections
                    string cleanTag = ScannerService.Sanitize(scan.TagNumber);
                    if (string.IsNullOrEmpty(cleanTag))
                    {
                        SendJson(res, HttpStatusCode.BadRequest, new { error = "TagNumber contains invalid characters" });
                        return;
                    }

                    // Perform database update
                    var asset = _repo.GetByTag(cleanTag);
                    bool isNew = asset == null;

                    if (asset == null)
                    {
                        asset = new Asset
                        {
                            TagNumber = cleanTag,
                            AssetDescription = scan.AssetDescription ?? "Scanned Asset",
                            MajorLoc = scan.MajorLoc ?? "Mobile Sync Location",
                            MinorLoc = scan.MinorLoc ?? "Room Not Specified",
                            Status = string.IsNullOrWhiteSpace(scan.Status) ? "VERIFIED" : scan.Status.ToUpperInvariant(),
                            Note = scan.Note ?? "Synced from Mobile device"
                        };
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(scan.AssetDescription)) asset.AssetDescription = scan.AssetDescription;
                        if (!string.IsNullOrWhiteSpace(scan.MajorLoc)) asset.MajorLoc = scan.MajorLoc;
                        if (!string.IsNullOrWhiteSpace(scan.MinorLoc)) asset.MinorLoc = scan.MinorLoc;
                        if (!string.IsNullOrWhiteSpace(scan.Status)) asset.Status = scan.Status.ToUpperInvariant();
                        if (!string.IsNullOrWhiteSpace(scan.Note)) asset.Note = scan.Note;
                    }

                    asset.DataHash = asset.GenerateHash();
                    _repo.Save(asset, "MobileSync");

                    // Trigger UI updates
                    ScanReceived?.Invoke(asset);

                    SendJson(res, HttpStatusCode.OK, new { success = true, is_new = isNew, tag = cleanTag });
                }
                else
                {
                    SendJson(res, HttpStatusCode.NotFound, new { error = "Endpoint not found" });
                }
            }
            catch (Exception ex)
            {
                SendJson(res, HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        private void SendJson(HttpListenerResponse res, HttpStatusCode code, object obj)
        {
            try
            {
                res.StatusCode = (int)code;
                res.ContentType = "application/json";
                byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
                res.ContentLength64 = buffer.Length;
                res.OutputStream.Write(buffer, 0, buffer.Length);
                res.OutputStream.Close();
            }
            catch { }
        }

        private static List<string> GetLocalIPv4Addresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && 
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                list.Add(ip.Address.ToString());
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private class ScanPayload
        {
            public string TagNumber { get; set; } = string.Empty;
            public string? AssetDescription { get; set; }
            public string? MajorLoc { get; set; }
            public string? MinorLoc { get; set; }
            public string? Status { get; set; }
            public string? Note { get; set; }
        }
    }
}
