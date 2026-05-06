
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using System.Linq;

namespace LogCleanerProject
{
    class Program
    {
        private const string VersionPrefix = "v2:";
        private static readonly byte[] Key = NormalizeKey("b14ca5898a4e4133bbce2ea2315a1916");

        static void Main(string[] args)
        {
            string centralDb = @"E:\APITEAMSV3\publish\api\APITeamsV3_Central.db";
            Console.WriteLine("--- Iniciando Limpieza de Logs ---");
            
            if (!File.Exists(centralDb))
            {
                Console.WriteLine("Error: No se encontro la base de datos central en " + centralDb);
                return;
            }

            try {
                using (var conn = new SqliteConnection("Data Source=" + centralDb))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT CompanyKey, Name, SmartConnectionString FROM CompanyConfigs WHERE IsActive = 1";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string key = reader.GetString(0);
                                string name = reader.GetString(1);
                                string encrypted = reader.GetString(2);
                                
                                Console.WriteLine("Procesando: " + name + " (" + key + ")...");
                                try {
                                    string connectionString = Decrypt(encrypted);
                                    
                                    if (connectionString.Contains("Data Source=") && (connectionString.Contains(".db") || connectionString.Contains(".sqlite")))
                                    {
                                        CleanSqlite(connectionString);
                                    }
                                    else
                                    {
                                        CleanSqlServer(connectionString);
                                    }
                                } catch (Exception ex) {
                                    Console.WriteLine("  [Error Decrypt/Process]: " + ex.Message);
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Error critico: " + ex.Message);
            }
            Console.WriteLine("--- Fin de la Limpieza ---");
        }

        private static void CleanSqlite(string connectionString)
        {
            try {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM TeamsLogOperativo";
                        int count = cmd.ExecuteNonQuery();
                        Console.WriteLine("  [SQLite] Exito.");
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("  [Error SQLite]: " + ex.Message);
            }
        }

        private static void CleanSqlServer(string connectionString)
        {
            try {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM TeamsLogOperativo";
                        int count = cmd.ExecuteNonQuery();
                        Console.WriteLine("  [SQL Server] Exito.");
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("  [Error SQL Server]: " + ex.Message);
            }
        }

        private static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            if (cipherText.StartsWith(VersionPrefix))
            {
                var payload = Convert.FromBase64String(cipherText.Substring(VersionPrefix.Length));
                var iv = new byte[16];
                var buffer = new byte[payload.Length - 16];
                Buffer.BlockCopy(payload, 0, iv, 0, 16);
                Buffer.BlockCopy(payload, 16, buffer, 0, buffer.Length);
                return DecryptInternal(buffer, iv, Key);
            }
            return DecryptInternal(Convert.FromBase64String(cipherText), new byte[16], Key);
        }

        private static string DecryptInternal(byte[] buffer, byte[] iv, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(buffer))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static byte[] NormalizeKey(string configuredKey)
        {
            try { 
                byte[] hex = new byte[configuredKey.Length / 2];
                for (int i = 0; i < hex.Length; i++) {
                    hex[i] = Convert.ToByte(configuredKey.Substring(i * 2, 2), 16);
                }
                return hex;
            } catch {}
            return Encoding.UTF8.GetBytes(configuredKey);
        }
    }
}
