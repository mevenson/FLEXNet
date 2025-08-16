using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using System.IO;
using System.IO.Ports;

using System.Reflection;
using System.Net;
using System.Net.Sockets;

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

namespace FLEXNetSharp
{
    #region classes
    public class DriveInfo
    {
        public int mode;
        public string MountedFilename;
        public byte[] cNumberOfSectorsPerTrack = new byte[1];
        public long NumberOfBytesPerTrack;
        public long NumberOfSectorsPerTrack;
        public int bytesPerRead = 1;
    }

    public class ImageFile
    {
        public string Name;
        public bool readOnly;
        public Stream stream;
        public DriveInfo driveInfo;
    }

    public class TcpIpSocketInfo
    {
        public bool enabled;
        public int serverPort;
        public Network localPort = new Network();
    }
    #endregion

    class Program
    {
        #region variables
        const byte FDC_BUSY       = 0x01;
        const byte FDC_DRQ        = 0x02;
        const byte FDC_TRKZ       = 0x04;
        const byte FDC_CRCERR     = 0x08;
        const byte FDC_SEEKERR    = 0x10;
        const byte FDC_RNF        = 0x10;
        const byte FDC_HDLOADED   = 0x20;
        const byte FDC_WRTPROTECT = 0x40;
        const byte FDC_NOTREADY   = 0x80;

        // set default to not log - use the config file to change this (T, t, Y, y, 1 will change it)
        static bool logReads  = false;
        static bool logWrites = false;

        static string logfileName = "ReadWrite.log";
        static StreamWriter logFileStream = null;

        static string shutDown;
        static bool done = false;

        //long            NextStatusUpdate = 0;

        static TcpIpSocketInfo tcpIpSocketInfo = null;
        static IPAddress localAddr = IPAddress.Any;                // Listen on all network interfaces
        static bool usingTCPIP = false;

        static List<Ports> listPorts = new List<Ports>();
        static ArrayList ports = new ArrayList();

        static CultureInfo ci = new CultureInfo("en-us");

        public static Version version = new Version();
        #endregion

