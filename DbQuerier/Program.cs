using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

class Program {
    static void Main() {
        var keyHex = "b14ca5898a4e4133bbce2ea2315a1916";
        var key = Convert.FromHexString(keyHex);
        var cipher = "fdb0mbddPsvSTC4id09+BqUX/kSxEm6ih4/ZAsIB0+FVIWqnmN+rcmFOPy8kHBKuIXpbzf4mgGVK7uZ3nwZg0NZIiyycC4xGiZe8dgnfOOiF7UX73xOJj6mipcBI2hYUmBtLgoCNj030ZGCI9WVVO1vRuu9RJ+ANmMEln6TcitSWVCHRxkqkL5ov41CuvYYobQnedjWFVXsXcteQfyuTHA==";
        var payload = Convert.FromBase64String(cipher);
        var iv = new byte[16];
        var buffer = new byte[payload.Length - 16];
        Buffer.BlockCopy(payload, 0, iv, 0, 16);
        Buffer.BlockCopy(payload, 16, buffer, 0, buffer.Length);
        
        string connStr = "";
        using (var aes = Aes.Create()) {
            aes.Key = key;
            aes.IV = iv;
            using (var ms = new MemoryStream(buffer))
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (var sr = new StreamReader(cs)) {
                connStr = sr.ReadToEnd();
            }
        }
        
        using (var sqlConn = new SqlConnection(connStr)) {
            sqlConn.Open();
            var cmd = sqlConn.CreateCommand();
            cmd.CommandText = "SELECT TOP 1 Id, JobId, Name FROM [Hangfire].[State] WHERE JobId > 2168 ORDER BY Id DESC";
            var reader = cmd.ExecuteReader();
            if (reader.Read()) {
                Console.WriteLine("New Job Found: " + reader.GetInt64(1) + " Status: " + reader.GetString(2));
            } else {
                Console.WriteLine("No job found after 2168.");
            }
        }
    }
}
