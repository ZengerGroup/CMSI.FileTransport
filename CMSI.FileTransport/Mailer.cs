using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CMSI.FileTransport
{
    internal class Mailer
    {
        SmtpClient Client;
        MailMessage Message;

        public Mailer()
        {
            Client = ConfigureSMTP();
        }
        public void SendMail(string batchID)
        {
            try
            {
                Message = ConfigureMessage(Configurator.MailRecipient);
                Message.Subject = String.Format("CMS Email Individual PDF output - {0}.", batchID);
                Message.Body = BuildMessage(batchID);
                Attachment reportFile = GetReportAttachment(batchID);
                if (reportFile != null) Message.Attachments.Add(reportFile);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                Client.Send(Message);
            }
            catch (Exception e) 
            { 
                Logger.WriteLog("Failed to send email report for batch {0}. Attempt 1 of 5.", false, batchID);
                RetrySendMail(batchID, 1);
            }
        }
        public void RetrySendMail(string batchID, int failedAttempts)
        {
            try
            {
                Message = ConfigureMessage(Configurator.MailRecipient);
                Message.Subject = String.Format("CMS Email Individual PDF output - {0}.", batchID);
                Message.Body = BuildMessage(batchID);
                Attachment reportFile = GetReportAttachment(batchID);
                if (reportFile != null) Message.Attachments.Add(reportFile);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                Client.Send(Message);
            }
            catch (Exception e) 
            { 
                Logger.WriteLog("Failed to send email report for batch {0}. Attempt {1} of 5.", false, batchID, (++failedAttempts).ToString()); 
                if(failedAttempts < 5) RetrySendMail(batchID, failedAttempts);
            }
        }
        public void SendError(string messageText)
        {
            try
            {
                MailAddress from = new MailAddress(Configurator.MailAccount);
                MailAddress to = new MailAddress("Data@zenger.com");
                MailMessage message = new MailMessage(from, to);
                message.Subject = "CMS Email Error!";
                message.Body = messageText;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                Client.Send(message);
            }
            catch (Exception e)
            {
                Logger.WriteLog("Failed to send error email.", false);
            }
        }
        private SmtpClient ConfigureSMTP()
        {
            SmtpClient smtp = new SmtpClient("smtp.office365.com");
            smtp.TargetName = "STARTTLS/smtp.office365.com";
            smtp.EnableSsl = true;
            smtp.Credentials = new NetworkCredential(Configurator.MailAccount, Configurator.MailSecret);
            return smtp;
        }
        private MailMessage ConfigureMessage(string[] recipients)
        {
            MailAddress from = new MailAddress(Configurator.MailAccount);
            MailAddress to = new MailAddress(recipients[0]);
            MailMessage message = new MailMessage(from, to);
            for (int i = 1; i < recipients.Length; i++) message.To.Add(recipients[i]);
            
            message.IsBodyHtml = true;
            return message;
        }
        private string BuildMessage(string batchID)
        {
            string message = String.Format("A folder named {0} has been uploaded to the ‘Print Files’ folder on the FTP containing the individual pdf output files.", batchID);
            message += Environment.NewLine + "Attached is a final count report by letter number and client.";
            return message;
        }
        private Attachment GetReportAttachment(string batchID)
        {
            try
            {
                string[] dailyFiles = Directory.GetFiles(Path.Combine(Configurator.DailyBatches, batchID));
                for (int i = 0; i < dailyFiles.Length; i++) if (dailyFiles[i].Contains(".xls"))
                    {
                        FileStream fileStream = new FileStream(dailyFiles[i], FileMode.Open, FileAccess.Read);
                        return new Attachment(fileStream, Path.GetFileName(dailyFiles[i]), "application/vnd.ms-excel");
                    }
            }
            catch { Logger.WriteLog("Failed to attach report file.", false); }
            return null;
        }
    }
}
