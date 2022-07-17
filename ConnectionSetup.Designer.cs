namespace EasyBus_Modbus_Scanner
{
    partial class ConnectionSetup
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConnectionSetup));
            this.ConnectionMode = new System.Windows.Forms.ComboBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.panel2 = new System.Windows.Forms.Panel();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.RTU = new System.Windows.Forms.RadioButton();
            this.StopBits = new System.Windows.Forms.ComboBox();
            this.Parity = new System.Windows.Forms.ComboBox();
            this.DataBits = new System.Windows.Forms.ComboBox();
            this.BaudRate = new System.Windows.Forms.ComboBox();
            this.ComPort = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.panel3 = new System.Windows.Forms.Panel();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.ConnectionTimeout = new System.Windows.Forms.TextBox();
            this.Port = new System.Windows.Forms.TextBox();
            this.IPaddress = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.Save = new System.Windows.Forms.Button();
            this.Close = new System.Windows.Forms.Button();
            this.panel4 = new System.Windows.Forms.Panel();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.PollingRate = new System.Windows.Forms.NumericUpDown();
            this.ResponseTimeout = new System.Windows.Forms.NumericUpDown();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PollingRate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ResponseTimeout)).BeginInit();
            this.SuspendLayout();
            // 
            // ConnectionMode
            // 
            this.ConnectionMode.FormattingEnabled = true;
            this.ConnectionMode.Items.AddRange(new object[] {
            "Modbus TCP/IP",
            "Serial"});
            this.ConnectionMode.Location = new System.Drawing.Point(16, 19);
            this.ConnectionMode.Name = "ConnectionMode";
            this.ConnectionMode.Size = new System.Drawing.Size(121, 21);
            this.ConnectionMode.TabIndex = 0;
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.ConnectionMode);
            this.panel1.Location = new System.Drawing.Point(12, 12);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(345, 55);
            this.panel1.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(2, 2);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(100, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Connection Method";
            // 
            // panel2
            // 
            this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel2.Controls.Add(this.radioButton2);
            this.panel2.Controls.Add(this.RTU);
            this.panel2.Controls.Add(this.StopBits);
            this.panel2.Controls.Add(this.Parity);
            this.panel2.Controls.Add(this.DataBits);
            this.panel2.Controls.Add(this.BaudRate);
            this.panel2.Controls.Add(this.ComPort);
            this.panel2.Controls.Add(this.label2);
            this.panel2.Location = new System.Drawing.Point(13, 86);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(344, 163);
            this.panel2.TabIndex = 2;
            this.panel2.Paint += new System.Windows.Forms.PaintEventHandler(this.panel2_Paint);
            // 
            // radioButton2
            // 
            this.radioButton2.AutoSize = true;
            this.radioButton2.Location = new System.Drawing.Point(279, 42);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(52, 17);
            this.radioButton2.TabIndex = 8;
            this.radioButton2.TabStop = true;
            this.radioButton2.Text = "ASCII";
            this.radioButton2.UseVisualStyleBackColor = true;
            // 
            // RTU
            // 
            this.RTU.AutoSize = true;
            this.RTU.Location = new System.Drawing.Point(279, 19);
            this.RTU.Name = "RTU";
            this.RTU.Size = new System.Drawing.Size(48, 17);
            this.RTU.TabIndex = 7;
            this.RTU.TabStop = true;
            this.RTU.Text = "RTU";
            this.RTU.UseVisualStyleBackColor = true;
            // 
            // StopBits
            // 
            this.StopBits.FormattingEnabled = true;
            this.StopBits.Items.AddRange(new object[] {
            "1 Stop Bit",
            "2 Stop Bits"});
            this.StopBits.Location = new System.Drawing.Point(15, 127);
            this.StopBits.Name = "StopBits";
            this.StopBits.Size = new System.Drawing.Size(121, 21);
            this.StopBits.TabIndex = 6;
            // 
            // Parity
            // 
            this.Parity.FormattingEnabled = true;
            this.Parity.Items.AddRange(new object[] {
            "No Parity",
            "Odd Parity",
            "Even Parity"});
            this.Parity.Location = new System.Drawing.Point(15, 100);
            this.Parity.Name = "Parity";
            this.Parity.Size = new System.Drawing.Size(121, 21);
            this.Parity.TabIndex = 5;
            // 
            // DataBits
            // 
            this.DataBits.FormattingEnabled = true;
            this.DataBits.Items.AddRange(new object[] {
            "7 Data bits",
            "8 Data bits"});
            this.DataBits.Location = new System.Drawing.Point(15, 73);
            this.DataBits.Name = "DataBits";
            this.DataBits.Size = new System.Drawing.Size(121, 21);
            this.DataBits.TabIndex = 4;
            // 
            // BaudRate
            // 
            this.BaudRate.FormattingEnabled = true;
            this.BaudRate.Items.AddRange(new object[] {
            "300",
            "600",
            "1200",
            "2400",
            "4800",
            "9600",
            "14400",
            "19200",
            "38400",
            "56000",
            "57600",
            "115200",
            "128000",
            "153600",
            "230400",
            "256000",
            "460800",
            "921600"});
            this.BaudRate.Location = new System.Drawing.Point(15, 46);
            this.BaudRate.Name = "BaudRate";
            this.BaudRate.Size = new System.Drawing.Size(121, 21);
            this.BaudRate.TabIndex = 3;
            // 
            // ComPort
            // 
            this.ComPort.FormattingEnabled = true;
            this.ComPort.Items.AddRange(new object[] {
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "COM10",
            "COM11",
            "COM12",
            "COM13",
            "COM14",
            "COM15",
            "COM16",
            "COM17",
            "COM18",
            "COM19",
            "COM20",
            "COM21",
            "COM22",
            "COM23",
            "COM24",
            "COM25",
            "COM26",
            "COM27",
            "COM28",
            "COM29",
            "COM30",
            "COM31",
            "COM32",
            "COM33",
            "COM34",
            "COM35",
            "COM36",
            "COM37",
            "COM38",
            "COM39",
            "COM40",
            "COM41",
            "COM42",
            "COM43",
            "COM44",
            "COM45",
            "COM46",
            "COM47",
            "COM48",
            "COM49",
            "COM50",
            "COM51",
            "COM52",
            "COM53",
            "COM54",
            "COM55",
            "COM56",
            "COM57",
            "COM58",
            "COM59",
            "COM60",
            "COM61",
            "COM62",
            "COM63",
            "COM64",
            "COM65",
            "COM66",
            "COM67",
            "COM68",
            "COM69",
            "COM70",
            "COM71",
            "COM72",
            "COM73",
            "COM74",
            "COM75",
            "COM76",
            "COM77",
            "COM78",
            "COM79",
            "COM80",
            "COM81",
            "COM82",
            "COM83",
            "COM84",
            "COM85",
            "COM86",
            "COM87",
            "COM88",
            "COM89",
            "COM90",
            "COM91",
            "COM92",
            "COM93",
            "COM94",
            "COM95",
            "COM96",
            "COM97",
            "COM98",
            "COM99",
            "COM100",
            "COM101",
            "COM102",
            "COM103",
            "COM104",
            "COM105",
            "COM106",
            "COM107",
            "COM108",
            "COM109",
            "COM110",
            "COM111",
            "COM112",
            "COM113",
            "COM114",
            "COM115",
            "COM116",
            "COM117",
            "COM118",
            "COM119",
            "COM120",
            "COM121",
            "COM122",
            "COM123",
            "COM124",
            "COM125",
            "COM126",
            "COM127",
            "COM128"});
            this.ComPort.Location = new System.Drawing.Point(15, 19);
            this.ComPort.Name = "ComPort";
            this.ComPort.Size = new System.Drawing.Size(121, 21);
            this.ComPort.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(2, 2);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Serial Settings";
            // 
            // panel3
            // 
            this.panel3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel3.Controls.Add(this.label6);
            this.panel3.Controls.Add(this.label5);
            this.panel3.Controls.Add(this.label4);
            this.panel3.Controls.Add(this.ConnectionTimeout);
            this.panel3.Controls.Add(this.Port);
            this.panel3.Controls.Add(this.IPaddress);
            this.panel3.Controls.Add(this.label3);
            this.panel3.Location = new System.Drawing.Point(13, 271);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(345, 128);
            this.panel3.TabIndex = 3;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(157, 65);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(102, 13);
            this.label6.TabIndex = 13;
            this.label6.Text = "Connection Timeout";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(16, 65);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(26, 13);
            this.label5.TabIndex = 12;
            this.label5.Text = "Port";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(16, 22);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(58, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "IP Address";
            // 
            // ConnectionTimeout
            // 
            this.ConnectionTimeout.Location = new System.Drawing.Point(156, 81);
            this.ConnectionTimeout.Name = "ConnectionTimeout";
            this.ConnectionTimeout.Size = new System.Drawing.Size(80, 20);
            this.ConnectionTimeout.TabIndex = 10;
            // 
            // Port
            // 
            this.Port.Location = new System.Drawing.Point(15, 81);
            this.Port.Name = "Port";
            this.Port.Size = new System.Drawing.Size(61, 20);
            this.Port.TabIndex = 9;
            // 
            // IPaddress
            // 
            this.IPaddress.Location = new System.Drawing.Point(15, 38);
            this.IPaddress.Name = "IPaddress";
            this.IPaddress.Size = new System.Drawing.Size(221, 20);
            this.IPaddress.TabIndex = 8;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(2, 2);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(120, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Modbus Server Address";
            // 
            // Save
            // 
            this.Save.Location = new System.Drawing.Point(402, 10);
            this.Save.Name = "Save";
            this.Save.Size = new System.Drawing.Size(75, 23);
            this.Save.TabIndex = 4;
            this.Save.Text = "Accept";
            this.Save.UseVisualStyleBackColor = true;
            this.Save.Click += new System.EventHandler(this.button1_Click);
            // 
            // Close
            // 
            this.Close.Location = new System.Drawing.Point(402, 44);
            this.Close.Name = "Close";
            this.Close.Size = new System.Drawing.Size(75, 23);
            this.Close.TabIndex = 5;
            this.Close.Text = "Close";
            this.Close.UseVisualStyleBackColor = true;
            this.Close.Click += new System.EventHandler(this.Close_Click);
            // 
            // panel4
            // 
            this.panel4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel4.Controls.Add(this.ResponseTimeout);
            this.panel4.Controls.Add(this.PollingRate);
            this.panel4.Controls.Add(this.label8);
            this.panel4.Controls.Add(this.label7);
            this.panel4.Location = new System.Drawing.Point(387, 86);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(107, 102);
            this.panel4.TabIndex = 6;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(3, 51);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(64, 13);
            this.label8.TabIndex = 10;
            this.label8.Text = "Polling Rate";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(3, 12);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(96, 13);
            this.label7.TabIndex = 9;
            this.label7.Text = "Response Timeout";
            // 
            // PollingRate
            // 
            this.PollingRate.Location = new System.Drawing.Point(6, 67);
            this.PollingRate.Maximum = new decimal(new int[] {
            999999,
            0,
            0,
            0});
            this.PollingRate.Minimum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this.PollingRate.Name = "PollingRate";
            this.PollingRate.Size = new System.Drawing.Size(75, 20);
            this.PollingRate.TabIndex = 17;
            this.PollingRate.Value = new decimal(new int[] {
            500,
            0,
            0,
            0});
            // 
            // ResponseTimeout
            // 
            this.ResponseTimeout.Location = new System.Drawing.Point(6, 28);
            this.ResponseTimeout.Maximum = new decimal(new int[] {
            9999999,
            0,
            0,
            0});
            this.ResponseTimeout.Name = "ResponseTimeout";
            this.ResponseTimeout.Size = new System.Drawing.Size(75, 20);
            this.ResponseTimeout.TabIndex = 18;
            this.ResponseTimeout.Value = new decimal(new int[] {
            5000,
            0,
            0,
            0});
            // 
            // ConnectionSetup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(506, 413);
            this.Controls.Add(this.panel4);
            this.Controls.Add(this.Close);
            this.Controls.Add(this.Save);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ConnectionSetup";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Connection Setup";
            this.Load += new System.EventHandler(this.ConnectionSetup_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PollingRate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ResponseTimeout)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox ConnectionMode;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox ComPort;
        private System.Windows.Forms.ComboBox StopBits;
        private System.Windows.Forms.ComboBox Parity;
        private System.Windows.Forms.ComboBox DataBits;
        private System.Windows.Forms.ComboBox BaudRate;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox ConnectionTimeout;
        private System.Windows.Forms.TextBox Port;
        private System.Windows.Forms.TextBox IPaddress;
        private System.Windows.Forms.Button Save;
        private System.Windows.Forms.RadioButton radioButton2;
        private System.Windows.Forms.RadioButton RTU;
        private System.Windows.Forms.Button Close;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown PollingRate;
        private System.Windows.Forms.NumericUpDown ResponseTimeout;
    }
}