namespace miniEAP
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.cmbMultiplier = new System.Windows.Forms.ComboBox();
            this.lblMultiplier = new System.Windows.Forms.Label();
            this.txtSend = new System.Windows.Forms.TextBox();
            this.txtReceive = new System.Windows.Forms.TextBox();
            this.lblSend = new System.Windows.Forms.Label();
            this.lblReceive = new System.Windows.Forms.Label();
            this.pnlHeartbeat = new System.Windows.Forms.Panel();
            this.timerHeartbeat = new System.Windows.Forms.Timer(this.components);
            this.timerLogSync = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // cmbMultiplier
            // 
            this.cmbMultiplier.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMultiplier.FormattingEnabled = true;
            this.cmbMultiplier.Items.AddRange(new object[] {
            "1",
            "2"});
            this.cmbMultiplier.Location = new System.Drawing.Point(150, 12);
            this.cmbMultiplier.Name = "cmbMultiplier";
            this.cmbMultiplier.Size = new System.Drawing.Size(121, 20);
            this.cmbMultiplier.TabIndex = 0;
            this.cmbMultiplier.SelectedIndexChanged += new System.EventHandler(this.cmbMultiplier_SelectedIndexChanged);
            // 
            // lblMultiplier
            // 
            this.lblMultiplier.AutoSize = true;
            this.lblMultiplier.Location = new System.Drawing.Point(12, 15);
            this.lblMultiplier.Name = "lblMultiplier";
            this.lblMultiplier.Size = new System.Drawing.Size(101, 12);
            this.lblMultiplier.TabIndex = 1;
            this.lblMultiplier.Text = "曝光機一次跑幾片";
            // 
            // txtSend
            // 
            this.txtSend.Location = new System.Drawing.Point(12, 60);
            this.txtSend.Multiline = true;
            this.txtSend.Name = "txtSend";
            this.txtSend.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtSend.Size = new System.Drawing.Size(690, 178);
            this.txtSend.TabIndex = 2;
            // 
            // txtReceive
            // 
            this.txtReceive.Location = new System.Drawing.Point(12, 270);
            this.txtReceive.Multiline = true;
            this.txtReceive.Name = "txtReceive";
            this.txtReceive.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtReceive.Size = new System.Drawing.Size(690, 181);
            this.txtReceive.TabIndex = 3;
            // 
            // lblSend
            // 
            this.lblSend.AutoSize = true;
            this.lblSend.Location = new System.Drawing.Point(12, 45);
            this.lblSend.Name = "lblSend";
            this.lblSend.Size = new System.Drawing.Size(70, 12);
            this.lblSend.TabIndex = 4;
            this.lblSend.Text = "Send Message";
            // 
            // lblReceive
            // 
            this.lblReceive.AutoSize = true;
            this.lblReceive.Location = new System.Drawing.Point(12, 255);
            this.lblReceive.Name = "lblReceive";
            this.lblReceive.Size = new System.Drawing.Size(84, 12);
            this.lblReceive.TabIndex = 5;
            this.lblReceive.Text = "Receive Message";
            // 
            // pnlHeartbeat
            // 
            this.pnlHeartbeat.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlHeartbeat.BackColor = System.Drawing.Color.Gray;
            this.pnlHeartbeat.Location = new System.Drawing.Point(678, 12);
            this.pnlHeartbeat.Name = "pnlHeartbeat";
            this.pnlHeartbeat.Size = new System.Drawing.Size(20, 20);
            this.pnlHeartbeat.TabIndex = 6;
            // 
            // timerHeartbeat
            // 
            this.timerHeartbeat.Interval = 1000;
            this.timerHeartbeat.Tick += new System.EventHandler(this.timerHeartbeat_Tick);
            // 
            // timerLogSync
            // 
            this.timerLogSync.Interval = 300000;
            this.timerLogSync.Tick += new System.EventHandler(this.timerLogSync_Tick);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(710, 462);
            this.Controls.Add(this.pnlHeartbeat);
            this.Controls.Add(this.lblReceive);
            this.Controls.Add(this.lblSend);
            this.Controls.Add(this.txtReceive);
            this.Controls.Add(this.txtSend);
            this.Controls.Add(this.lblMultiplier);
            this.Controls.Add(this.cmbMultiplier);
            this.Name = "Form1";
            this.Text = "miniEAP";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.ComboBox cmbMultiplier;
        private System.Windows.Forms.Label lblMultiplier;
        private System.Windows.Forms.TextBox txtSend;
        private System.Windows.Forms.TextBox txtReceive;
        private System.Windows.Forms.Label lblSend;
        private System.Windows.Forms.Label lblReceive;
        private System.Windows.Forms.Panel pnlHeartbeat;
        private System.Windows.Forms.Timer timerHeartbeat;
        private System.Windows.Forms.Timer timerLogSync;
    }
}
