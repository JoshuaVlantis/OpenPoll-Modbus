namespace Modbus_Scraper
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.save = new System.Windows.Forms.Button();
            this.port = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.ipadd = new System.Windows.Forms.TextBox();
            this.richedit = new System.Windows.Forms.RichTextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.num = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.startreg = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.slaveid = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.port2 = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.ipaddr2 = new System.Windows.Forms.TextBox();
            this.id1 = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.id2 = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.richedit2 = new System.Windows.Forms.RichTextBox();
            this.lblwait = new System.Windows.Forms.Label();
            this.pBar = new System.Windows.Forms.ProgressBar();
            this.SearchNode = new System.Windows.Forms.Button();
            this.brkbox = new System.Windows.Forms.CheckBox();
            this.label11 = new System.Windows.Forms.Label();
            this.showextrm = new System.Windows.Forms.CheckBox();
            this.connectiontimeout = new System.Windows.Forms.NumericUpDown();
            this.label12 = new System.Windows.Forms.Label();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.label13 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.num)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.startreg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.connectiontimeout)).BeginInit();
            this.SuspendLayout();
            // 
            // save
            // 
            this.save.Location = new System.Drawing.Point(259, 388);
            this.save.Name = "save";
            this.save.Size = new System.Drawing.Size(75, 23);
            this.save.TabIndex = 13;
            this.save.Text = "Save";
            this.save.UseVisualStyleBackColor = true;
            this.save.Click += new System.EventHandler(this.save_Click_1);
            // 
            // port
            // 
            this.port.Location = new System.Drawing.Point(173, 29);
            this.port.Name = "port";
            this.port.Size = new System.Drawing.Size(41, 20);
            this.port.TabIndex = 12;
            this.port.Text = "502";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(170, 14);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(26, 13);
            this.label2.TabIndex = 11;
            this.label2.Text = "Port";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(41, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "IP Address";
            // 
            // ipadd
            // 
            this.ipadd.Location = new System.Drawing.Point(44, 29);
            this.ipadd.Name = "ipadd";
            this.ipadd.Size = new System.Drawing.Size(123, 20);
            this.ipadd.TabIndex = 9;
            this.ipadd.Text = "127.0.0.1";
            // 
            // richedit
            // 
            this.richedit.Location = new System.Drawing.Point(44, 135);
            this.richedit.Name = "richedit";
            this.richedit.Size = new System.Drawing.Size(290, 234);
            this.richedit.TabIndex = 8;
            this.richedit.Text = "";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(51, 388);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 7;
            this.button1.Text = "Search";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click_1);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(159, 388);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 14;
            this.button2.Text = "Display";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // num
            // 
            this.num.Location = new System.Drawing.Point(155, 76);
            this.num.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.num.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.num.Name = "num";
            this.num.Size = new System.Drawing.Size(105, 20);
            this.num.TabIndex = 16;
            this.num.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(156, 61);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(43, 13);
            this.label3.TabIndex = 17;
            this.label3.Text = "Amount";
            // 
            // startreg
            // 
            this.startreg.Location = new System.Drawing.Point(44, 76);
            this.startreg.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.startreg.Name = "startreg";
            this.startreg.Size = new System.Drawing.Size(105, 20);
            this.startreg.TabIndex = 18;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(41, 61);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 13);
            this.label4.TabIndex = 19;
            this.label4.Text = "Starting Register";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(276, 7);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(80, 13);
            this.label5.TabIndex = 22;
            this.label5.Text = "Show Extremes";
            // 
            // slaveid
            // 
            this.slaveid.Location = new System.Drawing.Point(220, 29);
            this.slaveid.Name = "slaveid";
            this.slaveid.Size = new System.Drawing.Size(41, 20);
            this.slaveid.TabIndex = 27;
            this.slaveid.Text = "1";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(217, 14);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(18, 13);
            this.label6.TabIndex = 26;
            this.label6.Text = "ID";
            // 
            // port2
            // 
            this.port2.Location = new System.Drawing.Point(624, 45);
            this.port2.Name = "port2";
            this.port2.Size = new System.Drawing.Size(41, 20);
            this.port2.TabIndex = 31;
            this.port2.Text = "502";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(621, 29);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(26, 13);
            this.label7.TabIndex = 30;
            this.label7.Text = "Port";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(492, 29);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(58, 13);
            this.label8.TabIndex = 29;
            this.label8.Text = "IP Address";
            // 
            // ipaddr2
            // 
            this.ipaddr2.Location = new System.Drawing.Point(495, 45);
            this.ipaddr2.Name = "ipaddr2";
            this.ipaddr2.Size = new System.Drawing.Size(123, 20);
            this.ipaddr2.TabIndex = 28;
            this.ipaddr2.Text = "127.0.0.1";
            // 
            // id1
            // 
            this.id1.Location = new System.Drawing.Point(521, 97);
            this.id1.Name = "id1";
            this.id1.Size = new System.Drawing.Size(41, 20);
            this.id1.TabIndex = 33;
            this.id1.Text = "1";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(545, 78);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(63, 13);
            this.label9.TabIndex = 32;
            this.label9.Text = "ID Between";
            // 
            // id2
            // 
            this.id2.Location = new System.Drawing.Point(587, 97);
            this.id2.Name = "id2";
            this.id2.Size = new System.Drawing.Size(41, 20);
            this.id2.TabIndex = 34;
            this.id2.Text = "255";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(569, 101);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(16, 13);
            this.label10.TabIndex = 35;
            this.label10.Text = "->";
            // 
            // richedit2
            // 
            this.richedit2.Location = new System.Drawing.Point(437, 135);
            this.richedit2.Name = "richedit2";
            this.richedit2.Size = new System.Drawing.Size(288, 234);
            this.richedit2.TabIndex = 36;
            this.richedit2.Text = "";
            // 
            // lblwait
            // 
            this.lblwait.AutoSize = true;
            this.lblwait.BackColor = System.Drawing.Color.Transparent;
            this.lblwait.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblwait.Location = new System.Drawing.Point(339, 224);
            this.lblwait.Name = "lblwait";
            this.lblwait.Size = new System.Drawing.Size(93, 20);
            this.lblwait.TabIndex = 38;
            this.lblwait.Text = "Searching...";
            this.lblwait.Visible = false;
            // 
            // pBar
            // 
            this.pBar.Location = new System.Drawing.Point(305, 256);
            this.pBar.Name = "pBar";
            this.pBar.Size = new System.Drawing.Size(163, 23);
            this.pBar.TabIndex = 37;
            this.pBar.Visible = false;
            // 
            // SearchNode
            // 
            this.SearchNode.Location = new System.Drawing.Point(437, 388);
            this.SearchNode.Name = "SearchNode";
            this.SearchNode.Size = new System.Drawing.Size(75, 23);
            this.SearchNode.TabIndex = 39;
            this.SearchNode.Text = "Search";
            this.SearchNode.UseVisualStyleBackColor = true;
            this.SearchNode.Click += new System.EventHandler(this.button4_Click);
            // 
            // brkbox
            // 
            this.brkbox.AutoSize = true;
            this.brkbox.Checked = true;
            this.brkbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.brkbox.Location = new System.Drawing.Point(681, 48);
            this.brkbox.Name = "brkbox";
            this.brkbox.Size = new System.Drawing.Size(44, 17);
            this.brkbox.TabIndex = 42;
            this.brkbox.Text = "Yes";
            this.brkbox.UseVisualStyleBackColor = true;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(678, 29);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(92, 13);
            this.label11.TabIndex = 43;
            this.label11.Text = "Break once found";
            // 
            // showextrm
            // 
            this.showextrm.AutoSize = true;
            this.showextrm.Checked = true;
            this.showextrm.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showextrm.Location = new System.Drawing.Point(279, 31);
            this.showextrm.Name = "showextrm";
            this.showextrm.Size = new System.Drawing.Size(44, 17);
            this.showextrm.TabIndex = 44;
            this.showextrm.Text = "Yes";
            this.showextrm.UseVisualStyleBackColor = true;
            // 
            // connectiontimeout
            // 
            this.connectiontimeout.Location = new System.Drawing.Point(266, 76);
            this.connectiontimeout.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.connectiontimeout.Name = "connectiontimeout";
            this.connectiontimeout.Size = new System.Drawing.Size(120, 20);
            this.connectiontimeout.TabIndex = 45;
            this.connectiontimeout.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(274, 60);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(102, 13);
            this.label12.TabIndex = 46;
            this.label12.Text = "Connection Timeout";
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "01 Coil Status (0x)",
            "02 Input Status (1x)",
            "03 Holding Register (4x)",
            "04 Input Registers (3x)"});
            this.comboBox1.Location = new System.Drawing.Point(101, 108);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(225, 21);
            this.comboBox1.TabIndex = 47;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(43, 111);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(54, 13);
            this.label13.TabIndex = 48;
            this.label13.Text = "Function :";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.ClientSize = new System.Drawing.Size(770, 460);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.connectiontimeout);
            this.Controls.Add(this.showextrm);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.brkbox);
            this.Controls.Add(this.SearchNode);
            this.Controls.Add(this.lblwait);
            this.Controls.Add(this.pBar);
            this.Controls.Add(this.richedit2);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.id2);
            this.Controls.Add(this.id1);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.port2);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.ipaddr2);
            this.Controls.Add(this.slaveid);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.startreg);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.num);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.save);
            this.Controls.Add(this.port);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ipadd);
            this.Controls.Add(this.richedit);
            this.Controls.Add(this.button1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Modbus Scraper";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.num)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.startreg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.connectiontimeout)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button save;
        private System.Windows.Forms.TextBox port;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox ipadd;
        private System.Windows.Forms.RichTextBox richedit;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.NumericUpDown num;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown startreg;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox slaveid;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox port2;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox ipaddr2;
        private System.Windows.Forms.TextBox id1;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox id2;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.RichTextBox richedit2;
        private System.Windows.Forms.Label lblwait;
        private System.Windows.Forms.ProgressBar pBar;
        private System.Windows.Forms.Button SearchNode;
        private System.Windows.Forms.CheckBox brkbox;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.CheckBox showextrm;
        private System.Windows.Forms.NumericUpDown connectiontimeout;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Label label13;
    }
}

