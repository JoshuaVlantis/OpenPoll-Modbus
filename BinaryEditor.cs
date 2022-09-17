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
    public partial class BinaryEditor : Form
    {
        public BinaryEditor()
        {
            InitializeComponent();
        }

        private void BinaryEditor_Load(object sender, EventArgs e)
        {

        }

        private void send_Click(object sender, EventArgs e)
        {
            int ControlWord = 0;

            ////Set               Bitpos
            //ControlWord |= 1 << 0;
            ////Reset               Bitpos
            //ControlWord &= ~(1 << 0);

            if (b0.Checked)
                ControlWord |= 1 << 0;
            else
                ControlWord &= ~(1 << 0);

            if (b1.Checked)
                ControlWord |= 1 << 1;
            else
                ControlWord &= ~(1 << 1);

            if (b2.Checked)
                ControlWord |= 1 << 2;
            else
                ControlWord &= ~(1 << 2);

            if (b3.Checked)
                ControlWord |= 1 << 3;
            else
                ControlWord &= ~(1 << 3);

            if (b4.Checked)
                ControlWord |= 1 << 4;
            else
                ControlWord &= ~(1 << 4);

            if (b5.Checked)
                ControlWord |= 1 << 5;
            else
                ControlWord &= ~(1 << 5);

            if (b6.Checked)
                ControlWord |= 1 << 6;
            else
                ControlWord &= ~(1 << 6);

            if (b7.Checked)
                ControlWord |= 1 << 7;
            else
                ControlWord &= ~(1 << 7);

            if (b8.Checked)
                ControlWord |= 1 << 8;
            else
                ControlWord &= ~(1 << 8);

            if (b9.Checked)
                ControlWord |= 1 << 9;
            else
                ControlWord &= ~(1 << 9);

            if (b10.Checked)
                ControlWord |= 1 << 10;
            else
                ControlWord &= ~(1 << 10);

            if (b11.Checked)
                ControlWord |= 1 << 11;
            else
                ControlWord &= ~(1 << 11);

            if (b12.Checked)
                ControlWord |= 1 << 12;
            else
                ControlWord &= ~(1 << 12);

            if (b13.Checked)
                ControlWord |= 1 << 13;
            else
                ControlWord &= ~(1 << 13);

            if (b14.Checked)
                ControlWord |= 1 << 14;
            else
                ControlWord &= ~(1 << 14);

            if (b15.Checked)
                ControlWord |= 1 << 15;
            else
                ControlWord &= ~(1 << 15);

            Properties.Settings.Default.BinaryControlWord = ControlWord;
        }
    }
}
