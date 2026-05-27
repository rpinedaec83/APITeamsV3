using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;

var encryptionKey = "b14ca5898a4e4133bbce2ea2315a1916";
var centralDbPath = @"E:\APITEAMSV3\publish\api\APITeamsV3_Central.db";

if (!File.Exists(centralDbPath)) { Console.WriteLine($"DB not found: {centralDbPath}"); return; }

string tenantId = "", clientId = "", clientSecret = "";
using (var db = new SqliteConnection($"Data Source={centralDbPath}"))
{
    db.Open();
    var cmd = db.CreateCommand();
    cmd.CommandText = "SELECT GraphTenantId, GraphClientId, GraphClientSecretRef FROM CompanyConfigs WHERE CompanyKey = 'zegel'";
    using var r = cmd.ExecuteReader();
    if (r.Read())
    {
        tenantId     = r["GraphTenantId"]?.ToString() ?? "";
        clientId     = r["GraphClientId"]?.ToString() ?? "";
        var rawSec   = r["GraphClientSecretRef"]?.ToString() ?? "";
        try { clientSecret = Decrypt(rawSec, encryptionKey); } catch { clientSecret = rawSec; }
    }
}

Console.WriteLine($"Tenant: {tenantId} | Client: {clientId}");

// Graph client (app-only)
var cred = new ClientSecretCredential(tenantId, clientId, clientSecret);
var graph = new GraphServiceClient(cred);

var serviceAccountUpn = "app.teams@zegelipae.pe";

// Get user and drive
var user = await graph.Users[serviceAccountUpn].GetAsync(rc => rc.QueryParameters.Select = ["id","displayName"]);
var userId = user?.Id ?? "";
Console.WriteLine($"User ID: {userId}");

var drive = await graph.Users[userId].Drive.GetAsync(rc => rc.QueryParameters.Select = ["id"]);
var driveId = drive?.Id ?? "";
Console.WriteLine($"Drive ID: {driveId}");