        static void HandleCommand()
        {
            ConsoleKeyInfo ki = Console.ReadKey();
            if ((ki.Modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt)
            {
                switch (ki.Key)
                {
                    case ConsoleKey.F1:                 // re-initialize the connection parameters from config file
                        InitializeFromConfigFile();
                        return;

                    case ConsoleKey.F2:
                        return;

                    case ConsoleKey.F3:
                        return;

                    case ConsoleKey.F4:
                        return;

                    case ConsoleKey.F5:
                        return;

                    case ConsoleKey.F6:
                        //status = (RS232Error) clsConnection[FocusWindow].rsPort->Rts( !clsConnection[FocusWindow].rsPort->Rts() );
                        //*StatusLine << "Toggle RTS returns: ";
                        //if ( status >= 0 ) 
                        //{
                        //    *StatusLine << itoa( status, buffer, 10 );
                        //    return;
                        //}
                        break;

                    case ConsoleKey.F7:
                        //status = (RS232Error) clsConnection[FocusWindow].rsPort->Dtr( !clsConnection[FocusWindow].rsPort->Dtr() );
                        //*StatusLine << "Toggle Dtr returns: ";
                        //if ( status >= 0 ) 
                        //{
                        //    *StatusLine << itoa( status, buffer, 10 );
                        //    return;
                        //}
                        break;

                    case ConsoleKey.F8:
                        //status = (RS232Error) clsConnection[FocusWindow].rsPort->FlushRXBuffer();
                        //*StatusLine << "Flush RX Buffer returns: ";
                        break;

                    case ConsoleKey.F9:
                        //status = (RS232Error) clsConnection[FocusWindow].rsPort->FlushTXBuffer();
                        //*StatusLine << "Flush TX Buffer returns: ";
                        break;

                    case ConsoleKey.F10:
                        done = true;
                        return;

                    default:
                        return;
                }
            }
            else if ((ki.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
            {
                if (!usingTCPIP)
                {
                    switch (ki.Key)
                    {
                        case ConsoleKey.F3:
                            // go backwards

                            foreach (Ports serialPort in listPorts)
                            {
                                switch (serialPort.sp.BaudRate)
                                {
                                    case 110:
                                        serialPort.sp.BaudRate = 115200;
                                        break;
                                    case 1200:
                                        serialPort.sp.BaudRate = 110;
                                        break;
                                    case 2400:
                                        serialPort.sp.BaudRate = 1200;
                                        break;
                                    case 4800:
                                        serialPort.sp.BaudRate = 2400;
                                        break;
                                    case 9600:
                                        serialPort.sp.BaudRate = 4800;
                                        break;
                                    case 19200:
                                        serialPort.sp.BaudRate = 9600;
                                        break;
                                    case 38400:
                                        serialPort.sp.BaudRate = 19200;
                                        break;
                                    case 57600:
                                        serialPort.sp.BaudRate = 38400;
                                        break;
                                    case 76800:
                                        serialPort.sp.BaudRate = 57600;
                                        break;
                                    case 115200:
                                        serialPort.sp.BaudRate = 76800;
                                        break;
                                }
                            }
                            Console.WriteLine(string.Format("Baud Rates changed to: {0}", listPorts[0].sp.BaudRate));
                            break;
                    }
                }
            }
            else if ((ki.Modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift)
            {
                done = false;
            }
            else
            {
                switch (ki.Key)
                {
                    case ConsoleKey.F1:
                        //Helping = !Helping;
                        //UserWindow->Clear();
                        //if (Helping)
                        //    DrawHelp();
                        return;

                    case ConsoleKey.F2:
                        //do
                        //    FocusWindow = ++FocusWindow % WINDOW_COUNT;
                        //while (clsConnection[FocusWindow].twWindows == 0);
                        //if (!Helping)
                        //    UserWindow->Clear();
                        //clsConnection[FocusWindow].twWindows->Goto();
                        return;

                    case ConsoleKey.F3:

                        if (!usingTCPIP)
                        {
                            // only does the first port for now

                            foreach (Ports serialPort in listPorts)
                            {
                                switch (serialPort.sp.BaudRate)
                                {
                                    // go forwards

                                    case 110:
                                        serialPort.sp.BaudRate = 1200;
                                        break;
                                    case 1200:
                                        serialPort.sp.BaudRate = 2400;
                                        break;
                                    case 2400:
                                        serialPort.sp.BaudRate = 4800;
                                        break;
                                    case 4800:
                                        serialPort.sp.BaudRate = 9600;
                                        break;
                                    case 9600:
                                        serialPort.sp.BaudRate = 19200;
                                        break;
                                    case 19200:
                                        serialPort.sp.BaudRate = 38400;
                                        break;
                                    case 38400:
                                        serialPort.sp.BaudRate = 57600;
                                        break;
                                    case 57600:
                                        serialPort.sp.BaudRate = 76800;
                                        break;
                                    case 76800:
                                        serialPort.sp.BaudRate = 115200;
                                        break;
                                    case 115200:
                                        serialPort.sp.BaudRate = 110;
                                        break;
                                }
                            }
                            Console.WriteLine(string.Format("Baud Rates changed to: {0}", listPorts[0].sp.BaudRate));
                        }

                        //clsConnection[FocusWindow].nReading = !clsConnection[FocusWindow].nReading;
                        //*StatusLine << "Window "
                        //            << itoa(FocusWindow, buffer, 10);
                        //*StatusLine << " nReading flag is "
                        //            << itoa(clsConnection[FocusWindow].nReading, buffer, 10);
                        return;

                    case ConsoleKey.F4:
                        //ReadLine("New baud rate:", buffer, 10);
                        //if (buffer[0] != 0x00)
                        //{
                        //    status = clsConnection[FocusWindow].rsPort->Set(atol(buffer));
                        //    *StatusLine << "Set baud rate to "
                        //                << ltoa(atol(buffer), buffer, 10)
                        //                << " returns status of: ";
                        //    if (status == RS232_SUCCESS)
                        //        *clsConnection[FocusWindow].twWindows << "baud rate changed to : " << buffer << "\n";
                        //}
                        //else
                        //{
                        //    *clsConnection[FocusWindow].twWindows << "baud rate unchanged \n";
                        //    return;
                        //}
                        break;

                    case ConsoleKey.F5:
                        //ReadLine("New parity:", buffer, 10);
                        //if (buffer[0] != 0x00)
                        //{
                        //    status = clsConnection[FocusWindow].rsPort->Set(UNCHANGED, buffer[0]);
                        //    *StatusLine << "Set parity to "
                        //                << buffer[0]
                        //                << " returns status of: ";
                        //}
                        //else
                        //{
                        //    *clsConnection[FocusWindow].twWindows << "parity unchanged \n";
                        //    return;
                        //}
                        break;

                    case ConsoleKey.F6:
                        //ReadLine("New word length:", buffer, 10);
                        //if (buffer[0] != 0x00)
                        //{
                        //    status = clsConnection[FocusWindow].rsPort->Set(UNCHANGED, UNCHANGED, atoi(buffer));
                        //    *StatusLine << "Set word length to "
                        //                << itoa(atoi(buffer), buffer, 10)
                        //                << " returns status of: ";
                        //}
                        //else
                        //{
                        //    *clsConnection[FocusWindow].twWindows << "word length unchanged \n";
                        //    return;
                        //}
                        break;

                    case ConsoleKey.F7:
                        //ReadLine("New stop bits:", buffer, 10);
                        //if (buffer[0] != 0x00)
                        //{
                        //    status = clsConnection[FocusWindow].rsPort->Set(UNCHANGED,
                        //                        UNCHANGED,
                        //                        UNCHANGED,
                        //                        atoi(buffer));
                        //    *StatusLine << "Set stop bits to "
                        //                << itoa(atoi(buffer), buffer, 10)
                        //                << " returns status of: ";
                        //}
                        //else
                        //{
                        //    *clsConnection[FocusWindow].twWindows << "stop bits unchanged \n";
                        //    return;
                        //}
                        break;

                    case ConsoleKey.F8:
                        //clsConnection[nPort].g_displaySectorData = !clsConnection[nPort].g_displaySectorData;
                        //*StatusLine << "Sector Display is : " << (clsConnection[nPort].g_displaySectorData ? "ON" : "OFF") << " Status: ";
                        return;

                    case ConsoleKey.F9:
                        if (!usingTCPIP)
                        {
                            ReMountAllPorts();
                            Console.WriteLine("All mounted drives have been remounted");
                        }
                        return;

                    case ConsoleKey.F10:
                        if (!usingTCPIP)
                        {
                            foreach (Ports serialPort in listPorts)
                            {
                                serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);
                                Console.WriteLine("Serial Port " + serialPort.port.ToString() + " is reset to NOT CONNECTED");
                            }
                        }
                        return;

                }
            }
            //    *StatusLine << clsConnection[FocusWindow].rsPort->ErrorName( status );
        }

        static void ReMountAllPorts()
        {
            foreach (Ports serialPort in listPorts)
            {
                if (serialPort.commandFilename != null && serialPort.commandFilename.Length > 0)
                {
                    serialPort.imageFiles[serialPort.currentDrive].stream.Close();
                    byte status = serialPort.MountImageFile(serialPort.commandFilename, serialPort.currentDrive);
                }

            }
        }

        #region setup functions
        static void ParseConfigFile()
        {
            string ApplicationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            XmlDocument doc = new XmlDocument();
            doc.Load(Path.Combine(ApplicationPath, "fnconfig.xml"));

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
                                Console.WriteLine("Invalid port specified in config file for TCP IP port");
                            }
                            else
                                usingTCPIP = true;
                        }
                        else if (paramters.Name == "DefaultDirectory")
                        {
                            tcpIpSocketInfo.localPort.defaultStartDirectory = paramters.InnerText;
                        }
                        else if (paramters.Name == "Floppy")
                        {
                            foreach (XmlNode floppyNode in paramters.ChildNodes)
                            {
                                if (floppyNode.Name == "ImageFiles")
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
                                if (sdcardNode.Name == "ImageFiles")
                                {
                                    int index = 0;
                                    foreach (XmlNode imageFile in sdcardNode.ChildNodes)
                                    {
                                        if (imageFile.Name == "ImageFile")
                                        {
                                            tcpIpSocketInfo.localPort.sdCardImageFiles[index] = new ImageFile();
                                            tcpIpSocketInfo.localPort.sdCardImageFiles[index].Name = imageFile.InnerText;
                                        }
                                        else if (imageFile.Name == "Format")
                                        {
                                            tcpIpSocketInfo.localPort.SDCard_Format = imageFile.InnerText;
                                        }
                                        else if (imageFile.Name == "RAW")
                                        {
                                            string isRAW = imageFile.InnerText;

                                            tcpIpSocketInfo.localPort.SDCardIsRaw = isRAW.StartsWith("Y") || isRAW.StartsWith("T") || isRAW.StartsWith("1");
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
                        Ports p = new Ports();
                        p.defaultStartDirectory = Directory.GetCurrentDirectory();
                        p.port = Convert.ToInt32(portNode.Attributes["num"].Value);
                        foreach (XmlNode paramters in portNode.ChildNodes)
                        {
                            if (paramters.Name == "Rate")
                            {
                                p.rate = Convert.ToInt32(paramters.InnerText);
                            }
                            else if (paramters.Name == "CpuSpeed")
                            {
                                p.speed = paramters.InnerText;
                            }
                            else if (paramters.Name == "Verbose")
                            {
                                p.verbose = paramters.InnerText;
                            }
                            else if (paramters.Name == "AutoMount")
                            {
                                p.autoMount = paramters.InnerText;
                            }
                            else if (paramters.Name == "DefaultDirectory")
                            {
                                p.defaultStartDirectory = paramters.InnerText;
                            }
                            else if (paramters.Name == "ImageFiles")
                            {
                                int index = 0;
                                foreach (XmlNode imageFile in paramters.ChildNodes)
                                {
                                    if (imageFile.Name == "ImageFile")
                                    {
                                        p.imageFiles[index] = new ImageFile();
                                        p.imageFiles[index].Name = imageFile.InnerText;
                                        index++;
                                    }
                                }
                            }
                        }
                        p.currentWorkingDirectory = p.defaultStartDirectory;

                        bool portAlreadyExists = false;

                        for (int i = 0; i < listPorts.Count; i++)
                        {
                            // we already have this port in the list - just update it

                            if (listPorts[i].port == p.port)
                            {
                                portAlreadyExists = true;

                                listPorts[i].defaultStartDirectory = p.defaultStartDirectory;
                                listPorts[i].rate = p.rate;
                                listPorts[i].speed = p.speed;
                                listPorts[i].verbose = p.verbose;
                                listPorts[i].autoMount = p.autoMount;

                                int imageFileIndex = 0;
                                foreach (ImageFile imageFile in p.imageFiles)
                                {
                                    try
                                    {
                                        if (imageFile != null)
                                            listPorts[i].imageFiles[imageFileIndex++].Name = imageFile.Name;
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (!portAlreadyExists)
                            listPorts.Add(p);
                    }
                }
                else if (node.Name == "Shutdown")
                {
                    shutDown = node.InnerText;
                }
                else if (node.Name == "LogReads")
                {
                    if (node.InnerText.StartsWith("T") || node.InnerText.StartsWith("t") || node.InnerText.StartsWith("Y") || node.InnerText.StartsWith("y") || node.InnerText.StartsWith("1"));
                    logReads = true;
                }
                else if (node.Name == "LogWrites")
                {
                    if (node.InnerText.StartsWith("T") || node.InnerText.StartsWith("t") || node.InnerText.StartsWith("Y") || node.InnerText.StartsWith("y") || node.InnerText.StartsWith("1"));
                    logWrites = true;
                }
            }
        }

        static void InitializeFromConfigFile()
        {
            Console.WriteLine("FLEXNet version 5.0:1");

            ParseConfigFile();

            foreach (Ports serialPort in listPorts)
            {
                int nIndex = 0;

                if (serialPort.sp != null)
                {
                    // if we come in here with a non-null value for serialPort.sp that means that we have a COM port open for this logical serial port
                    // we must close it before we can open it again or open another.

                    serialPort.sp.Close();
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
                    Console.WriteLine(e.Message);
                    try
                    {
                        serialPort.sp.Close();
                        serialPort.sp.Open();
                    }
                    catch (Exception e1)
                    {
                        Console.WriteLine(e1.Message);
                    }
                }

                serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);

                foreach (ImageFile imageFile in serialPort.imageFiles)
                {
                    if (imageFile != null)
                    {
                        if (imageFile.Name.Length > 0)
                            serialPort.MountImageFile(imageFile.Name + ".DSK", nIndex);
                        nIndex++;
                    }
                }

                Console.WriteLine(string.Format("COM{0} parameters:", serialPort.port));
                Console.WriteLine(string.Format("    Rate:              {0}", serialPort.rate));
                Console.WriteLine(string.Format("    CpuSpeed:          {0}", serialPort.speed));
                Console.WriteLine(string.Format("    Verbose:           {0}", serialPort.verbose));
                Console.WriteLine(string.Format("    AutoMount:         {0}", serialPort.autoMount));
                Console.WriteLine(string.Format("    DefaultDirectory   {0}", serialPort.defaultStartDirectory));
                Console.WriteLine(string.Format("    ImageFiles"));
                for (int imageFileIndex = 0; imageFileIndex < serialPort.imageFiles.Length; imageFileIndex++)
                {
                    if (serialPort.imageFiles[imageFileIndex] != null)
                    {
                        Console.WriteLine(string.Format("        {0} - {1}", imageFileIndex, serialPort.imageFiles[imageFileIndex].Name));
                    }
                }
                Console.WriteLine(string.Format("    Current Working Directory: {0}", serialPort.currentWorkingDirectory));

                serialPort.sp.DtrEnable = true;
                serialPort.sp.RtsEnable = true;
            }

            if (usingTCPIP)
            {
                if (tcpIpSocketInfo != null)
                {
                    int nIndex = 0;

                    Console.WriteLine($"Openning listening port: {tcpIpSocketInfo.serverPort}");
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
                                tcpIpSocketInfo.localPort.MountSDCardImageFile(imageFile.Name + ".DSK", nIndex);

                            nIndex++;
                        }
                    }
                }
            }
        }
        #endregion
        #region ConnectioStates
        static void StateConnectionStateNotConnected(Ports serialPort, int c)
        {
            if (c == 0x55)
            {
                serialPort.WriteByte((byte)0x55);
                serialPort.SetState((int)CONNECTION_STATE.SYNCRONIZING);
            }
        }

        static void StateConnectionStateSynchronizing(Ports serialPort, int c)
        {
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

        static void StateConnectionStateConnected(Ports serialPort, int c)
        {
            if (c == '?')
            {
                // Query Current Directory

                serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                Console.Write(serialPort.currentWorkingDirectory);
                serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);
                Console.Write("\n");

                serialPort.sp.Write(serialPort.currentWorkingDirectory);
                serialPort.WriteByte((byte)0x0D, false);
                serialPort.WriteByte((byte)0x06);
            }
            else if (c == 'S')
            {
                // 'S'end Sector Request
                serialPort.imageFiles[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.S_MODE;
                serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
            }
            else if (c == 'R')
            {
                // 'R'eceive Sector Request

                serialPort.imageFiles[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.R_MODE;
                serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
            }
            else if (c == 'E')
            {
                // Exit

                serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);
                serialPort.WriteByte((byte)0x06);
                if (shutDown == "T")
                    done = true;
            }
            else if (c == 'Q')          // Quick Check for active connection
            {
                serialPort.WriteByte((byte)0x06);
            }
            else if (c == 'M')          // Mount drive image
            {
                serialPort.commandFilename = "";
                serialPort.SetState((int)CONNECTION_STATE.MOUNT_GETFILENAME);
            }
            else if (c == 'D')
            {
                // Delete file command

                serialPort.commandFilename = "";
                serialPort.SetState((int)CONNECTION_STATE.DELETE_GETFILENAME);
            }
            else if (c == 'A')
            {
                // Dir file command

                serialPort.commandFilename = "";
                serialPort.SetState((int)CONNECTION_STATE.DIR_GETFILENAME);
            }
            else if (c == 'I')
            {
                // List Directories file command

                // create a local temporaRY file to write the directory to before sending it to the serial port

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
                string driveName        = "";
                string volumeLabel      = "";
                byte[] volumeBuffer = new byte[0];

                // start by getting the volume information on the connected PC drive if thsi is not a UNC path

                if (!serialPort.currentWorkingDirectory.StartsWith(@"\\"))
                {
                    System.IO.DriveInfo systemDriveInfo = new System.IO.DriveInfo(Directory.GetDirectoryRoot(serialPort.currentWorkingDirectory));

                    // Get the drive and volume information

                    availableFreeSpace = systemDriveInfo.AvailableFreeSpace;
                    driveName        = systemDriveInfo.Name;
                    volumeLabel      = systemDriveInfo.VolumeLabel;

                    volumeBuffer = Encoding.ASCII.GetBytes("\r\n Volume in Drive " + driveName + " is " + volumeLabel + "\r\n");
                    Console.WriteLine("\r\n Volume in Drive " + driveName + " is " + volumeLabel);
                }
                else
                {
                    // if the diskette image is on a unc path - there will be no volume information available

                    volumeBuffer = Encoding.ASCII.GetBytes("\r\n Volume in Drive is a UNC Path\r\n");
                    Console.WriteLine("\r\n Volume in Drive is a UNC Path ");
                }

                // send volume info to the file we are going to send to the FLEX machine

                serialPort.streamDir.Write(volumeBuffer, 0, volumeBuffer.Length);

                // add the current working directory name to the output

                byte[] wrkDirBuffer = Encoding.ASCII.GetBytes(serialPort.currentWorkingDirectory + "\r\n\r\n");
                serialPort.streamDir.Write(wrkDirBuffer, 0, wrkDirBuffer.Length);
                Console.WriteLine(serialPort.currentWorkingDirectory + "\r\n");

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
            else if (c == 'P')
            {
                // Change Directory

                serialPort.commandFilename = "";
                serialPort.SetState((int)CONNECTION_STATE.CD_GETFILENAME);
            }
            else if (c == 'V')
            {
                // Change Drive (and optionally the directory)

                serialPort.commandFilename = "";
                serialPort.SetState((int)CONNECTION_STATE.DRIVE_GETFILENAME);
            }
            else if (c == 'C')
            {
                // Create a drive image

                serialPort.createFilename = "";
                serialPort.createPath = "";
                serialPort.createVolumeNumber = "";
                serialPort.createTrackCount = "";
                serialPort.createSectorCount = "";

                serialPort.SetState((int)CONNECTION_STATE.CREATE_GETPARAMETERS);
                serialPort.createState = (int)CREATE_STATE.GET_CREATE_PATH;
            }

            // now the Extended multi drive versions

            else if (c == 's')
            {
                // 's'end Sector Request with drive

                serialPort.SetState((int)CONNECTION_STATE.GET_READ_DRIVE);
            }
            else if (c == 'r')
            {
                // 'r'eceive Sector Request with drive

                serialPort.SetState((int)CONNECTION_STATE.GET_WRITE_DRIVE);
            }
            else if (c == 'm')      // Mount drive image with drive
            {
                serialPort.commandFilename = "";
                serialPort.SetState((int)CONNECTION_STATE.GET_MOUNT_DRIVE);
            }
            else if (c == 'd')      // Report which disk image is mounted to requested drive
            {
                serialPort.SetState((int)CONNECTION_STATE.GET_REQUESTED_MOUNT_DRIVE);
            }
            else if (c == 'c')      // Create a drive image
            {
                serialPort.createFilename = "";
                serialPort.createPath = "";
                serialPort.createVolumeNumber = "";
                serialPort.createTrackCount = "";
                serialPort.createSectorCount = "";

                serialPort.SetState((int)CONNECTION_STATE.GET_CREATE_DRIVE);
                serialPort.createState = (int)CREATE_STATE.GET_CREATE_PATH;
            }

            else                    // Unknown - command - go back to (int)CONNECTION_STATE.CONNECTED
            {
                if (serialPort.state != (int)CONNECTION_STATE.CONNECTED)
                    serialPort.SetState((int)CONNECTION_STATE.CONNECTED);

                if (c != 0x20)
                {
                    serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                    Console.Write("\n State is reset to CONNECTED - Unknown command recieved [" + c.ToString("X2", ci) + "]");
                    serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);
                }
            }
        }

        static void StateConnectionStateGetRequestedMountDrive(Ports serialPort, int c)
        {
            // Report which disk image is mounted to requested drive

            serialPort.currentDrive = c;

            serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
            Console.Write(serialPort.currentWorkingDirectory);
            Console.Write("\r");
            Console.Write("\n");
            serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);

            if (serialPort.imageFiles[serialPort.currentDrive].driveInfo != null)
                serialPort.sp.Write(serialPort.imageFiles[serialPort.currentDrive].driveInfo.MountedFilename);
            else
                serialPort.sp.Write("");

            serialPort.WriteByte(0x0D, false);
            serialPort.WriteByte(0x06);

            serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
        }

        static void StateConnectionStateGetReadDrive(Ports serialPort, int c)
        {
            try
            {
                serialPort.currentDrive = c;
                if (serialPort.imageFiles[serialPort.currentDrive].driveInfo != null)
                {
                    serialPort.imageFiles[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.S_MODE;
                    serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        static void StateConnectionStateGetWriteDrive(Ports serialPort, int c)
        {
            serialPort.currentDrive = c;
            serialPort.imageFiles[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.R_MODE;

            serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
        }

        static void StateConnectionStateGetMountDrive(Ports serialPort, int c)
        {
            serialPort.currentDrive = c;
            serialPort.SetState((int)CONNECTION_STATE.MOUNT_GETFILENAME);
        }

        static void StateConnectionStateGetCreateDrive(Ports serialPort, int c)
        {
            serialPort.currentDrive = c;
            serialPort.SetState((int)CONNECTION_STATE.CREATE_GETPARAMETERS);
        }

        static void StateConnectionStateGetTrack(Ports serialPort, int c)
        {
            serialPort.track[serialPort.currentDrive] = c;
            serialPort.SetState((int)CONNECTION_STATE.GET_SECTOR);
        }

        static void StateConnectionStateGetSector(Ports serialPort, int c)
        {
            serialPort.sector[serialPort.currentDrive] = c;

            if (serialPort.imageFiles[serialPort.currentDrive].driveInfo.mode == (int)SECTOR_ACCESS_MODE.S_MODE)
            {
                Console.WriteLine("\r\nState is SENDING_SECTOR");
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

        static void StateConnectionStateRecievingSector(Ports serialPort, int c)
        {
            serialPort.sectorBuffer[serialPort.sectorIndex++] = (byte)c;
            serialPort.calculatedCRC += (int)c;

            if (serialPort.sectorIndex >= 256)
            {
                serialPort.checksumIndex = 0;
                serialPort.SetState((int)CONNECTION_STATE.GET_CRC);
            }
        }

        static void StateConnectionStateGetCRC(Ports serialPort, int c)
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
        }

        static void StateConnectionStateMountGetFilename(Ports serialPort, int c)
        {
            if (c != 0x0d)
            {
                // just add the character to the filename

                serialPort.commandFilename += (char)c;
            }
            else
            {
                serialPort.commandFilename += ".DSK";

                // this should close any file that is currently open for this port/drive

                if (serialPort.imageFiles[serialPort.currentDrive] != null)
                {
                    if (serialPort.imageFiles[serialPort.currentDrive].stream != null)
                    {
                        serialPort.imageFiles[serialPort.currentDrive].stream.Close();
                        serialPort.imageFiles[serialPort.currentDrive].stream = null;
                    }
                }

                // Now mount the new file

                byte status = 0x06;
                if (serialPort.commandFilename.Length > 0)
                {
                    Console.WriteLine();
                    status = serialPort.MountImageFile(serialPort.commandFilename, serialPort.currentDrive);
                }

                serialPort.WriteByte(status);

                byte cMode = (byte)'W';
                if (serialPort.imageFiles[serialPort.currentDrive].readOnly)
                {
                    cMode = (byte)'R';
                }
                serialPort.WriteByte(cMode);
                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
            }
        }

        static void StateConnectionStateDeleteGetFilename(Ports serialPort, int c)
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

        static void StateConnectionStateDirGetFilename(Ports serialPort, int c)
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
                string driveName        = "";
                string volumeLabel      = "";
                byte[] volumeBuffer     = new byte[0];

                try
                {
                    // if this is not a UNC path, we can get the available size, drivename and volume label
                    // otherwise - we cannot.

                    if (!directoryRoot.StartsWith(@"\\"))
                    { 
                        systemDriveInfo = new System.IO.DriveInfo(directoryRoot);

                        availableFreeSpace  = systemDriveInfo.AvailableFreeSpace;
                        driveName           = systemDriveInfo.Name;
                        volumeLabel         = systemDriveInfo.VolumeLabel;

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
                    Console.WriteLine(e.Message);
                }
            }
        }

        static void StateConnectionStateCDGetFilename(Ports serialPort, int c)
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

        static void StateConnectionStateDriveGetFilename(Ports serialPort, int c)
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
                //    Console.WriteLine();
                //    Console.Write("SERVER: ");
                //}
                //lastActivityWasServer = true;
                //lastActivityWasClient = false;

                serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                Console.Write(status.ToString("X2", ci) + " ");
                serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);

                serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
            }
        }

        static void StateConnectionStateSendingDir(Ports serialPort, int c)
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

        static void StateConnectionStateCreateGetParameters(Ports serialPort, int c)
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
                    Console.Write("\n" + "Creating Image File " + fullFilename);
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

        static void StateConnectionStateWaitACK(Ports serialPort, int c)
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

        static void ProcessRS232Requests()
        {
            while (!done)
            {
                if (Console.KeyAvailable)
                {
                    HandleCommand();
                }

                foreach (Ports serialPort in listPorts)
                {
                    try 
                    { 
                        if (serialPort.sp.BytesToRead > 0)
                        {
                            int c = serialPort.sp.ReadByte();

                            if (((serialPort.state != (int)CONNECTION_STATE.RECEIVING_SECTOR) && (serialPort.state != (int)CONNECTION_STATE.GET_CRC)))
                            {
                                //if ((!lastActivityWasServer && !lastActivityWasClient) || lastActivityWasServer)
                                //{
                                //    Console.WriteLine();
                                //    Console.Write("CLIENT: ");
                                //}

                                Console.Write(c.ToString("X2", ci) + " ");

                                //lastActivityWasServer = false;
                                //lastActivityWasClient = true;
                            }

                            switch (serialPort.state)
                            {
                                case (int)CONNECTION_STATE.NOT_CONNECTED: StateConnectionStateNotConnected(serialPort, c); break;
                                case (int)CONNECTION_STATE.SYNCRONIZING: StateConnectionStateSynchronizing(serialPort, c); break;
                                case (int)CONNECTION_STATE.CONNECTED: StateConnectionStateConnected(serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_REQUESTED_MOUNT_DRIVE: StateConnectionStateGetRequestedMountDrive(serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_READ_DRIVE: StateConnectionStateGetReadDrive(serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_WRITE_DRIVE: StateConnectionStateGetWriteDrive(serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_MOUNT_DRIVE: StateConnectionStateGetMountDrive(serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_CREATE_DRIVE: StateConnectionStateGetCreateDrive(serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_TRACK: StateConnectionStateGetTrack(serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_SECTOR: StateConnectionStateGetSector(serialPort, c); break;
                                case (int)CONNECTION_STATE.RECEIVING_SECTOR: StateConnectionStateRecievingSector(serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_CRC: StateConnectionStateGetCRC(serialPort, c); break;
                                case (int)CONNECTION_STATE.MOUNT_GETFILENAME: StateConnectionStateMountGetFilename(serialPort, c); break;
                                case (int)CONNECTION_STATE.DELETE_GETFILENAME: StateConnectionStateDeleteGetFilename(serialPort, c); break;
                                case (int)CONNECTION_STATE.DIR_GETFILENAME: StateConnectionStateDirGetFilename(serialPort, c); break;
                                case (int)CONNECTION_STATE.CD_GETFILENAME: StateConnectionStateCDGetFilename(serialPort, c); break;
                                case (int)CONNECTION_STATE.DRIVE_GETFILENAME: StateConnectionStateDriveGetFilename(serialPort, c); break;
                                case (int)CONNECTION_STATE.SENDING_DIR: StateConnectionStateSendingDir(serialPort, c); break;
                                case (int)CONNECTION_STATE.CREATE_GETPARAMETERS: StateConnectionStateCreateGetParameters(serialPort, c); break;
                                case (int)CONNECTION_STATE.WAIT_ACK: StateConnectionStateWaitACK(serialPort, c); break;
                                default:
                                    serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);
                                    //sprintf (szHexTemp, "%02X", c);
                                    //*StatusLine << '\n' << "State is reset to NOT_CONNECTED - Unknown STATE " << szHexTemp;
                                    break;
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        static void LogFloppyReads(int drive, int track, int sector, int bytesToReadWrite, byte[] buffer, long offset)
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

        static void LogFloppyWrites(int drive, int track, int sector, int bytesToReadWrite, byte[] buffer, long offset)
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

        static void ProcessTcpIPRequests (TcpListener server)
        {
            if (logReads || logWrites)
            {
                logFileStream = new StreamWriter(File.Open(logfileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            }

            while (!done)
            {
                if (Console.KeyAvailable)
                {
                    HandleCommand();
                }

                TcpClient client = server.AcceptTcpClient(); // Accept an incoming connection

                int newBufferSize = 65536; // 64 KB (default is usually 8 KB)
                client.ReceiveBufferSize = newBufferSize;
                client.SendBufferSize = newBufferSize;

                // a client has connected 
                IPEndPoint remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                //Console.WriteLine($"Client connected from {remoteEndPoint.Address}:{remoteEndPoint.Port}");

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

                            Console.WriteLine($"cycles: {cyclesExecuted}");
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
                                        if (tcpIpSocketInfo.localPort.floppyImageFiles[currentDrive] != null)
                                        {
                                            tcpIpSocketInfo.localPort.track[currentDrive]   = buffer[3];
                                            tcpIpSocketInfo.localPort.sector[currentDrive]  = buffer[4];
                                            tcpIpSocketInfo.localPort.bytesToRead           = buffer[5] * 256;
                                            tcpIpSocketInfo.localPort.bytesToRead          += buffer[6];

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
                                    }
                                    break;

                                // these are for floppy write
                                case 0xA0:  // write one sector
                                case 0xB0:  // write multiple sectors
                                    {
                                        int currentDrive = buffer[2] & 0x03;
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
                                    Console.WriteLine($"invalid floppy command from TCPIP client: {buffer[0].ToString("X2")}");
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
                                    tcpIpSocketInfo.localPort.sizeDriveHeadRegister = buffer[2];          // SIZE/DRIVE/HEAD REGISTER
                                    tcpIpSocketInfo.localPort.cylinderHiRegister    = buffer[3];          // cylinder number high byte
                                    tcpIpSocketInfo.localPort.cylinderLowRegister   = buffer[4];          // cylinder number low byte
                                    tcpIpSocketInfo.localPort.sectorNumberRegister  = buffer[5];          // secttor in the cylinder to read
                                    tcpIpSocketInfo.localPort.sectorCountRegister   = buffer[6];          // number of sectors to read

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
                                }
                                break;

                            // this is for sdcard write
                            case 0x30:
                                {
                                    int currentDrive = (buffer[2] & 0x10) >> 4;
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
                                }
                                break;

                            default:
                                Console.WriteLine($"invalid sdcard command from TCPIP client: {buffer[0].ToString("X2")}");
                                break;
                        }
                        break;
                    }

                    default:
                        Console.WriteLine($"invalid command from TCPIP client: {buffer[0].ToString("X2")}");
                        break;
                }
                client.Close(); // Close connection
            }
        }

        static void Main(string[] args)
        {
            InitializeFromConfigFile();
            if (tcpIpSocketInfo != null && tcpIpSocketInfo.enabled)
            {
                usingTCPIP = true;

                TcpListener server = new TcpListener(localAddr, tcpIpSocketInfo.serverPort);
                server.Start();

                Console.WriteLine($"Server started on port {tcpIpSocketInfo.serverPort}. Waiting for connections...");
                ProcessTcpIPRequests(server);
            }
            else
                ProcessRS232Requests();
        }
    }
}
