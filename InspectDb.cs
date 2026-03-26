using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        string dbPath = @"E:\APITEAMSV3\publish\api\APITeamsV3_Central.db";
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(CompanyConfigs);";
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine("Column Name | Type");
                Console.WriteLine("-------------------");
                while (reader.Read())
                {
                    Console.WriteLine($"{reader["name"]} | {reader["type"]}");
                }
            }
        }
    }
}
