using EasyModbus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyBus_Modbus_Scanner
{
    public partial class ModbusScraper : Form
    {
        bool stop = false;
        String[] HoldingRegisters = new String[10000];
        int arrsize = 0;
        bool killthread = false;

        public ModbusScraper()
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
                catch { }
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            stop = false;
            bStop.Show();
            if (comboBox1.SelectedIndex == 4)
            {
                Threadcall2();
            }
            else
            {
                Threadcall1();
            }
        }
        public void Threadcall1()
        {
            Thread tid1 = new Thread(new ThreadStart(Thread1));
            tid1.IsBackground = true;

            //Filthy Thread kill hack
            if (!killthread)
            {
                button1.Hide();
                tid1.Start();
                killthread = true;
            }
            else
            {
                button1.Show();
                bStop.Hide();
                killthread = false;
                tid1.Abort();
            }
        }

        public void Threadcall2()
        {
            Thread tid2 = new Thread(new ThreadStart(Thread2));
            tid2.IsBackground = true;
            //Filthy Thread kill hack
            if (!killthread)
            {
                button1.Hide();
                tid2.Start();
                killthread = true;
            }
            else
            {
                button1.Show();
                bStop.Hide();
                killthread = false;
                tid2.Abort();
            }
        }

        public void Thread1()
        {
            Search();
            Threadcall1();
        }

        public void Thread2()
        {
            SearchID();
            Threadcall2();
        }

        void Search()
        {
            richedit.Clear();
            pBar.Minimum = 0;
            pBar.Maximum = 100;

            int islave = Int32.Parse(slaveid.Text);
            byte[] slave = BitConverter.GetBytes(islave);
            Array.Clear(HoldingRegisters, 0, HoldingRegisters.Length);
            int portnum = Int32.Parse(port.Text);

            lblwait.BeginInvoke(
                new Action(
                    () =>
                    {
                        lblwait.Show();
                    }
                )
            );

            pBar.BeginInvoke(
                new Action(
                    () =>
                    {
                        pBar.Show();
                    }
                )
            );

            ModbusClient modbusClient = new ModbusClient(ipadd.Text, portnum); //Ip-Address and Port of Modbus-TCP-Server
            modbusClient.UnitIdentifier = slave[0];
            modbusClient.ConnectionTimeout = Convert.ToInt32(connectiontimeout.Value);

            try
            {
                modbusClient.Connect(); //Connect to Server
            }
            catch
            {
                MessageBox.Show("No Modbus Server Detected");
            }

            bool[] readCoils = new bool[10000];
            bool[] readinputs = new bool[10000];
            int[] readHoldingRegisters = new int[10000];
            int[] readInputStatus = new int[1000];

            int startingreg = Decimal.ToInt32(startreg.Value);
            int pos = 0;
            int index1 = startingreg;
            int amount = Decimal.ToInt32(num.Value);
            int readregisteramount = 100;
            arrsize = 0;
            if (modbusClient.Connected)
            {
                for (int x = 0; x + startingreg < startingreg + num.Value; x++)
                {
                    if (!stop)
                    {
                        try
                        {
                            if (comboBox1.SelectedIndex == 0)
                            {
                                readCoils = modbusClient.ReadCoils(
                                    startingreg + x,
                                    readregisteramount
                                );
                            }
                            if (comboBox1.SelectedIndex == 1)
                            {
                                readinputs = modbusClient.ReadDiscreteInputs(
                                    startingreg + x,
                                    readregisteramount
                                );
                            }
                            if (comboBox1.SelectedIndex == 2)
                            {
                                readHoldingRegisters = modbusClient.ReadHoldingRegisters(
                                    startingreg + x,
                                    readregisteramount
                                );
                            }
                            if (comboBox1.SelectedIndex == 3)
                            {
                                readInputStatus = modbusClient.ReadInputRegisters(
                                    startingreg + x,
                                    readregisteramount
                                );
                            }

                            for (int j = 0; j < readregisteramount; j++)
                            {
                                if (x + startingreg < num.Value + startingreg)
                                {
                                    index1 = x + startingreg;

                                    if (comboBox1.SelectedIndex == 0)
                                    {
                                        HoldingRegisters[pos] =
                                            index1 + "   :  " + readCoils[j].ToString();
                                    }
                                    if (comboBox1.SelectedIndex == 1)
                                    {
                                        HoldingRegisters[pos] =
                                            index1 + "   :  " + readinputs[j].ToString();
                                    }
                                    if (comboBox1.SelectedIndex == 2)
                                    {
                                        HoldingRegisters[pos] =
                                            index1 + "   :  " + readHoldingRegisters[j].ToString();
                                    }
                                    if (comboBox1.SelectedIndex == 3)
                                    {
                                        HoldingRegisters[pos] =
                                            index1 + "   :  " + readInputStatus[j].ToString();
                                    }

                                    pos++;
                                    arrsize++;
                                    x++;
                                    //index1 = x;
                                }
                            }
                            if (x + startingreg < num.Value + startingreg)
                            {
                                x--;
                            }
                            if (readregisteramount < 100)
                            {
                                readregisteramount = 100;
                            }
                        }
                        catch (System.IO.IOException)
                        {
                            MessageBox.Show("Node ID Doesnt Exist");
                            break;
                        }
                        catch (EasyModbus.Exceptions.StartingAddressInvalidException)
                        {
                            if (readregisteramount == 10)
                            {
                                readregisteramount = 1;
                                x = x - 1;
                            }
                            else if (readregisteramount > 10)
                            {
                                readregisteramount = readregisteramount - 10;
                                x = x - 1;
                            }
                        }
                        catch { }

                        int OldRange = (amount - 0);
                        int NewRange = (100 - 0);
                        int NewValue = (((x - 0) * NewRange) / OldRange) + 0;
                        try
                        {
                            pBar.Value = NewValue;
                        }
                        catch { }
                    }
                    //Thread.Sleep(10);
                }
            }
            modbusClient.Disconnect(); //Disconnect from Server
            pBar.Hide();
            pBar.Value = 0;
            lblwait.Hide();
            display();
        }

        void SearchID()
        {
            richedit.Clear();
            int portnum = Int32.Parse(port.Text);
            int num1 = Int32.Parse(startreg.Text);
            int num2 = int.Parse(num.Text);
            int[] ids = new int[10000];
            int index = 0;

            ModbusClient modbusClient = new ModbusClient(ipadd.Text, portnum); //Ip-Address and Port of Modbus-TCP-Server
            //for(int i = num1; i < num2; i++)
            modbusClient.ConnectionTimeout = Convert.ToInt32(connectiontimeout.Value);

            lblwait.Show();
            try
            {
                modbusClient.Connect(); //Connect to Server
            }
            catch
            {
                MessageBox.Show("No Modbus Server Found");
            }

            pBar.Show();
            if (modbusClient.Connected)
            {
                for (int i = num1; i < num2 + 1; i++)
                {
                    if (!stop)
                    {
                        pBar.Minimum = 0;
                        pBar.Maximum = 100;

                        try
                        {
                            byte[] slave = BitConverter.GetBytes(i);
                            modbusClient.UnitIdentifier = slave[0];
                            int[] readHoldingRegisters = modbusClient.ReadHoldingRegisters(0, 10); //Read 10 Holding Registers from Server, starting with Address 1
                            ids[index] = i;
                            richedit.AppendText("Node ID : " + ids[index] + "\r\n");
                            index++;
                            if (showextrm.Checked)
                            {
                                break;
                            }
                        }
                        catch (System.IO.IOException)
                        {
                            //Node ID doesnt exist
                        }
                        catch
                        {
                            //Node ID exists but Registers could not be found
                            ids[index] = i;
                            richedit.AppendText("Node ID : " + ids[index] + "\r\n");
                            index++;
                            if (showextrm.Checked)
                            {
                                break;
                            }
                        }
                        int OldRange = (num2 - num1);
                        int NewRange = (100 - 0);
                        int NewValue = (((i - 0) * NewRange) / OldRange) + 0;
                        try
                        {
                            pBar.Value = NewValue;
                        }
                        catch { }
                    }
                }
            }
            pBar.Value = 0;
            pBar.Hide();
            lblwait.Hide();

            modbusClient.Disconnect(); //Disconnect from Server
        }

        private void save_Click(object sender, EventArgs e)
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
                catch { }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            display();
        }

        void display()
        {
            richedit.Clear();
            for (int i = 0; i < arrsize; i++)
            {
                if (!showextrm.Checked)
                {
                    if (
                        HoldingRegisters[i].Contains(":  0")
                        || HoldingRegisters[i].Contains(":  -32768")
                    )
                    {
                        //richedit.AppendText(HoldingRegisters[i] + "\r\n");
                    }
                    else
                    {
                        richedit.AppendText(HoldingRegisters[i] + "\n");
                    }
                }
                else
                {
                    richedit.AppendText(HoldingRegisters[i] + "\n");
                }
            }
            bStop.Hide();
        }

        private void ModbusScraper_Load(object sender, EventArgs e)
        {
            bStop.Hide();
            CheckForIllegalCrossThreadCalls = false;
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.SelectedIndex = 2;
            richedit.ReadOnly = true;
        }

        private void comboBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 4)
            {
                num.Value = 255;
                startreg.Value = 1;
                label4.Text = "Starting ID";
                label3.Text = "Ending ID";
                label5.Text = "Break Once Found";
                showextrm.Checked = true;
                slaveid.Hide();
                label6.Hide();
            }
            else
            {
                num.Value = 100;
                startreg.Value = 0;
                label4.Text = "Starting Register";
                label3.Text = "Amount";
                label5.Text = "Show Extremes";
                showextrm.Checked = true;
                slaveid.Show();
                label6.Show();
            }
        }

        private void showextrm_CheckedChanged_1(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex != 4)
            {
                Thread tid3 = new Thread(new ThreadStart(Thread3));
                tid3.IsBackground = true;
                tid3.Start();
            }
        }

        public void Thread3()
        {
            display();
        }

        private void bStop_Click(object sender, EventArgs e)
        {
            stop = true;
            bStop.Hide();
        }
    }
}
