using EasyModbus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyBus_Modbus_Scanner
{
    public partial class Home : Form
    {
        int dbrowaddress = 0;
        int dbcoloumaddress = 0;
        int[] readHoldingRegisters = new int[1000];
        int[] HoldingRegisterDataType = new int[1000];
        int CurrentRow;
        int writenumber;
        ulong PollCount;
        int oldstartingregsize;
        ModbusClient modbusClient = new ModbusClient(Properties.Settings.Default.IPAddress, Properties.Settings.Default.ServerPort); //Ip-Address and Port of Modbus-TCP-Server
        
        public Home()
        {
            InitializeComponent();
            bConnect.Enabled = true;
            bStop.Enabled = false;

            pTx.Image = EasyBus_Modbus_Scanner.Properties.Resources.Grey;

            CheckForIllegalCrossThreadCalls = false;
            DataGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            errorbox.Text = "";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }        
        public void Connect()
        {
            
            polltimer.Interval = Properties.Settings.Default.PollingRate;
            int islave = Properties.Settings.Default.NodeID;
            byte[] slave = BitConverter.GetBytes(1);
            modbusClient.UnitIdentifier = slave[0];
            modbusClient.ConnectionTimeout = Properties.Settings.Default.ConnectionTimeOut;

            try
            {
                modbusClient.Connect(); //Connect to Server
                readHoldingRegisters = modbusClient.ReadHoldingRegisters(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);

                PollCount++;
                errorbox.Text = "Connected";
            }
            catch
            {
                errorbox.Text = "Timeout";
            }
            //Adds data to data grid
            if(modbusClient.Connected)
            {
                for (int i = 0; i < Properties.Settings.Default.Amount; i++)
                {
                    ushort test = (ushort)readHoldingRegisters[i];
                    this.DataGrid.Rows.Add("4x",i + Properties.Settings.Default.Address, test);

                }
            }
        }

        //Poll Modbus to keep it up to date
        public void PollModbus()
        {
            polltimer.Interval = Properties.Settings.Default.PollingRate;
            int loopcount = 0;
            int oldloopcount = 0;
            int islave = Properties.Settings.Default.NodeID;
            byte[] slave = BitConverter.GetBytes(1);

            modbusClient.UnitIdentifier = slave[0];
            modbusClient.ConnectionTimeout = Properties.Settings.Default.ConnectionTimeOut;
            
            try
            {
                if (oldstartingregsize != Properties.Settings.Default.Address)
                {   
                    Array.Clear(HoldingRegisterDataType, 0, HoldingRegisterDataType.Length);
                }
                
                oldstartingregsize = Properties.Settings.Default.Address;
                
                if (!modbusClient.Connected) //If not connected try connect
                {
                    modbusClient.Connect(); //Connect to Server
                }
                
                oldloopcount = readHoldingRegisters.Length;
                
                pTx.Image = EasyBus_Modbus_Scanner.Properties.Resources.Green;
                readHoldingRegisters = modbusClient.ReadHoldingRegisters(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                PollCount++;
                tPollCount.Text = "Poll : " + PollCount;

                loopcount = readHoldingRegisters.Length;
                errorbox.Text = "Connected";
            }
            catch
            {
                errorbox.Text = "Timeout";
            }
            if (modbusClient.Connected)
            {
                try
                {
                    //If Register read size gets smaller remove rows
                    if (oldloopcount > loopcount && loopcount != 0)
                    {
                        for (int i = oldloopcount - 1; i > loopcount - 1; i--)
                        {
                            DataGrid.Rows.RemoveAt(i);
                        }
                    }
                    //If Register read size gets bigger add cells
                    if (oldloopcount < loopcount && loopcount != 0)
                    {
                        for (int i = 0; i < loopcount - oldloopcount; i++)
                        {
                            this.DataGrid.Rows.Add("4x", "", "");
                        }
                    }
                }
                catch { }
                for (int i = 0; i < loopcount; i++)
                {
                    try
                    {
                        //Data Types
                        //0 = Signed
                        //1 = Unsigned
                        //3 = Hex
                        //4 = Binary
                        
                        if (HoldingRegisterDataType[i] == 0)
                        {
                            short signed = (short)readHoldingRegisters[i];
                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                            DataGrid.Rows[i].Cells[2].Value = signed;
                            //DataGrid.Rows[i].Cells[3].Value = "Signed";
                        }
                        else if (HoldingRegisterDataType[i] == 1)
                        {
                            //Convert Signed Int into Unsigned int 
                            ushort unsigned = (ushort)readHoldingRegisters[i];
                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                            DataGrid.Rows[i].Cells[2].Value = unsigned;
                            //DataGrid.Rows[i].Cells[3].Value = "Unsigned";
                        }
                        else if (HoldingRegisterDataType[i] == 2)
                        {
                            // Convert Interget to Hex
                            string hex = Convert.ToString(readHoldingRegisters[i], 16);
                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                            DataGrid.Rows[i].Cells[2].Value = hex.ToUpper();
                            //DataGrid.Rows[i].Cells[3].Value = "Hex";
                        }
                        else if (HoldingRegisterDataType[i] == 3)
                        {
                            //Convert INT Value to Binary
                            short signed = (short)readHoldingRegisters[i];
                            int toadd = 0;
                            string binary = Convert.ToString(signed, 2);
                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                            toadd = 16 - binary.Length;
                            
                            for (int x = 0 ; x < toadd; x++)
                            {
                                binary = "0" + binary;
                            }
                            
                            // Add space to binary every 4 bits
                            for (int l = 0; l < binary.Length; l++)
                            {
                                if (l % 5 == 0)
                                {
                                    binary = binary.Insert(l, " ");
                                }
                            }

                            DataGrid.Rows[i].Cells[2].Value = binary;
                            //DataGrid.Rows[i].Cells[3].Value = "Binary";
                        }
                    }
                    catch { }
                }
                pTx.Image = EasyBus_Modbus_Scanner.Properties.Resources.Grey;
            }
        }

        public void WriteData()
        {
            if (modbusClient.Connected)
            {
                int islave = Properties.Settings.Default.NodeID;
                byte[] slave = BitConverter.GetBytes(1);

                modbusClient.UnitIdentifier = slave[0];
                modbusClient.ConnectionTimeout = Properties.Settings.Default.ConnectionTimeOut;

                try
                {
                    modbusClient.WriteSingleRegister(CurrentRow + Properties.Settings.Default.Address, writenumber);
                }
                catch
                {
                    errorbox.Text = "Write Error";
                }
            }
        }

        public void UpdateDataGrid()
        {
            //DataGrid.Rows[0].Cells[2].Value = "Test";
        }

        private void polltimer_Tick(object sender, EventArgs e)
        {

            Thread tid1 = new Thread(new ThreadStart(Thread1));
            tid1.IsBackground = true;

            tid1.Start();
            
        }
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            connectToolStripMenuItem.Enabled = false;
            bConnect.Enabled = false;
            bStop.Enabled = true;

            DataGrid.Rows.Clear();
            Connect();
            polltimer.Enabled = true;
         
        }

        public void Thread1()
        {
            PollModbus();
        }

        private void bStop_Click(object sender, EventArgs e)
        {
            connectToolStripMenuItem.Enabled = true;
            modbusClient.Disconnect();
            bConnect.Enabled = true;
            bStop.Enabled = false;

            polltimer.Enabled=false;
        }
        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form cnnt = new ConnectionSetup();
            cnnt.Show();
        }

        private void modbusScraperToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form mbscraper = new ModbusScraper();
            mbscraper.Show();
        }
        private void setupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form stup= new Setup();
            stup.Show();
        }

        private void DataGrid_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void DataGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            CurrentRow = DataGrid.CurrentRow.Index;
            try
            {
                writenumber = Convert.ToInt32(DataGrid.Rows[CurrentRow].Cells[2].Value);
            }
            catch
            { }

            WriteData();
        }

        private void GCcall_Tick(object sender, EventArgs e)
        {
            //GC.Collect();
        }

