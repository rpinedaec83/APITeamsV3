using APITeams.Helpers;
using System.Data;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.Graph;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace APITeams
{
    public class AppDatos
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public SqlConnection GetSqlConnection() {
            string conexion = ConstantsAPI.ConnectionString;
            SqlConnection connection;
            connection = new SqlConnection(conexion);
            return connection;
        }
        public DataTable ConsultarDB(int opcion, int idSeccion = 0)
        {
            DataTable dt = new DataTable();
            SqlDataAdapter adapter;
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandTimeout = 0;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            if (idSeccion != 0)
            {
                param = new SqlParameter("@IdSeccion", idSeccion)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.Int32
                };
                command.Parameters.Add(param);
            }
            param = new SqlParameter("@Opcion", opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            adapter = new SqlDataAdapter(command);
            adapter.Fill(ds);
            for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                dt = ds.Tables[0];
            }
            connection.Close();
            return dt;
        }
        public DataTable ReporteHorarios(int opcion, int NroDias = 0)
        {
            DataTable dt = new DataTable();
            SqlDataAdapter adapter;
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandTimeout = 0;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            if (NroDias != 0)
            {
                param = new SqlParameter("@NroDias", NroDias)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.Int32
                };
                command.Parameters.Add(param);
            }
            param = new SqlParameter("@Opcion", opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            adapter = new SqlDataAdapter(command);
            adapter.Fill(ds);
            for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                dt = ds.Tables[0];
            }
            connection.Close();
            return dt;
        }

        public DataTable GetGroups(int opcion, int IdSeccion = 0)
        {
            DataTable dt = new DataTable();
            SqlDataAdapter adapter;
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            if (IdSeccion != 0)
            {
                param = new SqlParameter("@IdSeccion", IdSeccion)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.Int32
                };
                command.Parameters.Add(param);
            }
            param = new SqlParameter("@Opcion", opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            adapter = new SqlDataAdapter(command);
            adapter.Fill(ds);
            for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                dt = ds.Tables[0];
            }
            connection.Close();
            return dt;
        }

        //public DataTable DT_DeleteEvents(string idGroup)
        //{
        //    DatabaseProviderFactory factory = new DatabaseProviderFactory();
        //    Database db = factory.Create("ConnectionBD");

        //    DataSet dsNewTeams = new DataSet();
        //    DataTable dt = new DataTable();
        //    DbCommand dbCommand = db.GetStoredProcCommand("cTeamsSincronizarApi");
        //    db.AddInParameter(dbCommand, "@Opcion", DbType.Int32, 21);
        //    db.AddInParameter(dbCommand, "@idTeamsGroup", DbType.String, idGroup);
        //    using (dsNewTeams = db.ExecuteDataSet(dbCommand))
        //    {

        //        if (dsNewTeams.Tables[0].Rows.Count > 0)
        //        {
        //            dt = dsNewTeams.Tables[0];
        //        }
        //    }
        //    return dt;
        //}

        public void InsertNewTeam(int Opcion,EducationClass newClass, List<string> owners, int idSeccion)
        {
            DataTable dt = new DataTable();
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", Opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@idTeamsGroup", newClass.Id)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@nombreTeam", newClass.DisplayName)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@descripcionTeam", newClass.Description)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@mailNickName", newClass.MailNickname)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@idSeccionSmart", idSeccion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            int p = 1;
            owners.ForEach(elem =>
            {
                param = new SqlParameter($"@propietario{p}", elem)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                p += 1;
            });

            command.ExecuteNonQuery();
            connection.Close();
        }

        public void InsertNewMemberTeam(int Opcion,NewMember member, string tipo)
        {
            if (member.IdGroup != null && member.Email != null && member.Email != "")
            {
                
                DataTable dt = new DataTable();
                SqlCommand command = new SqlCommand();
                SqlParameter param;
                DataSet ds = new DataSet();
                SqlConnection connection = GetSqlConnection();
                connection.Open();
                command.Connection = connection;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "cTeamsPorSeccion";
                param = new SqlParameter("@Opcion", Opcion)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.Int32
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@idTeamsGroup", member.IdGroup)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@CodigoAlumno", member.CodigoAlumno)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Nombres", member.Nombres)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Apellidos", member.Apellidos)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Email", member.Email)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Tipo", tipo)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        public void InsertCalendario(int opcion, int idSeccion, string webJoinUrl, string idEvento)
        {
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);


            if (idSeccion != 0)
            {
                param = new SqlParameter("@IdSeccion", idSeccion)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.Int32
                };
                command.Parameters.Add(param);
            }

            param = new SqlParameter("@JoinUrl", webJoinUrl)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@IdEvento", idEvento)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            command.ExecuteNonQuery();
            connection.Close();

        }

        public void UpdateTeam(int opcion, Microsoft.Graph.Group newGroup, string idGroup, string email)
        {
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            
            if (opcion == 25)
            {
                param = new SqlParameter("@idTeamsGroup", newGroup.Id)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@nombreTeam", newGroup.DisplayName)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@descripcionTeam", newGroup.Description)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
            }
            if (opcion == 27)
            {
                param = new SqlParameter("@idTeamsGroup", idGroup)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@propietario3", email)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
            }
            command.ExecuteNonQuery();
            connection.Close();
        }

        //public DataTable VerificaTeams(int opcion, int IdCurso)
        //{
        //    DatabaseProviderFactory factory = new DatabaseProviderFactory();
        //    Database db = factory.Create("ConnectionBD");

        //    DataSet dsNewTeams = new DataSet();
        //    DataTable dt = new DataTable();
        //    DbCommand dbCommand = db.GetStoredProcCommand("cTeamsPorSeccion");
        //    db.AddInParameter(dbCommand, "@Opcion", DbType.Int32, opcion);
        //    db.AddInParameter(dbCommand, "@IdSeccion", DbType.String, IdCurso);
        //    using (dsNewTeams = db.ExecuteDataSet(dbCommand))
        //    {

        //        if (dsNewTeams.Tables[0].Rows.Count > 0)
        //        {
        //            dt = dsNewTeams.Tables[0];
        //        }
        //    }
        //    return dt;
        //}

        public void UpdateFacilitador(OldFacilitador oldFacilitador, Facilitador facilitador)
        {
            if (oldFacilitador != null)
            {
                SqlCommand command = new SqlCommand();
                SqlParameter param;
                DataSet ds = new DataSet();
                SqlConnection connection = GetSqlConnection();
                connection.Open();
                command.Connection = connection;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "cTeamsPorSeccion";
                param = new SqlParameter("@Opcion", 28)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.Int32
                };
                command.Parameters.Add(param);

                param = new SqlParameter("@idTeamsGroup", oldFacilitador.IdTeam)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@CodigoAlumno", oldFacilitador.CodigoFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Email", oldFacilitador.EmailFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Nombres", oldFacilitador.NombresFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Apellidos", oldFacilitador.ApellidosFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@mailNickName", oldFacilitador.OldCodigoFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@propietario1", oldFacilitador.OldEmailFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                command.ExecuteNonQuery();
                connection.Close();
            }
            if (facilitador != null)
            {
                SqlCommand command = new SqlCommand();
                SqlParameter param;
                DataSet ds = new DataSet();
                SqlConnection connection = GetSqlConnection();
                connection.Open();
                command.Connection = connection;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "cTeamsPorSeccion";
                param = new SqlParameter("@Opcion", 28)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.Int32
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@idTeamsGroup", facilitador.IdTeam)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@CodigoAlumno", facilitador.CodigoFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Email", facilitador.EmailFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Nombres", facilitador.NombresFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Apellidos", facilitador.ApellidosFacilitador)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
                command.ExecuteNonQuery();
                connection.Close();
            }

        }

        public void DeleteTeam(int Opcion, string idGroup)
        {
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", Opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@idTeamsGroup", idGroup)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            command.ExecuteNonQuery();
            connection.Close();
        }

        public void DeleteMember(int Opcion, string idGroup, string idMember)
        {

            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", Opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@idTeamsGroup", idGroup)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@CodigoAlumno", idMember)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            command.ExecuteNonQuery();
            connection.Close();
        }

        //internal DataTable TraerLinks(int opcion, int IdCurso)
        //{
        //    DatabaseProviderFactory factory = new DatabaseProviderFactory();
        //    Database db = factory.Create("ConnectionBD");

        //    DataSet dsNewTeams = new DataSet();
        //    DataTable dt = new DataTable();
        //    DbCommand dbCommand = db.GetStoredProcCommand("cTeamsPorSeccion");
        //    db.AddInParameter(dbCommand, "@Opcion", DbType.Int32, opcion);
        //    db.AddInParameter(dbCommand, "@IdSeccion", DbType.String, IdCurso);
        //    using (dsNewTeams = db.ExecuteDataSet(dbCommand))
        //    {

        //        if (dsNewTeams.Tables[0].Rows.Count > 0)
        //        {
        //            dt = dsNewTeams.Tables[0];
        //        }
        //    }
        //    return dt;
        //}

        public void UpdateActiveGroup(string idGroup, bool isActive)
        {
            string active = !isActive ? "A" : "I";
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", 26)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@idTeamsGroup", idGroup)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@IsActive", active)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            command.ExecuteNonQuery();
            connection.Close();
        }

        public DataTable ConsultaTeams(int opcion, string codigo)
        {
            DataTable dt = new DataTable();
            SqlDataAdapter adapter;
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            if (opcion == 35) {
                param = new SqlParameter("@CodigoAlumno", codigo)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
            }
            else
            {
                param = new SqlParameter("@CodigoSeccion", codigo)
                {
                    Direction = ParameterDirection.Input,
                    DbType = DbType.String
                };
                command.Parameters.Add(param);
            }
            param = new SqlParameter("@Opcion", opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            adapter = new SqlDataAdapter(command);
            adapter.Fill(ds);
            for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                dt = ds.Tables[0];
            }
            connection.Close();
            return dt;
        }

        //public DataTable CreacionEquiposaDemanda(int opcion, int IdCurso)
        //{
        //    DatabaseProviderFactory factory = new DatabaseProviderFactory();
        //    Database db = factory.Create("ConnectionBD");

        //    DataSet dsNewTeams = new DataSet();
        //    DataTable dt = new DataTable();
        //    DbCommand dbCommand = db.GetStoredProcCommand("cTeamsPorSeccion");
        //    db.AddInParameter(dbCommand, "@Opcion", DbType.Int32, opcion);
        //    db.AddInParameter(dbCommand, "@IdSeccion", DbType.String, IdCurso);
        //    using (dsNewTeams = db.ExecuteDataSet(dbCommand))
        //    {

        //        if (dsNewTeams.Tables[0].Rows.Count > 0)
        //        {
        //            dt = dsNewTeams.Tables[0];
        //        }
        //    }
        //    return dt;
        //}
        public void ActualizacionTeamById(int opcion, int IdCurso)
        {
           
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@IdSeccion", IdCurso)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            command.ExecuteNonQuery();
            connection.Close();
        }
        //public DataTable TraerUsuario(int opcion, int IdCurso)
        //{
        //    DatabaseProviderFactory factory = new DatabaseProviderFactory();
        //    Database db = factory.Create("ConnectionBD");

        //    DataSet dsNewTeams = new DataSet();
        //    DataTable dt = new DataTable();
        //    DbCommand dbCommand = db.GetStoredProcCommand("cTeamsPorSeccion");
        //    db.AddInParameter(dbCommand, "@Opcion", DbType.Int32, opcion);
        //    db.AddInParameter(dbCommand, "@IdSeccion", DbType.String, IdCurso);
        //    using (dsNewTeams = db.ExecuteDataSet(dbCommand))
        //    {

        //        if (dsNewTeams.Tables[0].Rows.Count > 0)
        //        {
        //            dt = dsNewTeams.Tables[0];
        //        }
        //    }
        //    return dt;
        //}

        public DataTable TraerAgendas(int opcion, int IdCurso)
        {
            DataTable dt = new DataTable();
            SqlDataAdapter adapter;
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", opcion)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@IdSeccion", IdCurso)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@FechaMaximaAgendas", ConstantsAPI.FECHA_MAXIMA_AGENDAS)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            adapter = new SqlDataAdapter(command);
            adapter.Fill(ds);
            for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                dt = ds.Tables[0];
            }
            connection.Close();
            return dt;
        }
        public void DeleteTeamById(string idGroup)
        {
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", 27)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@descripcionTeam", idGroup)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@Sede", ConstantsAPI.SEDE)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);
            command.ExecuteNonQuery();
            connection.Close();
        }
        //REVISAR
        public void DeleteCodigoSeccion(int Opcion, string codigoSeccion)
        {
            SqlCommand sqlCommand = new SqlCommand();
            DataSet dataSet = new DataSet();
            SqlConnection sqlConnection = this.GetSqlConnection();
            sqlConnection.Open();
            sqlCommand.Connection = sqlConnection;
            sqlCommand.CommandType = CommandType.StoredProcedure;
            sqlCommand.CommandText = "cTeamsPorSeccion";
            SqlParameter sqlParameter1 = new SqlParameter("@Opcion", (object)Opcion);
            sqlParameter1.Direction = ParameterDirection.Input;
            sqlParameter1.DbType = DbType.Int32;
            sqlCommand.Parameters.Add(sqlParameter1);
            SqlParameter sqlParameter2 = new SqlParameter("@codigoSeccion", (object)codigoSeccion);
            sqlParameter2.Direction = ParameterDirection.Input;
            sqlParameter2.DbType = DbType.String;
            sqlCommand.Parameters.Add(sqlParameter2);
            SqlParameter sqlParameter3 = new SqlParameter("@Sede", ConstantsAPI.SEDE);
            sqlParameter3.Direction = ParameterDirection.Input;
            sqlParameter3.DbType = DbType.String;
            sqlCommand.Parameters.Add(sqlParameter3);
            sqlCommand.ExecuteNonQuery();
            sqlConnection.Close();
        }

        public void InsertDBLog(string TipoError, string ErrorLog, string Referencia = null) {
            SqlCommand sqlCommand = new SqlCommand();
            DataSet dataSet = new DataSet();
            SqlConnection sqlConnection = this.GetSqlConnection();
            sqlConnection.Open();
            sqlCommand.Connection = sqlConnection;
            sqlCommand.CommandType = CommandType.StoredProcedure;
            sqlCommand.CommandText = "cTeamsPorSeccion";
            SqlParameter sqlParameter1 = new SqlParameter("@Opcion",42);
            sqlParameter1.Direction = ParameterDirection.Input;
            sqlParameter1.DbType = DbType.Int32;
            sqlCommand.Parameters.Add(sqlParameter1);
            SqlParameter sqlParameter2 = new SqlParameter("@TipoError", TipoError);
            sqlParameter2.Direction = ParameterDirection.Input;
            sqlParameter2.DbType = DbType.String;
            sqlCommand.Parameters.Add(sqlParameter2);
            SqlParameter sqlParameter3 = new SqlParameter("@ErrorLog", ErrorLog);
            sqlParameter3.Direction = ParameterDirection.Input;
            sqlParameter3.DbType = DbType.String;
            sqlCommand.Parameters.Add(sqlParameter3);

            if (!string.IsNullOrEmpty(Referencia))
            {
                SqlParameter sqlParameter4 = new SqlParameter("@Referencia", Referencia);
                sqlParameter4.Direction = ParameterDirection.Input;
                sqlParameter4.DbType = DbType.String;
                sqlCommand.Parameters.Add(sqlParameter4);
            }

            sqlCommand.ExecuteNonQuery();
            sqlConnection.Close();

        }

        public DataTable GetDBLog() {
            DataTable dt = new DataTable();
            SqlDataAdapter adapter;
            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", 43)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            adapter = new SqlDataAdapter(command);
            adapter.Fill(ds);
            for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                dt = ds.Tables[0];
            }
            connection.Close();
            return dt;

        }

        public void UpdateDBLog()
        {
            SqlCommand sqlCommand = new SqlCommand();
            DataSet dataSet = new DataSet();
            SqlConnection sqlConnection = this.GetSqlConnection();
            sqlConnection.Open();
            sqlCommand.Connection = sqlConnection;
            sqlCommand.CommandType = CommandType.StoredProcedure;
            sqlCommand.CommandText = "cTeamsPorSeccion";
            SqlParameter sqlParameter1 = new SqlParameter("@Opcion", 44);
            sqlParameter1.Direction = ParameterDirection.Input;
            sqlParameter1.DbType = DbType.Int32;
            sqlCommand.Parameters.Add(sqlParameter1);
            sqlCommand.ExecuteNonQuery();
            sqlConnection.Close();

        }

        public void InsertarAgenda(NewGroupMeeting newGroup, Event evnt, string email) {

            //        INSERT INTO TeamsHorarios(IdTeams, IdEvento, IdHorario, IdCurso, NumeroReunion, Codigo, Fecha, Inicio, Fin, CodigoAlumno, CorreoAlumno, CodigoFacilitador, CorreoFacilitador, Estado, JoinUrl, UsuarioCreacion, FechaCreacion)

            //VALUES(@IdTeamsGroup, @IdEvento, @IdHorario, @IdSeccion, 1, @CodigoSesion, @Fecha, @Inicio, @Fin, @CodigoAlumno, @Email, @CodigoFacilitador, @propietario4, 'A', @JoinUrl, 1, GETDATE())

            SqlCommand command = new SqlCommand();
            SqlParameter param;
            DataSet ds = new DataSet();
            SqlConnection connection = GetSqlConnection();
            connection.Open();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "cTeamsPorSeccion";
            param = new SqlParameter("@Opcion", 46)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);
            param = new SqlParameter("@IdTeamsGroup", newGroup.IdTeam)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@IdEvento", evnt.Id)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@IdSeccion", newGroup.IdCurso)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@CodigoSesion", newGroup.Codigo)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@Fecha", newGroup.Fecha.ToString("yyyy-MM-dd"))
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.DateTime
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@Inicio", newGroup.Inicio)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@Fin", newGroup.Fin)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.Int32
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@CodigoAlumno", email)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@Email", email)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@CodigoFacilitador", newGroup.CodigoFacilitador)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);

            param = new SqlParameter("@JoinUrl", evnt.OnlineMeeting.JoinUrl)
            {
                Direction = ParameterDirection.Input,
                DbType = DbType.String
            };
            command.Parameters.Add(param);


            command.ExecuteNonQuery();
            connection.Close();

        }


        //public void UpdateFacilitadorPorSeccion(OldFacilitador oldFacilitador, Facilitador facilitador)
        //{
        //    DatabaseProviderFactory factory = new DatabaseProviderFactory();
        //    Database db = factory.Create("ConnectionBD");
        //    DbCommand dbCommand;
        //    if (oldFacilitador != null)
        //    {
        //        using (dbCommand = db.GetStoredProcCommand("cTeamsPorSeccion"))
        //        {
        //            db.AddInParameter(dbCommand, "@Opcion", DbType.Int32, 17);
        //            db.AddInParameter(dbCommand, "@idTeamsGroup", DbType.String, oldFacilitador.IdTeam);
        //            db.AddInParameter(dbCommand, "@CodigoAlumno", DbType.String, oldFacilitador.CodigoFacilitador);
        //            db.AddInParameter(dbCommand, "@Email", DbType.String, oldFacilitador.EmailFacilitador);
        //            db.AddInParameter(dbCommand, "@Nombres", DbType.String, oldFacilitador.NombresFacilitador);
        //            db.AddInParameter(dbCommand, "@Apellidos", DbType.String, oldFacilitador.ApellidosFacilitador);
        //            db.AddInParameter(dbCommand, "@mailNickName", DbType.String, oldFacilitador.OldCodigoFacilitador);
        //            db.AddInParameter(dbCommand, "@propietario1", DbType.String, oldFacilitador.OldEmailFacilitador);
        //            db.ExecuteNonQuery(dbCommand);
        //        }
        //    }
        //    if (facilitador != null)
        //    {
        //        using (dbCommand = db.GetStoredProcCommand("cTeamsPorSeccion"))
        //        {
        //            db.AddInParameter(dbCommand, "@Opcion", DbType.Int32, 18);
        //            db.AddInParameter(dbCommand, "@idTeamsGroup", DbType.String, facilitador.IdTeam);
        //            db.AddInParameter(dbCommand, "@CodigoAlumno", DbType.String, facilitador.CodigoFacilitador);
        //            db.AddInParameter(dbCommand, "@Email", DbType.String, facilitador.EmailFacilitador);
        //            db.AddInParameter(dbCommand, "@Nombres", DbType.String, facilitador.NombresFacilitador);
        //            db.AddInParameter(dbCommand, "@Apellidos", DbType.String, facilitador.ApellidosFacilitador);
        //            db.ExecuteNonQuery(dbCommand);
        //        }
        //    }

        //}
    }

}
