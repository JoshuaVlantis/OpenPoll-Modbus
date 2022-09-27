using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyBus_Modbus_Scanner
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Run();
        }

        private static void Run()
        {
            Application.Run(HomeVar.home);
        }
    }

    public static class HomeVar
    {
       public static Home home = new Home();
    }
}
