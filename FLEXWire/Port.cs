using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
using System.Drawing;

namespace FLEXWire
{
    // This class is used to connect through the FLEX FNET driver on the FLEX machine

    #region RS232 port class
    public class Port
    {
        public int tabPageIndex = 0;        

        public Stream streamDir = null;
        public System.IO.Ports.SerialPort sp;
        public int port;
        public int state;
        public int createState;
        public int rate;
        public string speed;
        public string verbose;
        public string autoMount;
        public ImageFile[] floppyImageFiles = new ImageFile[4];
        public string dirFilename;
        public int currentDrive = 0;

        public int[] track = new int[4];
        public int[] sector = new int[4];

        public int sectorIndex = 0;
        public int calculatedCRC = 0;

        public int checksumIndex = 0;
        public int checksum = 0;

        public byte[] sectorBuffer = new byte[256];

        public string currentWorkingDirectory;
        public string commandFilename;
        public string createFilename;
        public string createPath;
        public string createVolumeNumber;
        public string createTrackCount;
        public string createSectorCount;

        public string defaultStartDirectory = "";

        public bool g_displaySectorData;

        CultureInfo ci = new CultureInfo("en-us");

        public void WriteByte(byte byteToWrite)
        {
            WriteByte(byteToWrite, true);
        }

