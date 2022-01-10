using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyModbus;

namespace Modbus_Scraper
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            string fileName = @"C:\Windows\Temp\log.txt";

            if (File.Exists(fileName))
            {
                File.AppendAllText(fileName, richedit.Text);
            }
            else
            {
                try
                {
                    // Create a new file     
                    using (FileStream fs = File.Create(fileName))
                    {
                        File.AppendAllText(fileName, richedit.Text);
                    }
                }
                catch
                { }
            }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            int portnum = Int32.Parse(port.Text);
            ModbusClient modbusClient = new ModbusClient(ipadd.Text, portnum);         //Ip-Address and Port of Modbus-TCP-Server
            modbusClient.Connect();                                                    //Connect to Server

            int[] readHoldingRegisters = new int[10000];
            int index1 = 0;

            for (int x = 0; x < 100; x++)
            {
                readHoldingRegisters = modbusClient.ReadHoldingRegisters(x * 100, 100);

                for (int i = 0; i < 100; i++)
                {
                    richedit.AppendText("\r\n" + +(index1) + "    :    " + readHoldingRegisters[i].ToString());
                    richedit.ScrollToCaret();
                    index1++;
                }
            }
            modbusClient.Disconnect();                                                //Disconnect from Server
        }

        private void save_Click_1(object sender, EventArgs e)
        {
            string fileName = @"C:\Windows\Temp\log.txt";

            if (File.Exists(fileName))
            {
                File.WriteAllText(fileName, String.Empty);
                File.AppendAllText(fileName, richedit.Text);
            }
            else
            {
                try
                {
                    // Create a new file     
                    using (FileStream fs = File.Create(fileName))
                    {
                        File.WriteAllText(fileName, String.Empty);
                        File.AppendAllText(fileName, richedit.Text);
                    }
                }
                catch
                { }
            }
        }
    }
}
