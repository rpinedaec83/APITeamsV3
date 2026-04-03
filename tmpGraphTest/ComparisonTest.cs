using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Data.Sqlite;
using Azure.Identity;
using Microsoft.Graph;
using Xunit.Abstractions;

namespace GraphTest
{
    public class ComparisonTest
    {
        private readonly ITestOutputHelper _output;

        public ComparisonTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task CompareDbWithGraph()
        {
            string groupId = "a57f02b1-1f41-4c87-8da1-7f0f2ed4869b";
            string companyKey = "zegel";

            string centralDbPath = @"Data Source=c:\Sources\APITeamsV3\APITeamsV3.API\APITeamsV3_Central.db";
            string smartDbPath = @"Data Source=c:\Sources\APITeamsV3\APITeamsV3.API\Smart_IDAT.db";

            string tenantId = "", clientId = "", clientSecret = "";

            // 1. Get Graph Credentials
            using (var conn = new SqliteConnection(centralDbPath))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT GraphTenantId, GraphClientId, GraphClientSecretRef FROM CompanyConfigs WHERE CompanyKey = @key";
                cmd.Parameters.AddWithValue("@key", companyKey);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        tenantId = reader.GetString(0);
                        clientId = reader.GetString(1);
                        clientSecret = reader.GetString(2);
                    }
                }
            }

            // 2. Get Intended Data from Smart DB
            string sectionId = "";
            string intendedTeacher = "";
            List<string> intendedStudents = new List<string>();

            using (var conn = new SqliteConnection(smartDbPath))
            {
                conn.Open();
                
                // Get Section ID from Group ID
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT IdSeccionSmart FROM TeamsEquipos WHERE IdTeamsGroup = @groupId";
                cmd.Parameters.AddWithValue("@groupId", groupId);
                var result = cmd.ExecuteScalar();
                if (result != null) sectionId = result.ToString();

                if (!string.IsNullOrEmpty(sectionId))
                {
                    // Get Intended Teacher
                    cmd.CommandText = "SELECT EmailFacilitador FROM TeamsProgramacionGeneral WHERE IdCurso = @sectionId";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@sectionId", sectionId);
                    var teacherResult = cmd.ExecuteScalar();
                    if (teacherResult != null) intendedTeacher = teacherResult.ToString();

                    // Get Intended Students (Active enrollments only)
                    cmd.CommandText = @"
                        SELECT a.EmailInstitucion 
                        FROM AlumnoCurso ac
                        JOIN Alumno a ON ac.IdAlumno = a.IdAlumno
                        WHERE ac.IdSeccion = @sectionId AND ac.EsMatricula = 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            intendedStudents.Add(reader.GetString(0).ToLower().Trim());
                        }
                    }
                }
            }

            _output.WriteLine($"Checking Section: {sectionId} (Group: {groupId})");
            _output.WriteLine($"Intended Teacher: {intendedTeacher}");
            _output.WriteLine($"Intended Students Total: {intendedStudents.Count}");

            // 3. Get Actual Data from Graph
            var options = new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud };
            var creds = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
            var graphClient = new GraphServiceClient(creds);

            List<string> actualOwners = new List<string>();
            List<string> actualMembers = new List<string>();

            try
            {
                var owners = await graphClient.Groups[groupId].Owners.GetAsync();
                foreach (var o in owners.Value)
                {
                    if (o is Microsoft.Graph.Models.User u)
                    {
                        actualOwners.Add(u.Mail?.ToLower().Trim());
                        actualOwners.Add(u.UserPrincipalName?.ToLower().Trim());
                    }
                }

                var members = await graphClient.Groups[groupId].Members.GetAsync();
                foreach (var m in members.Value)
                {
                    if (m is Microsoft.Graph.Models.User u)
                    {
                        actualMembers.Add(u.Mail?.ToLower().Trim());
                        actualMembers.Add(u.UserPrincipalName?.ToLower().Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Graph Error: {ex.Message}");
                return;
            }

            // 4. Compare
            _output.WriteLine("\n=== OWNER COMPARISON ===");
            bool teacherFound = actualOwners.Contains(intendedTeacher.ToLower().Trim());
            _output.WriteLine($"Teacher {intendedTeacher}: {(teacherFound ? "FOUND" : "MISSING")}");

            _output.WriteLine("\n=== MEMBER (STUDENT) COMPARISON ===");
            int foundCount = 0;
            List<string> missingStudents = new List<string>();
            foreach (var student in intendedStudents)
            {
                if (actualMembers.Contains(student))
                {
                    foundCount++;
                }
                else
                {
                    missingStudents.Add(student);
                }
            }

            _output.WriteLine($"Students Synced: {foundCount} / {intendedStudents.Count}");
            if (missingStudents.Any())
            {
                _output.WriteLine("\n--- MISSING STUDENTS ---");
                foreach (var missing in missingStudents)
                {
                    _output.WriteLine($"- {missing}");
                }
            }
            
            _output.WriteLine("\n--- EXTRA MEMBERS IN GRAPH (Not in Academic DB) ---");
            // Distinct list of actual members (email or UPN) that are not in intendedStudents
            var extraEmails = actualMembers.Where(m => !string.IsNullOrEmpty(m) && !intendedStudents.Contains(m) && m != intendedTeacher.ToLower().Trim()).Distinct();
            foreach(var extra in extraEmails)
            {
                _output.WriteLine($"- {extra}");
            }
        }
    }
}
