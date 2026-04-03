// Decompiled with JetBrains decompiler
// Type: APITeams.Helpers.ConstantsAPI
// Assembly: APITeams, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FA9EDE4F-0C75-47EA-A53C-B1020911C874
// Assembly location: C:\Users\rober\OneDrive\Escritorio\Zegel\Zegel\APITeams.exe

using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;


namespace APITeams.Helpers
{
    public class ConstantsAPI
    {
        public const string URL_Microsft = "https://graph.microsoft.com/v1.0/";
        public const string OWNERS = "owners@odata.bind";
        public const string MEMBERS = "members@odata.bind";
        public const string ID_DATA = "@odata.id";
        public static readonly string URL_REDIRECT = ConfigurationManager.AppSettings[nameof(URL_REDIRECT)];
        public static readonly string DOMINIO = ConfigurationManager.AppSettings[nameof(DOMINIO)];
        public static readonly string SUBJECTAGENDA = ConfigurationManager.AppSettings[nameof(SUBJECTAGENDA)];
        public static readonly string FECHA_MAXIMA_AGENDAS = ConfigurationManager.AppSettings[nameof(FECHA_MAXIMA_AGENDAS)];
        public static readonly string PASS_APP = ConfigurationManager.AppSettings[nameof(PASS_APP)];
        public static readonly string MENSAJE_AGENDA = ConfigurationManager.AppSettings[nameof(MENSAJE_AGENDA)];
        public static readonly int LIMITE_EQUIPOS = Convert.ToInt32(ConfigurationManager.AppSettings[nameof(LIMITE_EQUIPOS)]);
        public static readonly int LIMITE_AGENDAS = Convert.ToInt32(ConfigurationManager.AppSettings[nameof(LIMITE_AGENDAS)]);
        public static readonly bool AUTOMATICO = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(AUTOMATICO)]);
        public static readonly bool CREARTEAMS = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(CREARTEAMS)]);
        public static readonly bool ACTUALIZARTEAMS = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(ACTUALIZARTEAMS)]);
        public static readonly bool CREARAGENDAS = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(CREARAGENDAS)]);
        public static readonly string ALT_DOMINIO = ConfigurationManager.AppSettings[nameof(ALT_DOMINIO)];
        public static readonly string SEDE = ConfigurationManager.AppSettings[nameof(SEDE)];
        public static readonly string UnidadNegocio = ConfigurationManager.AppSettings[nameof(UnidadNegocio)];
        public static readonly string UnidadAcademica = ConfigurationManager.AppSettings[nameof(UnidadAcademica)];
        public static readonly string Periodo = ConfigurationManager.AppSettings[nameof(Periodo)];
        public static readonly bool ACTIVARAUTOMATICO = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(ACTIVARAUTOMATICO)]);
        public static readonly bool CHECKTEAMS = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(CHECKTEAMS)]);
        public static readonly bool REGULARIZARAGENDAS = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(REGULARIZARAGENDAS)]);
        public static readonly bool ACTUALIZAPROP = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(ACTUALIZAPROP)]);
        public static readonly string EMPRESA = ConfigurationManager.AppSettings[nameof(EMPRESA)];
        public static readonly string MAIL_LOG_ERRORS = ConfigurationManager.AppSettings[nameof(MAIL_LOG_ERRORS)];
        public static readonly bool AGENDATEAMS = Convert.ToBoolean(ConfigurationManager.AppSettings[nameof(AGENDATEAMS)]);

        private static string _connectionString;

        public static string Decrypt(string encrypt, string key)
        {
            using (TripleDESCryptoServiceProvider cryptoServiceProvider1 = new TripleDESCryptoServiceProvider())
            {
                using (MD5CryptoServiceProvider cryptoServiceProvider2 = new MD5CryptoServiceProvider())
                {
                    byte[] hash = cryptoServiceProvider2.ComputeHash(Encoding.UTF8.GetBytes(key));
                    cryptoServiceProvider1.Key = hash;
                    cryptoServiceProvider1.Mode = CipherMode.ECB;
                    byte[] inputBuffer = Convert.FromBase64String(encrypt);
                    return Encoding.UTF8.GetString(cryptoServiceProvider1.CreateDecryptor().TransformFinalBlock(inputBuffer, 0, inputBuffer.Length));
                }
            }
        }

        public static string ConnectionString
        {
            get
            {
                if (ConstantsAPI._connectionString == null)
                    ConstantsAPI._connectionString = ConstantsAPI.Decrypt(ConfigurationManager.ConnectionStrings["ConnectionBD"].ConnectionString, "real-soft-solutions");
                return ConstantsAPI._connectionString;
            }
        }
    }
}