// === METHOD 1: Get SharePoint site ID from OneDrive root ===
Console.WriteLine("\n=== Getting SharePoint site for OneDrive ===");
string siteId = "";
try
{
    var root = await graph.Drives[driveId].Root.GetAsync(rc =>
        rc.QueryParameters.Select = ["id","name","sharepointIds"]);

    var spIds = root?.SharepointIds;
    if (spIds != null)
    {
        siteId = spIds.SiteId ?? "";
        Console.WriteLine($"SharePoint Site ID:      {spIds.SiteId}");
        Console.WriteLine($"SharePoint Site URL:     {spIds.SiteUrl}");
        Console.WriteLine($"SharePoint Web ID:       {spIds.WebId}");
        Console.WriteLine($"SharePoint List ID:      {spIds.ListId}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Could not get SharePoint IDs: {ex.Message}");
}

// === METHOD 2: Access recycleBin via SharePoint REST API using bearer token ===
Console.WriteLine("\n=== Querying Recycle Bin via SharePoint REST API ===");
try
{
    // Get token for SharePoint
    var tokenCred = new ClientSecretCredential(tenantId, clientId, clientSecret);
    var tokenContext = new Azure.Core.TokenRequestContext(["https://seesac-my.sharepoint.com/.default"]);
    var token = await tokenCred.GetTokenAsync(tokenContext);

    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    // The actual OneDrive site URL discovered from SharepointIds
    var oneDriveSiteUrl = "https://seesac-my.sharepoint.com/personal/app_teams_zegelipae_pe";
    Console.WriteLine($"Using OneDrive site URL: {oneDriveSiteUrl}");

    // Query recycle bin via SharePoint REST
    var recycleBinUrl = $"{oneDriveSiteUrl}/_api/site/RecycleBin?$select=Id,Title,DirName,Size,DeletedDate,LeafName&$filter=ItemType eq 1&$orderby=DeletedDate desc&$top=100";
    Console.WriteLine($"RecycleBin API URL: {recycleBinUrl}");

    var response = await httpClient.GetAsync(recycleBinUrl);
    Console.WriteLine($"HTTP Status: {response.StatusCode}");

    if (response.IsSuccessStatusCode)
    {
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var items = doc.RootElement
            .GetProperty("value");

        int count = 0;
        int mp4Count = 0;
        foreach (var item in items.EnumerateArray())
        {
            count++;
            var leafName = item.TryGetProperty("LeafName", out var ln) ? ln.GetString() ?? "" : "";
            var size = item.TryGetProperty("Size", out var sz) ? sz.GetInt64() : 0;
            var deleted = item.TryGetProperty("DeletedDate", out var dd) ? dd.GetString() ?? "" : "";
            var title = item.TryGetProperty("Title", out var t) ? t.GetString() ?? "" : "";

            if (leafName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                mp4Count++;
                Console.WriteLine($"  [MP4] {leafName} | {size/1024/1024.0:F1} MB | Deleted: {deleted}");
            }
        }
        Console.WriteLine($"\nTotal items in RecycleBin: {count} | .mp4 files: {mp4Count}");
    }
    else
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Error response: {error[..Math.Min(500, error.Length)]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"SharePoint RecycleBin error: {ex.Message}");
}

// === METHOD 3: Try Graph Sites recycleBin if we have the siteId ===
if (!string.IsNullOrEmpty(siteId))
{
    Console.WriteLine($"\n=== Querying RecycleBin via Graph API (Sites/{siteId}/recycleBin) ===");
    try
    {
        // Use raw HTTP via Graph for recycleBin (not in SDK directly)
        var tokenCred = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var tokenContext = new Azure.Core.TokenRequestContext(["https://graph.microsoft.com/.default"]);
        var token = await tokenCred.GetTokenAsync(tokenContext);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/recycleBin/items?$select=id,name,size,deletedDateTime,lastModifiedDateTime&$top=100";
        Console.WriteLine($"URL: {url}");

        var response = await httpClient.GetAsync(url);
        Console.WriteLine($"HTTP Status: {response.StatusCode}");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var items = doc.RootElement.GetProperty("value");

            int count = 0, mp4Count = 0;
            foreach (var item in items.EnumerateArray())
            {
                count++;
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var size = item.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                var deleted = item.TryGetProperty("deletedDateTime", out var dd) ? dd.GetString() ?? "" : "";

                if (name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    mp4Count++;
                    Console.WriteLine($"  [MP4] {name} | {size/1024/1024.0:F1} MB | Deleted: {deleted}");
                }
            }
            Console.WriteLine($"\nTotal items: {count} | .mp4 files: {mp4Count}");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error: {error[..Math.Min(500, error.Length)]}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Graph recycleBin error: {ex.Message}");
    }
}

// === METHOD 4: Drive special recycle folder ===
Console.WriteLine("\n=== Trying Drive special recycle folder ===");
try
{
    // Try accessing the Recycle Bin as a folder path in Spanish and English
    foreach (var path in new[] { "Papelera de reciclaje", "Recycle Bin", "RecycleBin", ".Trash" })
    {
        try
        {
            var folder = await graph.Drives[driveId].Items["root"].ItemWithPath(path)
                .GetAsync(rc => rc.QueryParameters.Select = ["id","name"]);
            Console.WriteLine($"  Found folder '{path}': ID={folder?.Id}");

            if (!string.IsNullOrEmpty(folder?.Id))
            {
                var children = await graph.Drives[driveId].Items[folder.Id].Children
                    .GetAsync(rc => rc.QueryParameters.Select = ["id","name","size","file","createdDateTime"]);
                var mp4s = (children?.Value ?? []).Where(i => (i.Name ?? "").EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)).ToList();
                Console.WriteLine($"  Children: {children?.Value?.Count ?? 0} total, {mp4s.Count} .mp4 files");
                foreach (var f in mp4s)
                    Console.WriteLine($"    {f.Name} ({(f.Size ?? 0)/1024/1024.0:F1} MB)");
            }
        }
        catch { Console.WriteLine($"  Folder '{path}' not found or not accessible."); }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Drive folder error: {ex.Message}");
}

Console.WriteLine("\nDone.");

// Helpers
static string Decrypt(string cipherText, string keyString)
{
    if (string.IsNullOrEmpty(cipherText)) return cipherText;
    var key = NormalizeKey(keyString);
    const string V2 = "v2:";
    if (cipherText.StartsWith(V2, StringComparison.Ordinal))
    {
        var payload = Convert.FromBase64String(cipherText[V2.Length..]);
        var iv = new byte[16]; var buf = new byte[payload.Length - 16];
        Buffer.BlockCopy(payload, 0, iv, 0, 16);
        Buffer.BlockCopy(payload, 16, buf, 0, buf.Length);
        return DecryptInternal(buf, iv, key);
    }
    return DecryptInternal(Convert.FromBase64String(cipherText), new byte[16], key);
}

static string DecryptInternal(byte[] buf, byte[] iv, byte[] key)
{
    using var aes = Aes.Create(); aes.Key = key; aes.IV = iv;
    using var ms = new MemoryStream(buf);
    using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
    using var sr = new StreamReader(cs); return sr.ReadToEnd();
}

static byte[] NormalizeKey(string k)
{
    try { var h = Convert.FromHexString(k); if (h.Length is 16 or 24 or 32) return h; } catch { }
    var u = Encoding.UTF8.GetBytes(k); if (u.Length is 16 or 24 or 32) return u;
    throw new InvalidOperationException("Key must be 16/24/32 bytes.");
}
