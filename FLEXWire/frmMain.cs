using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

using System.IO;
using System.Net;

using System.Globalization;
using System.Reflection;
using System.Xml;
using System.IO.Ports;
using System.Net.Sockets;

using System.Threading;

#region enums
enum CONSOLE_ATTRIBUTE
{
    REVERSE_ATTRIBUTE = 0,
    NORMAL_ATTRIBUTE
};

enum SECTOR_ACCESS_MODE
{
    S_MODE = 0,
    R_MODE
};

//             STATUS REGISTER SUMMARY (WD2797)
// 
// ALL     TYPE I      READ        READ        READ        WRITE       WRITE
// BIT     COMMANDS    ADDRESS     SECTOR      TRACK       SECTOR      TRACK
// ----------------------------------------------------------------------------------
// S7      NOT READY   NOT READY   NOT READY   NOT READY   NOT READY   NOT READY
// S6      WRITE       0           0           0           WRITE       WRITE
//         PROTECT                                         PROTECT     PROTECT
// S5      HEAD LOADED 0           RECORD TYPE 0           0           0
// S4      SEEK ERROR  RNF         RNF         0           RNF         0
// S3      CRC ERROR   CRC ERROR   CRC ERROR   0           CRC ERROR   0
// S2      TRACK 0     LOST DATA   LOST DATA   LOST DATA   LOST DATA   LOST DATA
// S1      INDEX PULSE DRO         DRO         DRO         DRO         DRO
// SO      BUSY        BUSY        BUSY        BUSY        BUSY        BUSY        
// ----------------------------------------------------------------------------------
enum CONNECTION_STATE
{
    NOT_CONNECTED = -1,
    SYNCRONIZING,
    CONNECTED,
    GET_REQUESTED_MOUNT_DRIVE,
    GET_READ_DRIVE,
    GET_WRITE_DRIVE,
    GET_MOUNT_DRIVE,
    GET_CREATE_DRIVE,
    GET_TRACK,
    GET_SECTOR,
    RECEIVING_SECTOR,
    GET_CRC,
    MOUNT_GETFILENAME,
    DELETE_GETFILENAME,
    DIR_GETFILENAME,
    CD_GETFILENAME,
    DRIVE_GETFILENAME,
    SENDING_DIR,
    CREATE_GETPARAMETERS,
    WAIT_ACK,
    PROCESSING_MOUNT,
    PROCESSING_DIR,
    PROCESSING_LIST
};

enum CREATE_STATE
{
    GET_CREATE_PATH = 0,
    GET_CREATE_NAME,
    GET_CREATE_VOLUME,
    GET_CREATE_TRACK_COUNT,
    GET_CREATE_SECTOR_COUNT,
    CREATE_THE_IMAGE
};
#endregion

namespace FLEXWire
{
    public partial class frmMain : Form
    {
        #region variables
        const byte FDC_BUSY = 0x01;
        const byte FDC_DRQ = 0x02;
        const byte FDC_TRKZ = 0x04;
        const byte FDC_CRCERR = 0x08;
        const byte FDC_SEEKERR = 0x10;
        const byte FDC_RNF = 0x10;
        const byte FDC_HDLOADED = 0x20;
        const byte FDC_WRTPROTECT = 0x40;
        const byte FDC_NOTREADY = 0x80;

        TcpListener listener;
        Thread rs232Thread = null;
        Thread listenerThread = null;

        // set default to not log - use the config file to change this (T, t, Y, y, 1 will change it)
        bool logReads = false;
        bool logWrites = false;

        string logfileName = "ReadWrite.log";
        StreamWriter logFileStream = null;

        bool shutDown;
        bool done = false;

        TcpIpSocketInfo tcpIpSocketInfo = null;
        IPAddress localAddr = IPAddress.Any;                // Listen on all network interfaces
        bool usingTCPIP = false;

        public List<Port> listPorts = new List<Port>();
        //ArrayList ports = new ArrayList();
        bool usingPorts = false;

        CultureInfo ci = new CultureInfo("en-us");

        public Version version = new Version();
        public string applicationPath;


        // there is only one IDE interface, with 2 possible SD Cards adapters on that interface.
        // Each SDCard adapter can only have 1 drive attached (SDCard inserted)
        public List<TcpIpSDCardAdapter> mf_SDCardDrives = new List<TcpIpSDCardAdapter>();

        public List<PortFloppyTabPage> mf_portTabPages      = new List<PortFloppyTabPage>();
        public List<TcpIpFloppyDrive>  mf_tcpipFloppyDrives = new List<TcpIpFloppyDrive>();

        public RichTextBox mf_outputWindow = new RichTextBox();
        #endregion
        #region output Window handlers
        public void WriteLineToOutputWindow(string message = null, bool scrollToCaret = false)
        {
            try
            {
                if (message != null)
                {
                    if (outputWindow.InvokeRequired)
                        outputWindow.Invoke(new MethodInvoker(() => outputWindow.AppendText($"{message}\n")));
                    else
                        outputWindow.AppendText($"{message}\n");
                }

                if (scrollToCaret)
                {
                    if (outputWindow.InvokeRequired)
                        outputWindow.Invoke(new MethodInvoker(() => outputWindow.ScrollToCaret()));
                    else
                        outputWindow.ScrollToCaret();
                }
            }
            catch (Exception e)
            {
                string errorMessage = e.Message;
            }
        }

