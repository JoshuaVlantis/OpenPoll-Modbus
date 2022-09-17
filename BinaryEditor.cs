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
            ConvertToBin();
        }

        public void ConvertToBin()
        {
            int data = Properties.Settings.Default.BinaryControlWord;
            string binary = Convert.ToString(data, 2);

            int toadd = 0;
            toadd = 16 - binary.Length;

            for (int x = 0; x < toadd; x++)
            {
                binary = "0" + binary;
            }

            char[] cbinary = binary.ToCharArray();

            char[] Zero = "0".ToCharArray();
            char[] One = "1".ToCharArray();

            if (cbinary[15 - 15] == Zero[0])
                b15.Checked = false;
            else if (cbinary[15 - 15] == One[0])
                b15.Checked = true;

            if (cbinary[15 - 14] == Zero[0])
                b14.Checked = false;
            else if (cbinary[15 - 14] == One[0])
                b14.Checked = true;

            if (cbinary[15 - 13] == Zero[0])
                b13.Checked = false;
            else if (cbinary[15 - 13] == One[0])
                b13.Checked = true;

            if (cbinary[15 - 12] == Zero[0])
                b12.Checked = false;
            else if (cbinary[15 - 12] == One[0])
                b12.Checked = true;

            if (cbinary[15 - 11] == Zero[0])
                b11.Checked = false;
            else if (cbinary[15 - 11] == One[0])
                b11.Checked = true;

            if (cbinary[15 - 10] == Zero[0])
                b10.Checked = false;
            else if (cbinary[15 - 10] == One[0])
                b10.Checked = true;

            if (cbinary[15 - 9] == Zero[0])
                b9.Checked = false;
            else if (cbinary[15 - 9] == One[0])
                b9.Checked = true;

            if (cbinary[15 - 8] == Zero[0])
                b8.Checked = false;
            else if (cbinary[15 - 8] == One[0])
                b8.Checked = true;

            if (cbinary[15 - 7] == Zero[0])
                b7.Checked = false;
            else if (cbinary[15 - 7] == One[0])
                b7.Checked = true;

            if (cbinary[15 - 6] == Zero[0])
                b6.Checked = false;
            else if (cbinary[15 - 6] == One[0])
                b6.Checked = true;

            if (cbinary[15 - 5] == Zero[0])
                b5.Checked = false;
            else if (cbinary[15 - 5] == One[0])
                b5.Checked = true;

            if (cbinary[15 - 4] == Zero[0])
                b4.Checked = false;
            else if (cbinary[15 - 4] == One[0])
                b4.Checked = true;

            if (cbinary[15 - 3] == Zero[0])
                b3.Checked = false;
            else if (cbinary[15 - 3] == One[0])
                b3.Checked = true;

            if (cbinary[15 - 2] == Zero[0])
                b2.Checked = false;
            else if (cbinary[15 - 2] == One[0])
                b2.Checked = true;

            if (cbinary[15 - 1] == Zero[0])
                b1.Checked = false;
            else if (cbinary[15 - 1] == One[0])
                b1.Checked = true;

            if (cbinary[15 - 0] == Zero[0])
                b0.Checked = false;
            else if (cbinary[15 - 0] == One[0])
                b0.Checked = true;

            //Fill check boxes
        }

        public void send_Click(object sender, EventArgs e)
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

            //Credit to stefan
            if (System.Windows.Forms.Application.OpenForms["Home"] != null)
            {
                (System.Windows.Forms.Application.OpenForms["Home"] as Home).BinaryEdit();
            }

            this.Close();
        }
    }
}
