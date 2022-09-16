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
    public partial class Setup : Form
    {
        public Setup()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.NodeID = Decimal.ToInt32(NodeID.Value);
            Properties.Settings.Default.Address = Decimal.ToInt32(startreg.Value);
            Properties.Settings.Default.Amount = Decimal.ToInt32(amount.Value);
            Properties.Settings.Default.Function = comboBox1.SelectedIndex;
            Properties.Settings.Default.Save();
            //Properties.Settings.Default.Upgrade();
            this.Close();
        }

        private void Setup_Load(object sender, EventArgs e)
        {
            NodeID.Value = Properties.Settings.Default.NodeID;
            startreg.Value = Properties.Settings.Default.Address;
            amount.Value = Properties.Settings.Default.Amount;
            comboBox1.SelectedIndex = Properties.Settings.Default.Function;
        }

        private void Setup_Deactivate(object sender, EventArgs e)
        {
            this.Close();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