        public void WriteToOutputWindow(string message)
        {
            if (outputWindow.InvokeRequired)
                outputWindow.Invoke(new MethodInvoker(() => outputWindow.AppendText($"{message}\n")));
            else
                outputWindow.AppendText($"{message}\n");

            if (outputWindow.InvokeRequired)
                outputWindow.Invoke(new MethodInvoker(() => outputWindow.ScrollToCaret()));
            else
                outputWindow.ScrollToCaret();
        }
        #endregion
        #region setup functions
        void ParsePortsFloppiesFromConfigFile(string filename)
        {
            // this will only update a port's floppy images if the port existed when the program first loaded.
            // it will not add additional ports from the configuration file.

            string ApplicationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            XmlDocument doc = new XmlDocument();
            doc.Load(filename);

            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                if (node.Name == "Ports")
                {
                    foreach (XmlNode portNode in node.ChildNodes)
                    {
                        Port serialPort = new Port();
                        serialPort.defaultStartDirectory = Directory.GetCurrentDirectory();
                        serialPort.port = Convert.ToInt32(portNode.Attributes["num"].Value);
                        foreach (XmlNode paramters in portNode.ChildNodes)
                        {
                            if (paramters.Name == "ImageFiles")
                            {
                                // first get rid of the exiting mappings
                                for (int i = 0; i < serialPort.floppyImageFiles.Length; i++)
                                {
                                    // if the file is open - close it
                                    if (serialPort.floppyImageFiles[i] != null)
                                        serialPort.floppyImageFiles[i].stream.Close();

                                    serialPort.floppyImageFiles[i] = new ImageFile();
                                    serialPort.floppyImageFiles[i].Name = "";
                                    serialPort.floppyImageFiles[i].driveNumber = i;
                                }

                                // now add the ones from the config file
                                int index = 0;
                                foreach (XmlNode imageFile in paramters.ChildNodes)
                                {
                                    if (imageFile.Name == "ImageFile")
                                    {
                                        serialPort.floppyImageFiles[index] = new ImageFile();
                                        serialPort.floppyImageFiles[index].Name = imageFile.InnerText;
                                        serialPort.floppyImageFiles[index].driveNumber = index;
                                        index++;
                                    }
                                }
                            }
                        }
                        serialPort.currentWorkingDirectory = serialPort.defaultStartDirectory;

                        for (int i = 0; i < listPorts.Count; i++)
                        {
                            // we already have this port in the list - just update it

                            if (listPorts[i].port == serialPort.port)
                            {
                                int imageFileIndex = 0;
                                foreach (ImageFile imageFile in serialPort.floppyImageFiles)
                                {
                                    try
                                    {
                                        if (imageFile.stream != null)
                                        {
                                            listPorts[i].floppyImageFiles[imageFileIndex].Name = imageFile.Name;
                                            imageFileIndex++;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
        }

        void ParseConfigFile(string filename)
        {
            applicationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            XmlDocument doc = new XmlDocument();
            doc.Load(filename);

            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                if (node.Name == "TCPIP")
                {
                    tcpIpSocketInfo = new TcpIpSocketInfo();
                    foreach (XmlNode paramters in node.ChildNodes)
                    {
                        if (paramters.Name == "UseTCPIP")
                        {
                            if (paramters.InnerText[0] == 'Y' || paramters.InnerText[0] == 'T' || paramters.InnerText[0] == 'y' || paramters.InnerText[0] == 't' || paramters.InnerText[0] == '1')
                            {
                                tcpIpSocketInfo.enabled = true;
                                usingTCPIP = true;
                            }
                            else
                                tcpIpSocketInfo.enabled = false;
                        }
                        else if (paramters.Name == "SERVER_PORT")
                        {
                            bool success = Int32.TryParse(paramters.InnerText, out tcpIpSocketInfo.serverPort);
                            if (!success)
                            {
                                tcpIpSocketInfo.enabled = false;
                                usingTCPIP = false;
                                WriteLineToOutputWindow("Invalid port specified in config file for TCP IP port");
                            }
                            else
                            {
                                textBoxServerPort.Text = paramters.InnerText;
                                usingTCPIP = true;
                            }
                        }
                        else if (paramters.Name == "DefaultDirectory")
                        {
                            tcpIpSocketInfo.localPort.defaultStartDirectory = paramters.InnerText;
                            groupBoxTCPIP.Text = $"TCP IP Connection - {tcpIpSocketInfo.localPort.defaultStartDirectory}";
                        }
                        else if (paramters.Name == "Floppy")
                        {
                            foreach (XmlNode floppyNode in paramters.ChildNodes)
                            {
                                if (floppyNode.Name == "Verbose")
                                {
                                    tcpIpSocketInfo.localPort.FloppyVerbose = floppyNode.InnerText;
                                }
                                else if (floppyNode.Name == "ImageFiles")
                                {
                                    int index = 0;
                                    foreach (XmlNode imageFile in floppyNode.ChildNodes)
                                    {
                                        if (imageFile.Name == "ImageFile")
                                        {
                                            tcpIpSocketInfo.localPort.floppyImageFiles[index] = new ImageFile();
                                            tcpIpSocketInfo.localPort.floppyImageFiles[index].Name = imageFile.InnerText;
                                            index++;
                                        }
                                    }
                                }
                            }
                        }
                        else if (paramters.Name == "SDCard")
                        {
                            foreach (XmlNode sdcardNode in paramters.ChildNodes)
                            {
                                if (sdcardNode.Name == "Verbose")
                                {
                                    tcpIpSocketInfo.localPort.SDCardVerbose = sdcardNode.InnerText;
                                }
                                else if (sdcardNode.Name == "ImageFiles")
                                {
                                    int index = 0;
                                    foreach (XmlNode imageFile in sdcardNode.ChildNodes)
                                    {
                                        if (imageFile.Name == "ImageFile")
                                        {
                                            tcpIpSocketInfo.localPort.sdCardImageFiles[index] = new ImageFile();
                                            tcpIpSocketInfo.localPort.sdCardImageFiles[index].Name = imageFile.InnerText;
                                            index++;
                                        }
                                    }
                                }
                            }
                        }

                        tcpIpSocketInfo.localPort.currentWorkingDirectory = tcpIpSocketInfo.localPort.defaultStartDirectory;
                    }
                }
                else if (node.Name == "Ports")
                {
                    foreach (XmlNode portNode in node.ChildNodes)
                    {
                        Port serialPort = new Port();
                        serialPort.defaultStartDirectory = Directory.GetCurrentDirectory();
                        serialPort.port = Convert.ToInt32(portNode.Attributes["num"].Value);
                        foreach (XmlNode paramters in portNode.ChildNodes)
                        {
                            if (paramters.Name == "Rate")
                            {
                                serialPort.rate = Convert.ToInt32(paramters.InnerText);
                            }
                            else if (paramters.Name == "CpuSpeed")
                            {
                                serialPort.speed = paramters.InnerText;
                            }
                            else if (paramters.Name == "Verbose")
                            {
                                serialPort.verbose = paramters.InnerText;
                            }
                            else if (paramters.Name == "AutoMount")
                            {
                                serialPort.autoMount = paramters.InnerText;
                            }
                            else if (paramters.Name == "DefaultDirectory")
                            {
                                serialPort.defaultStartDirectory = paramters.InnerText;
                                groupBoxRS232Ports.Text = $"RS232 Ports - {serialPort.defaultStartDirectory }";
                            }
                            else if (paramters.Name == "ImageFiles")
                            {
                                int index = 0;
                                foreach (XmlNode imageFile in paramters.ChildNodes)
                                {
                                    if (imageFile.Name == "ImageFile")
                                    {
                                        serialPort.floppyImageFiles[index] = new ImageFile();
                                        serialPort.floppyImageFiles[index].Name = imageFile.InnerText;
                                        serialPort.floppyImageFiles[index].driveNumber = index;
                                        index++;
                                    }
                                }
                            }
                        }
                        serialPort.currentWorkingDirectory = serialPort.defaultStartDirectory;

                        bool portAlreadyExists = false;

                        for (int i = 0; i < listPorts.Count; i++)
                        {
                            // we already have this port in the list - just update it

                            if (listPorts[i].port == serialPort.port)
                            {
                                portAlreadyExists = true;

                                listPorts[i].defaultStartDirectory = serialPort.defaultStartDirectory;
                                listPorts[i].rate = serialPort.rate;
                                listPorts[i].speed = serialPort.speed;
                                listPorts[i].verbose = serialPort.verbose;
                                listPorts[i].autoMount = serialPort.autoMount;

                                int imageFileIndex = 0;
                                foreach (ImageFile imageFile in serialPort.floppyImageFiles)
                                {
                                    try
                                    {
                                        if (imageFile != null)
                                        {
                                            listPorts[i].floppyImageFiles[imageFileIndex++].Name = imageFile.Name;
                                            usingPorts = true;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (!portAlreadyExists)
                            listPorts.Add(serialPort);
                    }
                }
                else if (node.Name == "Shutdown")
                {
                    if (node.InnerText.StartsWith("T") || node.InnerText.StartsWith("t") || node.InnerText.StartsWith("Y") || node.InnerText.StartsWith("y") || node.InnerText.StartsWith("1"))
                    {
                        shutDown = true;
                        checkBoxShutdown.Checked = true;
                    }
                    else
                    {
                        shutDown = false;
                        checkBoxShutdown.Checked = false;
                    }
                }
                else if (node.Name == "LogReads")
                {
                    if (node.InnerText.StartsWith("T") || node.InnerText.StartsWith("t") || node.InnerText.StartsWith("Y") || node.InnerText.StartsWith("y") || node.InnerText.StartsWith("1")) ;
                    checkBoxLogReads.Checked = true;
                    logReads = true;
                }
                else if (node.Name == "LogWrites")
                {
                    if (node.InnerText.StartsWith("T") || node.InnerText.StartsWith("t") || node.InnerText.StartsWith("Y") || node.InnerText.StartsWith("y") || node.InnerText.StartsWith("1")) ;
                    checkBoxLogWrites.Checked = true;
                    logWrites = true;
                }
                else if (node.Name == "LogFilename")
                {
                    textBoxLogFilename.Text = node.InnerText;
                }
                else if (node.Name == "ConfigurationFilename")
                {
                    if (node.InnerText.Length > 0)
                        textBoxConfigurationFilename.Text = node.InnerText;
                    else
                        textBoxConfigurationFilename.Text = "FNCONFIG.xml";
                }
            }
        }

        void InitializeFromConfigFile(string filename)
        {
            int listPortIndex = 0;      // this will be used to set the tabPageIndex in the serial port class

            WriteLineToOutputWindow("FLEXWire version 1.0:1");

            // save our current working directory so we can default to the execution directory for config file save if need be..
            string currentWorkingDirectory = Directory.GetCurrentDirectory();
            string applicationPathDirectory = Application.ExecutablePath.Replace($"{Application.ProductName}.exe", "").Trim('\\');

            Directory.SetCurrentDirectory(applicationPathDirectory);
            ParseConfigFile(filename);
            Directory.SetCurrentDirectory(currentWorkingDirectory);

            if (listPorts.Count > 0)
            {
                int nIndex = 0;
                foreach (Port serialPort in listPorts)
                {
                    if (serialPort.sp != null)
                    {
                        // if we come in here with a non-null value for serialPort.sp that means that we have a COM port open for this logical serial port
                        // we must close it before we can open it again or open another.

                        serialPort.sp.Close();
                        serialPort.sp = null;
                    }

                    if (serialPort.sp == null)
                        serialPort.sp = new SerialPort("COM" + serialPort.port.ToString(), serialPort.rate, Parity.None, 8, StopBits.One);

                    serialPort.sp.ReadBufferSize = 32768;
                    serialPort.sp.WriteBufferSize = 32768;
                    try
                    {
                        serialPort.sp.Open();
                    }
                    catch (Exception e)
                    {
                        WriteLineToOutputWindow(e.Message);
                        try
                        {
                            serialPort.sp.Close();
                            serialPort.sp.Open();
                        }
                        catch (Exception e1)
                        {
                            WriteLineToOutputWindow(e1.Message);
                        }
                    }

                    if (serialPort.sp.IsOpen)
                    {
                        serialPort.tabPageIndex = listPortIndex++;
                        serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);

                        WriteLineToOutputWindow(string.Format("COM{0} parameters:", serialPort.port));
                        WriteLineToOutputWindow(string.Format("    Rate:              {0}", serialPort.rate));
                        WriteLineToOutputWindow(string.Format("    CpuSpeed:          {0}", serialPort.speed));
                        WriteLineToOutputWindow(string.Format("    Verbose:           {0}", serialPort.verbose));
                        WriteLineToOutputWindow(string.Format("    AutoMount:         {0}", serialPort.autoMount));
                        WriteLineToOutputWindow(string.Format("    DefaultDirectory   {0}", serialPort.defaultStartDirectory));
                        WriteLineToOutputWindow(string.Format("    ImageFiles"));
                        for (int imageFileIndex = 0; imageFileIndex < serialPort.floppyImageFiles.Length; imageFileIndex++)
                        {
                            if (serialPort.floppyImageFiles[imageFileIndex] != null)
                            {
                                WriteLineToOutputWindow(string.Format("        {0} - {1}", imageFileIndex, serialPort.floppyImageFiles[imageFileIndex].Name));
                            }
                        }
                        WriteLineToOutputWindow(string.Format("    Current Working Directory: {0}", serialPort.currentWorkingDirectory));

                        serialPort.sp.DtrEnable = true;
                        serialPort.sp.RtsEnable = true;

                        // set verbose checkbox
                        mf_portTabPages[serialPort.tabPageIndex].verboseCheckBox.Checked = serialPort.verbose == "V" ? true : false;

                        // now populate the tab pages from the config file and name the tabs

                        foreach (ImageFile imageFile in serialPort.floppyImageFiles)
                        {
                            if (imageFile != null)
                            {
                                // get the tab page name
                                mf_portTabPages[serialPort.tabPageIndex].tabPage.Text = $"COM{serialPort.port.ToString()}";

                                if (imageFile.Name.Length > 0)
                                {
                                    // if there are any floppies assigned to any port - we are using RS232
                                    usingPorts = true;

                                    serialPort.MountImageFile(imageFile.Name + ".DSK", nIndex, listPortIndex);

                                    mf_portTabPages[serialPort.tabPageIndex].textBoxFloppyDrives[imageFile.driveNumber].Text = imageFile.Name;
                                    if (imageFile.readOnly)
                                        mf_portTabPages[serialPort.tabPageIndex].pictureBoxFloppyDrives[imageFile.driveNumber].Image = Properties.Resources.reddot;
                                    else
                                        mf_portTabPages[serialPort.tabPageIndex].pictureBoxFloppyDrives[imageFile.driveNumber].Image = Properties.Resources.greendot;
                                }
                                nIndex++;
                            }
                        }
                    }
                    else
                    {
                        WriteLineToOutputWindow("Could not open RS232 COM" + serialPort.port.ToString());
                    }
                }

                int numberOfTabPages = tabControlPorts.TabPages.Count;
                for (int i = numberOfTabPages - 1; i >= 0; i--)
                {
                    if (tabControlPorts.TabPages[i].Text.StartsWith("tabPage"))
                        tabControlPorts.TabPages.Remove(tabControlPorts.TabPages[i]);
                }

                Application.DoEvents();
            }

            if (usingTCPIP)
            {
                if (tcpIpSocketInfo != null)
                {
                    int nIndex = 0;

                    checkboxFloppyVerbose.Checked = tcpIpSocketInfo.localPort.FloppyVerbose == "V" ? true : false;
                    checkboxSDCardVerbose.Checked = tcpIpSocketInfo.localPort.SDCardVerbose == "V" ? true : false;

                    WriteLineToOutputWindow($"Openning listening port: {tcpIpSocketInfo.serverPort}");
                    foreach (ImageFile imageFile in tcpIpSocketInfo.localPort.floppyImageFiles)
                    {
                        if (imageFile != null)
                        {
                            if (imageFile.Name.Length > 0)
                                tcpIpSocketInfo.localPort.MountFloppyImageFile(imageFile.Name + ".DSK", nIndex);
                            nIndex++;
                        }
                    }

                    nIndex = 0;
                    foreach (ImageFile imageFile in tcpIpSocketInfo.localPort.sdCardImageFiles)
                    {
                        if (imageFile != null)
                        {
                            if (imageFile.Name.Length > 0)
                            {
                                tcpIpSocketInfo.localPort.MountSDCardImageFile(imageFile.Name + ".DSK", nIndex);
                            }
                            nIndex++;
                        }
                    }
                }
            }
        }
        #endregion
        #region ConnectioStates
        void StateConnectionStateNotConnected(Port serialPort, int c)
        {
            WriteLineToOutputWindow($"received: 0x{c.ToString("X2")}");

            if (c == 0x55)
            {
                serialPort.WriteByte((byte)0x55);
                serialPort.SetState((int)CONNECTION_STATE.SYNCRONIZING);
            }
        }

        void StateConnectionStateSynchronizing(Port serialPort, int c)
        {
            WriteLineToOutputWindow($"received: 0x{c.ToString("X2")}");
            if (c != 0x55)
            {
                if (c == 0xAA)
                {
                    serialPort.WriteByte((byte)0xAA);
                    serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
                }
                else
                {
                    serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);
                }
            }
        }

        void StateConnectionStateConnected(Port serialPort, int c)
        {
            //  these commands are common between NETPC and FLEXNet
            //
            //  ? - Query Current Directory
            //  E - Exit
            //  Q - Quick Check for active connection
            //  A - Dir file command
            //  I - List Directories file command
            //  P - Change Directory
            //  V - Change Drive (and optionally the directory)
            //
            // these are for supporting the old NETPC 3.4 and 3.5 that only supported a single drive
            //
            //  S - 'S'end Sector Request - set activity LED to green - reading
            //  R - 'R'eceive Sector Request - set activity LED to red - writing
            //  M - Mount drive image
            //  D - Delete file command
            //  C - Create a drive image
            //
            //  these are for FLEXNet multi drive commands      -
            //
            //  s - 's'end Sector Request with drive
            //  r - 'r'eceive Sector Request with drive
            //  m - Mount drive image with drive
            //  d - Report which disk image is mounted to requested drive
            //  c - Create a drive image

            // whenever the state is set back to "CONNECTED", set the activity led to grey - no activity
            // mf_portTabPages[serialPort.tabPageIndex].pictureBoxActivityTabDrives[serialPort.currentDrive].Image = Properties.Resources.greydot;

            switch (c)
            {
                case '?':           // Query Current Directory
                    {
                        serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                        WriteToOutputWindow(serialPort.currentWorkingDirectory);
                        serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);
                        WriteToOutputWindow("\n");

                        serialPort.sp.Write(serialPort.currentWorkingDirectory);
                        serialPort.WriteByte((byte)0x0D, false);
                        serialPort.WriteByte((byte)0x06);
                    }
                    break;
                case 'S':           // 'S'end Sector Request - set activity LED to green - reading
                    {
                        //mf_portTabPages[serialPort.tabPageIndex].pictureBoxActivityTabDrives[serialPort.currentDrive].Image = Properties.Resources.greendot;
                        serialPort.floppyImageFiles[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.S_MODE;
                        serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
                    }
                    break;
                case 'R':           // 'R'eceive Sector Request - set activity LED to red - writing
                    {
                        //mf_portTabPages[serialPort.tabPageIndex].pictureBoxActivityTabDrives[serialPort.currentDrive].Image = Properties.Resources.reddot;
                        serialPort.floppyImageFiles[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.R_MODE;
                        serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
                    }
                    break;
                case 'E':           // Exit
                    {
                        serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);
                        serialPort.WriteByte((byte)0x06);
                        if (shutDown)
                            done = true;
                    }
                    break;
                case 'Q':           // Quick Check for active connection
                    {
                        serialPort.WriteByte((byte)0x06);
                    }
                    break;
                case 'M':           // Mount drive image
                    {
                        serialPort.commandFilename = "";
                        serialPort.SetState((int)CONNECTION_STATE.MOUNT_GETFILENAME);
                    }
                    break;
                case 'D':           // Delete file command
                    {
                        serialPort.commandFilename = "";
                        serialPort.SetState((int)CONNECTION_STATE.DELETE_GETFILENAME);
                    }
                    break;
                case 'A':           // Dir file command
                    {
                        serialPort.commandFilename = "";
                        serialPort.SetState((int)CONNECTION_STATE.DIR_GETFILENAME);
                    }
                    break;
                case 'I':           // List Directories file command
                    {
                        // create a local temporarY file to write the directory to before sending it to the serial port

                        serialPort.dirFilename = "dirtxt" + serialPort.port.ToString() + serialPort.currentDrive.ToString() + ".txt";

                        if (serialPort.streamDir != null)
                        {
                            serialPort.streamDir.Close();
                            serialPort.streamDir = null;
                            File.Delete(serialPort.dirFilename);
                        }
                        serialPort.streamDir = File.Open(serialPort.dirFilename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

                        // file is open - let's start writing to it.

                        long availableFreeSpace = 0L;
                        string driveName = "";
                        string volumeLabel = "";
                        byte[] volumeBuffer = new byte[0];

                        // start by getting the volume information on the connected PC drive if thsi is not a UNC path

                        if (!serialPort.currentWorkingDirectory.StartsWith(@"\\"))
                        {
                            System.IO.DriveInfo systemDriveInfo = new System.IO.DriveInfo(Directory.GetDirectoryRoot(serialPort.currentWorkingDirectory));

                            // Get the drive and volume information

                            availableFreeSpace = systemDriveInfo.AvailableFreeSpace;
                            driveName = systemDriveInfo.Name;
                            volumeLabel = systemDriveInfo.VolumeLabel;

                            volumeBuffer = Encoding.ASCII.GetBytes("\r\n Volume in Drive " + driveName + " is " + volumeLabel + "\r\n");
                            WriteLineToOutputWindow("\r\n Volume in Drive " + driveName + " is " + volumeLabel);
                        }
                        else
                        {
                            // if the diskette image is on a unc path - there will be no volume information available

                            volumeBuffer = Encoding.ASCII.GetBytes("\r\n Volume in Drive is a UNC Path\r\n");
                            WriteLineToOutputWindow("\r\n Volume in Drive is a UNC Path ");
                        }

                        // send volume info to the file we are going to send to the FLEX machine

                        serialPort.streamDir.Write(volumeBuffer, 0, volumeBuffer.Length);

                        // add the current working directory name to the output

                        byte[] wrkDirBuffer = Encoding.ASCII.GetBytes(serialPort.currentWorkingDirectory + "\r\n\r\n");
                        serialPort.streamDir.Write(wrkDirBuffer, 0, wrkDirBuffer.Length);
                        WriteLineToOutputWindow(serialPort.currentWorkingDirectory + "\r\n");

                        // get the list of directories in the current working directory

                        string[] files = Directory.GetDirectories(serialPort.currentWorkingDirectory);

                        int maxFilenameSize = 0;
                        foreach (string file in files)
                        {
                            if (file.Length > maxFilenameSize)
                                maxFilenameSize = file.Length;
                        }
                        maxFilenameSize = maxFilenameSize - serialPort.currentWorkingDirectory.Length;

                        int fileCount = 0;
                        foreach (string file in files)
                        {
                            FileInfo fi = new FileInfo(file);
                            DateTime fCreation = fi.CreationTime;

                            string fileInfoLine = file;
                            fileInfoLine = fileInfoLine.Replace(serialPort.currentWorkingDirectory + @"\", ""); // get rid of path info
                            fileInfoLine = fileInfoLine.PadRight(maxFilenameSize, ' ');                         // pad to proper length
                            fileInfoLine = fileInfoLine + "    <DIR>   " +
                                                          fCreation.Month.ToString("00") + "/" + fCreation.Day.ToString("00") + "/" + fCreation.Year.ToString("0000") +
                                                          " " +
                                                          fCreation.Hour.ToString("00") + ":" + fCreation.Minute.ToString("00") + ":" + fCreation.Second.ToString("00") +
                                                          "\r\n";
                            if (fileInfoLine.Length > 0)
                            {
                                fileCount += 1;
                                byte[] bArray = Encoding.ASCII.GetBytes(fileInfoLine);
                                serialPort.streamDir.Write(bArray, 0, bArray.Length);
                            }
                        }

                        byte[] fileCountBuffer = Encoding.ASCII.GetBytes("    " + fileCount.ToString() + " files\r\n");
                        serialPort.streamDir.Write(fileCountBuffer, 0, fileCountBuffer.Length);

                        byte[] freeSpaceBuffer = Encoding.ASCII.GetBytes("        " + availableFreeSpace.ToString() + " bytes free\r\n\r\n");
                        serialPort.streamDir.Write(freeSpaceBuffer, 0, freeSpaceBuffer.Length);
                        serialPort.streamDir.Close();

                        // we have finished building the file we are going to send to the remote and closed it
                        // now open it in read mode and pump it out to the remote a byte at a time by setting
                        // the next state to CONNECTION_STATE.SENDING_DIR. Iwe fail setting the state, set it
                        // to CONNECTION_STATE.CONNECTED

                        try
                        {
                            serialPort.streamDir = File.Open(serialPort.dirFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
                            serialPort.WriteByte((byte)'\r', false);
                            serialPort.WriteByte((byte)'\n', false);
                            serialPort.SetState((int)CONNECTION_STATE.SENDING_DIR);
                        }
                        catch
                        {
                            File.Delete(serialPort.dirFilename);

                            serialPort.WriteByte((byte)0x06);
                            serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
                        }
                    }
                    break;
                case 'P':           // Change Directory
                    {
                        serialPort.commandFilename = "";
                        serialPort.SetState((int)CONNECTION_STATE.CD_GETFILENAME);
                    }
                    break;
                case 'V':           // Change Drive (and optionally the directory)
                    {
                        serialPort.commandFilename = "";
                        serialPort.SetState((int)CONNECTION_STATE.DRIVE_GETFILENAME);
                    }
                    break;
                case 'C':           // Create a drive image
                    {
                        serialPort.createFilename = "";
                        serialPort.createPath = "";
                        serialPort.createVolumeNumber = "";
                        serialPort.createTrackCount = "";
                        serialPort.createSectorCount = "";

                        serialPort.SetState((int)CONNECTION_STATE.CREATE_GETPARAMETERS);
                        serialPort.createState = (int)CREATE_STATE.GET_CREATE_PATH;
                    }
                    break;

                // now the Extended multi drive versions

                case 's':           // 's'end Sector Request with drive
                    {
                        serialPort.SetState((int)CONNECTION_STATE.GET_READ_DRIVE);
                    }
                    break;
                case 'r':           // 'r'eceive Sector Request with drive
                    {
                        serialPort.SetState((int)CONNECTION_STATE.GET_WRITE_DRIVE);
                    }
                    break;
                case 'm':           // Mount drive image with drive
                    {
                        serialPort.commandFilename = "";
                        serialPort.SetState((int)CONNECTION_STATE.GET_MOUNT_DRIVE);
                    }
                    break;
                case 'd':           // Report which disk image is mounted to requested drive
                    {
                        serialPort.SetState((int)CONNECTION_STATE.GET_REQUESTED_MOUNT_DRIVE);
                    }
                    break;
                case 'c':           // Create a drive image
                    {
                        serialPort.createFilename = "";
                        serialPort.createPath = "";
                        serialPort.createVolumeNumber = "";
                        serialPort.createTrackCount = "";
                        serialPort.createSectorCount = "";

                        serialPort.SetState((int)CONNECTION_STATE.GET_CREATE_DRIVE);
                        serialPort.createState = (int)CREATE_STATE.GET_CREATE_PATH;
                    }
                    break;
                default:            // Unknown - command - go back to (int)CONNECTION_STATE.CONNECTED
                    {
                        if (serialPort.state != (int)CONNECTION_STATE.CONNECTED)
                            serialPort.SetState((int)CONNECTION_STATE.CONNECTED);

                        if (c != 0x20)
                        {
                            serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                            WriteToOutputWindow("\n State is reset to CONNECTED - Unknown command recieved [" + c.ToString("X2", ci) + "]");
                            serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);
                        }
                    }
                    break;
            }
        }

        void StateConnectionStateGetRequestedMountDrive(Port serialPort, int c)
        {
            // Report which disk image is mounted to requested drive

            serialPort.currentDrive = c;

            serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
            WriteToOutputWindow(serialPort.currentWorkingDirectory);
            WriteToOutputWindow("\r");
            WriteToOutputWindow("\n");
            serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);

            if (serialPort.floppyImageFiles[serialPort.currentDrive].driveInfo != null)
                serialPort.sp.Write(serialPort.floppyImageFiles[serialPort.currentDrive].driveInfo.MountedFilename.Replace(@"\", "/"));
            else
                serialPort.sp.Write("");

            serialPort.WriteByte(0x0D, false);
            serialPort.WriteByte(0x06);

            serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
        }

        void StateConnectionStateGetReadDrive(Port serialPort, int c)
        {
            try
            {
                serialPort.currentDrive = c;
                if (serialPort.floppyImageFiles[serialPort.currentDrive].driveInfo != null)
                {
                    serialPort.floppyImageFiles[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.S_MODE;
                    serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
                }
            }
            catch (Exception e)
            {
                WriteLineToOutputWindow(e.Message);
            }
        }

        void StateConnectionStateGetWriteDrive(Port serialPort, int c)
        {
            serialPort.currentDrive = c;
            serialPort.floppyImageFiles[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.R_MODE;

            serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
        }

        void StateConnectionStateGetMountDrive(Port serialPort, int c)
        {
            serialPort.currentDrive = c;
            serialPort.SetState((int)CONNECTION_STATE.MOUNT_GETFILENAME);
        }

        void StateConnectionStateGetCreateDrive(Port serialPort, int c)
        {
            serialPort.currentDrive = c;
            serialPort.SetState((int)CONNECTION_STATE.CREATE_GETPARAMETERS);
        }

        void StateConnectionStateGetTrack(Port serialPort, int c)
        {
            serialPort.track[serialPort.currentDrive] = c;
            serialPort.SetState((int)CONNECTION_STATE.GET_SECTOR);
        }

        void StateConnectionStateGetSector(Port serialPort, int c)
        {
            serialPort.sector[serialPort.currentDrive] = c;

            if (serialPort.floppyImageFiles[serialPort.currentDrive].driveInfo.mode == (int)SECTOR_ACCESS_MODE.S_MODE)
            {
                if (serialPort.verbose == "V")
                    WriteLineToOutputWindow("\r\nState is SENDING_SECTOR");

                serialPort.SendSector();
                serialPort.SetState((int)CONNECTION_STATE.WAIT_ACK);
            }
            else
            {
                serialPort.sectorIndex = 0;
                serialPort.calculatedCRC = 0;
                serialPort.SetState((int)CONNECTION_STATE.RECEIVING_SECTOR);
            }
        }

        void StateConnectionStateRecievingSector(Port serialPort, int c)
        {
            serialPort.sectorBuffer[serialPort.sectorIndex++] = (byte)c;
            serialPort.calculatedCRC += (int)c;

            if (serialPort.sectorIndex >= 256)
            {
                serialPort.checksumIndex = 0;
                serialPort.SetState((int)CONNECTION_STATE.GET_CRC);
            }
        }

        void StateConnectionStateGetCRC(Port serialPort, int c)
        {
            if (serialPort.checksumIndex++ == 0)
                serialPort.checksum = (int)c * 256;
            else
            {
                serialPort.checksum += (int)c;

                byte status = serialPort.WriteSector();
                serialPort.WriteByte(status);
                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
            }
            //mf_portTabPages[serialPort.tabPageIndex].pictureBoxActivityTabDrives[serialPort.currentDrive].Image = Properties.Resources.greydot;
        }

        void StateConnectionStateMountGetFilename(Port serialPort, int c)
        {
            if (c != 0x0d)
            {
                // just add the character to the filename

                serialPort.commandFilename += (char)c;
            }
            else
            {
                if (serialPort.commandFilename.Length > 0)
                    serialPort.commandFilename += ".DSK";

                // this should close any file that is currently open for this port/drive

                if (serialPort.floppyImageFiles[serialPort.currentDrive] != null)
                {
                    if (serialPort.floppyImageFiles[serialPort.currentDrive].stream != null)
                    {
                        serialPort.floppyImageFiles[serialPort.currentDrive].stream.Close();
                        serialPort.floppyImageFiles[serialPort.currentDrive].stream = null;
                    }
                }

                // Now mount the new file

                byte status = 0x06;
                if (serialPort.commandFilename.Length > 0)
                {
                    WriteLineToOutputWindow();
                    status = serialPort.MountImageFile(serialPort.commandFilename, serialPort.currentDrive, serialPort.tabPageIndex);
                }

                serialPort.WriteByte(status);

                // todo: show file in text box

                string textBoxName = $"textBoxPortFloppyDrive{serialPort.currentDrive}Tab{serialPort.tabPageIndex}";
                string pictureBoxName = $"pictureBoxPortFloppyDrive{serialPort.currentDrive}Tab{serialPort.tabPageIndex}";
                TextBox textBox = Controls.Find(textBoxName, true).FirstOrDefault() as TextBox;
                PictureBox pictureBox = Controls.Find(pictureBoxName, true).FirstOrDefault() as PictureBox;

                if (textBox.InvokeRequired)
                    textBox.Invoke(new MethodInvoker(() => textBox.Text = serialPort.commandFilename));
                else
                    textBox.Text = serialPort.commandFilename;

                byte cMode = (byte)'R';
                if (serialPort.floppyImageFiles[serialPort.currentDrive].readOnly)
                {
                    if (pictureBox.InvokeRequired)
                        pictureBox.Invoke(new MethodInvoker(() => pictureBox.Image = Properties.Resources.reddot));
                    else
                        pictureBox.Image = Properties.Resources.reddot;
                }
                else
                {
                    cMode = (byte)'W';
                    if (pictureBox.InvokeRequired)
                        pictureBox.Invoke(new MethodInvoker(() => pictureBox.Image = Properties.Resources.greendot));
                    else
                        pictureBox.Image = Properties.Resources.greendot;
                }

                serialPort.WriteByte(cMode);
                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
            }
        }

        void StateConnectionStateDeleteGetFilename(Port serialPort, int c)
        {
            //    if (c != 0x0d)
            //        serialPort.commandFilename[serialPort.nCommandFilenameIndex++] = c;
            //    else
            //    {
            //        strcpy (serialPort.szFileToDelete, serialPort.currentWorkingDirectory );
            //        if ((strlen ((char*)serialPort.commandFilename) + strlen (serialPort.szFileToDelete)) < 126)
            //        {
            //            strcat (serialPort.szFileToDelete, "/");
            //            strcat (serialPort.szFileToDelete, (char*)serialPort.commandFilename);

            //            int nStatus = -1;

            //            // do not attempt to delete if the file is mounted

            //            for (int i = 0; i < (int) strlen (serialPort.szFileToDelete); i++)
            //            {
            //                if (serialPort.szFileToDelete[i] >= 'A' && serialPort.szFileToDelete[i] <= 'Z')
            //                    serialPort.szFileToDelete[i] = serialPort.szFileToDelete[i] & 0x5F;
            //            }

            //            if (strcmp (serialPort.szMountedFilename[serialPort.nCurrentDrive], serialPort.szFileToDelete) != 0)
            //            {
            //                // see if the file can be opened exclusively in r/w mode, if
            //                // it can - we can delete it, otherwise we fail attempting to
            //                // delete it so do not attempt

            //                FILE *x = fopen (serialPort.szFileToDelete, "r+b");
            //                if (x != NULL)
            //                {
            //                    // we were able to open it - close it and delete it

            //                    fclose (x);
            //                    nStatus = unlink (serialPort.szFileToDelete);
            //                }
            //                else
            //                    *serialPort.twWindows << "\n" << "attempted to delete open image" << "\n";
            //            }
            //            else
            //                *serialPort.twWindows << "\n" << "attempted to delete mounted image" << "\n";


            //            if (nStatus == 0)
            //            {
            //                *serialPort.twWindows << "image deleted" << "\n";
            //                rsPort->Write (0x06);
            //            }
            //            else
            //            {
            //                *serialPort.twWindows << "unable to delete image" << "\n";
            //                rsPort->Write (0x15);
            //            }
            //        }
            //        else
            //        {
            //            *serialPort.twWindows << "\n" << "attempted to delete with image name > 128 characters" << "\n";
            //            rsPort->Write (0x15);
            //        }

            //        serialPort.SetState  (CONNECTED);
            //    }
        }

        void StateConnectionStateDirGetFilename(Port serialPort, int c)
        {
            if (c != 0x0d)
                serialPort.commandFilename += (char)c;
            else
            {
                if (serialPort.commandFilename.Length == 0)
                    serialPort.commandFilename = "*.DSK";
                else
                    serialPort.commandFilename += ".DSK";

                serialPort.dirFilename = "dirtxt" + serialPort.port.ToString() + serialPort.currentDrive.ToString() + ".txt";

                if (serialPort.streamDir != null)
                {
                    serialPort.streamDir.Close();
                    serialPort.streamDir = null;
                    File.Delete(serialPort.dirFilename);
                }

                // get the list of files in the current working directory

                string[] files = Directory.GetFiles(serialPort.currentWorkingDirectory, "*.DSK", SearchOption.TopDirectoryOnly);

                serialPort.streamDir = File.Open(serialPort.dirFilename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

                System.IO.DriveInfo systemDriveInfo = null;

                string directoryRoot = Directory.GetDirectoryRoot(serialPort.currentWorkingDirectory);
                long availableFreeSpace = 0L;
                string driveName = "";
                string volumeLabel = "";
                byte[] volumeBuffer = new byte[0];

                try
                {
                    // if this is not a UNC path, we can get the available size, drivename and volume label
                    // otherwise - we cannot.

                    if (!directoryRoot.StartsWith(@"\\"))
                    {
                        systemDriveInfo = new System.IO.DriveInfo(directoryRoot);

                        availableFreeSpace = systemDriveInfo.AvailableFreeSpace;
                        driveName = systemDriveInfo.Name;
                        volumeLabel = systemDriveInfo.VolumeLabel;

                        volumeBuffer = Encoding.ASCII.GetBytes("\r\n Volume in Drive " + driveName + " is " + volumeLabel + "\r\n");
                    }
                    else
                    {
                        volumeBuffer = Encoding.ASCII.GetBytes("\r\n Volume in Drive is a UNC Path\r\n");
                    }

                    serialPort.streamDir.Write(volumeBuffer, 0, volumeBuffer.Length);

                    byte[] buffer = Encoding.ASCII.GetBytes(serialPort.currentWorkingDirectory + "\r\n\r\n");
                    serialPort.streamDir.Write(buffer, 0, buffer.Length);

                    // first get the max filename size

                    int maxFilenameSize = 0;
                    foreach (string file in files)
                    {
                        if (file.Length > maxFilenameSize)
                            maxFilenameSize = file.Length;
                    }
                    maxFilenameSize = maxFilenameSize - serialPort.currentWorkingDirectory.Length;

                    int fileCount = 0;
                    //foreach (string file in files)
                    //{
                    //    string filename = file + "\r\n";
                    //    filename = filename.Replace(@"\", "/");
                    //    serialPort.currentWorkingDirectory.Replace(@"\", "/");
                    //    filename = filename.Replace(serialPort.currentWorkingDirectory + "/", "");

                    //    byte[] bArray = Encoding.ASCII.GetBytes(filename);
                    //    serialPort.streamDir.Write(bArray, 0, bArray.Length);
                    //}
                    //serialPort.streamDir.Close();
                    foreach (string file in files)
                    {
                        FileInfo fi = new FileInfo(file);
                        DateTime fCreation = fi.CreationTime;

                        string fileInfoLine = file;
                        fileInfoLine = fileInfoLine.Replace(serialPort.currentWorkingDirectory + @"\", ""); // get rid of path info
                        fileInfoLine = fileInfoLine.PadRight(maxFilenameSize, ' ');                         // pad to proper length
                        fileInfoLine = fileInfoLine + "    " +
                                                        fCreation.Month.ToString("00") + "/" + fCreation.Day.ToString("00") + "/" + fCreation.Year.ToString("0000") +
                                                        " " +
                                                        fCreation.Hour.ToString("00") + ":" + fCreation.Minute.ToString("00") + ":" + fCreation.Second.ToString("00") +
                                                        "\r\n";
                        if (fileInfoLine.Length > 0)
                        {
                            fileCount += 1;
                            byte[] bArray = Encoding.ASCII.GetBytes(fileInfoLine);
                            serialPort.streamDir.Write(bArray, 0, bArray.Length);
                        }
                    }

                    byte[] fileCountBuffer = Encoding.ASCII.GetBytes("    " + fileCount.ToString() + " files\r\n");
                    serialPort.streamDir.Write(fileCountBuffer, 0, fileCountBuffer.Length);

                    byte[] freeSpaceBuffer = new byte[0];

                    if (!directoryRoot.StartsWith(@"\\"))
                        freeSpaceBuffer = Encoding.ASCII.GetBytes("        " + availableFreeSpace.ToString() + " bytes free\r\n\r\n");
                    else
                        freeSpaceBuffer = Encoding.ASCII.GetBytes("        " + "cannot report free space on unc path\r\n\r\n");

                    serialPort.streamDir.Write(freeSpaceBuffer, 0, freeSpaceBuffer.Length);

                    serialPort.streamDir.Close();

                    // --------------------------------------

                    serialPort.streamDir = File.Open(serialPort.dirFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (serialPort.streamDir != null)
                    {
                        serialPort.WriteByte((byte)'\r', false);
                        serialPort.WriteByte((byte)'\n', false);
                        serialPort.SetState((int)CONNECTION_STATE.SENDING_DIR);
                    }
                    else
                    {
                        serialPort.WriteByte(0x06);
                        File.Delete(serialPort.dirFilename);
                        serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
                    }
                }
                catch (Exception e)
                {
                    WriteLineToOutputWindow(e.Message);
                }
            }
        }

        void StateConnectionStateCDGetFilename(Port serialPort, int c)
        {
            if (c != 0x0d)
                serialPort.commandFilename += (char)c;
            else
            {
                byte status = 0x00;

                try
                {
                    Directory.SetCurrentDirectory(serialPort.commandFilename);
                    status = 0x06;

                    serialPort.currentWorkingDirectory = Directory.GetCurrentDirectory();
                    serialPort.currentWorkingDirectory.TrimEnd('\\');
                }
                catch
                {
                    status = 0x15;
                }

                serialPort.WriteByte(status);
                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
            }
        }

        void StateConnectionStateDriveGetFilename(Port serialPort, int c)
        {
            if (c != 0x0d)
                serialPort.commandFilename += (char)c;
            else
            {
                //int nNumDrives;
                //int nCurDrive;

                byte status = 0x00;

                //_dos_setdrive (serialPort.commandFilename[0] - 0x40, &nNumDrives);
                //_dos_getdrive (&nCurDrive);

                //if (nCurDrive = (serialPort.commandFilename[0] - 0x40))
                //{
                try
                {
                    Directory.SetCurrentDirectory(serialPort.commandFilename);
                    status = 0x06;

                    serialPort.currentWorkingDirectory = Directory.GetCurrentDirectory();
                    serialPort.currentWorkingDirectory.TrimEnd('\\');
                }
                catch
                {
                    status = 0x15;
                }
                //}
                //else
                //    status = 0x15;

                //if ((!lastActivityWasServer && !lastActivityWasClient) || lastActivityWasClient)
                //{
                //    WriteLine();
                //    WriteToOutputWindow("SERVER: ");
                //}
                //lastActivityWasServer = true;
                //lastActivityWasClient = false;

                serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                WriteToOutputWindow(status.ToString("X2", ci) + " ");
                serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);

                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
            }
        }

        void StateConnectionStateSendingDir(Port serialPort, int c)
        {
            if (c == ' ')
            {
                string line = "";
                int buffer = 0x00;

                while ((buffer = serialPort.streamDir.ReadByte()) != -1)
                {
                    if (buffer != (int)'\n')
                        line += (char)buffer;
                    else
                        break;
                }
                serialPort.WriteByte((byte)'\r', false);
                serialPort.sp.Write(line);
                serialPort.WriteByte((byte)'\n', false);

                if (buffer == -1)
                {
                    serialPort.streamDir.Close();
                    serialPort.streamDir = null;
                    File.Delete(serialPort.dirFilename);

                    serialPort.WriteByte(0x06);
                    serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
                }
            }
            else if (c == 0x1b)
            {
                serialPort.WriteByte((byte)'\r', false);
                serialPort.WriteByte((byte)'\n', false);

                serialPort.streamDir.Close();
                serialPort.streamDir = null;
                File.Delete(serialPort.dirFilename);

                serialPort.WriteByte(0x06);
                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
            }
        }

        void StateConnectionStateCreateGetParameters(Port serialPort, int c)
        {
            if (c != 0x0d)
            {
                switch (serialPort.createState)
                {
                    case (int)CREATE_STATE.GET_CREATE_PATH:
                        serialPort.createPath += (char)c;
                        break;
                    case (int)CREATE_STATE.GET_CREATE_NAME:
                        serialPort.createFilename += (char)c;
                        break;
                    case (int)CREATE_STATE.GET_CREATE_VOLUME:
                        serialPort.createVolumeNumber += (char)c;
                        break;
                    case (int)CREATE_STATE.GET_CREATE_TRACK_COUNT:
                        serialPort.createTrackCount += (char)c;
                        break;
                    case (int)CREATE_STATE.GET_CREATE_SECTOR_COUNT:
                        serialPort.createSectorCount += (char)c;
                        break;
                    default:
                        serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
                        break;
                }
            }
            else
            {
                if (serialPort.createState != (int)CREATE_STATE.GET_CREATE_SECTOR_COUNT)
                {
                    serialPort.createState++;
                    serialPort.SetState((int)CONNECTION_STATE.CREATE_GETPARAMETERS);
                }
                else
                {
                    string fullFilename = serialPort.createPath + "/" + serialPort.createFilename + ".DSK";

                    byte status;
                    WriteToOutputWindow("\n" + "Creating Image File " + fullFilename);
                    status = serialPort.CreateImageFile();
                    serialPort.WriteByte(status);

                    // Cannot automount because we do not know what drive to mount image too.
                    //
                    //if (serialPort.autoMount.ToUpper() == "T" || serialPort.autoMount.ToUpper() == "Y")
                    //{
                    //    if (serialPort.createPath ==  ".")
                    //    {
                    //        if (serialPort.imageFile[serialPort.currentDrive].stream != null)
                    //        {
                    //            serialPort.imageFile[serialPort.currentDrive].stream.Close();
                    //            serialPort.imageFile[serialPort.currentDrive].stream = null;
                    //        }

                    //        serialPort.MountImageFile(serialPort.currentWorkingDirectory + "/" + serialPort.createFilename + ".DSK", serialPort.currentDrive);
                    //    }
                    //    else
                    //    {
                    //        try
                    //        {
                    //            Directory.SetCurrentDirectory(serialPort.createPath);
                    //            if (serialPort.imageFile[serialPort.currentDrive].stream != null)
                    //            {
                    //                serialPort.imageFile[serialPort.currentDrive].stream.Close();
                    //                serialPort.imageFile[serialPort.currentDrive].stream = null;
                    //            }

                    //            serialPort.MountImageFile(serialPort.createPath + "/" + serialPort.createFilename + ".DSK", serialPort.currentDrive);
                    //        }
                    //        catch
                    //        {
                    //        }
                    //    }
                    //}
                    serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
                }
            }
        }

        void StateConnectionStateWaitACK(Port serialPort, int c)
        {
            //*StatusLine << '\n' << "State is WAIT_ACK";

            if (c == 0x06)
                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
            //else if (c == 's')
            //{
            //    // 's'end Sector Request with drive

            //    serialPort.SetState((int)CONNECTION_STATE.GET_READ_DRIVE);
            //}
            else
                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
        }
        #endregion
        #region loggers
        void LogFloppyReads(int drive, int track, int sector, int bytesToReadWrite, byte[] buffer, long offset)
        {
            if (logFileStream != null && logReads)
            {
                logFileStream.Write("READ: {0} {1} {2} - {3} - 0x{4}"
                                        , drive.ToString("D2")
                                        , track.ToString("D3")
                                        , sector.ToString("D3")
                                        , bytesToReadWrite.ToString("D3")
                                        , offset.ToString("X8")
                                        );

                for (int index = 1, counter = 0; index < 257; index++, counter++)
                {
                    if (counter % 16 == 0)
                        logFileStream.Write(string.Format("\n    {0}  ", counter.ToString("X4")));

                    logFileStream.Write("{0} ", buffer[index].ToString("X2"));
                }
                logFileStream.WriteLine();
                logFileStream.WriteLine();

                logFileStream.Flush();
            }
        }

        void LogFloppyWrites(int drive, int track, int sector, int bytesToReadWrite, byte[] buffer, long offset)
        {
            if (logFileStream != null && logWrites)
            {
                logFileStream.Write("WRITE: {0} {1} {2} - {3} - 0x{4}"
                                        , drive.ToString("D2")
                                        , track.ToString("D3")
                                        , sector.ToString("D3")
                                        , bytesToReadWrite.ToString("D3")
                                        , offset.ToString("X8")
                                        );

                for (int index = 6, counter = 0; index < 262; index++, counter++)
                {
                    if (counter % 16 == 0)
                        logFileStream.Write(string.Format("\n    {0}  ", counter.ToString("X4")));

                    logFileStream.Write("{0} ", buffer[index].ToString("X2"));
                }
                logFileStream.WriteLine();
                logFileStream.WriteLine();

                logFileStream.Flush();
            }
        }
        #endregion
        #region listeners - these need to run as threads
        void ProcessRS232Requests()
        {
            while (!done)
            {
                Application.DoEvents();
                foreach (Port serialPort in listPorts)
                {
                    try
                    {
                        Application.DoEvents();

                        if (serialPort.sp.IsOpen)
                        { 
                            if (serialPort.sp.BytesToRead > 0)
                            {
                                int c = serialPort.sp.ReadByte();

                                if (((serialPort.state != (int)CONNECTION_STATE.RECEIVING_SECTOR) && (serialPort.state != (int)CONNECTION_STATE.GET_CRC)))
                                {
                                    //if ((!lastActivityWasServer && !lastActivityWasClient) || lastActivityWasServer)
                                    //{
                                    //    WriteLine();
                                    //    WriteToOutputWindow("CLIENT: ");
                                    //}

                                    //WriteToOutputWindow(c.ToString("X2", ci) + " ");

                                    //lastActivityWasServer = false;
                                    //lastActivityWasClient = true;
                                }

                                switch (serialPort.state)
                                {
                                    case (int)CONNECTION_STATE.NOT_CONNECTED:               StateConnectionStateNotConnected(serialPort, c); break;
                                    case (int)CONNECTION_STATE.SYNCRONIZING:                StateConnectionStateSynchronizing(serialPort, c); break;
                                    case (int)CONNECTION_STATE.CONNECTED:                   StateConnectionStateConnected(serialPort, c); break;
                                    case (int)CONNECTION_STATE.GET_REQUESTED_MOUNT_DRIVE:   StateConnectionStateGetRequestedMountDrive(serialPort, c); break;
                                    case (int)CONNECTION_STATE.GET_READ_DRIVE:              StateConnectionStateGetReadDrive(serialPort, c); break;
                                    case (int)CONNECTION_STATE.GET_WRITE_DRIVE:             StateConnectionStateGetWriteDrive(serialPort, c); break;
                                    case (int)CONNECTION_STATE.GET_MOUNT_DRIVE:             StateConnectionStateGetMountDrive(serialPort, c); break;
                                    case (int)CONNECTION_STATE.GET_CREATE_DRIVE:            StateConnectionStateGetCreateDrive(serialPort, c); break;
                                    case (int)CONNECTION_STATE.GET_TRACK:                   StateConnectionStateGetTrack(serialPort, c); break;
                                    case (int)CONNECTION_STATE.GET_SECTOR:                  StateConnectionStateGetSector(serialPort, c); break;
                                    case (int)CONNECTION_STATE.RECEIVING_SECTOR:            StateConnectionStateRecievingSector(serialPort, c); break;
                                    case (int)CONNECTION_STATE.GET_CRC:                     StateConnectionStateGetCRC(serialPort, c); break;
                                    case (int)CONNECTION_STATE.MOUNT_GETFILENAME:           StateConnectionStateMountGetFilename(serialPort, c); break;
                                    case (int)CONNECTION_STATE.DELETE_GETFILENAME:          StateConnectionStateDeleteGetFilename(serialPort, c); break;
                                    case (int)CONNECTION_STATE.DIR_GETFILENAME:             StateConnectionStateDirGetFilename(serialPort, c); break;
                                    case (int)CONNECTION_STATE.CD_GETFILENAME:              StateConnectionStateCDGetFilename(serialPort, c); break;
                                    case (int)CONNECTION_STATE.DRIVE_GETFILENAME:           StateConnectionStateDriveGetFilename(serialPort, c); break;
                                    case (int)CONNECTION_STATE.SENDING_DIR:                 StateConnectionStateSendingDir(serialPort, c); break;
                                    case (int)CONNECTION_STATE.CREATE_GETPARAMETERS:        StateConnectionStateCreateGetParameters(serialPort, c); break;
                                    case (int)CONNECTION_STATE.WAIT_ACK:                    StateConnectionStateWaitACK(serialPort, c); break;
                                    default:
                                        serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);
                                        WriteLineToOutputWindow($"State is reset to NOT_CONNECTED - Unknown STATE {c.ToString("X2")}");
                                        break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        WriteLineToOutputWindow(e.Message);
                    }
                }
            }
        }

        void ProcessTcpIPRequests()
        {
            if (logReads || logWrites)
            {
                logFileStream = new StreamWriter(File.Open(logfileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            }

            while (!done)
            {
                Application.DoEvents();
                try
                {
                    TcpClient client = listener.AcceptTcpClient(); // Accept an incoming connection

                    int newBufferSize = 65536; // 64 KB (default is usually 8 KB)
                    client.ReceiveBufferSize = newBufferSize;
                    client.SendBufferSize = newBufferSize;

                    // a client has connected 
                    IPEndPoint remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                    string connectedIpAddress = remoteEndPoint.Address.ToString();

                    if (textBoxConnectedTo.InvokeRequired)
                        textBoxConnectedTo.Invoke(new MethodInvoker(() => textBoxConnectedTo.Text = connectedIpAddress));
                    else
                        textBoxConnectedTo.Text = connectedIpAddress;

                    //WriteLine($"Client connected from {remoteEndPoint.Address}:{remoteEndPoint.Port}");

                    NetworkStream stream = client.GetStream();

                    // Read data from the client
                    byte[] buffer = new byte[65536];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    switch (buffer[0])
                    {
                        // the 0xFF command is the command to report the number of cyscles executed in the last second
                        // this is used for debugging ONLY.
                        case 0xFF:
                            {
                                // report number of cycles executed in last second

                                // first send ACK back to client
                                buffer[0] = 0x06;
                                stream.Write(buffer, 0, 1); // send ACK

                                // set up the number of cycles from the buffer
                                long cyclesExecuted = 0;

                                cyclesExecuted += buffer[1] * 256 * 256 * 256;
                                cyclesExecuted += buffer[2] * 256 * 256;
                                cyclesExecuted += buffer[3] * 256;
                                cyclesExecuted += buffer[4];

                                string cyclesPerSecond = cyclesExecuted.ToString("#,##0");
                                if (textBoxCyclesPerSecond.InvokeRequired)
                                    textBoxCyclesPerSecond.Invoke(new MethodInvoker(() => textBoxCyclesPerSecond.Text = cyclesPerSecond));
                                else
                                    textBoxCyclesPerSecond.Text = cyclesPerSecond;

                                //WriteLineToOutputWindow($"cycles: {cyclesExecuted}");
                            }
                            break;

                        // The command in buffer[1] is for the floppy controller emualtion
                        case 0x46:  // (F)loppy
                            {
                                switch (buffer[1] & 0xF0)
                                {
                                    // this will handle all of the type 1 commands:
                                    //
                                    // ALL     TYPE I      
                                    // BIT     COMMANDS    
                                    // --------------------
                                    // S7      NOT READY   
                                    // S6      WRITE PROTECT     
                                    // S5      HEAD LOADED 
                                    // S4      SEEK ERROR  
                                    // S3      CRC ERROR   
                                    // S2      TRACK 0     
                                    // S1      INDEX PULSE 
                                    // SO      BUSY        
                                    // --------------------------------

                                    case 0x00:      // RESTORE
                                        {
                                            byte command = buffer[1];
                                            int currentDrive = buffer[2] & 0x03;
                                            if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive] != null)
                                            {
                                                buffer[0] = (byte)(FDC_HDLOADED | FDC_TRKZ);
                                                if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].readOnly)
                                                    buffer[0] |= (byte)FDC_WRTPROTECT;
                                            }
                                            else
                                            {
                                                // the drive is not mounted - set appropriate status bits
                                                buffer[0] = (byte)(FDC_NOTREADY);
                                            }

                                            // send the status back to the client
                                            stream.Write(buffer, 0, 1);
                                        }
                                        break;

                                    case 0x10:      // SEEK
                                        {
                                            byte command = buffer[1];
                                            int currentDrive = buffer[2] & 0x03;
                                            if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive] != null)
                                            {
                                                int track = buffer[3];

                                                // start out by initializing the with setting the write protect bit
                                                if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].readOnly)
                                                    buffer[0] = (byte)FDC_WRTPROTECT;
                                                else
                                                    buffer[0] = 0;

                                                if ((command & 0x08) == 0x08)   // head load requested?
                                                    buffer[0] |= (byte)(FDC_HDLOADED);
                                                else
                                                    buffer[0] &= (byte)(~FDC_HDLOADED & 0xff);

                                                // if the user requested a seek to track - - set TRKZ bit
                                                if (track == 0)
                                                    buffer[0] |= (byte)(FDC_TRKZ);

                                                // if the user specified he Verify bit - make sure the track exists.
                                                if ((command & 0x04) == 0x04)
                                                {
                                                    long lOffsetToStartOfTrack = ((long)tcpIpSocketInfo.localPort.track[currentDrive] * tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].driveInfo.NumberOfBytesPerTrack);
                                                    if (lOffsetToStartOfTrack <= (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].stream.Length - tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].driveInfo.NumberOfBytesPerTrack))
                                                    {
                                                        buffer[0] |= (byte)(FDC_SEEKERR);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                // the drive is not mounted - set appropriate status bits
                                                buffer[0] = (byte)(FDC_NOTREADY);
                                            }

                                            // send the status back to the client
                                            stream.Write(buffer, 0, 1);
                                        }
                                        break;

                                    // TODO: finish fleshing these commands out.

                                    case 0x20:      // STEP
                                    case 0x30:      // STEP W/TRACK UPDATE
                                    case 0x40:      // STEP IN
                                    case 0x50:      // STEP IN  W/TRACK UPDATE
                                    case 0x60:      // STEP OUT
                                    case 0x70:      // STEP OUT W/TRACK UPDATE
                                        {
                                            byte command = buffer[1];
                                            int currentDrive = buffer[2] & 0x03;

                                            if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive] != null)
                                            {
                                                buffer[0] = 0;
                                            }
                                            else
                                            {
                                                buffer[0] = (byte)(FDC_NOTREADY);
                                            }

                                            stream.Write(buffer, 0, 1);
                                        }
                                        break;

                                    // handle floppy reads and writes (type 2)
                                    //
                                    // ALL     READ        WRITE       
                                    // BIT     SECTOR      SECTOR      
                                    // --------------------------------
                                    // S7      NOT READY   NOT READY   
                                    // S6      0           WRITE PROTECT     
                                    // S5      RECORD TYPE 0           
                                    // S4      RNF         RNF         
                                    // S3      CRC ERROR   CRC ERROR   
                                    // S2      LOST DATA   LOST DATA   
                                    // S1      DRO         DRO         
                                    // SO      BUSY        BUSY        
                                    // --------------------------------

                                    // these are for floppy read
                                    case 0x80:  // read one sector
                                    case 0x90:  // read multiple sectors
                                        {
                                            int currentDrive = buffer[2] & 0x03;
                                            //mf_tcpipFloppyDrives[currentDrive].pictureBoxTCPIPFloppyActivityDrive.Image = Properties.Resources.greendot;

                                            if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive] != null)
                                            {
                                                tcpIpSocketInfo.localPort.track[currentDrive] = buffer[3];
                                                tcpIpSocketInfo.localPort.sector[currentDrive] = buffer[4];
                                                tcpIpSocketInfo.localPort.bytesToRead = buffer[5] * 256;
                                                tcpIpSocketInfo.localPort.bytesToRead += buffer[6];

                                                // we are done using the buffer as input - use it to send response now.

                                                long lSectorOffset = tcpIpSocketInfo.localPort.GetFloppySectorOffset(currentDrive);
                                                if (lSectorOffset < tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].stream.Length - 256)
                                                {
                                                    // reuse the buffer we read from the client as the return buffer
                                                    tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].stream.Seek(lSectorOffset, SeekOrigin.Begin);
                                                    tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].stream.Read(buffer, 0, tcpIpSocketInfo.localPort.bytesToRead);

                                                    // shift the buffer up one byte to make room for the status byte
                                                    for (int i = tcpIpSocketInfo.localPort.bytesToRead; i > 0; i--)
                                                        buffer[i] = buffer[i - 1];

                                                    // log AFTER we fill the buffer, but before we replace buffer[0] with status
                                                    LogFloppyReads(currentDrive, tcpIpSocketInfo.localPort.track[currentDrive], tcpIpSocketInfo.localPort.sector[currentDrive], tcpIpSocketInfo.localPort.bytesToRead, buffer, lSectorOffset);

                                                    buffer[0] = 0x00;   // for now - just say success
                                                }
                                                else
                                                {
                                                    buffer[0] = FDC_RNF;   // set write protected bit in status
                                                }
                                            }
                                            else
                                            {
                                                buffer[0] = FDC_NOTREADY;   // signal drive not ready
                                                tcpIpSocketInfo.localPort.bytesToRead = 0;
                                            }

                                            // now send this response back to the client
                                            stream.Write(buffer, 0, tcpIpSocketInfo.localPort.bytesToRead + 1);
                                            //mf_tcpipFloppyDrives[currentDrive].pictureBoxTCPIPFloppyActivityDrive.Image = Properties.Resources.greydot;
                                        }
                                        break;

                                    // these are for floppy write
                                    case 0xA0:  // write one sector
                                    case 0xB0:  // write multiple sectors
                                        {
                                            int currentDrive = buffer[2] & 0x03;
                                            //mf_tcpipFloppyDrives[currentDrive].pictureBoxTCPIPFloppyActivityDrive.Image = Properties.Resources.reddot;

                                            //tcpIpSocketInfo.localPort.currentDrive = currentDrive;
                                            tcpIpSocketInfo.localPort.track[currentDrive] = buffer[3];
                                            tcpIpSocketInfo.localPort.sector[currentDrive] = buffer[4];
                                            tcpIpSocketInfo.localPort.bytesToWrite = buffer[5] * 256;
                                            tcpIpSocketInfo.localPort.bytesToWrite += buffer[6];

                                            if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive] != null)
                                            {
                                                if (!tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].readOnly)
                                                {
                                                    long lSectorOffset = tcpIpSocketInfo.localPort.GetFloppySectorOffset(currentDrive);
                                                    if (lSectorOffset < tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].stream.Length - 256)
                                                    {
                                                        tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].stream.Seek(lSectorOffset, SeekOrigin.Begin);

                                                        for (int i = 0; i < tcpIpSocketInfo.localPort.bytesToWrite; i++)
                                                        {
                                                            tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive].stream.WriteByte(buffer[7 + i]);
                                                        }

                                                        LogFloppyWrites(currentDrive, tcpIpSocketInfo.localPort.track[currentDrive], tcpIpSocketInfo.localPort.sector[currentDrive], tcpIpSocketInfo.localPort.bytesToWrite, buffer, lSectorOffset);

                                                        buffer[0] = 0x00;   // for now - just say success
                                                    }
                                                    else
                                                    {
                                                        buffer[0] = FDC_RNF;   // set write protected bit in status
                                                    }
                                                }
                                                else
                                                {
                                                    buffer[0] = FDC_WRTPROTECT;   // set write protected bit in status
                                                }
                                            }
                                            else
                                            {
                                                buffer[0] = FDC_NOTREADY;          // set drive not ready
                                            }
                                            // now send this response back to the client
                                            stream.Write(buffer, 0, 1);
                                            //mf_tcpipFloppyDrives[currentDrive].pictureBoxTCPIPFloppyActivityDrive.Image = Properties.Resources.greydot;
                                        }
                                        break;

                                    // handle the other floppy commands (type 3)
                                    //
                                    // ALL     READ        READ        WRITE
                                    // BIT     ADDRESS     TRACK       TRACK
                                    // ----------------------------------------------
                                    // S7      NOT READY   NOT READY   NOT READY
                                    // S6      0           0           WRITE PROTECT
                                    // S5      0           0           0
                                    // S4      RNF         0           0
                                    // S3      CRC ERROR   0           0
                                    // S2      LOST DATA   LOST DATA   LOST DATA
                                    // S1      DRO         DRO         DRO
                                    // SO      BUSY        BUSY        BUSY        
                                    // ----------------------------------------------------------------------------------
                                    case 0xC0:      // READ ADDRESS
                                    case 0xE0:      // READ TRACK
                                    case 0xF0:      // WRITE TRACK
                                        {
                                            byte command = buffer[1];
                                            int currentDrive = buffer[2] & 0x03;

                                            if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive] != null)
                                            {
                                                buffer[0] = 0;
                                            }
                                            else
                                            {
                                                buffer[0] = (byte)(FDC_NOTREADY);
                                            }

                                            stream.Write(buffer, 0, 1);
                                        }
                                        break;

                                    // handle the other floppy commands (type 4)
                                    case 0xD0:      // FORCE INTERRUPT      <- type 4
                                        {
                                            // force interrupt does not involve the drive - always successfull

                                            buffer[0] = 0;
                                            stream.Write(buffer, 0, 1);
                                        }
                                        break;

                                    default:
                                        WriteLineToOutputWindow($"invalid floppy command from TCPIP client: {buffer[0].ToString("X2")}");
                                        break;
                                }
                            }
                            break;

