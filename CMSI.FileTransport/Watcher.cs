using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMSI.FileTransport
{
    internal class Watcher
    {
        string[] DataDirectories;
        string[] ZipDirectories;
        string[] PSLDirectories;
        public Transporter FileTransport;
        public Mailer MailSender;
        public Watcher()
        {
            DataDirectories = Configurator.DataDirectories.Split("|");
            ZipDirectories = Configurator.ZipDirectories.Split("|");
            PSLDirectories = Configurator.PSLDirectories.Split("|");
            MailSender = new Mailer();
        }
        public Watcher(string path) //Used for testing, takes a filepath as an argument and treats it as though the file was found in a watched folder.
        {
            Transporter FileTransport = new Transporter(path);
            if (FileTransport.EstablishConnection().Result)
            {
                Logger.Display("Connection has been established.", false);
                if (FileTransport.SendFileName().Result)
                {
                    if (FileTransport.SendFile().Result)
                    {
                        Logger.Display("File transport has completed!", false);
                        while (true)
                        {
                            if (FileTransport.GetZip().Result) break;
                        }
                    }
                    else Logger.Display("File transport has failed.", false);
                    FileTransport.Disconnect();
                }
                else Logger.Display("File name not received by server.", false);
            }
            else Logger.Display("Failed to establish connection.", false);
        }
        //Primary Scan
        public void Scan()
        {
            try { ScanDataDirectories(); }
            catch (Exception e) { Logger.ErrorExit([e.Message], 104); }
            try { ScanZipDirectories(); }
            catch (Exception e) { Logger.ErrorExit([e.Message], 105); }
            try { ScanPSLDirectories(); }
            catch (Exception e) { Logger.ErrorExit([e.Message], 106); }
        }
        //Data directory processing.
        private void ScanDataDirectories()
        {
            foreach (string dir in DataDirectories)
            {
                string[] files = Directory.GetFiles(dir);
                if(files.Length > 0)
                {
                    SendFile(files[0]);
                    Archive(files[0], "data");
                }
            }
        }
        private void SendFile(string path)
        {
            Logger.Display("Found data file: {0}", true, path);
            FileTransport = new Transporter(path);
            if (FileTransport.EstablishConnection().Result)
            {
                Logger.Display("Connection has been established.", false);
                /*
                if (FileTransport.SendFileName().Result)
                {
                    if (FileTransport.SendFile().Result) AwaitResponse();
                    else Logger.Display("File transport has failed.", false);
                    FileTransport.Disconnect();
                }
                else Logger.Display("File name not received by server.", false);
                */
                if (FileTransport.SendFile().Result) AwaitResponse();
                else Logger.Display("File transport has failed.", false);
                FileTransport.Disconnect();
            }
            else Logger.Display("Failed to establish connection.", false);
        }
        private void AwaitResponse()
        {
            // Was within scope for "if (FileTransport.SendFile().Result)", return if failing.
            Logger.Display("File transport has completed!", false);
            int failCount = 0;
            while (failCount < 5)
            {
                if (FileTransport.GetZip().Result) break;
                else failCount++;
            }
        }
        //Zip directory processing
        private void ScanZipDirectories()
        {
            foreach (string dir in ZipDirectories)
            {
                string[] files = Directory.GetFiles(dir);
                for (int i = 0; i < files.Length; i++)
                {
                    UnpackZip(files[i]);
                    Archive(files[i], "zip");
                }
            }
        }
        private void UnpackZip(string zipPath)
        {
            Logger.Display("Found zip file: {0}", true, zipPath);
            //Unzip zip file, send contents to S: Drive
            string batchOutput = Unzip(zipPath);
            if(batchOutput.Contains("BATCH NAME ERROR"))
                Logger.Display("Failed to process {0}Please Check daily batch folders.", false, zipPath + Environment.NewLine);
            //Move TXT/CSN to PSL hotfolder
            if (!SendToLive(batchOutput))
                Logger.Display("Failed to move data file to live. Check daily folder: {0}", false, batchOutput);
        }
        private string Unzip(string zipPath)
        {
            try
            {
                string tempPath = Path.Combine(Configurator.Unzipped, Path.GetFileNameWithoutExtension(zipPath));
                ZipFile.ExtractToDirectory(zipPath, tempPath);
                string[] tempFiles = Directory.GetFiles(tempPath);
                string batchOutput = GetBatchName(tempFiles);
                Directory.CreateDirectory(batchOutput);
                for (int i = 0; i < tempFiles.Length; i++) File.Copy(tempFiles[i], Path.Combine(batchOutput, Path.GetFileName(tempFiles[i])));
                Directory.Delete(tempPath, true);
                return batchOutput;
            }
            catch (Exception e)
            {

                Logger.Display("Unable to unzip!", false);//Pervert
                Logger.Display(e.Message, false);
                return null; 
            }
        }
        private string GetBatchName(string[] tempFiles)
        {
            for (int i = 0; i < tempFiles.Length; i++)
                if (!tempFiles[i].Contains("cms") && tempFiles[i].Contains(".txt"))
                    return Path.Combine(Configurator.DailyBatches, Path.GetFileNameWithoutExtension(tempFiles[i]));
            return Path.Combine(Configurator.DailyBatches, String.Format("BATCH NAME ERROR - {0}", DateTime.Now.ToString("F")));
        }
        private bool SendToLive(string dailyPath)
        {
            try
            {
                string[] batchFiles = Directory.GetFiles(dailyPath);
                for(int i = 0; i < batchFiles.Length; i++)
                    if (FindDataFile(batchFiles[i]))
                    {
                        Logger.Display("Moving {0} to PSL hotfolder.", false, batchFiles[i]);
                        File.Copy(batchFiles[i], Path.Combine(Configurator.PSLHotFolder, Path.GetFileName(batchFiles[i])));
                        return true;
                    }
                return false; 
            }
            catch { return false; }
        }
        private bool FindDataFile(string filePath)
        {
            string[] splitName = Path.GetFileNameWithoutExtension(filePath).Split("_");
            if(splitName.Length != 3) return false;
            if (splitName[2] == "email") return true;
            else return false;
        }
        //PSL directory processing
        private void ScanPSLDirectories()
        {
            foreach (string dir in PSLDirectories)
            {
                string[] batches = Directory.GetDirectories(dir);
                for (int i = 0; i < batches.Length; i++)
                {
                    if (SendToPrintFiles(batches[i])) MailSender.SendMail(Path.GetFileNameWithoutExtension(batches[i]));
                    Archive(batches[i], "output");
                }
            }
        }
        private bool CheckPslOutput(string batchPath)
        {
            int count = Directory.GetFiles(batchPath).Length;
            if (batchPath[^1] == '\\') batchPath = batchPath.Substring(0, batchPath.Length - 2);
            string dailyFile = GetDailyFilePath(batchPath);
            if (dailyFile == null)
            {
                Logger.Display("Unable to find daily data file.", false);
                return false;
            }
            return CheckLineCount(dailyFile, count);
        }
        private string GetDailyFilePath(string batchPath)
        {
            string[] dailyBatches = Directory.GetDirectories(Configurator.DailyBatches);
            for (int i = 0; i < dailyBatches.Length; i++) 
                if (Path.GetFileName(dailyBatches[i]) == Path.GetFileName(batchPath)) return ScanDailyBatch(dailyBatches[i]);
            return null;
        }
        private string ScanDailyBatch(string batchPath)
        {
            string[] dailyFiles = Directory.GetFiles(batchPath);
            for (int i = 0; i < dailyFiles.Length; i++) if (dailyFiles[i].Contains(".txt") && !dailyFiles[i].Contains("cms")) return dailyFiles[i];
            return null;
        }
        private bool CheckLineCount(string filePath, int count)
        {
            int lineCount = File.ReadLines(filePath).Count();
            return lineCount - 1 == count;
        }
        private bool SendToPrintFiles(string dirPath)
        {
            try
            {
                string outPath = Path.Combine(Configurator.PrintFiles, Path.GetFileName(dirPath));
                string[] files = Directory.GetFiles(dirPath);
                for (int i = 0; i < files.Length; i++) 
                {
                    Directory.CreateDirectory(outPath); //Create the directory each loop in case it's pulled away during the upload.
                    File.Copy(files[i], Path.Combine(outPath, Path.GetFileName(files[i])));
                }
                return true;
            }
            catch { return false; }
        }
        //Shared
        private void Archive(string path, string type)
        {
            string outPath = Path.Combine(GetArchivePath(type), Path.GetFileName(path));
            try
            {
                if(type != "output")
                {
                    if (File.Exists(outPath)) outPath = outPath.Replace(".", "_COPY.");
                    File.Copy(path, outPath);
                    File.Delete(path);
                }
                else
                {
                    if (Directory.Exists(outPath)) outPath += "_COPY";
                    string[] files = Directory.GetFiles(path);
                    Directory.CreateDirectory(outPath);
                    for (int i = 0; i < files.Length; i++) File.Copy(files[i], Path.Combine(outPath, Path.GetFileName(files[i])));
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                Logger.Display("Failed to archive file: {0}", false, path);
                File.Delete(path);
            }
        }
        private string GetArchivePath(string type)
        {
            switch (type)
            {
                case "data":
                    return Configurator.DataArchive;
                case "zip":
                    return Configurator.ZipArchive;
                case "output":
                    return Configurator.PSLArchive;
                default:
                    Logger.WriteLog("Howmst??", false);
                    return @"Z:\";
            }
        }
    }
}
