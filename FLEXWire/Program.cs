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
using System.Windows.Forms;

namespace FLEXWire
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
        public int driveNumber;
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

    static class Program
    {
        public static frmMain mainForm;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
    }
}