                        // The command in buffer[1] is for the sd card controller emualtion
                        case 0x53:  // (S)D Card
                            {
                                // this is for sdcard read
                                //
                                //  The SDH (Sector Count / Drive/Head) Register in the IDE (ATA) interface is used to select the drive, head, and
                                //  addressing mode when accessing a device. It is located at I/O port 0x1F6 (primary IDE) or 0x176 (secondary IDE).
                                //
                                //  SDH Register (Drive/Head Select Register) Layout
                                //  The SDH register is an 8-bit register with the following bit layout:
                                //
                                //      Bit	Name	Meaning
                                //      7	1	    This bit is always set to 1.
                                //      6	LBA	    0 = CHS mode, 1 = LBA mode.
                                //      5	1	    This bit is always set to 1.
                                //      4	DRV	    0 = Select Drive 0 (Master), 1 = Select Drive 1 (Slave).
                                //      3:0	HEAD	Head number (0-15 in CHS mode, 0-3 in LBA mode).
                                //
                                //  Bit Breakdown
                                //      Bit 7 (Always 1): Fixed at 1, required by the ATA specification.
                                //      Bit 6 (LBA Mode):
                                //          0 = CHS (Cylinder-Head-Sector) mode.
                                //          1 = LBA (Logical Block Addressing) mode.
                                //      Bit 5 (Always 1): Fixed at 1, required by the ATA specification.
                                //      Bit 4 (Drive Select):
                                //          0 = Drive 0 (Master)
                                //          1 = Drive 1 (Slave)
                                //      Bits 3-0 (Head Number):
                                //          In CHS mode, selects one of 16 heads (0-15).
                                //          In LBA mode, bits 3-0 contain LBA bits 24-27.
                                //
                                //  Key Notes
                                //      LBA mode is required for drives > 528 MB since CHS has a 1024-cylinder limit.
                                //      In LBA mode, the sector number is divided across multiple registers:
                                //          SDH register stores bits 24-27.
                                //          Cylinder High Register stores bits 16-23.
                                //          Cylinder Low Register stores bits 8-15.
                                //          Sector Number Register stores bits 0-7.

                                switch (buffer[1] & 0xF0)
                                {
                                    case 0x20:
                                        {
                                            int currentDrive = (buffer[2] & 0x10) >> 4;
                                            //mf_SDCardDrives[currentDrive].pictureBoxTCPIPSDCardActivityDrive.Image = Properties.Resources.greendot;

                                            tcpIpSocketInfo.localPort.sizeDriveHeadRegister = buffer[2];          // SIZE/DRIVE/HEAD REGISTER
                                            tcpIpSocketInfo.localPort.cylinderHiRegister = buffer[3];          // cylinder number high byte
                                            tcpIpSocketInfo.localPort.cylinderLowRegister = buffer[4];          // cylinder number low byte
                                            tcpIpSocketInfo.localPort.sectorNumberRegister = buffer[5];          // secttor in the cylinder to read
                                            tcpIpSocketInfo.localPort.sectorCountRegister = buffer[6];          // number of sectors to read

                                            long lSectorOffset = tcpIpSocketInfo.localPort.GetSDCardSectorOffset(currentDrive);

                                            tcpIpSocketInfo.localPort.bytesToRead = tcpIpSocketInfo.localPort.sectorCountRegister * 256;
                                            // reuse the buffer we read from the client as the return buffer
                                            tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].stream.Seek(lSectorOffset, SeekOrigin.Begin);
                                            int bytesReadFromSDCard = tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].stream.Read(buffer, 0, tcpIpSocketInfo.localPort.bytesToRead * tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].driveInfo.bytesPerRead);

