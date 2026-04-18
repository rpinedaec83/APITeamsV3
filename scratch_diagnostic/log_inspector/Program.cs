using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

var tenantKey = "zegel";
var centralDbPath = "E:\\APITEAMSV3\\publish\\api\\APITeamsV3_Central.db";

string encryptedConn = "";
using (var connection = new SqliteConnection($"Data Source={centralDbPath}")) {
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT SmartConnectionString FROM CompanyConfigs WHERE CompanyKey = @key";
    command.Parameters.AddWithValue("@key", tenantKey);
    encryptedConn = command.ExecuteScalar()?.ToString() ?? "";
}

var encryptionKey = "b14ca5898a4e4133bbce2ea2315a1916";
var decryptedConn = DecryptRobust(encryptedConn, encryptionKey);

using (var conn = new SqlConnection(decryptedConn)) {
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TeamsHorarios'";
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) {
        Console.WriteLine(reader["COLUMN_NAME"]);
    }
}

static string DecryptRobust(string cipherText, string keyString)
{
    const string VersionPrefix = "v2:";
    byte[] key = NormalizeKey(keyString);
    byte[] payload;
    byte[] iv = new byte[16];
    byte[] buffer;
    if (cipherText.StartsWith(VersionPrefix)) {
        payload = Convert.FromBase64String(cipherText.Substring(VersionPrefix.Length));
    } else {
        payload = Convert.FromBase64String(cipherText);
    }
    Buffer.BlockCopy(payload, 0, iv, 0, 16);
    buffer = new byte[payload.Length - 16];
    Buffer.BlockCopy(payload, 16, buffer, 0, buffer.Length);

    using var aes = Aes.Create();
    aes.Key = key;
    aes.IV = iv;
    using var decryptor = aes.CreateDecryptor();
    using var ms = new MemoryStream(buffer);
    using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
    using var reader = new StreamReader(cs);
    return reader.ReadToEnd();
}

static byte[] NormalizeKey(string configuredKey)
{
    if (configuredKey.Length == 32) {
        try {
            var bytes = new byte[16];
            for (int i = 0; i < 16; i++) {
                bytes[i] = Convert.ToByte(configuredKey.Substring(i * 2, 2), 16);
            }
            return bytes;
        } catch {}
    }
    return Encoding.UTF8.GetBytes(configuredKey);
}