        public void WriteByte(byte byteToWrite, bool displayOnScreen)
        {
            byte[] byteBuffer = new byte[1];
            byteBuffer[0] = byteToWrite;
            sp.Write(byteBuffer, 0, 1);

            if (displayOnScreen)
            {
                if (verbose == "V")
                {
                    if (Program.mainForm.InvokeRequired)
                    {
                        Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.outputWindow.AppendText($"{byteToWrite.ToString("X2")}\n")));
                        Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.outputWindow.ScrollToCaret()));
                    }
                    else
                    {
                        Program.mainForm.outputWindow.AppendText($"{byteToWrite.ToString("X2")}\n");
                        Program.mainForm.outputWindow.ScrollToCaret();
                    }
                }
                //SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                //Console.Write(byteToWrite.ToString("X2", ci) + " ");
                //SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);
            }
        }

        Color outputWindowsForeColor = Program.mainForm.mf_outputWindow.SelectionColor;
        Color outputWindowsBackColor = Program.mainForm.mf_outputWindow.SelectionBackColor;

        public void SetAttribute(int attr)
        {
            return;     // turn off for now

            if (attr == (int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE)
            {
                if (Program.mainForm.InvokeRequired)
                {
                    Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.mf_outputWindow.SelectionColor = outputWindowsBackColor.IsEmpty ? Color.Black : outputWindowsBackColor));
                    Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.mf_outputWindow.SelectionBackColor = outputWindowsForeColor.IsEmpty ? Color.White: outputWindowsForeColor));
                }
                else
                {
                    Program.mainForm.mf_outputWindow.SelectionColor = outputWindowsBackColor.IsEmpty ? Color.Black : outputWindowsBackColor;
                    Program.mainForm.mf_outputWindow.SelectionBackColor = outputWindowsForeColor.IsEmpty ? Color.White : outputWindowsForeColor;
                }
            }
            else if (attr == (int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE)
            {
                if (Program.mainForm.InvokeRequired)
                {
                    Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.mf_outputWindow.SelectionColor = outputWindowsBackColor.IsEmpty ? Color.White : outputWindowsBackColor));
                    Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.mf_outputWindow.SelectionBackColor = outputWindowsForeColor.IsEmpty ? Color.Black : outputWindowsForeColor));
                }
                else
                {
                    Program.mainForm.mf_outputWindow.SelectionColor = outputWindowsBackColor.IsEmpty ? Color.White : outputWindowsBackColor;
                    Program.mainForm.mf_outputWindow.SelectionBackColor = outputWindowsForeColor.IsEmpty ? Color.Black : outputWindowsForeColor;
                }
            }
        }

        public void SetState(int newState)
        {
            state = newState;
            string statusLine = "";

            switch (newState)
            {
                case (int)CONNECTION_STATE.NOT_CONNECTED:               statusLine = "State is NOT_CONNECTED"; break;
                case (int)CONNECTION_STATE.SYNCRONIZING:                statusLine = "State is SYNCRONIZING"; break;
                case (int)CONNECTION_STATE.CONNECTED:                   statusLine = "State is CONNECTED"; break;
                case (int)CONNECTION_STATE.GET_REQUESTED_MOUNT_DRIVE:   statusLine = "State is GET_REQUESTED_MOUNT_DRIVE"; break;
                case (int)CONNECTION_STATE.GET_READ_DRIVE:              statusLine = "State is GET_DRIVE"; break;
                case (int)CONNECTION_STATE.GET_WRITE_DRIVE:             statusLine = "State is GET_DRIVE"; break;
                case (int)CONNECTION_STATE.GET_MOUNT_DRIVE:             statusLine = "State is GET_MOUNT_DRIVE"; break;
                case (int)CONNECTION_STATE.GET_CREATE_DRIVE:            statusLine = "State is GET_CREATE_DRIVE"; break;
                case (int)CONNECTION_STATE.GET_TRACK:                   statusLine = "State is GET_TRACK"; break;
                case (int)CONNECTION_STATE.GET_SECTOR:                  statusLine = "State is GET_SECTOR"; break;
                case (int)CONNECTION_STATE.RECEIVING_SECTOR:            statusLine = "State is RECEIVING_SECTOR"; break;
                case (int)CONNECTION_STATE.GET_CRC:                     statusLine = "State is GET_CRC"; break;
                case (int)CONNECTION_STATE.MOUNT_GETFILENAME:           statusLine = "State is MOUNT_GETFILENAME"; break;
                case (int)CONNECTION_STATE.WAIT_ACK:                    statusLine = "State is WAIT_ACK"; break;
                case (int)CONNECTION_STATE.PROCESSING_MOUNT:            statusLine = "State is PROCESSING_MOUNT"; break;
                case (int)CONNECTION_STATE.PROCESSING_DIR:              statusLine = "State is PROCESSING_DIR"; break;
                case (int)CONNECTION_STATE.PROCESSING_LIST:             statusLine = "State is PROCESSING_LIST"; break;
                case (int)CONNECTION_STATE.DELETE_GETFILENAME:          statusLine = "State is DELETE_GETFILENAME"; break;
                case (int)CONNECTION_STATE.DIR_GETFILENAME:             statusLine = "State is DIR_GETFILENAME"; break;
                case (int)CONNECTION_STATE.CD_GETFILENAME:              statusLine = "State is CD_GETFILENAME"; break;
                case (int)CONNECTION_STATE.DRIVE_GETFILENAME:           statusLine = "State is DRIVE_GETFILENAME"; break;
                case (int)CONNECTION_STATE.SENDING_DIR:                 statusLine = "State is SENDING_DIR"; break;

                case (int)CONNECTION_STATE.CREATE_GETPARAMETERS:
                    switch (createState)
                    {
                        case (int)CREATE_STATE.GET_CREATE_PATH:
                            statusLine = "State is CREATE_GETPARAMETERS GET_CREATE_PATH";
                            break;
                        case (int)CREATE_STATE.GET_CREATE_NAME:
                            statusLine = "State is CREATE_GETPARAMETERS GET_CREATE_NAME";
                            break;
                        case (int)CREATE_STATE.GET_CREATE_VOLUME:
                            statusLine = "State is CREATE_GETPARAMETERS GET_CREATE_VOLUME";
                            break;
                        case (int)CREATE_STATE.GET_CREATE_TRACK_COUNT:
                            statusLine = "State is CREATE_GETPARAMETERS GET_CREATE_TRACK_COUNT";
                            break;
                        case (int)CREATE_STATE.GET_CREATE_SECTOR_COUNT:
                            statusLine = "State is CREATE_GETPARAMETERS GET_CREATE_SECTOR_COUNT";
                            break;
                        case (int)CREATE_STATE.CREATE_THE_IMAGE:
                            statusLine = "State is CREATE_GETPARAMETERS CREATE_THE_IMAGE";
                            break;
                    }
                    break;

                default: statusLine = "State is UNKNOWN - [" + newState.ToString("X2") + "]"; break;
            }

            if (verbose == "V")
            {
                if (Program.mainForm.InvokeRequired)
                {
                    Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.outputWindow.AppendText($"{statusLine}\n")));
                    Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.outputWindow.ScrollToCaret()));
                }
                else
                {
                    Program.mainForm.outputWindow.AppendText($"{statusLine}\n");
                    Program.mainForm.outputWindow.ScrollToCaret();
                }
            }
        }

        public byte MountImageFile(string fileName, int nDrive, int tabPageIndex)
        {
            byte c = 0x06;
            string Message = "";

            Directory.SetCurrentDirectory(currentWorkingDirectory);

            string fileToLoad = fileName;

            try
            {
                if (floppyImageFiles[nDrive] == null)
                    floppyImageFiles[nDrive] = new ImageFile();

                floppyImageFiles[nDrive].stream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                floppyImageFiles[nDrive].readOnly = false;
            }
            catch
            {
                try
                {
                    floppyImageFiles[nDrive].stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    floppyImageFiles[nDrive].readOnly = true;
                }
                catch
                {
                    //// if we cannot load the requested file - attempt to load the 'BLANK.DSK' image

                    Message = "Unable to load imagefile " + fileToLoad + " from directory " + currentWorkingDirectory;
                    //try
                    //{
                    //    imageFile[nDrive].stream = File.Open("BLANK.DSK", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                    //    imageFile[nDrive].readOnly = false;
                    //}
                    //catch
                    //{
                    //    Message = "Unable to load default imagefile [BLANK.DSK] from directory " + currentWorkingDirectory;
                    //    c = 0x15;
                    //}
                    c = 0x15;
                }
            }

            if (c == 0x06)
            {
                if (fileToLoad.Substring(1, 1) == ":")
                    Message = "Loaded imagefile " + fileToLoad + " from directory " + Directory.GetParent(fileToLoad);
                else
                    Message = "Loaded imagefile " + fileToLoad + " from directory " + currentWorkingDirectory;

                floppyImageFiles[nDrive].Name = fileToLoad.Replace(@"\", "/");
                floppyImageFiles[nDrive].driveInfo = new DriveInfo();

                floppyImageFiles[nDrive].stream.Seek(512 + 39, SeekOrigin.Begin);
                floppyImageFiles[nDrive].stream.Read(floppyImageFiles[nDrive].driveInfo.cNumberOfSectorsPerTrack, 0, 1);
                floppyImageFiles[nDrive].driveInfo.NumberOfSectorsPerTrack = Convert.ToInt16(floppyImageFiles[nDrive].driveInfo.cNumberOfSectorsPerTrack[0]);
                floppyImageFiles[nDrive].driveInfo.NumberOfBytesPerTrack = floppyImageFiles[nDrive].driveInfo.NumberOfSectorsPerTrack * 256;
                floppyImageFiles[nDrive].driveInfo.NumberOfBytesPerTrack = (long)(floppyImageFiles[nDrive].driveInfo.NumberOfSectorsPerTrack * 256L);

                if (fileToLoad.Substring(1, 1) == ":")
                    floppyImageFiles[nDrive].driveInfo.MountedFilename = fileToLoad;
                else
                    floppyImageFiles[nDrive].driveInfo.MountedFilename = currentWorkingDirectory + "/" + fileToLoad;

                floppyImageFiles[nDrive].driveInfo.MountedFilename = floppyImageFiles[nDrive].driveInfo.MountedFilename.ToUpper();
                floppyImageFiles[nDrive].Name = fileToLoad.Replace(@"\", "/");
            }

            Message = Message.Replace("/", @"\");
            Message += $" as drive # {nDrive}";

            /*
            if (verbose == "V")
            {
                if (Program.mainForm.InvokeRequired)
                {
                    Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.outputWindow.AppendText($"{Message}\n")));
                    Program.mainForm.Invoke(new MethodInvoker(() => Program.mainForm.outputWindow.ScrollToCaret()));
                }
                else
                {
                    Program.mainForm.outputWindow.AppendText($"{Message}\n");
                    Program.mainForm.outputWindow.ScrollToCaret();
                }
            }
            */

            return (c);
        }

        public long GetSectorOffset()
        {
            long lSectorOffset;
            long lOffsetToStartOfTrack;
            long lOffsetFromTrackStartToSector;

            lOffsetToStartOfTrack = ((long)track[currentDrive] * floppyImageFiles[currentDrive].driveInfo.NumberOfBytesPerTrack);

            if (sector[currentDrive] == 0)
                lOffsetFromTrackStartToSector = (long)sector[currentDrive] * 256L;
            else
                lOffsetFromTrackStartToSector = (long)(sector[currentDrive] - 1) * 256L;

            lSectorOffset = lOffsetToStartOfTrack + lOffsetFromTrackStartToSector;

            //Console.Write("[" + lSectorOffset.ToString("X8") + "]");
            return (lSectorOffset);
        }

        public byte WriteSector()
        {
            byte status = 0x15;

            if (calculatedCRC == checksum)
            {
                long lSectorOffset = GetSectorOffset();

                floppyImageFiles[currentDrive].stream.Seek(lSectorOffset, SeekOrigin.Begin);
                try
                {
                    floppyImageFiles[currentDrive].stream.Write(sectorBuffer, 0, 256);
                    floppyImageFiles[currentDrive].stream.Flush();
                    status = 0x06;
                }
                catch
                {
                }
            }
            else
            {
                sectorIndex = 0;
                calculatedCRC = 0;
                checksumIndex = 0;
                checksum = 0;
            }

            return (status);
        }

        public void SendSector()
        {
            int checksum = 0;

            long lSectorOffset = GetSectorOffset();

            if (floppyImageFiles[currentDrive].stream != null)
            {
                floppyImageFiles[currentDrive].stream.Seek(lSectorOffset, SeekOrigin.Begin);
                floppyImageFiles[currentDrive].stream.Read(sectorBuffer, 0, 256);

                for (int nIndex = 0; nIndex < 256; nIndex++)
                {
                    WriteByte(sectorBuffer[nIndex], g_displaySectorData);
                    checksum += (char)(sectorBuffer[nIndex] & 0xFF);
                }

                WriteByte((byte)((checksum / 256) & 0xFF));
                WriteByte((byte)((checksum % 256) & 0xFF));
            }
        }

        public byte CreateImageFile()
        {
            byte cStatus = 0x15;

            string fullFilename = createPath + "/" + createFilename + ".DSK";

            int track;
            int sector;

            int volumeNumber = Convert.ToInt32(createVolumeNumber);
            int numberOfTracks = Convert.ToInt32(createTrackCount);
            int numberOfSectors = Convert.ToInt32(createSectorCount);

            if ((numberOfTracks > 0) && (numberOfTracks <= 256))
            {
                if ((numberOfSectors > 0) && (numberOfSectors <= 255))
                {

                    // total number of user sectors = number tracks minus one times the number of sectors
                    // because track 0 is not for users

                    int nTotalSectors = (numberOfTracks - 1) * numberOfSectors;

                    DateTime now = DateTime.Now;

                    if (nTotalSectors > 0)
                    {
                        try
                        {
                            Stream fp = File.Open(fullFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
                            cStatus = 0x15;     // file already exists
                            fp.Close();
                        }
                        catch
                        {
                            // file does not yet exist - create it

                            try
                            {
                                Stream fp = File.Open(fullFilename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                                for (track = 0; track < numberOfTracks; track++)
                                {
                                    for (sector = 0; sector < numberOfSectors; sector++)
                                    {
                                        if (track > 0)
                                        {
                                            if (sector == (numberOfSectors - 1))
                                            {
                                                if (track == (numberOfTracks - 1))
                                                {
                                                    sectorBuffer[0] = (byte)0x00;
                                                    sectorBuffer[1] = (byte)0x00;
                                                }
                                                else
                                                {
                                                    sectorBuffer[0] = (byte)(track + 1);
                                                    sectorBuffer[1] = (byte)1;
                                                }
                                            }
                                            else
                                            {
                                                sectorBuffer[0] = (byte)track;
                                                sectorBuffer[1] = (byte)(sector + 2);
                                            }
                                        }
                                        else
                                        {
                                            switch (sector)
                                            {
                                                case 0:
                                                    break;
                                                case 1:
                                                    break;
                                                case 2:

                                                    char[] cArray = createFilename.ToCharArray();
                                                    for (int i = 0; i < cArray.Length && i < 11; i++)
                                                    {
                                                        sectorBuffer[16 + i] = (byte)cArray[i];
                                                    }

                                                    sectorBuffer[27] = (byte)(volumeNumber / 256);
                                                    sectorBuffer[28] = (byte)(volumeNumber % 256);
                                                    sectorBuffer[29] = (byte)0x01;                  // first user track
                                                    sectorBuffer[30] = (byte)0x01;                  // first user sector
                                                    sectorBuffer[31] = (byte)(numberOfTracks - 1);  // last user track
                                                    sectorBuffer[32] = (byte)numberOfSectors;       // last user sector
                                                    sectorBuffer[33] = (byte)(nTotalSectors / 256);
                                                    sectorBuffer[34] = (byte)(nTotalSectors % 256);
                                                    sectorBuffer[35] = (byte)now.Month;             // month
                                                    sectorBuffer[36] = (byte)now.Day;               // day
                                                    sectorBuffer[37] = (byte)(now.Year - 100);      // year (make Y2K compatible)
                                                    sectorBuffer[38] = (byte)(numberOfTracks - 1);  // max track
                                                    sectorBuffer[39] = (byte)numberOfSectors;       // max sector
                                                    break;

                                                case 3:
                                                    for (int i = 0; i < 256; i++)
                                                        sectorBuffer[i] = 0x00;
                                                    break;

                                                default:
                                                    if (sector == (numberOfSectors - 1))
                                                    {
                                                        sectorBuffer[0] = (byte)0x00;
                                                        sectorBuffer[1] = (byte)0x00;
                                                    }
                                                    else
                                                    {
                                                        sectorBuffer[0] = (byte)track;
                                                        sectorBuffer[1] = (byte)(sector + 2);
                                                    }
                                                    break;
                                            }
                                        }
                                        fp.Write(sectorBuffer, 0, 256);
                                    }
                                }

                                cStatus = 0x06;
                                fp.Close();
                            }
                            catch
                            {
                                cStatus = 0x15;     // could not create file
                            }
                        }
                    }
                    else
                        cStatus = 0x15;     // total number of sectors not > 0
                }
                else
                    cStatus = 0x15;     // too many sectors
            }
            else
                cStatus = 0x15;     // too many tracks

            return (cStatus);
        }
    }
    #endregion
}
