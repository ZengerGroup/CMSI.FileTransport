using System.Runtime.CompilerServices;

namespace CMSI.FileTransport
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Logger.Display("Launched without file, beginning to watch directories.", false);
            Watcher FileWatcher = new Watcher();
            while (true)
            {
                Console.WriteLine("Scanning...");
                FileWatcher.Scan();
                Console.WriteLine("Scan complete. Next Scan at {0}.", DateTime.Now.AddMinutes(5).ToString("F"));
                Thread.Sleep(300000);
            }
        }
    }
}
