using System;
using Microsoft.Data.Sqlite;
class Program {
    static void Main() {
        try {
            var conn = new SqliteConnection("Data Source=E:\\APITEAMSV3\\publish\\api\\APITeamsV3_Central.db");
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Data FROM [Hangfire.State] WHERE JobId = 2165 AND Name = 'Failed' ORDER BY Id DESC LIMIT 1";
            var reader = cmd.ExecuteReader();
            if(reader.Read()) {
                Console.WriteLine(reader.GetString(0));
            } else {
                Console.WriteLine("No failed state found for job 2165.");
            }
        } catch (Exception ex) {
            Console.WriteLine(ex.Message);
        }
    }
}