                                            // now compress if bytes per read is not 1

                                            if (tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].driveInfo.bytesPerRead != 1)
                                            {
                                                for (int i = 0; i < bytesReadFromSDCard / tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].driveInfo.bytesPerRead; i++)
                                                {
                                                    buffer[i] = buffer[i * 2];
                                                }
                                            }

                                            // shift the buffer up one byte to make room for the status byte
                                            for (int i = 256; i > 0; i--)
                                            {
                                                buffer[i] = buffer[i - 1];
                                            }

                                            buffer[0] = 0x00;   // for now - just say success

                                            // now send this response back to the client
                                            stream.Write(buffer, 0, (bytesReadFromSDCard / tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].driveInfo.bytesPerRead) + 1);
                                            //mf_SDCardDrives[currentDrive].pictureBoxTCPIPSDCardActivityDrive.Image = Properties.Resources.greydot;
                                        }
                                        break;

                                    // this is for sdcard write
                                    case 0x30:
                                        {
                                            int currentDrive = (buffer[2] & 0x10) >> 4;
                                            //mf_SDCardDrives[currentDrive].pictureBoxTCPIPSDCardActivityDrive.Image = Properties.Resources.reddot;

                                            tcpIpSocketInfo.localPort.sizeDriveHeadRegister = buffer[2];          // SIZE/DRIVE/HEAD REGISTER
                                            tcpIpSocketInfo.localPort.cylinderHiRegister = buffer[3];          // cylinder number high byte
                                            tcpIpSocketInfo.localPort.cylinderLowRegister = buffer[4];          // cylinder number low byte
                                            tcpIpSocketInfo.localPort.sectorNumberRegister = buffer[5];          // secttor in the cylinder to read
                                            tcpIpSocketInfo.localPort.sectorCountRegister = buffer[6];          // number of sectors to read

                                            long lSectorOffset = tcpIpSocketInfo.localPort.GetSDCardSectorOffset(currentDrive);
                                            tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].stream.Seek(lSectorOffset, SeekOrigin.Begin);
                                            for (int i = 0; i < tcpIpSocketInfo.localPort.sectorCountRegister * 256; i++)
                                            {
                                                tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].stream.WriteByte(buffer[7 + i]);
                                                for (int j = 0; j < tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].driveInfo.bytesPerRead - 1; j++)
                                                {
                                                    tcpIpSocketInfo.localPort.sdCardImageFiles[currentDrive].stream.WriteByte(0x00);
                                                }
                                            }
                                            buffer[0] = 0x00;   // for now - just say success

                                            // now send this response back to the client
                                            stream.Write(buffer, 0, 1);
                                            //mf_SDCardDrives[currentDrive].pictureBoxTCPIPSDCardActivityDrive.Image = Properties.Resources.greydot;
                                        }
                                        break;

                                    default:
                                        WriteLineToOutputWindow($"invalid sdcard command from TCPIP client: {buffer[0].ToString("X2")}");
                                        break;
                                }
                                break;
                            }

                        default:
                            WriteLineToOutputWindow($"invalid command from TCPIP client: {buffer[0].ToString("X2")}");
                            break;
                    }
                    client.Close(); // Close connection
                }
                catch (Exception e)
                {
                    string message = e.Message;
                    break;
                }
            }
        }
        #endregion
        #region form and menu handlers
        private void saveConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // using the menu to save allows you to specify the file name to save it as.
            // using the button saves to the currently active filene.
            //
            
            buttonBrowseConfigurationFilename_Click(sender, e);
            if (textBoxConfigurationFilename.Text != "")
                SaveConfiguration(textBoxConfigurationFilename.Text);
        }

        private void loadConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Multiselect = false;
            dlg.InitialDirectory = applicationPath.Replace("/", "\\");       // InitialDirectory doesn't like forward slashes
            dlg.DefaultExt = ".xml";

            DialogResult r = dlg.ShowDialog(this);
            if (r == DialogResult.OK)
            {
                InitializeFromConfigFile(dlg.FileName);
            }

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (rs232Thread != null)
                rs232Thread.Abort();

            listener.Stop();
            Thread.Sleep(1000);
            if (listenerThread != null)
                listenerThread.Join();

            Environment.Exit(1);
        }


        private void remountTcpIpFloppyImagesToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void remountFloppyImagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // save our current working directory so we can default to the execution directory for config file save if need be..
            string currentWorkingDirectory = Directory.GetCurrentDirectory();
            string applicationPathDirectory = Application.ExecutablePath.Replace($"{Application.ProductName}.exe", "").Trim('\\');

            Directory.SetCurrentDirectory(applicationPathDirectory);
            ParsePortsFloppiesFromConfigFile(textBoxConfigurationFilename.Text);
            Directory.SetCurrentDirectory(currentWorkingDirectory);

            foreach (Port serialPort in listPorts)
            {
                int nIndex = 0;
                foreach (ImageFile imageFile in serialPort.floppyImageFiles)
                {
                    if (imageFile != null)
                    {
                        mf_portTabPages[serialPort.tabPageIndex].tabPage.Text = $"COM{serialPort.port.ToString()}";

                        if (imageFile.Name.Length > 0)
                        {
                            serialPort.MountImageFile(imageFile.Name + ".DSK", nIndex, serialPort.tabPageIndex);

                            mf_portTabPages[serialPort.tabPageIndex].textBoxFloppyDrives[imageFile.driveNumber].Text = imageFile.Name;
                            if (imageFile.readOnly)
                                mf_portTabPages[serialPort.tabPageIndex].pictureBoxFloppyDrives[imageFile.driveNumber].Image = Properties.Resources.reddot;
                            else
                                mf_portTabPages[serialPort.tabPageIndex].pictureBoxFloppyDrives[imageFile.driveNumber].Image = Properties.Resources.greendot;
                        }
                        nIndex++;
                    }
                }
            }
        }

        private void remountSDCardToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (rs232Thread != null)
                rs232Thread.Abort();

            listener.Stop();
            Thread.Sleep(100);
            if (listenerThread != null)
                listenerThread.Join();
        }

        private void resyncAllPortsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Port serialPort in listPorts)
                serialPort.SetState((int)CONNECTION_STATE.SYNCRONIZING);
        }

        public class PortFloppyTabPage
        {
            public TabPage tabPage = new TabPage();
            public CheckBox verboseCheckBox = new CheckBox();
            public List<TextBox> textBoxFloppyDrives = new List<TextBox>();
            public List<PictureBox> pictureBoxFloppyDrives = new List<PictureBox>();
            public List<PictureBox> pictureBoxActivityTabDrives = new List<PictureBox>();
        }

        public class TcpIpSDCardAdapter
        {
            public TextBox    textBoxSDCardDrive    = new TextBox();
            public PictureBox pictureBoxSDCardDrive = new PictureBox();
            public PictureBox pictureBoxTCPIPSDCardActivityDrive = new PictureBox();
        }

        public class TcpIpFloppyDrive
        {
            public TextBox textBoxFloppyDrive = new TextBox();
            public PictureBox pictureBoxFloppyDrive = new PictureBox();
            public PictureBox pictureBoxTCPIPFloppyActivityDrive = new PictureBox();
        }

        void MakeControlsLists ()
        {
            mf_outputWindow = outputWindow;

            for (int tabPageIndex = 0; tabPageIndex < 4; tabPageIndex++)
            {
                PortFloppyTabPage portFloppyTabPage = new PortFloppyTabPage();

                string tabPageName = $"TabPage{tabPageIndex}";
                TabPage tabPage = Controls.Find(tabPageName, true).FirstOrDefault() as TabPage;
                portFloppyTabPage.tabPage = tabPage;

                string checkBoxName = $"checkboxPortFloppyVerboseTab{tabPageIndex}";
                CheckBox checkBox = Controls.Find(checkBoxName, true).FirstOrDefault() as CheckBox;
                portFloppyTabPage.verboseCheckBox = checkBox;

                mf_portTabPages.Add(portFloppyTabPage);
            }

            for (int tabPageIndex = 0; tabPageIndex < 4; tabPageIndex++)
            {
                for (int drive = 0; drive < 4; drive++)
                {
                    string textBoxName    = $"textBoxPortFloppyDrive{drive}Tab{tabPageIndex}";
                    TextBox textBox = Controls.Find(textBoxName, true).FirstOrDefault() as TextBox;
                    mf_portTabPages[tabPageIndex].textBoxFloppyDrives.Add(textBox);

                    string pictureBoxName = $"pictureBoxPortFloppyDrive{drive}Tab{tabPageIndex}";
                    PictureBox pictureBox = Controls.Find(pictureBoxName, true).FirstOrDefault() as PictureBox;
                    mf_portTabPages[tabPageIndex].pictureBoxFloppyDrives.Add(pictureBox);

                    string pictureBoxActivityName = $"pictureBox4ActivityTab{tabPageIndex}Drive{drive}";
                    PictureBox pictureBoxActivity = Controls.Find(pictureBoxActivityName, true).FirstOrDefault() as PictureBox;
                    mf_portTabPages[tabPageIndex].pictureBoxActivityTabDrives.Add(pictureBox);
                }
            }

            for (int drive = 0; drive < 2; drive++)
            {
                TcpIpSDCardAdapter tcpIpSDCard = new TcpIpSDCardAdapter();

                string textBoxName = $"textBoxSDCardDrive{drive}";
                tcpIpSDCard.textBoxSDCardDrive = Controls.Find(textBoxName, true).FirstOrDefault() as TextBox;

                string pictureBoxName = $"picturBoxTCPIPSDCardDrive{drive}";
                tcpIpSDCard.pictureBoxSDCardDrive = Controls.Find(pictureBoxName, true).FirstOrDefault() as PictureBox;

                string pictureBoxActivityName = $"pictureBoxTCPIPSDCardActivityDrive{drive}";
                tcpIpSDCard.pictureBoxTCPIPSDCardActivityDrive = Controls.Find(pictureBoxActivityName, true).FirstOrDefault() as PictureBox;
                
                mf_SDCardDrives.Add(tcpIpSDCard);
            }

            for (int drive = 0; drive < 4; drive++)
            {
                TcpIpFloppyDrive tcpIpFloppyDrive = new TcpIpFloppyDrive();

                string textBoxName = $"textBoxTCPIPFloppyDrive{drive}";
                tcpIpFloppyDrive.textBoxFloppyDrive = Controls.Find(textBoxName, true).FirstOrDefault() as TextBox;

                string pictureBoxName = $"pictureBoxTCPIPFLoppyDrive{drive}";
                tcpIpFloppyDrive.pictureBoxFloppyDrive = Controls.Find(pictureBoxName, true).FirstOrDefault() as PictureBox;

                string pictureBoxActivityName = $"pictureBoxTCPIPFloppyActivityDrive{drive}";
                tcpIpFloppyDrive.pictureBoxTCPIPFloppyActivityDrive = Controls.Find(pictureBoxActivityName, true).FirstOrDefault() as PictureBox;

                mf_tcpipFloppyDrives.Add(tcpIpFloppyDrive);
            }

            //TabPage currentPage = Controls.Find(tabPageName, true).FirstOrDefault() as TabPage;
            //currentPage.Text = $"COM{serialPort.port.ToString()}";
        }

        public frmMain()
        {
            InitializeComponent();

            //  make lists of the controls 
            MakeControlsLists();

            // do this as the very first thing.
            Program.mainForm = this;

            string configFilename = "FNConfig.xml";
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\FLEXWire");
            if (key != null)
            {
                // Read the value
                object value = key.GetValue("Configuration File");

                if (value != null)
                    configFilename = (string)value;

                key.Close();
            }
            textBoxConfigurationFilename.Text = configFilename;
            InitializeFromConfigFile(configFilename);

            bool cancel = false;

            if (tcpIpSocketInfo != null && tcpIpSocketInfo.enabled)
            {
                usingTCPIP = true;
                bool listening = false;

                listener = new TcpListener(localAddr, tcpIpSocketInfo.serverPort);
                while (!listening && !cancel)
                {
                    try
                    {
                        listener.Start();
                        listening = true;
                    }
                    catch (Exception e)
                    {
                        string message = e.Message;
                        message += " Would you like to retry?";

                        DialogResult result = MessageBox.Show(message, "Error setting up listener", MessageBoxButtons.YesNo);
                        switch (result)
                        {
                            case DialogResult.Yes:
                                break;

                            case DialogResult.No:
                                cancel = true;
                                break;
                        }
                    }
                }

                if (!cancel && listening)
                {
                    WriteLineToOutputWindow($"Server started on port {tcpIpSocketInfo.serverPort}. Waiting for connections...");

                    listenerThread = new Thread(new ThreadStart(ProcessTcpIPRequests));
                    listenerThread.Start();
                }
            }

            if (cancel)
                MessageBox.Show("Colud not set up TCP IP listeners - using RS232 ports instead");

            foreach (Port serialPort in listPorts)
            {
                if (serialPort.sp.IsOpen)
                    WriteLineToOutputWindow($"RS232 Server started on serial port {serialPort.sp.PortName}. Waiting for connections...", true);
            }

            //if (usingPorts)
            {
                rs232Thread = new Thread(new ThreadStart(ProcessRS232Requests));
                rs232Thread.Start();
            }
            //else
            //{
            //    // we need to re-position the outputWindow to take the place of the RS232 group box
            //    // start by turning of the window's anchors.

            //    outputWindow.Anchor = AnchorStyles.None;

            //    Point newOutputWindowLocation = new Point(26, groupBoxRS232Ports.Location.Y + 104);

            //    // we dont need to show the RS232 Group box
            //    groupBoxRS232Ports.Visible = false;
            //    outputWindow.Location = newOutputWindowLocation;

            //    // now caclulate the new applicatin window size
            //    Size newMainFormSize = new Size(512, groupBoxRS232Ports.Location.Y + mainMenuStrip.Size.Height + outputWindow.Size.Height);

            //    // use this to make the application smaller
            //    this.Size = newMainFormSize;

            //    // when we are done - re anchor the window
            //    outputWindow.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            //}
        }

        #endregion
        #region checkbox handlers
        private void checkBoxLogReads_CheckedChanged(object sender, EventArgs e)
        {
            logReads = checkBoxLogReads.Checked;
        }

        private void checkBoxLogWrites_CheckedChanged(object sender, EventArgs e)
        {
            logWrites = checkBoxLogWrites.Checked;
        }

        private void checkBoxShutdown_CheckedChanged(object sender, EventArgs e)
        {
            shutDown = checkBoxShutdown.Checked;
        }

        private void checkboxSDCardVerbose_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxSDCardVerbose.Checked)
                tcpIpSocketInfo.localPort.SDCardVerbose = "V";
            else
                tcpIpSocketInfo.localPort.SDCardVerbose = "";
        }

        private void checkboxFloppyVerbose_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxFloppyVerbose.Checked)
                tcpIpSocketInfo.localPort.FloppyVerbose = "V";
            else
                tcpIpSocketInfo.localPort.FloppyVerbose = "";
        }

        private void checkboxPortFloppyVerboseTab0_CheckedChanged(object sender, EventArgs e)
        {
            //string checkboxName = checkboxPortFloppyVerboseTab0.Name;
            //string indexString = checkboxName.Replace("checkboxPortFloppyVerboseTab", "");
            //int tabPageIndex = Int32.Parse(indexString);

            if (checkboxPortFloppyVerboseTab0.Checked)
                listPorts[0].verbose = "V";
            else
                listPorts[0].verbose = "";
        }

        private void checkboxPortFloppyVerboseTab1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxPortFloppyVerboseTab1.Checked)
                listPorts[1].verbose = "V";
            else
                listPorts[1].verbose = "";
        }

        private void checkboxPortFloppyVerboseTab2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxPortFloppyVerboseTab2.Checked)
                listPorts[2].verbose = "V";
            else
                listPorts[2].verbose = "";
        }

        private void checkboxPortFloppyVerboseTab3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxPortFloppyVerboseTab3.Checked)
                listPorts[3].verbose = "V";
            else
                listPorts[3].verbose = "";
        }
        #endregion
        #region browse button handlers

        private void BrowseSDCardDriveHandler(int driveNumber, object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Multiselect = false;
            dlg.InitialDirectory = (tcpIpSocketInfo.localPort.defaultStartDirectory.Replace("/", "\\"));       // InitialDirectory doesn't like forward slashes
            dlg.DefaultExt = ".dsk";

            DialogResult r = dlg.ShowDialog(this);
            if (r == DialogResult.OK)
            {
                //                     textBoxSDCardDrive1
                string textBoxName = $"textBoxSDCardDrive{driveNumber}";
                string pictureBoxName = $"picturBoxTCPIPSDCardDrive{driveNumber}";
                TextBox foundTextBox = Controls.Find(textBoxName, true).FirstOrDefault() as TextBox;
                PictureBox foundPictureBox = Controls.Find(pictureBoxName, true).FirstOrDefault() as PictureBox;

                if (foundTextBox != null && foundPictureBox != null)
                {
                    foundTextBox.Text = dlg.FileName.Replace(tcpIpSocketInfo.localPort.defaultStartDirectory.Replace("/", "\\"), "").TrimStart('\\');
                    tcpIpSocketInfo.localPort.MountSDCardImageFile(foundTextBox.Text, driveNumber);

                    if (tcpIpSocketInfo.localPort.sdCardImageFiles[driveNumber].readOnly)
                        foundPictureBox.Image = Properties.Resources.reddot;
                    else
                        foundPictureBox.Image = Properties.Resources.greendot;
                }
                else
                    MessageBox.Show("could not find control to update!");
            }
        }

        private void buttonBrowseConfigurationFilename_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();

            dlg.InitialDirectory = applicationPath.Replace("/", "\\");       // InitialDirectory doesn't like forward slashes
            dlg.DefaultExt = ".txt";

            DialogResult r = dlg.ShowDialog(this);
            if (r == DialogResult.OK)
            {
                textBoxConfigurationFilename.Text = dlg.FileName;
            }
        }

        private void buttonBrowseLogFilename_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();

            dlg.InitialDirectory = applicationPath.Replace("/", "\\");       // InitialDirectory doesn't like forward slashes
            dlg.DefaultExt = ".xml";

            DialogResult r = dlg.ShowDialog(this);
            if (r == DialogResult.OK)
            {
                textBoxLogFilename.Text = dlg.FileName;
            }
        }

        private void buttonSaveChanges_Click(object sender, EventArgs e)
        {
            if (textBoxConfigurationFilename.Text.Length > 0)
                SaveConfiguration(textBoxConfigurationFilename.Text);
            else
                MessageBox.Show("You must specify a file name int the text box to the left to use this button shortcut");
        }

        #region BrowseSDCardDriveHandlers
        private void buttonBrowseSDCardDrive0_Click(object sender, EventArgs e)
        {
            BrowseSDCardDriveHandler(0, sender, e);
        }

        private void buttonBrowseSDCardDrive1_Click(object sender, EventArgs e)
        {
            BrowseSDCardDriveHandler(1, sender, e);
        }
        #endregion
        private void BrowseTCPIPFloppyDriveHandler(int driveNumber, object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Multiselect = false;
            dlg.InitialDirectory = (tcpIpSocketInfo.localPort.defaultStartDirectory.Replace("/", "\\"));       // InitialDirectory doesn't like forward slashes
            dlg.DefaultExt = ".dsk";

            DialogResult r = dlg.ShowDialog(this);
            if (r == DialogResult.OK)
            {
                //                     textBoxSDCardDrive1
                string textBoxName = $"textBoxTCPIPFloppyDrive{driveNumber}";
                string pictureBoxName = $"pictureBoxTCPIPFLoppyDrive{driveNumber}";
                TextBox foundTextBox = Controls.Find(textBoxName, true).FirstOrDefault() as TextBox;
                PictureBox foundPictureBox = Controls.Find(pictureBoxName, true).FirstOrDefault() as PictureBox;

                if (foundTextBox != null && foundPictureBox != null)
                {
                    foundTextBox.Text = dlg.FileName.Replace(tcpIpSocketInfo.localPort.defaultStartDirectory.Replace("/", "\\"), "").TrimStart('\\');
                    tcpIpSocketInfo.localPort.MountFloppyImageFile(foundTextBox.Text, driveNumber);

                    if (tcpIpSocketInfo.localPort.floppyImageFiles[driveNumber].readOnly)
                        foundPictureBox.Image = Properties.Resources.reddot;
                    else
                        foundPictureBox.Image = Properties.Resources.greendot;
                }
                else
                    MessageBox.Show("could not find control to update!");
            }
        }
        #region BrowseTCPIPFloppyDriveHandlers
        private void buttonBrowseTCPIPFloppyDrive0_Click(object sender, EventArgs e)
        {
            BrowseTCPIPFloppyDriveHandler(0, sender, e);
        }

        private void buttonBrowseTCPIPFloppyDrive1_Click(object sender, EventArgs e)
        {
            BrowseTCPIPFloppyDriveHandler(1, sender, e);
        }

        private void buttonBrowseTCPIPFloppyDrive2_Click(object sender, EventArgs e)
        {
            BrowseTCPIPFloppyDriveHandler(2, sender, e);
        }

        private void buttonBrowseTCPIPFloppyDrive3_Click(object sender, EventArgs e)
        {
            BrowseTCPIPFloppyDriveHandler(3, sender, e);
        }
        #endregion
        private void BrowsePortFloppyDriveTabHandler(int tabIndex, int driveNumber, object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Multiselect = false;
            dlg.InitialDirectory = (tcpIpSocketInfo.localPort.defaultStartDirectory.Replace("/", "\\"));       // InitialDirectory doesn't like forward slashes
            dlg.DefaultExt = ".dsk";

            DialogResult r = dlg.ShowDialog(this);
            if (r == DialogResult.OK)
            {
                //                     textBoxSDCardDrive1
                string textBoxName = $"textBoxPortFloppyDrive{driveNumber}Tab{tabIndex}";
                string pictureBoxName = $"pictureBoxPortFloppyDrive{driveNumber.ToString()}Tab{tabIndex}";
                TextBox foundTextBox = Controls.Find(textBoxName, true).FirstOrDefault() as TextBox;
                PictureBox foundPictureBox = Controls.Find(pictureBoxName, true).FirstOrDefault() as PictureBox;

                if (foundTextBox != null && foundPictureBox != null)
                {
                    foundTextBox.Text = dlg.FileName.Replace(tcpIpSocketInfo.localPort.defaultStartDirectory.Replace("/", "\\"), "").TrimStart('\\');
                    listPorts[tabIndex].MountImageFile(foundTextBox.Text, driveNumber, tabIndex);

                    if (listPorts[tabIndex].floppyImageFiles[driveNumber].readOnly)
                        foundPictureBox.Image = Properties.Resources.reddot;
                    else
                        foundPictureBox.Image = Properties.Resources.greendot;
                }
                else
                    MessageBox.Show("could not find control to update!");
            }
        }
        #region BrowsePortFloppyDriveTabHandlers
        private void buttonBrowsePortFloppyDrive0Tab0_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(0, 0, sender, e);
        }

        private void buttonBrowsePortFloppyDrive1Tab0_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(0, 1, sender, e);
        }

        private void buttonBrowsePortFloppyDrive2Tab0_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(0, 2, sender, e);
        }

        private void buttonBrowsePortFloppyDrive3Tab0_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(0, 3, sender, e);
        }

        private void buttonBrowsePortFloppyDrive0Tab1_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(1, 0, sender, e);
        }

        private void buttonBrowsePortFloppyDrive1Tab1_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(1, 1, sender, e);
        }

        private void buttonBrowsePortFloppyDrive2Tab1_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(1, 2, sender, e);
        }

        private void buttonBrowsePortFloppyDrive3Tab1_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(1, 3, sender, e);
        }

        private void buttonBrowsePortFloppyDrive0Tab2_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(2, 0, sender, e);
        }

        private void buttonBrowsePortFloppyDrive1Tab2_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(2, 1, sender, e);
        }

        private void buttonBrowsePortFloppyDrive2Tab2_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(2, 2, sender, e);
        }

        private void buttonBrowsePortFloppyDrive3Tab2_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(2, 3, sender, e);
        }

        private void buttonBrowsePortFloppyDrive0Tab3_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(3, 0, sender, e);
        }

        private void buttonBrowsePortFloppyDrive1Tab3_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(3, 1, sender, e);
        }

        private void buttonBrowsePortFloppyDrive2Tab3_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(3, 2, sender, e);
        }

        private void buttonBrowsePortFloppyDrive3Tab3_Click(object sender, EventArgs e)
        {
            BrowsePortFloppyDriveTabHandler(3, 3, sender, e);
        }
        #endregion
        #endregion

        // Helper method to add XML elements
        static void AddXmlElement(XmlDocument doc, XmlElement parent, string name, string value)
        {
            XmlElement elem = doc.CreateElement(name);
            elem.InnerText = value;
            parent.AppendChild(elem);
        }

        private void SaveConfiguration(string fileName)
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\FLEXWire");

            if (textBoxConfigurationFilename.TextLength > 0)
            {
                key.SetValue("Configuration File", textBoxConfigurationFilename.Text);
                key.Close();
            }
            else
            {
                key.SetValue("Configuration File", "FNCONFIG.xml");
                key.Close();
            }

            XmlDocument xmlDoc = new XmlDocument();

            // Create XML declaration
            XmlDeclaration xmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDoc.AppendChild(xmlDeclaration);

            // Create the root element <Configuration>
            XmlElement root = xmlDoc.CreateElement("Configuration");
            xmlDoc.AppendChild(root);

            #region generic
            // Add the LogReads element to the root
            XmlElement rootDataLogReadsElement = xmlDoc.CreateElement("LogReads");
            rootDataLogReadsElement.InnerText = checkBoxLogReads.Checked ? "T" : "F";
            root.AppendChild(rootDataLogReadsElement);

            // Add the LogWrites element to the root
            XmlElement rootDataLogWritesElement = xmlDoc.CreateElement("LogWrites");
            rootDataLogWritesElement.InnerText = checkBoxLogWrites.Checked ? "T" : "F";
            root.AppendChild(rootDataLogWritesElement);

            // Add the LogFilename element to the root
            XmlElement rootDataLogFilenameElement = xmlDoc.CreateElement("LogFilename");
            rootDataLogFilenameElement.InnerText = textBoxLogFilename.Text;
            root.AppendChild(rootDataLogFilenameElement);

            // Add the Configuration Filename element to the root
            XmlElement rootDataConfigurationFilenameElement = xmlDoc.CreateElement("ConfigurationFilename");
            rootDataConfigurationFilenameElement.InnerText = textBoxConfigurationFilename.Text;
            root.AppendChild(rootDataConfigurationFilenameElement);

            // Add the Shutdown element to the root
            XmlElement rootShutdownElement = xmlDoc.CreateElement("Shutdown");
            rootShutdownElement.InnerText = checkBoxShutdown.Checked ? "T" : "F";
            root.AppendChild(rootShutdownElement);

            #endregion
            #region TCPIP
            XmlComment commentTCPIP = xmlDoc.CreateComment("these are for the SD Cards and floppy disk images used by the wifi pico driver");
            xmlDoc.DocumentElement.AppendChild(commentTCPIP);

            #region TCP IP Generic section
            // Add the TCPIP element to the root
            XmlElement tcpipElement = xmlDoc.CreateElement("TCPIP");
            tcpipElement.InnerText = "";
            root.AppendChild(tcpipElement);

            // Add the UseTCPIP element to the TCPIP element
            XmlElement tcpipEnable = xmlDoc.CreateElement("UseTCPIP");
            tcpipEnable.InnerText = "T";
            tcpipElement.AppendChild(tcpipEnable);

            // Add the SERVERPORT element to the Tcpip element
            XmlElement tcpipServerPortElement = xmlDoc.CreateElement("SERVER_PORT");
            tcpipServerPortElement.InnerText = textBoxServerPort.Text;
            tcpipElement.AppendChild(tcpipServerPortElement);

            // Add the DefaultDirectory element to the Tcpip element
            XmlElement tcpipDefaultDirectoryElement = xmlDoc.CreateElement("DefaultDirectory");
            tcpipDefaultDirectoryElement.InnerText = tcpIpSocketInfo.localPort.defaultStartDirectory;
            tcpipElement.AppendChild(tcpipDefaultDirectoryElement);
            #endregion
            #region TCP IP Floppy section
            // Add the Floppy element to the Tcpip element
            XmlElement tcpipFloppyElement = xmlDoc.CreateElement("Floppy");
            tcpipFloppyElement.InnerText = "";
            tcpipElement.AppendChild(tcpipFloppyElement);

            // Add the FloppyVerbose element to the TcpipFloppy element
            XmlElement tcpipFloppyVerboseElement = xmlDoc.CreateElement("Verbose");
            tcpipFloppyVerboseElement.InnerText = "V";
            tcpipFloppyElement.AppendChild(tcpipFloppyVerboseElement);

            // Add the FloppImageFiles element to the TcpipFloppy element
            XmlElement tcpipFloppyImageFilesElement = xmlDoc.CreateElement("ImageFiles");
            tcpipFloppyImageFilesElement.InnerText = "";
            tcpipFloppyElement.AppendChild(tcpipFloppyImageFilesElement);

            for (int i = 0; i < 4; i++)
            {
                if (tcpIpSocketInfo.localPort.floppyImageFiles[i] != null)
                {
                    // this would also work
                    // tcpipFloppyImageFileElement.InnerText = mf_tcpipFloppyDrives[i].textBoxFloppyDrive.Text.Replace(extension, ""); 

                    string extension = Path.GetExtension(tcpIpSocketInfo.localPort.floppyImageFiles[i].Name);

                    XmlElement tcpipFloppyImageFileElement = xmlDoc.CreateElement("ImageFile");
                    tcpipFloppyImageFileElement.InnerText = tcpIpSocketInfo.localPort.floppyImageFiles[i].Name.Replace(extension, "");
                    tcpipFloppyImageFilesElement.AppendChild(tcpipFloppyImageFileElement);
                }
            }
            #endregion
            #region TCP IP SD Card section
            // Add the SDCard element to the Tcpip element
            XmlElement tcpipSDCardElement = xmlDoc.CreateElement("SDCard");
            tcpipSDCardElement.InnerText = "";
            tcpipElement.AppendChild(tcpipSDCardElement);

            // Add the SDCard element to the TcpipSDCard element
            XmlElement tcpipSDCardVerboseElement = xmlDoc.CreateElement("Verbose");
            tcpipSDCardVerboseElement.InnerText = "V";
            tcpipSDCardElement.AppendChild(tcpipSDCardVerboseElement);

            // Add the SDCardImageFiles element to the TcpipSDCard element
            XmlElement tcpipSDCardImageFilesElement = xmlDoc.CreateElement("ImageFiles");
            tcpipSDCardImageFilesElement.InnerText = "";
            tcpipSDCardElement.AppendChild(tcpipSDCardImageFilesElement);

            for (int i = 0; i < 4; i++)
            {
                if (tcpIpSocketInfo.localPort.sdCardImageFiles[i] != null)
                {
                    XmlElement tcpipSDCardImageFileElement = xmlDoc.CreateElement("ImageFile");
                    string extension = Path.GetExtension(tcpIpSocketInfo.localPort.sdCardImageFiles[i].Name);
                    tcpipSDCardImageFileElement.InnerText = tcpIpSocketInfo.localPort.sdCardImageFiles[i].Name.Replace(extension, "");
                    tcpipSDCardImageFilesElement.AppendChild(tcpipSDCardImageFileElement);
                }
            }
            #endregion
            #endregion
            #region RS232 Ports
            // ----------- Ports --------------------------
            // Add the Ports element to the root

            XmlComment commentRS232 = xmlDoc.CreateComment("these are for the floppy disk images used by the rs232 ports");
            xmlDoc.DocumentElement.AppendChild(commentRS232);

            XmlElement portsElement = xmlDoc.CreateElement("Ports");
            portsElement.InnerText = "";
            root.AppendChild(portsElement);

            #region Individual Ports Generic section
            for (int i = 0; i < listPorts.Count; i++)
            {
                XmlElement portElement = xmlDoc.CreateElement("Port");
                portElement.InnerText = "";
                portElement.SetAttribute("num", $"{listPorts[i].port}");
                portsElement.AppendChild(portElement);

                XmlElement portRate = xmlDoc.CreateElement("Rate");
                portRate.InnerText = "19200";
                portElement.AppendChild(portRate);

                XmlElement portCpuSpeed = xmlDoc.CreateElement("CpuSpeed");
                portCpuSpeed.InnerText = "F";
                portElement.AppendChild(portCpuSpeed);

                // Add the FloppyVerbose element to the Port element
                XmlElement portVerboseElement = xmlDoc.CreateElement("Verbose");
                portVerboseElement.InnerText = "V";
                portElement.AppendChild(portVerboseElement);

                // Add the FloppyAutoMount element to the Port element
                XmlElement portAutoMountElement = xmlDoc.CreateElement("AutoMount");
                portAutoMountElement.InnerText = "T";
                portElement.AppendChild(portAutoMountElement);

                // Add the DefaultDirectory element to the port element
                XmlElement portDefaultDirectoryElement = xmlDoc.CreateElement("DefaultDirectory");
                portDefaultDirectoryElement.InnerText = listPorts[i].defaultStartDirectory;
                portElement.AppendChild(portDefaultDirectoryElement);

                #region Individual Ports Floppy section
                // Add the FloppImageFiles element to the TcpipFloppy element
                XmlElement portImageFilesElement = xmlDoc.CreateElement("ImageFiles");
                portImageFilesElement.InnerText = "";
                portElement.AppendChild(portImageFilesElement);

                for (int j = 0; j < 4; j++)
                {
                    if (listPorts[i].floppyImageFiles[j] != null)
                    {
                        string extension = Path.GetExtension(tcpIpSocketInfo.localPort.floppyImageFiles[i].Name);

                        XmlElement portFloppyImageFileElement = xmlDoc.CreateElement("ImageFile");
                        if (listPorts[i].floppyImageFiles[j].Name.Length > 0)
                            portFloppyImageFileElement.InnerText= portFloppyImageFileElement.InnerText = listPorts[i].floppyImageFiles[j].Name.Replace(extension, "");
                        portImageFilesElement.AppendChild(portFloppyImageFileElement);
                    }
                }
                #endregion
            }
            #endregion
            #endregion

            // save our current working directory so we can default to the execution directory for config file save if need be..
            string currentWorkingDirectory = Directory.GetCurrentDirectory();
            string applicationPathDirectory = Application.ExecutablePath.Replace($"{Application.ProductName}.exe", "");

            Directory.SetCurrentDirectory(applicationPathDirectory);

            // Save the XML document to a file
            xmlDoc.Save(fileName);

            Directory.SetCurrentDirectory(currentWorkingDirectory);
        }

        private void buttonSetComParametersTab0_Click(object sender, EventArgs e)
        {
            COMParameters dlg = new COMParameters();
            dlg.selectedPort = $"COM{listPorts[0].port.ToString()}";
            dlg.selectedRate = $"{listPorts[0].rate.ToString()}";
            DialogResult r = dlg.ShowDialog();
            if (r == DialogResult.OK)
            {
                if (listPorts[0].sp.IsOpen)
                {
                    listPorts[0].sp.Close();
                    listPorts[0].sp = null;
                }

                listPorts[0].port = Int32.Parse(dlg.selectedPort.Replace("COM", ""));
                listPorts[0].rate = Int32.Parse(dlg.selectedRate);
                tabControlPorts.TabPages[0].Text = dlg.selectedPort;

                listPorts[0].sp = new SerialPort("COM" + listPorts[0].port.ToString(), listPorts[0].rate, Parity.None, 8, StopBits.One);

                listPorts[0].sp.ReadBufferSize = 32768;
                listPorts[0].sp.WriteBufferSize = 32768;
                try
                {
                    listPorts[0].sp.Open();
                }
                catch (Exception e0)
                {
                    WriteLineToOutputWindow(e0.Message);
                    try
                    {
                        listPorts[0].sp.Close();
                        listPorts[0].sp.Open();
                    }
                    catch (Exception e1)
                    {
                        WriteLineToOutputWindow(e1.Message);
                    }
                }
            }
        }

        private void buttonSetComParametersTab1_Click(object sender, EventArgs e)
        {
            COMParameters dlg = new COMParameters();
            dlg.selectedPort = $"COM{listPorts[1].port.ToString()}";
            dlg.selectedRate = $"{listPorts[1].rate.ToString()}";
            DialogResult r = dlg.ShowDialog();
            if (r == DialogResult.OK)
            {
                listPorts[1].port = Int32.Parse(dlg.selectedPort.Replace("COM", ""));
                listPorts[1].rate = Int32.Parse(dlg.selectedRate);
                tabControlPorts.TabPages[1].Text = dlg.selectedPort;
            }
        }

        private void buttonSetComParametersTab2_Click(object sender, EventArgs e)
        {
            COMParameters dlg = new COMParameters();
            dlg.selectedPort = $"COM{listPorts[2].port.ToString()}";
            dlg.selectedRate = $"{listPorts[2].rate.ToString()}";
            DialogResult r = dlg.ShowDialog();
            if (r == DialogResult.OK)
            {
                listPorts[2].port = Int32.Parse(dlg.selectedPort.Replace("COM", ""));
                listPorts[2].rate = Int32.Parse(dlg.selectedRate);
                tabControlPorts.TabPages[2].Text = dlg.selectedPort;
            }
        }

        private void buttonSetComParametersTab3_Click(object sender, EventArgs e)
        {
            COMParameters dlg = new COMParameters();
            dlg.selectedPort = $"COM{listPorts[3].port.ToString()}";
            dlg.selectedRate = $"{listPorts[3].rate.ToString()}";
            DialogResult r = dlg.ShowDialog();
            if (r == DialogResult.OK)
            {
                listPorts[3].port = Int32.Parse(dlg.selectedPort.Replace("COM", ""));
                listPorts[3].rate = Int32.Parse(dlg.selectedRate);
                tabControlPorts.TabPages[3].Text = dlg.selectedPort;
            }
        }

        private void buttonRemovePort0_Click(object sender, EventArgs e)
        {
            //tabControlPorts.TabPages[0].vis
        }

        private void buttonRemovePort1_Click(object sender, EventArgs e)
        {

        }

        private void buttonRemovePort2_Click(object sender, EventArgs e)
        {

        }

        private void buttonRemovePort3_Click(object sender, EventArgs e)
        {

        }

        private void buttonAddPort_Click(object sender, EventArgs e)
        {

        }

        private void resyncPort (Port serialPort)
        {
            serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);
            //serialPort.SetState((int)CONNECTION_STATE.SYNCRONIZING);
        }

        private void buttonResyncTab0_Click(object sender, EventArgs e)
        {
            resyncPort(listPorts[0]);
        }

        private void buttonResyncTab1_Click(object sender, EventArgs e)
        {
            resyncPort(listPorts[1]);
        }

        private void buttonResyncTab2_Click(object sender, EventArgs e)
        {
            resyncPort(listPorts[2]);
        }

        private void buttonResyncTab3_Click(object sender, EventArgs e)
        {
            resyncPort(listPorts[3]);
        }

        private void copyOutputWindowsConetntsToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void clearOutputWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            outputWindow.Clear();
        }

        private void outputWindow_MouseDown(object sender, MouseEventArgs e)
        {
            // Show the context menu on right-click
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStripOutputWindow.Show(outputWindow, e.Location);
            }
        }
    }
}
