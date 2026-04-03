using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace APITeams.Helpers
{
    public class Utils
    {
        public static void SendEmail()
        {

            string to = "rpinedaec@gmail.com"; //To address    
            string from = "rpineda@zegelipae.edu.pe"; //From address    
            MailMessage message = new MailMessage(from, to);

            string mailbody = "In this article you will learn how to send a email using Asp.Net & C#";
            message.Subject = "Sending Email Using Asp.Net & C#";
            message.Body = mailbody;
            message.BodyEncoding = Encoding.UTF8;
            message.IsBodyHtml = true;
            SmtpClient client = new SmtpClient("smtp.office365.com", 587); //Gmail smtp    
            System.Net.NetworkCredential basicCredential1 = new
            System.Net.NetworkCredential("rpineda@zegelipae.edu.pe", "salmos19leonel");
            client.EnableSsl = true;
            client.UseDefaultCredentials = false;
            client.Credentials = basicCredential1;
            try
            {
                client.Send(message);
            }

            catch (Exception ex)
            {
                throw ex;
            }
        }
        public List<UsuariosValidar> UsrValid { get; set; }

        
        public void AgregarUsuariosValidar(string email)
        {
            UsuariosValidar usuarios = new UsuariosValidar
            {
                Usuario = email
            };
            UsrValid.Add(usuarios);
        }
    }
}
