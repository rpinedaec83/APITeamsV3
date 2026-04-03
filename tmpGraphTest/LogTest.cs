using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace GraphTest
{
    public class LogTest
    {
        private readonly ITestOutputHelper _output;

        public LogTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void QueryLogs()
        {
            string connectionString = @"Data Source=c:\Sources\APITeamsV3\APITeamsV3.API\Smart_IDAT.db";
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Fecha, Tipo, EntidadAfectada, Referencia, Mensaje FROM TeamsLogOperativo WHERE Referencia = 'a57f02b1-1f41-4c87-8da1-7f0f2ed4869b' OR Referencia = 'fdcc3a85-6879-4eb8-955e-20b881e3f1f8' OR Mensaje LIKE '%fpprofesor%' OR Mensaje LIKE '%planeamiento%' ORDER BY Fecha DESC LIMIT 20";
                
                _output.WriteLine("--- RECENT APITEAMSV3 LOGS ---");
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var fecha = reader.IsDBNull(0) ? "" : reader.GetDateTime(0).ToString("yyyy-MM-dd HH:mm:ss");
                        var tipo = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        var entidad = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        var referencia = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        var mensaje = reader.IsDBNull(4) ? "" : reader.GetString(4);
                        
                        _output.WriteLine($"[{fecha}] {tipo} | {entidad} | {referencia} | {mensaje}");
                    }
                }
            }
        }
    }
}
