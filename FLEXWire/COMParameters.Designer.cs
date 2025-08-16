
namespace FLEXWire
{
    partial class COMParameters
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
            this.groupBoxCOMPort = new System.Windows.Forms.GroupBox();
            this.labelCOMPort = new System.Windows.Forms.Label();
            this.comboBoxCOMPorts = new System.Windows.Forms.ComboBox();
            this.labelBaudRate = new System.Windows.Forms.Label();
            this.comboBoxBaudRate = new System.Windows.Forms.ComboBox();
            this.buttonOK = new System.Windows.Forms.Button();
            this.groupBoxCOMPort.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxCOMPort
            // 
            this.groupBoxCOMPort.Controls.Add(this.labelCOMPort);
            this.groupBoxCOMPort.Controls.Add(this.comboBoxCOMPorts);
            this.groupBoxCOMPort.Controls.Add(this.labelBaudRate);
            this.groupBoxCOMPort.Controls.Add(this.comboBoxBaudRate);
            this.groupBoxCOMPort.Location = new System.Drawing.Point(12, 12);
            this.groupBoxCOMPort.Name = "groupBoxCOMPort";
            this.groupBoxCOMPort.Size = new System.Drawing.Size(200, 72);
            this.groupBoxCOMPort.TabIndex = 24;
            this.groupBoxCOMPort.TabStop = false;
            this.groupBoxCOMPort.Text = "COM Port Setup";
            // 
            // labelCOMPort
            // 
            this.labelCOMPort.AutoSize = true;
            this.labelCOMPort.Location = new System.Drawing.Point(50, 23);
            this.labelCOMPort.Name = "labelCOMPort";
            this.labelCOMPort.Size = new System.Drawing.Size(53, 13);
            this.labelCOMPort.TabIndex = 7;
            this.labelCOMPort.Text = "COM Port";
            // 
            // comboBoxCOMPorts
            // 
            this.comboBoxCOMPorts.DropDownHeight = 68;
            this.comboBoxCOMPorts.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCOMPorts.FormattingEnabled = true;
            this.comboBoxCOMPorts.IntegralHeight = false;
            this.comboBoxCOMPorts.Location = new System.Drawing.Point(109, 19);
            this.comboBoxCOMPorts.Name = "comboBoxCOMPorts";
            this.comboBoxCOMPorts.Size = new System.Drawing.Size(59, 21);
            this.comboBoxCOMPorts.TabIndex = 0;
            // 
            // labelBaudRate
            // 
            this.labelBaudRate.AutoSize = true;
            this.labelBaudRate.Location = new System.Drawing.Point(34, 47);
            this.labelBaudRate.Name = "labelBaudRate";
            this.labelBaudRate.Size = new System.Drawing.Size(58, 13);
            this.labelBaudRate.TabIndex = 17;
            this.labelBaudRate.Text = "Baud Rate";
            // 
            // comboBoxBaudRate
            // 
            this.comboBoxBaudRate.FormattingEnabled = true;
            this.comboBoxBaudRate.Items.AddRange(new object[] {
            "110",
            "300",
            "1200",
            "2400",
            "4800",
            "9600",
            "19200",
            "38400",
            "57600",
            "115200"});
            this.comboBoxBaudRate.Location = new System.Drawing.Point(109, 43);
            this.comboBoxBaudRate.Name = "comboBoxBaudRate";
            this.comboBoxBaudRate.Size = new System.Drawing.Size(59, 21);
            this.comboBoxBaudRate.TabIndex = 1;
            // 
            // buttonOK
            // 
            this.buttonOK.Location = new System.Drawing.Point(73, 98);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 25;
            this.buttonOK.Text = "&OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // COMParameters
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(220, 133);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.groupBoxCOMPort);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "COMParameters";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "COMParameters";
            this.Load += new System.EventHandler(this.COMParameters_Load);
            this.groupBoxCOMPort.ResumeLayout(false);
            this.groupBoxCOMPort.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxCOMPort;
        private System.Windows.Forms.Label labelCOMPort;
        private System.Windows.Forms.ComboBox comboBoxCOMPorts;
        private System.Windows.Forms.Label labelBaudRate;
        private System.Windows.Forms.ComboBox comboBoxBaudRate;
        private System.Windows.Forms.Button buttonOK;
    }
}