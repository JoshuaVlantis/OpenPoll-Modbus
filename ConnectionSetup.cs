using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyBus_Modbus_Scanner
{
    public partial class ConnectionSetup : Form
    {
        public ConnectionSetup()
        {
            InitializeComponent();
            ConnectionMode.SelectedIndex = 0;
            ComPort.SelectedIndex = 0;
            BaudRate.SelectedIndex = 5;
            DataBits.SelectedIndex = 1;
            Parity.SelectedIndex = 2;
            StopBits.SelectedIndex = 0;
            RTU.Checked = true;
        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.ConnectionMode = ConnectionMode.SelectedIndex;
            
            Properties.Settings.Default.IPAddress = IPaddress.Text;
            Properties.Settings.Default.ServerPort = Int32.Parse(Port.Text);
            Properties.Settings.Default.ConnectionTimeOut = Int32.Parse(ConnectionTimeout.Text);

            Properties.Settings.Default.ComPort = ComPort.SelectedIndex;
            Properties.Settings.Default.BaudRateIndex = BaudRate.SelectedIndex;
            Properties.Settings.Default.BaudRate = Int32.Parse(BaudRate.Text);
            Properties.Settings.Default.DataBitsIndex = DataBits.SelectedIndex;
            Properties.Settings.Default.Parity = Parity.SelectedIndex;
            Properties.Settings.Default.StopBits = StopBits.SelectedIndex;

            Properties.Settings.Default.ResponseTimeout = Decimal.ToInt32(ResponseTimeout.Value);
            Properties.Settings.Default.PollingRate = Decimal.ToInt32(PollingRate.Value);
            Properties.Settings.Default.Save();
            this.Close();

        }

        private void Close_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ConnectionSetup_Load(object sender, EventArgs e)
        {
            ConnectionMode.SelectedIndex = Properties.Settings.Default.ConnectionMode;

            IPaddress.Text= Properties.Settings.Default.IPAddress;
            Port.Text = Properties.Settings.Default.ServerPort.ToString();
            ConnectionTimeout.Text = Properties.Settings.Default.ConnectionTimeOut.ToString();

            ComPort.SelectedIndex =Properties.Settings.Default.ComPort;
            BaudRate.SelectedIndex = Properties.Settings.Default.BaudRateIndex ;          
            DataBits.SelectedIndex= Properties.Settings.Default.DataBitsIndex;
            Parity.SelectedIndex= Properties.Settings.Default.Parity;
            StopBits.SelectedIndex = Properties.Settings.Default.StopBits;

            ResponseTimeout.Text = Properties.Settings.Default.ResponseTimeout.ToString();
            PollingRate.Text = Properties.Settings.Default.PollingRate.ToString();
            Properties.Settings.Default.Save();

        }

        private void ResponseTimeout_TextChanged(object sender, EventArgs e)
        {

        }
    }
}

/*
    Values will be saved to settings.settings this will make it easier to create profiles at a later stage.
    
    Connection Mode 
        0 : Modbus TCP/IP
        1 : Modbus Serial Port
    

    
 
 */
