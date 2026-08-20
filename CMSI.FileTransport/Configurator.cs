using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CMSI.FileTransport
{
    internal static class Configurator
    {
        //Watched Directories
        public static string DataDirectories = ConfigurationManager.AppSettings["DataDirectories"];
        public static string ZipDirectories = ConfigurationManager.AppSettings["ZipDirectories"];
        public static string PSLDirectories = ConfigurationManager.AppSettings["PSLDirectories"];
		//Archives
		public static string DataArchive = ConfigurationManager.AppSettings["DataArchive"];
        public static string ZipArchive = ConfigurationManager.AppSettings["ZipArchive"];
        public static string PSLArchive = ConfigurationManager.AppSettings["PSLArchive"];
        //Network Info
        public static string IP = ConfigurationManager.AppSettings["IP"];
        public static string Port = ConfigurationManager.AppSettings["Port"];
        //File Output info
        public static string OutputPath = ConfigurationManager.AppSettings["OutputPath"];
        public static string DailyBatches = ConfigurationManager.AppSettings["DailyBatches"];
        public static string PSLHotFolder = ConfigurationManager.AppSettings["PSLHotfolder"];
        public static string PrintFiles = ConfigurationManager.AppSettings["PrintFiles"];
        //Logging
        public static string LogPath = ConfigurationManager.AppSettings["LogPath"];
        //Working
        public static string Unzipped = ConfigurationManager.AppSettings["Unzipped"];
        //Email
        public static string MailAccount = ConfigurationManager.AppSettings["MailAccount"];
        public static string MailSecret = ConfigurationManager.AppSettings["MailSecret"];
        public static string[] MailRecipient = ConfigurationManager.AppSettings["MailRecipient"].Split("|");
    }
}
