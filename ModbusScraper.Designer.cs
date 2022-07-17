namespace EasyBus_Modbus_Scanner
{
    partial class ModbusScraper
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ModbusScraper));
            this.bStop = new System.Windows.Forms.Button();
            this.label13 = new System.Windows.Forms.Label();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.label12 = new System.Windows.Forms.Label();
            this.connectiontimeout = new System.Windows.Forms.NumericUpDown();
            this.showextrm = new System.Windows.Forms.CheckBox();
            this.lblwait = new System.Windows.Forms.Label();
            this.pBar = new System.Windows.Forms.ProgressBar();
            this.slaveid = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.startreg = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.num = new System.Windows.Forms.NumericUpDown();
            this.save = new System.Windows.Forms.Button();
            this.port = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.ipadd = new System.Windows.Forms.TextBox();
            this.richedit = new System.Windows.Forms.RichTextBox();
            this.button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.connectiontimeout)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.startreg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.num)).BeginInit();
            this.SuspendLayout();
            // 
            // bStop
            // 
            this.bStop.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bStop.Location = new System.Drawing.Point(149, 409);
            this.bStop.Name = "bStop";
            this.bStop.Size = new System.Drawing.Size(75, 23);
            this.bStop.TabIndex = 71;
            this.bStop.Text = "Stop";
            this.bStop.UseVisualStyleBackColor = true;
            this.bStop.Click += new System.EventHandler(this.bStop_Click);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(12, 105);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(48, 13);
            this.label13.TabIndex = 70;
            this.label13.Text = "Function";
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "01 Coil Status (0x)",
            "02 Input Status (1x)",
            "03 Holding Register (4x)",
            "04 Input Registers (3x)",
            "Seach Node IDs"});
            this.comboBox1.Location = new System.Drawing.Point(15, 121);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(338, 21);
            this.comboBox1.TabIndex = 69;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged_1);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(271, 16);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(102, 13);
            this.label12.TabIndex = 68;
            this.label12.Text = "Connection Timeout";
            // 
            // connectiontimeout
            // 
            this.connectiontimeout.Location = new System.Drawing.Point(274, 32);
            this.connectiontimeout.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.connectiontimeout.Name = "connectiontimeout";
            this.connectiontimeout.Size = new System.Drawing.Size(79, 20);
            this.connectiontimeout.TabIndex = 67;
            this.connectiontimeout.Value = new decimal(new int[] {
            500,
            0,
            0,
            0});
            // 
            // showextrm
            // 
            this.showextrm.AutoSize = true;
            this.showextrm.Checked = true;
            this.showextrm.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showextrm.Location = new System.Drawing.Point(274, 79);
            this.showextrm.Name = "showextrm";
            this.showextrm.Size = new System.Drawing.Size(44, 17);
            this.showextrm.TabIndex = 66;
            this.showextrm.Text = "Yes";
            this.showextrm.UseVisualStyleBackColor = true;
            this.showextrm.CheckedChanged += new System.EventHandler(this.showextrm_CheckedChanged_1);
            // 
            // lblwait
            // 
            this.lblwait.AutoSize = true;
            this.lblwait.BackColor = System.Drawing.Color.Transparent;
            this.lblwait.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblwait.Location = new System.Drawing.Point(140, 248);
            this.lblwait.Name = "lblwait";
            this.lblwait.Size = new System.Drawing.Size(105, 20);
            this.lblwait.TabIndex = 65;
            this.lblwait.Text = "Searching...";
            this.lblwait.Visible = false;
            // 
            // pBar
            // 
            this.pBar.Location = new System.Drawing.Point(104, 280);
            this.pBar.Name = "pBar";
            this.pBar.Size = new System.Drawing.Size(163, 23);
            this.pBar.TabIndex = 64;
            this.pBar.Visible = false;
            // 
            // slaveid
            // 
            this.slaveid.Location = new System.Drawing.Point(215, 31);
            this.slaveid.Name = "slaveid";
            this.slaveid.Size = new System.Drawing.Size(41, 20);
            this.slaveid.TabIndex = 63;
            this.slaveid.Text = "1";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(212, 16);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(18, 13);
            this.label6.TabIndex = 62;
            this.label6.Text = "ID";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(272, 64);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(80, 13);
            this.label5.TabIndex = 61;
            this.label5.Text = "Show Extremes";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 63);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 13);
            this.label4.TabIndex = 60;
            this.label4.Text = "Starting Register";
            // 
            // startreg
            // 
            this.startreg.Location = new System.Drawing.Point(15, 78);
            this.startreg.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.startreg.Name = "startreg";
            this.startreg.Size = new System.Drawing.Size(105, 20);
            this.startreg.TabIndex = 59;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(141, 63);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(43, 13);
            this.label3.TabIndex = 58;
            this.label3.Text = "Amount";
            // 
            // num
            // 
            this.num.Location = new System.Drawing.Point(144, 78);
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
            this.num.TabIndex = 57;
            this.num.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // save
            // 
            this.save.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.save.Location = new System.Drawing.Point(277, 409);
            this.save.Name = "save";
            this.save.Size = new System.Drawing.Size(75, 23);
            this.save.TabIndex = 56;
            this.save.Text = "Save";
            this.save.UseVisualStyleBackColor = true;
            this.save.Click += new System.EventHandler(this.save_Click);
            // 
            // port
            // 
            this.port.Location = new System.Drawing.Point(156, 31);
            this.port.Name = "port";
            this.port.Size = new System.Drawing.Size(41, 20);
            this.port.TabIndex = 55;
            this.port.Text = "502";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(153, 16);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(26, 13);
            this.label2.TabIndex = 54;
            this.label2.Text = "Port";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 53;
            this.label1.Text = "IP Address";
            // 
            // ipadd
            // 
            this.ipadd.Location = new System.Drawing.Point(15, 31);
            this.ipadd.Name = "ipadd";
            this.ipadd.Size = new System.Drawing.Size(123, 20);
            this.ipadd.TabIndex = 52;
            this.ipadd.Text = "127.0.0.1";
            // 
            // richedit
            // 
            this.richedit.Location = new System.Drawing.Point(15, 156);
            this.richedit.Name = "richedit";
            this.richedit.Size = new System.Drawing.Size(337, 234);
            this.richedit.TabIndex = 51;
            this.richedit.Text = "";
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.Location = new System.Drawing.Point(15, 409);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 50;
            this.button1.Text = "Search";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // ModbusScraper
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.ClientSize = new System.Drawing.Size(374, 449);
            this.Controls.Add(this.bStop);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.connectiontimeout);
            this.Controls.Add(this.showextrm);
            this.Controls.Add(this.lblwait);
            this.Controls.Add(this.pBar);
            this.Controls.Add(this.slaveid);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.startreg);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.num);
            this.Controls.Add(this.save);
            this.Controls.Add(this.port);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ipadd);
            this.Controls.Add(this.richedit);
            this.Controls.Add(this.button1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ModbusScraper";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Modbus Scraper";
            this.Load += new System.EventHandler(this.ModbusScraper_Load);
            ((System.ComponentModel.ISupportInitialize)(this.connectiontimeout)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.startreg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.num)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bStop;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.NumericUpDown connectiontimeout;
        private System.Windows.Forms.CheckBox showextrm;
        private System.Windows.Forms.Label lblwait;
        private System.Windows.Forms.ProgressBar pBar;
        private System.Windows.Forms.TextBox slaveid;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown startreg;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown num;
        private System.Windows.Forms.Button save;
        private System.Windows.Forms.TextBox port;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox ipadd;
        private System.Windows.Forms.RichTextBox richedit;
        private System.Windows.Forms.Button button1;
    }
}