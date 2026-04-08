using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

static bool TryDecodeHex(string value, out byte[] bytes)
{
    bytes = Array.Empty<byte>();
    if (value.Length % 2 != 0) return false;
    try { bytes = Convert.FromHexString(value); return true; } catch (FormatException) { return false; }
}

static bool TryDecodeBase64(string value, out byte[] bytes)
{
    bytes = Array.Empty<byte>();
    try { bytes = Convert.FromBase64String(value); return true; } catch (FormatException) { return false; }
}

static byte[] NormalizeKey(string configuredKey)
{
    if (TryDecodeHex(configuredKey, out var hexBytes) && (hexBytes.Length is 16 or 24 or 32)) return hexBytes;
    if (TryDecodeBase64(configuredKey, out var base64Bytes) && (base64Bytes.Length is 16 or 24 or 32)) return base64Bytes;
    var utf8Bytes = Encoding.UTF8.GetBytes(configuredKey);
    if (utf8Bytes.Length is 16 or 24 or 32) return utf8Bytes;
    throw new InvalidOperationException("EncryptionKey must be 16, 24, or 32 bytes");
}

static List<byte[]> BuildDecryptKeyCandidates(string configuredKey, byte[] primaryKey)
{
    var candidates = new List<byte[]> { primaryKey };
    if (configuredKey.Length == 32)
    {
        byte[]? alternateKey = null;
        if (TryDecodeHex(configuredKey, out var hexBytes) && hexBytes.Length == 16 && primaryKey.Length == 16)
        {
            alternateKey = Encoding.UTF8.GetBytes(configuredKey);
        }
        else
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(configuredKey);
            if (primaryKey.Length == 32 && utf8Bytes.Length == 32 && TryDecodeHex(configuredKey, out hexBytes) && hexBytes.Length == 16)
            {
                alternateKey = hexBytes;
            }
        }
        if (alternateKey != null && !candidates.Exists(candidate => candidate.AsSpan().SequenceEqual(alternateKey)))
        {
            candidates.Add(alternateKey);
        }
    }
    return candidates;
}

static string DecryptWithKey(string cipherText, byte[] key)
{
    const string VersionPrefix = "v2:";
    if (cipherText.StartsWith(VersionPrefix, StringComparison.Ordinal))
    {
        var payload = Convert.FromBase64String(cipherText[VersionPrefix.Length..]);
        if (payload.Length < 17) throw new CryptographicException("Encrypted payload is invalid.");
        var iv = new byte[16];
        var buffer = new byte[payload.Length - iv.Length];
        Buffer.BlockCopy(payload, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(payload, iv.Length, buffer, 0, buffer.Length);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var memoryStream = new MemoryStream(buffer);
        using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
        using var streamReader = new StreamReader(cryptoStream);
        return streamReader.ReadToEnd();
    }
    using (var aes = Aes.Create())
    {
        aes.Key = key;
        aes.IV = new byte[16];
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var memoryStream = new MemoryStream(Convert.FromBase64String(cipherText));
        using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
        using var streamReader = new StreamReader(cryptoStream);
        return streamReader.ReadToEnd();
    }
}

static string Decrypt(string cipherText, string configuredKey)
{
    var primaryKey = NormalizeKey(configuredKey);
    var candidates = BuildDecryptKeyCandidates(configuredKey, primaryKey);
    CryptographicException? last = null;
    foreach (var key in candidates)
    {
        try { return DecryptWithKey(cipherText, key); }
        catch (CryptographicException ex) { last = ex; }
    }
    throw last ?? new CryptographicException("Unable to decrypt");
}

string configuredKey = Environment.GetEnvironmentVariable("APITEAMSV3_ENCRYPTION_KEY") ?? "b14ca5898a4e4133bbce2ea2315a1916";
using var conn = new SqliteConnection(@"Data Source=C:\Sources\APITeamsV3\APITeamsV3.API\APITeamsV3_Central.db");
conn.Open();
using var cmd = conn.CreateCommand();
cmd.CommandText = "select SmartConnectionString from CompanyConfigs where CompanyKey = 'zegel'";
var encrypted = (string?)cmd.ExecuteScalar();
Console.WriteLine("UsingKey=" + configuredKey);
Console.WriteLine("Encrypted=" + encrypted);
Console.WriteLine("Decrypted=" + Decrypt(encrypted!, configuredKey));
