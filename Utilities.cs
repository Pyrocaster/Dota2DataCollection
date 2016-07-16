using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;
using System.Configuration;

public class Utilties
{
    /// <summary>
    /// Retrun true on sucessful creation of email, false if email generation is unsuccessful.
    /// Will send to email id's identified in the 'MailToOnFailure'
    /// </summary>
    /// <param name="subject">Subject line content for email to be generated</param>
    /// <param name="body">Body content for email to be generated</param>
    /// <returns></returns>
    public bool SendEmail(string subject, string body)
    {
        MailMessage msg = new MailMessage();
        msg.From = new MailAddress("srinivas.bhaskar@outlook.com");
        var people = ConfigurationManager.AppSettings.Get("test");
        msg.To.Add("srinivas.bhaskar@outlook.com");
        msg.Subject = subject;
        msg.Body = body;
        SmtpClient client = new SmtpClient("smtp.live.com");
        client.Port = 25;
        client.EnableSsl = true;
        //checking in without acutal password
        client.Credentials = new NetworkCredential("pyrocasterappbot@outlook.com", "**");
        client.Send(msg);

        return true;
    }

}
