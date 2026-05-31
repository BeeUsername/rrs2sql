namespace RRStoSQL
{
	partial class MainForm
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
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.listBoxConfigs = new System.Windows.Forms.ListBox();
			this.panelButtons = new System.Windows.Forms.Panel();
			this.btnAddConfig = new System.Windows.Forms.Button();
			this.btnConvert = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.listViewSteps = new System.Windows.Forms.ListView();
			this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.groupBox1.SuspendLayout();
			this.panelButtons.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.SuspendLayout();
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.listBoxConfigs);
			this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
			this.groupBox1.Location = new System.Drawing.Point(0, 0);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(301, 106);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Конфиги:";
			this.groupBox1.Enter += new System.EventHandler(this.groupBox1_Enter);
			// 
			// listBoxConfigs
			// 
			this.listBoxConfigs.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listBoxConfigs.FormattingEnabled = true;
			this.listBoxConfigs.HorizontalScrollbar = true;
			this.listBoxConfigs.Location = new System.Drawing.Point(3, 16);
			this.listBoxConfigs.Name = "listBoxConfigs";
			this.listBoxConfigs.Size = new System.Drawing.Size(295, 87);
			this.listBoxConfigs.TabIndex = 0;
			this.listBoxConfigs.SelectedIndexChanged += new System.EventHandler(this.listBoxConfigs_SelectedIndexChanged);
			// 
			// panelButtons
			// 
			this.panelButtons.Controls.Add(this.btnAddConfig);
			this.panelButtons.Controls.Add(this.btnConvert);
			this.panelButtons.Dock = System.Windows.Forms.DockStyle.Top;
			this.panelButtons.Location = new System.Drawing.Point(0, 106);
			this.panelButtons.Name = "panelButtons";
			this.panelButtons.Size = new System.Drawing.Size(301, 52);
			this.panelButtons.TabIndex = 1;
			this.panelButtons.Paint += new System.Windows.Forms.PaintEventHandler(this.panel1_Paint_1);
			// 
			// btnAddConfig
			// 
			this.btnAddConfig.Location = new System.Drawing.Point(6, 3);
			this.btnAddConfig.Name = "btnAddConfig";
			this.btnAddConfig.Size = new System.Drawing.Size(84, 46);
			this.btnAddConfig.TabIndex = 1;
			this.btnAddConfig.Text = "Добавить конфиг";
			this.btnAddConfig.UseVisualStyleBackColor = true;
			this.btnAddConfig.Click += new System.EventHandler(this.btnAddConfig_Click);
			// 
			// btnConvert
			// 
			this.btnConvert.Location = new System.Drawing.Point(93, 3);
			this.btnConvert.Name = "btnConvert";
			this.btnConvert.Size = new System.Drawing.Size(202, 46);
			this.btnConvert.TabIndex = 0;
			this.btnConvert.Text = "Конвертировать";
			this.btnConvert.UseVisualStyleBackColor = true;
			this.btnConvert.Click += new System.EventHandler(this.btnConvert_Click);
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.listViewSteps);
			this.groupBox2.Location = new System.Drawing.Point(6, 161);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(292, 286);
			this.groupBox2.TabIndex = 2;
			this.groupBox2.TabStop = false;
			this.groupBox2.Enter += new System.EventHandler(this.groupBox2_Enter);
			// 
			// listViewSteps
			// 
			this.listViewSteps.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader2,
            this.columnHeader1});
			this.listViewSteps.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listViewSteps.FullRowSelect = true;
			this.listViewSteps.GridLines = true;
			this.listViewSteps.HideSelection = false;
			this.listViewSteps.Location = new System.Drawing.Point(3, 16);
			this.listViewSteps.Name = "listViewSteps";
			this.listViewSteps.Size = new System.Drawing.Size(286, 267);
			this.listViewSteps.TabIndex = 0;
			this.listViewSteps.UseCompatibleStateImageBehavior = false;
			this.listViewSteps.View = System.Windows.Forms.View.Details;
			this.listViewSteps.SelectedIndexChanged += new System.EventHandler(this.listViewSteps_SelectedIndexChanged);
			// 
			// columnHeader2
			// 
			this.columnHeader2.DisplayIndex = 1;
			this.columnHeader2.Text = "Статус";
			this.columnHeader2.Width = 65;
			// 
			// columnHeader1
			// 
			this.columnHeader1.DisplayIndex = 0;
			this.columnHeader1.Text = "Этап";
			this.columnHeader1.Width = 215;
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(301, 452);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.panelButtons);
			this.Controls.Add(this.groupBox1);
			this.Name = "MainForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "RRStoSQL";
			this.Load += new System.EventHandler(this.MainForm_Load);
			this.groupBox1.ResumeLayout(false);
			this.panelButtons.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.ListBox listBoxConfigs;
		private System.Windows.Forms.Panel panelButtons;
		private System.Windows.Forms.Button btnAddConfig;
		private System.Windows.Forms.Button btnConvert;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.ListView listViewSteps;
		private System.Windows.Forms.ColumnHeader columnHeader1;
		private System.Windows.Forms.ColumnHeader columnHeader2;
	}
}

