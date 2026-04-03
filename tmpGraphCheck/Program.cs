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
            command.CommandText = "SELECT Fecha, Tipo, EntidadAfectada, Referencia, Mensaje FROM TeamsLogOperativo WHERE Referencia = 'a57f02b1-1f41-4c87-8da1-7f0f2ed4869b' OR Referencia = 'fpprofesor@zegel.pe' OR Referencia = 'planeamiento@zegelipae.pe' ORDER BY Fecha DESC LIMIT 25";
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine("--- LOG RESULTS ---");
                while (reader.Read())
                {
                    var fecha = reader.IsDBNull(0) ? "" : reader.GetDateTime(0).ToString("yyyy-MM-dd HH:mm:ss");
                    var tipo = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var entidad = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    var referencia = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    var mensaje = reader.IsDBNull(4) ? "" : reader.GetString(4);
                    
                    Console.WriteLine($"[{fecha}] {tipo} | {entidad} | {referencia} | {mensaje}");
                }
            }
        }
    }
}