//DATA TYPES###################################################################################
        private void DataGrid_MouseClick(object sender, MouseEventArgs e)
        {
            //Data Types
            //0 = Signed
            //1 = Unsigned
            //3 = Hex
            //4 = Binary
            
            if (e.Button == MouseButtons.Right)
            {
                ContextMenu m = new ContextMenu();

                
                int currentMouseOverRow = DataGrid.HitTest(e.X, e.Y).RowIndex;
                int currentMouseOverColumn = DataGrid.HitTest(e.X, e.Y).ColumnIndex;

                //If mouse is over the fourth column

                if (currentMouseOverRow >= 0 && currentMouseOverColumn == 2)
                {
                    dbcoloumaddress = currentMouseOverColumn;
                    dbrowaddress = currentMouseOverRow;
                    
                    m.MenuItems.Add(new MenuItem("Signed"));
                    m.MenuItems.Add(new MenuItem("Unsigned"));
                    m.MenuItems.Add(new MenuItem("Hex"));
                    m.MenuItems.Add(new MenuItem("Binary"));

                    // If menu Item is clicked, display a message box.
                    m.MenuItems[0].Click += new EventHandler(Signed);
                    m.MenuItems[1].Click += new EventHandler(Unsigned);
                    m.MenuItems[2].Click += new EventHandler(Hex);
                    m.MenuItems[3].Click += new EventHandler(Binary);
                    
                }
                m.Show(DataGrid, new Point(e.X, e.Y));
            }
        }
        private void Signed(object sender, System.EventArgs e)
        {
            HoldingRegisterDataType[dbrowaddress] = 0;
        }
        private void Unsigned(object sender, System.EventArgs e)
        {
            HoldingRegisterDataType[dbrowaddress] = 1;
        }
        private void Hex(object sender, System.EventArgs e)
        {
            HoldingRegisterDataType[dbrowaddress] = 2;
        }
        private void Binary(object sender, System.EventArgs e)
        {
            HoldingRegisterDataType[dbrowaddress] = 3;
        }
        //#############################################################################################
        private void DataGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}
