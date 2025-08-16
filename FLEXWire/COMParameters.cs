using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO.Ports;
using System.IO;

namespace FLEXWire
{
    public partial class COMParameters : Form
    {
        public string selectedPort = "";
        public string selectedRate = "";

        public string thisPortName = "";

        public COMParameters()
        {
            InitializeComponent();
        }

        private bool ComPortIsAvailable(string portName)
        {
            bool isAvaiable = false;

            try
            {
                using (SerialPort port = new SerialPort(portName))
                {
                    port.Open();
                    isAvaiable = true;
                    port.Close();
                }
            }
            catch (IOException ex)
            {
                isAvaiable = false;
                MessageBox.Show($"COM{Program.mainForm.listPorts[0].port.ToString()}");
            }
            catch (UnauthorizedAccessException ex)
            {
                isAvaiable = false;
                // MessageBox.Show($"Access to COM Port {portName} is unauthorized: {ex.Message}");
            }
            catch (Exception ex)
            {
                isAvaiable = false;
                // MessageBox.Show($"An error occurred: {ex.Message}");
            }

            return isAvaiable;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            selectedPort = comboBoxCOMPorts.Text;
            selectedRate = comboBoxBaudRate.Text;

            DialogResult = DialogResult.OK;
        }

        private void COMParameters_Load(object sender, EventArgs e)
        {
            List<string> sortedPortNames = new List<string>();

            // get the available com ports loaded into the list box comboBoxCOMPorts

            thisPortName = $"COM{Program.mainForm.listPorts[0].port.ToString()}";

            string[] portNames = SerialPort.GetPortNames().OrderBy(port => port).ToArray();
            foreach (string name in portNames)
            {
                if (name.Length == 4)
                {
                    if (!sortedPortNames.Contains(name))
                        sortedPortNames.Add(name);
                }
            }
            foreach (string name in portNames)
            {
                if (name.Length == 5)
                {
                    if (!sortedPortNames.Contains(name))
                        sortedPortNames.Add(name);
                }
            }

            comboBoxCOMPorts.Items.Clear();
            foreach (string portName in sortedPortNames)
            {
                if (ComPortIsAvailable(portName) || portName == thisPortName)
                    comboBoxCOMPorts.Items.Add(portName);
            }

            comboBoxCOMPorts.SelectedItem = thisPortName;

            comboBoxCOMPorts.SelectedText = selectedPort;
            comboBoxBaudRate.SelectedText = selectedRate;
        }
    }
}
