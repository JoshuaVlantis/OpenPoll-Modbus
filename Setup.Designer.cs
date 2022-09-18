namespace EasyBus_Modbus_Scanner
{
    partial class Setup
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
            this.NodeID = new System.Windows.Forms.NumericUpDown();
            this.label9 = new System.Windows.Forms.Label();
            this.Save = new System.Windows.Forms.Button();
            this.label13 = new System.Windows.Forms.Label();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.startreg = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.amount = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.NodeID)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.startreg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.amount)).BeginInit();
            this.SuspendLayout();
            // 
            // NodeID
            // 
            this.NodeID.Location = new System.Drawing.Point(65, 24);
            this.NodeID.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.NodeID.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.NodeID.Name = "NodeID";
            this.NodeID.Size = new System.Drawing.Size(64, 20);
            this.NodeID.TabIndex = 16;
            this.NodeID.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(12, 26);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(47, 13);
            this.label9.TabIndex = 17;
            this.label9.Text = "Node-ID";
            // 
            // Save
            // 
            this.Save.Location = new System.Drawing.Point(287, 21);
            this.Save.Name = "Save";
            this.Save.Size = new System.Drawing.Size(75, 23);
            this.Save.TabIndex = 18;
            this.Save.Text = "Save";
            this.Save.UseVisualStyleBackColor = true;
            this.Save.Click += new System.EventHandler(this.Save_Click);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(12, 63);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(48, 13);
            this.label13.TabIndex = 72;
            this.label13.Text = "Function";
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "01 Coil Status (0x)",
            "02 Input Status (1x)",
            "03 Holding Register (4x)",
            "04 Input Registers (3x)"});
            this.comboBox1.Location = new System.Drawing.Point(65, 60);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(162, 21);
            this.comboBox1.TabIndex = 71;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 92);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 13);
            this.label4.TabIndex = 76;
            this.label4.Text = "Starting Register";
            // 
            // startreg
            // 
            this.startreg.Location = new System.Drawing.Point(15, 107);
            this.startreg.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.startreg.Name = "startreg";
            this.startreg.Size = new System.Drawing.Size(105, 20);
            this.startreg.TabIndex = 75;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(141, 92);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(43, 13);
            this.label3.TabIndex = 74;
            this.label3.Text = "Amount";
            // 
            // amount
            // 
            this.amount.Location = new System.Drawing.Point(144, 107);
            this.amount.Maximum = new decimal(new int[] {
            123,
            0,
            0,
            0});
            this.amount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.amount.Name = "amount";
            this.amount.Size = new System.Drawing.Size(105, 20);
            this.amount.TabIndex = 73;
            this.amount.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // Setup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(374, 145);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.startreg);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.amount);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.Save);
            this.Controls.Add(this.NodeID);
            this.Controls.Add(this.label9);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "Setup";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Setup";
            this.Deactivate += new System.EventHandler(this.Setup_Deactivate);
            this.Load += new System.EventHandler(this.Setup_Load);
            ((System.ComponentModel.ISupportInitialize)(this.NodeID)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.startreg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.amount)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown NodeID;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button Save;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown startreg;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown amount;
    }
}