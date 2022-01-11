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
            this.save = new System.Windows.Forms.Button();
            this.port = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.ipadd = new System.Windows.Forms.TextBox();
            this.richedit = new System.Windows.Forms.RichTextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.lblwait = new System.Windows.Forms.Label();
            this.num = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.startreg = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.overflow = new System.Windows.Forms.RadioButton();
            this.label5 = new System.Windows.Forms.Label();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            ((System.ComponentModel.ISupportInitialize)(this.num)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.startreg)).BeginInit();
            this.SuspendLayout();
            // 
            // save
            // 
            this.save.Location = new System.Drawing.Point(239, 388);
            this.save.Name = "save";
            this.save.Size = new System.Drawing.Size(75, 23);
            this.save.TabIndex = 13;
            this.save.Text = "Save";
            this.save.UseVisualStyleBackColor = true;
            this.save.Click += new System.EventHandler(this.save_Click_1);
            // 
            // port
            // 
            this.port.Location = new System.Drawing.Point(194, 45);
            this.port.Name = "port";
            this.port.Size = new System.Drawing.Size(41, 20);
            this.port.TabIndex = 12;
            this.port.Text = "502";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(191, 29);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(26, 13);
            this.label2.TabIndex = 11;
            this.label2.Text = "Port";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(41, 29);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "IP Address";
            // 
            // ipadd
            // 
            this.ipadd.Location = new System.Drawing.Point(44, 45);
            this.ipadd.Name = "ipadd";
            this.ipadd.Size = new System.Drawing.Size(123, 20);
            this.ipadd.TabIndex = 9;
            this.ipadd.Text = "127.0.0.1";
            // 
            // richedit
            // 
            this.richedit.Location = new System.Drawing.Point(44, 84);
            this.richedit.Name = "richedit";
            this.richedit.Size = new System.Drawing.Size(603, 285);
            this.richedit.TabIndex = 8;
            this.richedit.Text = "";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(44, 388);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 7;
            this.button1.Text = "Search";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click_1);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(142, 388);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 14;
            this.button2.Text = "Display";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // lblwait
            // 
            this.lblwait.AutoSize = true;
            this.lblwait.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblwait.Location = new System.Drawing.Point(288, 217);
            this.lblwait.Name = "lblwait";
            this.lblwait.Size = new System.Drawing.Size(107, 25);
            this.lblwait.TabIndex = 15;
            this.lblwait.Text = "Loading...";
            this.lblwait.Visible = false;
            // 
            // num
            // 
            this.num.Location = new System.Drawing.Point(416, 45);
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
            this.num.Size = new System.Drawing.Size(120, 20);
            this.num.TabIndex = 16;
            this.num.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(413, 29);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(43, 13);
            this.label3.TabIndex = 17;
            this.label3.Text = "Amount";
            // 
            // startreg
            // 
            this.startreg.Location = new System.Drawing.Point(259, 45);
            this.startreg.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.startreg.Name = "startreg";
            this.startreg.Size = new System.Drawing.Size(120, 20);
            this.startreg.TabIndex = 18;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(256, 29);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 13);
            this.label4.TabIndex = 19;
            this.label4.Text = "Starting Register";
            // 
            // overflow
            // 
            this.overflow.AutoSize = true;
            this.overflow.Location = new System.Drawing.Point(674, 135);
            this.overflow.Name = "overflow";
            this.overflow.Size = new System.Drawing.Size(68, 17);
            this.overflow.TabIndex = 20;
            this.overflow.TabStop = true;
            this.overflow.Text = "Extremes";
            this.overflow.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(686, 87);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(37, 13);
            this.label5.TabIndex = 22;
            this.label5.Text = "Ignore";
            // 
            // radioButton1
            // 
            this.radioButton1.AutoSize = true;
            this.radioButton1.Checked = true;
            this.radioButton1.Location = new System.Drawing.Point(674, 112);
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new System.Drawing.Size(62, 17);
            this.radioButton1.TabIndex = 23;
            this.radioButton1.TabStop = true;
            this.radioButton1.Text = "Nothing";
            this.radioButton1.UseVisualStyleBackColor = true;
            this.radioButton1.CheckedChanged += new System.EventHandler(this.radioButton1_CheckedChanged);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.radioButton1);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.overflow);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.startreg);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.num);
            this.Controls.Add(this.lblwait);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.save);
            this.Controls.Add(this.port);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ipadd);
            this.Controls.Add(this.richedit);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "Modbus Scraper";
            ((System.ComponentModel.ISupportInitialize)(this.num)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.startreg)).EndInit();
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
        private System.Windows.Forms.Label lblwait;
        private System.Windows.Forms.NumericUpDown num;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown startreg;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.RadioButton overflow;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.RadioButton radioButton1;
    }
}

