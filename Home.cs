using EasyModbus;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace EasyBus_Modbus_Scanner
{
    public partial class Home : Form
    {
        int dbrowaddress = 0;
        int dbcoloumaddress = 0;
        int err = 0;
        bool[] readCoils = new bool[255];
        bool[] readinputs = new bool[255];
        int[] Registers = new int[255];
        int[] readInputStatus = new int[255];

        int[] DataType = new int[255];
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
            modbusClient.IPAddress = Properties.Settings.Default.IPAddress;
            modbusClient.Port = Properties.Settings.Default.ServerPort;

            polltimer.Interval = Properties.Settings.Default.PollingRate;
            int islave = Properties.Settings.Default.NodeID;
            byte[] slave = BitConverter.GetBytes(1);
            modbusClient.UnitIdentifier = slave[0];
            modbusClient.ConnectionTimeout = Properties.Settings.Default.ConnectionTimeOut;

            try
            {
                try
                {
                    if(!modbusClient.Connected)
                    {
                        modbusClient.Connect(); //Connect to Server
                    }
                }
                catch
                {
                    errorbox.Text = "Timeout";
                }

                switch (Properties.Settings.Default.Function)
                {
                    case 0: //0x
                        readCoils = modbusClient.ReadCoils(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                        break;

                    case 1://1x
                        readinputs = modbusClient.ReadDiscreteInputs(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                        break;

                    case 2://4x
                        Registers = modbusClient.ReadHoldingRegisters(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                        break;

                    case 3://3x
                        readInputStatus = modbusClient.ReadInputRegisters(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                        break;
                }
                //Adds data to data grid
                if (modbusClient.Connected)
                {
                    int addloop = Properties.Settings.Default.Amount;
                    int startingaddy = Properties.Settings.Default.Address;
                    for (int xc = 0; xc < addloop; xc++)
                    {
                        switch (Properties.Settings.Default.Function)
                        {
                            case 0:

                                if (readCoils[xc])
                                {
                                    this.DataGrid.Rows.Add("0x", xc + startingaddy, 1);
                                }
                                else
                                {
                                    this.DataGrid.Rows.Add("0x", xc + startingaddy, 0);
                                }
                                err++;
                                break;

                            case 1:

                                if (readinputs[xc])
                                {
                                    this.DataGrid.Rows.Add("1x", xc + startingaddy, 1);
                                }
                                else
                                {
                                    this.DataGrid.Rows.Add("1x", xc + startingaddy, 0);
                                }
                                break;

                            case 2:
                                ushort HoldingRegister = (ushort)Registers[xc];
                                this.DataGrid.Rows.Add("4x", xc + startingaddy, HoldingRegister);
                                break;

                            case 3:
                                ushort InputRegisters = (ushort)readInputStatus[xc];
                                this.DataGrid.Rows.Add("3x", xc + startingaddy, InputRegisters);
                                break;
                        }
                    }
                    PollCount++;
                    errorbox.Text = "Connected";
                }
            }
            catch (EasyModbus.Exceptions.StartingAddressInvalidException)
            {
                errorbox.Text = "Illegal Data Address";
            }
            catch(EasyModbus.Exceptions.FunctionCodeNotSupportedException)
            {
                errorbox.Text = "Function Code Doesnt Exist";
            }
            catch
            {
                errorbox.Text = "Timeout";
            }
        }

        //Poll Modbus to keep it up to date
        public void PollModbus()
        {
            modbusClient.IPAddress = Properties.Settings.Default.IPAddress;
            modbusClient.Port = Properties.Settings.Default.ServerPort;

            polltimer.Interval = Properties.Settings.Default.PollingRate;
            int loopcount = 0;
            int oldloopcount = 0;
            int islave = Properties.Settings.Default.NodeID;
            byte[] slave = BitConverter.GetBytes(1);

            modbusClient.UnitIdentifier = slave[0];
            modbusClient.ConnectionTimeout = Properties.Settings.Default.ConnectionTimeOut;

            try
            {

                if (!modbusClient.Connected) //If not connected try connect
                {
                    Connect();
                }
                else
                {
                    if (oldstartingregsize != Properties.Settings.Default.Address)
                    {
                        Array.Clear(DataType, 0, DataType.Length);
                    }

                    oldstartingregsize = Properties.Settings.Default.Address;

                    switch (Properties.Settings.Default.Function)
                    {
                        case 0://0x
                            oldloopcount = readCoils.Length;
                            break;

                        case 1://1x
                            oldloopcount = readinputs.Length;
                            break;

                        case 2://4x
                            oldloopcount = Registers.Length;
                            break;

                        case 3://3x
                            oldloopcount = readInputStatus.Length;
                            break;
                    }

                    pTx.Image = EasyBus_Modbus_Scanner.Properties.Resources.Green;

                    switch (Properties.Settings.Default.Function)
                    {
                        case 0://0x
                            readCoils = modbusClient.ReadCoils(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                            loopcount = readCoils.Length;
                            break;

                        case 1://1x
                            readinputs = modbusClient.ReadDiscreteInputs(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                            loopcount = readinputs.Length;
                            break;

                        case 2://4x
                            Registers = modbusClient.ReadHoldingRegisters(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                            loopcount = Registers.Length;
                            break;

                        case 3://3x
                            readInputStatus = modbusClient.ReadInputRegisters(Properties.Settings.Default.Address, Properties.Settings.Default.Amount);
                            loopcount = readInputStatus.Length;
                            break;
                    }

                    PollCount++;
                    tPollCount.Text = "Poll : " + PollCount;
                    errorbox.Text = "Connected";

                }
            }
            catch (EasyModbus.Exceptions.StartingAddressInvalidException)
            {
                errorbox.Text = "Illegal Data Address";
            }
            catch (EasyModbus.Exceptions.FunctionCodeNotSupportedException)
            {
                errorbox.Text = "Function Code Doesnt Exist";
            }
            catch
            {
                errorbox.Text = "Timeout";
                Connect();
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
                            switch (Properties.Settings.Default.Function)
                            {
                                case 0:
                                    this.DataGrid.Rows.Add("0x", "", "");
                                    break;

                                case 1:
                                    this.DataGrid.Rows.Add("1x", "", "");
                                    break;

                                case 2:
                                    this.DataGrid.Rows.Add("4x", "", "");
                                    break;

                                case 3:
                                    this.DataGrid.Rows.Add("3x", "", "");
                                    break;
                            }
                        }
                    }
                }
                catch { }
                if (DataGrid.RowCount == 0)
                {
                    Connect();
                }
                else
                {
                    for (int i = 0; i < loopcount; i++)
                    {
                        try
                        {
                            //Data Types
                            //0 = Signed
                            //1 = Unsigned
                            //3 = Hex
                            //4 = Binary

                            if (Properties.Settings.Default.Function == 2 || Properties.Settings.Default.Function == 3)
                            {
                                if (DataType[i] == 0)
                                {
                                    switch (Properties.Settings.Default.Function)
                                    {
                                        case 2:
                                            short signed = (short)Registers[i];
                                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                            DataGrid.Rows[i].Cells[2].Value = signed;
                                            //DataGrid.Rows[i].Cells[3].Value = "Signed";
                                            break;

                                        case 3:
                                            short signed2 = (short)readInputStatus[i];
                                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                            DataGrid.Rows[i].Cells[2].Value = signed2;
                                            break;
                                    }

                                }
                                else if (DataType[i] == 1)
                                {
                                    switch (Properties.Settings.Default.Function)
                                    {
                                        case 2:
                                            //Convert Signed Int into Unsigned int 
                                            ushort unsigned = (ushort)Registers[i];
                                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                            DataGrid.Rows[i].Cells[2].Value = unsigned;
                                            //DataGrid.Rows[i].Cells[3].Value = "Unsigned";
                                            break;

                                        case 3:
                                            //Convert Signed Int into Unsigned int 
                                            ushort unsigned2 = (ushort)readInputStatus[i];
                                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                            DataGrid.Rows[i].Cells[2].Value = unsigned2;
                                            //DataGrid.Rows[i].Cells[3].Value = "Unsigned";
                                            break;
                                    }

                                }
                                else if (DataType[i] == 2)
                                {
                                    switch (Properties.Settings.Default.Function)
                                    {
                                        case 2:
                                            // Convert Interget to Hex
                                            string hex = Convert.ToString(Registers[i], 16);
                                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                            DataGrid.Rows[i].Cells[2].Value = "0x" + hex.ToUpper();
                                            //DataGrid.Rows[i].Cells[3].Value = "Hex";
                                            break;

                                        case 3:
                                            // Convert Interget to Hex
                                            string hex2 = Convert.ToString(readInputStatus[i], 16);
                                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                            DataGrid.Rows[i].Cells[2].Value = "0x" + hex2.ToUpper();
                                            //DataGrid.Rows[i].Cells[3].Value = "Hex";
                                            break;
                                    }

                                }
                                else if (DataType[i] == 3)
                                {

                                    switch (Properties.Settings.Default.Function)
                                    {
                                        case 2:
                                            //Convert INT Value to Binary
                                            short signed = (short)Registers[i];
                                            int toadd = 0;
                                            string binary = Convert.ToString(signed, 2);
                                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                            toadd = 16 - binary.Length;

                                            for (int x = 0; x < toadd; x++)
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
                                            break;

                                        case 3:
                                            //Convert INT Value to Binary
                                            short signed2 = (short)readInputStatus[i];
                                            int toadd2 = 0;
                                            string binary2 = Convert.ToString(signed2, 2);
                                            DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                            toadd2 = 16 - binary2.Length;

                                            for (int x2 = 0; x2 < toadd2; x2++)
                                            {
                                                binary2 = "0" + binary2;
                                            }

                                            // Add space to binary every 4 bits
                                            for (int l2 = 0; l2 < binary2.Length; l2++)
                                            {
                                                if (l2 % 5 == 0)
                                                {
                                                    binary2 = binary2.Insert(l2, " ");
                                                }
                                            }

                                            DataGrid.Rows[i].Cells[2].Value = binary2;
                                            //DataGrid.Rows[i].Cells[3].Value = "Binary";
                                            break;
                                    }

                                }
                            }
                            else
                            {
                                switch (Properties.Settings.Default.Function)
                                {
                                    case 0:
                                        DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;

                                        if (readCoils[i])
                                        {
                                            DataGrid.Rows[i].Cells[2].Value = 1;
                                        }
                                        else
                                        {
                                            DataGrid.Rows[i].Cells[2].Value = 0;
                                        }
                                        break;

                                    case 1:
                                        DataGrid.Rows[i].Cells[1].Value = i + Properties.Settings.Default.Address;
                                        if (readinputs[i])
                                        {
                                            DataGrid.Rows[i].Cells[2].Value = 1;
                                        }
                                        else
                                        {
                                            DataGrid.Rows[i].Cells[2].Value = 0;
                                        }

                                        break;
                                }
                            }

                        }
                        catch { }
                    }
                }
                pTx.Image = EasyBus_Modbus_Scanner.Properties.Resources.Grey;
            }
        }
        
        //Writes data to modbus registers
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
                    switch (Properties.Settings.Default.Function)
                    {
                        case 0://0x
                            
                            if (writenumber == 0)
                            {
                                modbusClient.WriteSingleCoil(CurrentRow + Properties.Settings.Default.Address, false);
                            }
                            else if(writenumber == 1)
                            {
                                modbusClient.WriteSingleCoil(CurrentRow + Properties.Settings.Default.Address, true);
                            }
                            
                            break;

                        case 1://1x

                            break;

                        case 2://4x
                            modbusClient.WriteSingleRegister(CurrentRow + Properties.Settings.Default.Address, writenumber);
                            break;

                        case 3://3x
                            
                            break;
                    }
                }
                catch
                {
                    errorbox.Text = "Write Error";
                }
            }
        }

        public void UpdateDataGrid()
        {

        }

        private void polltimer_Tick(object sender, EventArgs e)
        {

            Thread tid1 = new Thread(new ThreadStart(Thread1));
            tid1.IsBackground = true;

            tid1.Start();

        }
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            setupToolStripMenuItem.Enabled = false;
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
            setupToolStripMenuItem.Enabled = true;
            connectToolStripMenuItem.Enabled = true;
            modbusClient.Disconnect();
            bConnect.Enabled = true;
            bStop.Enabled = false;

            polltimer.Enabled = false;
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
            Form stup = new Setup();
            stup.Show();
        }

        private void DataGrid_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        //Gets updated cell value and sends it off to write the data
        private void DataGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            CurrentRow = DataGrid.CurrentRow.Index;
            try
            {
                //Data Types
                //0 = Signed
                //1 = Unsigned
                //3 = Hex
                //4 = Binary
                if (Properties.Settings.Default.Function == 2)
                {
                    switch (DataType[CurrentRow])
                    {
                        //0 = Signed
                        case 0:

                            writenumber = Convert.ToInt16(DataGrid.Rows[CurrentRow].Cells[2].Value);
                            break;

                        //1 = Unsigned
                        case 1:

                            writenumber = Convert.ToUInt16(DataGrid.Rows[CurrentRow].Cells[2].Value);
                            break;

                        //3 = Hex
                        case 2:

                            writenumber = Convert.ToInt16(DataGrid.Rows[CurrentRow].Cells[2].Value);
                            break;

                        //4 = Binary
                        case 3:
                            string trimmed = DataGrid.Rows[CurrentRow].Cells[2].Value.ToString();
                            writenumber = Convert.ToInt16(DataGrid.Rows[CurrentRow].Cells[2].Value);
                            break;
                    }
                }
                else if (Properties.Settings.Default.Function == 0)
                {
                    writenumber = Convert.ToInt16(DataGrid.Rows[CurrentRow].Cells[2].Value);
                }
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
                    if (Properties.Settings.Default.Function == 2 || Properties.Settings.Default.Function == 3)
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

                }
                m.Show(DataGrid, new Point(e.X, e.Y));
            }
        }
        private void Signed(object sender, System.EventArgs e)
        {
            DataType[dbrowaddress] = 0;
            DataGrid.CurrentCell.ReadOnly = false;

        }
        private void Unsigned(object sender, System.EventArgs e)
        {
            DataType[dbrowaddress] = 1;
            DataGrid.CurrentCell.ReadOnly = false;

        }
        private void Hex(object sender, System.EventArgs e)
        {
            DataType[dbrowaddress] = 2;
            DataGrid.CurrentCell.ReadOnly = false;
        }
        private void Binary(object sender, System.EventArgs e)
        {
            DataType[dbrowaddress] = 3;
            DataGrid.CurrentCell.ReadOnly = true;

        }
        //#############################################################################################

        //Changes cell value to read only so user cant add some rubbish binary
        //Double click will allow user to edit binary with binary editor
        private void DataGrid_SelectionChanged(object sender, EventArgs e)
        {
            int columnIndex = DataGrid.CurrentCell.ColumnIndex;
            int rowIndex = DataGrid.CurrentCell.RowIndex;

            if (DataType[rowIndex] == 3)
            {
                DataGrid.CurrentCell.ReadOnly = true;
            }
            else
            {
                DataGrid.CurrentCell.ReadOnly = false;
            }
        }

        private void DataGrid_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            int columnIndex = DataGrid.CurrentCell.ColumnIndex;
            int rowIndex = DataGrid.CurrentCell.RowIndex;

            if (DataType[rowIndex] == 3)
            {
                //Gets binary and converts it to a INT to send to binary editor
                string data = DataGrid.Rows[rowIndex].Cells[2].Value.ToString();
                data = data.Replace(" ", "");
                Properties.Settings.Default.BinaryControlWord = Convert.ToInt32(data, 2);

                Form bnr = new BinaryEditor();
                CurrentRow = rowIndex;
                bnr.Show();
            }
        }

        //Credit to stefan 
        public void BinaryEdit()
        {
            writenumber = Properties.Settings.Default.BinaryControlWord;
            WriteData();
        }
    }
}
