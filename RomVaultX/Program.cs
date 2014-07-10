using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RomVaultX
{
    static class Program
    {

        public static readonly Encoding Enc = Encoding.GetEncoding(28591);
        public static SynchronizationContext SyncCont;
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RomVaultX());
        }
    }
}
