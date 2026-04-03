using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        string connectionString = @"Data Source=c:\Sources\APITeamsV3\APITeamsV3.API\Smart_IDAT.db";
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Fecha, Tipo, Referencia, Mensaje
                FROM TeamsLogOperativo
                ORDER BY Fecha DESC
                LIMIT 20";

            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine("--- RECENT APITEAMSV3 LOGS ---");
                while (reader.Read())
                {
                    Console.WriteLine($"[{reader.GetString(0)}] {reader.GetString(1)} | {reader.GetString(2)} | {reader.GetString(3)}");
                }
            }
        }
    }
}
