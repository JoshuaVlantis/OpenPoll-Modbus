namespace EasyBus_Modbus_Scanner
{
    partial class Home
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Home));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.connectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.connectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.modbusScraperToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.polltimer = new System.Windows.Forms.Timer(this.components);
            this.tPollCount = new System.Windows.Forms.Label();
            this.DataGrid = new System.Windows.Forms.DataGridView();
            this.DataGridNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridData = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.errorbox = new System.Windows.Forms.Label();
            this.GCcall = new System.Windows.Forms.Timer(this.components);
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.pTx = new System.Windows.Forms.PictureBox();
            this.bConnect = new System.Windows.Forms.ToolStripButton();
            this.bStop = new System.Windows.Forms.ToolStripButton();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DataGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pTx)).BeginInit();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.menuStrip1.AutoSize = false;
            this.menuStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.connectionToolStripMenuItem,
            this.setupToolStripMenuItem,
            this.modbusScraperToolStripMenuItem,
            this.toolStripMenuItem1});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(723, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // connectionToolStripMenuItem
            // 
            this.connectionToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.connectToolStripMenuItem});
            this.connectionToolStripMenuItem.Name = "connectionToolStripMenuItem";
            this.connectionToolStripMenuItem.Size = new System.Drawing.Size(81, 20);
            this.connectionToolStripMenuItem.Text = "Connection";
            // 
            // connectToolStripMenuItem
            // 
            this.connectToolStripMenuItem.Name = "connectToolStripMenuItem";
            this.connectToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            this.connectToolStripMenuItem.Text = "Connect...";
            this.connectToolStripMenuItem.Click += new System.EventHandler(this.connectToolStripMenuItem_Click);
            // 
            // setupToolStripMenuItem
            // 
            this.setupToolStripMenuItem.Name = "setupToolStripMenuItem";
            this.setupToolStripMenuItem.Size = new System.Drawing.Size(49, 20);
            this.setupToolStripMenuItem.Text = "Setup";
            this.setupToolStripMenuItem.Click += new System.EventHandler(this.setupToolStripMenuItem_Click);
            // 
            // modbusScraperToolStripMenuItem
            // 
            this.modbusScraperToolStripMenuItem.Name = "modbusScraperToolStripMenuItem";
            this.modbusScraperToolStripMenuItem.Size = new System.Drawing.Size(105, 20);
            this.modbusScraperToolStripMenuItem.Text = "Modbus Scraper";
            this.modbusScraperToolStripMenuItem.Click += new System.EventHandler(this.modbusScraperToolStripMenuItem_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.toolStrip1.AutoSize = false;
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bConnect,
            this.bStop});
            this.toolStrip1.Location = new System.Drawing.Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(498, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // polltimer
            // 
            this.polltimer.Interval = 1000;
            this.polltimer.Tick += new System.EventHandler(this.polltimer_Tick);
            // 
            // tPollCount
            // 
            this.tPollCount.AutoSize = true;
            this.tPollCount.BackColor = System.Drawing.SystemColors.Window;
            this.tPollCount.Location = new System.Drawing.Point(201, 52);
            this.tPollCount.Name = "tPollCount";
            this.tPollCount.Size = new System.Drawing.Size(30, 13);
            this.tPollCount.TabIndex = 7;
            this.tPollCount.Text = "Poll :";
            // 
            // DataGrid
            // 
            this.DataGrid.AllowUserToAddRows = false;
            this.DataGrid.AllowUserToDeleteRows = false;
            this.DataGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DataGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.DataGrid.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            this.DataGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DataGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.DataGridNumber,
            this.DataGridName,
            this.DataGridData});
            this.DataGrid.Location = new System.Drawing.Point(-41, 68);
            this.DataGrid.Name = "DataGrid";
            this.DataGrid.Size = new System.Drawing.Size(514, 383);
            this.DataGrid.TabIndex = 1;
            this.DataGrid.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGrid_CellContentDoubleClick);
            this.DataGrid.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGrid_CellEndEdit);
            this.DataGrid.SelectionChanged += new System.EventHandler(this.DataGrid_SelectionChanged);
            this.DataGrid.MouseClick += new System.Windows.Forms.MouseEventHandler(this.DataGrid_MouseClick);
            // 
            // DataGridNumber
            // 
            this.DataGridNumber.HeaderText = "𝒇𝒙";
            this.DataGridNumber.Name = "DataGridNumber";
            this.DataGridNumber.ReadOnly = true;
            this.DataGridNumber.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.DataGridNumber.Width = 25;
            // 
            // DataGridName
            // 
            this.DataGridName.HeaderText = "Register";
            this.DataGridName.Name = "DataGridName";
            this.DataGridName.ReadOnly = true;
            this.DataGridName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.DataGridName.Width = 52;
            // 
            // DataGridData
            // 
            this.DataGridData.HeaderText = "Data";
            this.DataGridData.Name = "DataGridData";
            this.DataGridData.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.DataGridData.Width = 36;
            // 
            // errorbox
            // 
            this.errorbox.AutoSize = true;
            this.errorbox.Location = new System.Drawing.Point(2, 52);
            this.errorbox.Name = "errorbox";
            this.errorbox.Size = new System.Drawing.Size(33, 13);
            this.errorbox.TabIndex = 9;
            this.errorbox.Text = "Label";
            // 
            // GCcall
            // 
            this.GCcall.Enabled = true;
            this.GCcall.Interval = 5000;
            this.GCcall.Tick += new System.EventHandler(this.GCcall_Tick);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(22, 20);
            this.toolStripMenuItem1.Text = " ";
            // 
            // pTx
            // 
            this.pTx.Image = global::EasyBus_Modbus_Scanner.Properties.Resources.Green;
            this.pTx.Location = new System.Drawing.Point(187, 54);
            this.pTx.Name = "pTx";
            this.pTx.Size = new System.Drawing.Size(12, 10);
            this.pTx.TabIndex = 8;
            this.pTx.TabStop = false;
            // 
            // bConnect
            // 
            this.bConnect.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.bConnect.Image = ((System.Drawing.Image)(resources.GetObject("bConnect.Image")));
            this.bConnect.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bConnect.Name = "bConnect";
            this.bConnect.Size = new System.Drawing.Size(23, 22);
            this.bConnect.Text = "Connect";
            this.bConnect.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // bStop
            // 
            this.bStop.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.bStop.Image = ((System.Drawing.Image)(resources.GetObject("bStop.Image")));
            this.bStop.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bStop.Name = "bStop";
            this.bStop.Size = new System.Drawing.Size(23, 22);
            this.bStop.Text = "Disconnect";
            this.bStop.Click += new System.EventHandler(this.bStop_Click);
            // 
            // Home
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(473, 450);
            this.Controls.Add(this.errorbox);
            this.Controls.Add(this.pTx);
            this.Controls.Add(this.tPollCount);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.DataGrid);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Home";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "EasyBus-Modbus Scanner";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DataGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pTx)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem connectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem connectToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton bConnect;
        private System.Windows.Forms.ToolStripMenuItem modbusScraperToolStripMenuItem;
        private System.Windows.Forms.Timer polltimer;
        private System.Windows.Forms.ToolStripButton bStop;
        private System.Windows.Forms.Label tPollCount;
        private System.Windows.Forms.PictureBox pTx;
        private System.Windows.Forms.DataGridView DataGrid;
        private System.Windows.Forms.Label errorbox;
        private System.Windows.Forms.Timer GCcall;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridName;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridData;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
    }
}

