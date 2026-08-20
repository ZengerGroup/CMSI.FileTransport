using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace CMSI.FileTransport
{
    internal class Transporter
    {
        FileStream FStream;
        TcpClient Client;
        NetworkStream NetStream;
        string FilePath;
        string FileName;
        byte[] DataPackage;
        bool StreamSuccessful;
        IPAddress IP;
        int Port;
        //For receiving
        int ZipLength;
        int BytesRead;
        string ZipOutputPath;
        byte[] ZipStreamBuffer;
        //Constructor
        public Transporter(string filePath)
        {
            FStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            Client = new TcpClient();
            FilePath = filePath;
            FileName = Path.GetFileName(FilePath);
            ZipOutputPath = GetZipOutputPath();
            ZipStreamBuffer = new byte[1024];
            if (!IPAddress.TryParse(Configurator.IP, out IP)) Environment.Exit(1000);
            if (!Int32.TryParse(Configurator.Port, out Port)) Environment.Exit(1001);
        }
        //Send file
        public async Task<bool> EstablishConnection()
        {
            int attempts = 0;
            while(attempts < 10)
            {
                Logger.Display("Attempting to connect: {0}", false, attempts.ToString());
                try
                {
                    await Client.ConnectAsync(IP, Port);
                    NetStream = Client.GetStream();
                    break;
                }
                catch
                {
                    attempts++;
                }
            }
            if (attempts < 10) return true;
            else return false;
        }
        public async Task<bool> SendFileName()
        {
            Logger.Display("File name to string: {0}", false, FileName);
            NetStream.Write(Encoding.UTF8.GetBytes(String.Format("FN|{0}|FN", FileName)));
            return GetStatus();
        }
        public async Task<bool> SendFile()
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(FilePath);
                byte[] dataLength = BitConverter.GetBytes(fileData.Length + 4);
                DataPackage = new byte[4 + fileData.Length];
                dataLength.CopyTo(DataPackage, 0);
                fileData.CopyTo(DataPackage, 4);
                int dataSent = 0;
                while(dataSent < DataPackage.Length)
                {
                    SendBufferToStream(dataSent);
                    if (!GetStatus()) return false;
                    dataSent += 1024;
                }
                FStream.Close();
                return true;
            }
            catch { return false; }
        }
        public async void SendBufferToStream(int dataSent)
        {
            int bufferSize = ((DataPackage.Length - dataSent) > 1024) ? 1024 : DataPackage.Length - dataSent;
            byte[] buffer = new byte[bufferSize];
            DataPackage[dataSent..(dataSent + bufferSize)].CopyTo(buffer, 0);
            NetStream.Write(buffer, 0, buffer.Length);
        }
        private bool GetStatus()
        {
            byte[] status = new byte[3];
            NetStream.Read(status, 0, 3);
            NetStream.Flush();
            if(Encoding.UTF8.GetString(status).Contains("OK!")) return true;
            else return false;
        }
        //Receive file
        public async Task<bool> CheckStreamForData()
        {
            return NetStream.DataAvailable;
        }
        public async Task<bool> GetZip()
        {

            BytesRead = 0;
            ZipLength = -1;
            int i;
            try
            {
                while ((i = NetStream.Read(ZipStreamBuffer, 0, ZipStreamBuffer.Length)) != 0)
                {
                    ReadFileStream(ZipStreamBuffer);
                    if (ZipLength > 0 && BytesRead >= ZipLength) break;
                }
                return true;
            }
            catch (Exception e)
            {
                Logger.Display("Connection interrupted.", false);
                Logger.Display(e.Message, false);
                return false;
            }
        }
        private string GetZipOutputPath()
        {
            string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return Path.Combine(Configurator.OutputPath, String.Format("{0}_email.zip", new string(Random.Shared.GetItems(allowedChars.ToCharArray(), 6))));
        }
        private async Task<bool> ReadFirstBuffer(byte[] buffer)
        {
            try
            {
                ZipLength = BitConverter.ToInt32(buffer[0..4]);
                AppendAllBytes(ZipOutputPath, buffer[4..buffer.Length]);
                BytesRead = 1024;
                return true;
            }
            catch
            {
                return false;
            }
        }
        private async Task<bool> ReadSubsequentBuffer(byte[] buffer)
        {
            try
            {
                AppendAllBytes(ZipOutputPath, buffer);
                BytesRead += buffer.Length;
                return true;
            }
            catch
            {
                return false;
            }
        }
        static bool AppendAllBytes(string path, byte[] bytes)
        {
            try
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Append))
                {
                    fileStream.Write(bytes, 0, bytes.Length);
                }
                return true;
            }
            catch (Exception e)
            {
                Logger.Display(e.Message, false);
                return false;
            }
        }
        private async void ReadFileStream(byte[] buffer)
        {
            if (ZipLength < 0)
            {
                if (ReadFirstBuffer(buffer).Result) SendResponse("OK!");
                else SendResponse("BAD");
            }
            else
            {
                if (ReadSubsequentBuffer(buffer).Result) SendResponse("OK!");
                else SendResponse("BAD");
            }

        }
        private void SendResponse(string statusString)
        {
            try
            {
                byte[] statusBytes = Encoding.UTF8.GetBytes(statusString);
                NetStream.Write(statusBytes, 0, statusBytes.Length);
                NetStream.Flush();
            }
            catch
            {
                Logger.Display("Unable to send response along connection.", false);
            }

        }
        //Close connection
        public void Disconnect()
        {
            NetStream.Flush();
            Client.Close();
        }
    }
}
