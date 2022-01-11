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
        String[] HoldingRegisters = new String[10000];
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
            Array.Clear(HoldingRegisters, 0, HoldingRegisters.Length);
            int portnum = Int32.Parse(port.Text);
            lblwait.Show();
            ModbusClient modbusClient = new ModbusClient(ipadd.Text, portnum);         //Ip-Address and Port of Modbus-TCP-Server
            //modbusClient.UnitIdentifier = 77;
            try
            {
                modbusClient.Connect();                                                //Connect to Server
            }
            catch
            {
                MessageBox.Show("No Modbus Server Detected");
            }


            int[] readHoldingRegisters = new int[10000];

            int startingreg = Decimal.ToInt32(startreg.Value);
            int pos = 0;
            int index1 = startingreg;

            for (int x = 0; x + startingreg < startingreg + num.Value; x++)
            {
                try
                {
                    readHoldingRegisters = modbusClient.ReadHoldingRegisters(startingreg + x, 1);

                    if (overflow.Checked)
                    {
                        if (readHoldingRegisters[0] != 0 && readHoldingRegisters[0] != -32768)
                        {

                            HoldingRegisters[pos] = index1 + "   :  " + readHoldingRegisters[0].ToString();
                        }
                    }
                    else
                    {

                        HoldingRegisters[pos] = index1 + "   :  " + readHoldingRegisters[0].ToString();
                    }
                }
                catch { }
                index1++;
                pos++;
            }
            modbusClient.Disconnect();                                                //Disconnect from Server
            lblwait.Hide();
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

        private void button2_Click(object sender, EventArgs e)
        {
            richedit.Clear();
            for (int i = 0; i < num.Value; i++)
            {
                if (!String.IsNullOrEmpty(HoldingRegisters[i]) || !String.IsNullOrWhiteSpace(HoldingRegisters[i]))
                {
                    richedit.AppendText(HoldingRegisters[i]+ "\r\n");
                    //richedit.ScrollToCaret();
                }
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
