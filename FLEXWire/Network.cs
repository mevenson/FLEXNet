using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

using System.IO;
using System.Collections.Generic;

namespace FLEXWire
{
    // This class is used to connect through the WIFI driver driver on the pico W FLEX machine emulator

    #region TCP IP port class
    public class Network
    {
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
        public ImageFile[] sdCardImageFiles = new ImageFile[4];
        public string dirFilename;

        public string SDCardVerbose = "";
        public string FloppyVerbose = "";

        public byte dataRegister;              // DATA REGISTER
        public byte errorRegister;             // ERROR REGISTER
        public byte writePrecompRegister;      // WRITE PRECOMP REGISTER
        public byte sectorCountRegister;       // SECTOR COUNT
        public byte sectorNumberRegister;      // SECTOR NUMBER
        public byte cylinderLowRegister;       // CYLINDER NUMBER (LSB)
        public byte cylinderHiRegister;        // CYLINDER NUMBER (MSB)
        public byte sizeDriveHeadRegister;     // SIZE/DRIVE/HEAD REGISTER
        public byte statusRegister;            // STATUS REGISTER
        public byte commandRegister;           // COMMAND REGISTER

        public int[] track = new int[4];
        public int[] sector = new int[4];
        public int bytesToRead;
        public int bytesToWrite;

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

        List<TextBox>    mf_textBoxSDCardDrives            = new List<TextBox>();
        List<PictureBox> mf_picturBoxTCPIPSDCardDrives = new List<PictureBox>();

        // the constructor will figure out where the controls are on the main form that it wants to manipulate
        public Network()
        {
            for (int drive = 0; drive < 2; drive++)
            {
                string textBoxName = $"textBoxSDCardDrive{drive}";
                TextBox foundTextBox = Program.mainForm.Controls.Find(textBoxName, true).FirstOrDefault() as TextBox;
                mf_textBoxSDCardDrives.Add(foundTextBox);

                string pictureBoxName = $"picturBoxTCPIPSDCardDrive{drive}";
                PictureBox foundPictureBox = Program.mainForm.Controls.Find(pictureBoxName, true).FirstOrDefault() as PictureBox;
                mf_picturBoxTCPIPSDCardDrives.Add(foundPictureBox);
            }
        }

        public byte MountFloppyImageFile(string fileName, int nDrive)
        {
            byte c = 0x06;
            string Message = "";

            Directory.SetCurrentDirectory(currentWorkingDirectory);

            string fileToLoad = fileName;

            try
            {
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
                    //    SDCard_imageFile[nDrive].stream = File.Open("BLANK.DSK", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                    //    SDCard_imageFile[nDrive].readOnly = false;
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

                if (floppyImageFiles[nDrive] != null)
                {
                    switch (nDrive)
                    {
                        case 0:
                            Program.mainForm.textBoxTCPIPFloppyDrive0.Text = fileToLoad;
                            if (floppyImageFiles[nDrive].readOnly)
                                Program.mainForm.pictureBoxTCPIPFLoppyDrive0.Image = Properties.Resources.reddot;
                            else
                                Program.mainForm.pictureBoxTCPIPFLoppyDrive0.Image = Properties.Resources.greendot;
                            break;
                        case 1:
                            Program.mainForm.textBoxTCPIPFloppyDrive1.Text = fileToLoad;
                            if (floppyImageFiles[nDrive].readOnly)
                                Program.mainForm.pictureBoxTCPIPFLoppyDrive1.Image = Properties.Resources.reddot;
                            else
                                Program.mainForm.pictureBoxTCPIPFLoppyDrive1.Image = Properties.Resources.greendot;
                            break;
                        case 2:
                            Program.mainForm.textBoxTCPIPFloppyDrive2.Text = fileToLoad;
                            if (floppyImageFiles[nDrive].readOnly)
                                Program.mainForm.pictureBoxTCPIPFLoppyDrive2.Image = Properties.Resources.reddot;
                            else
                                Program.mainForm.pictureBoxTCPIPFLoppyDrive2.Image = Properties.Resources.greendot;
                            break;
                        case 3:
                            Program.mainForm.textBoxTCPIPFloppyDrive3.Text = fileToLoad;
                            if (floppyImageFiles[nDrive].readOnly)
                                Program.mainForm.pictureBoxTCPIPFLoppyDrive3.Image = Properties.Resources.reddot;
                            else
                                Program.mainForm.pictureBoxTCPIPFLoppyDrive3.Image = Properties.Resources.greendot;
                            break;
                    }
                }
            }

            Message = Message.Replace("/", @"\");
            if (Message.Length > 0)
                Message += " for use by TCPIP connections";

            Message += $" as Floppy drive # {nDrive}";

            Program.mainForm.WriteLineToOutputWindow(Message);

            return c;
        }

        public byte MountSDCardImageFile(string fileName, int nDrive)
        {
            byte c = 0x06;
            string Message = "";

            Directory.SetCurrentDirectory(currentWorkingDirectory);

            string fileToLoad = fileName;

            if (sdCardImageFiles[nDrive] == null)
            {
                sdCardImageFiles[nDrive] = new ImageFile();
            }

            try
            {
                sdCardImageFiles[nDrive].stream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                sdCardImageFiles[nDrive].readOnly = false;
            }
            catch (Exception e)
            {
                try
                {
                    sdCardImageFiles[nDrive].stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    sdCardImageFiles[nDrive].readOnly = true;
                }
                catch
                {
                    //// if we cannot load the requested file - attempt to load the 'BLANK.DSK' image

                    Message = "Unable to load imagefile " + fileToLoad + " from directory " + currentWorkingDirectory;
                    //try
                    //{
                    //    SDCard_imageFile[nDrive].stream = File.Open("BLANK.DSK", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                    //    SDCard_imageFile[nDrive].readOnly = false;
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

                sdCardImageFiles[nDrive].driveInfo = new DriveInfo();
                sdCardImageFiles[nDrive].driveInfo.bytesPerRead = 2;

                sdCardImageFiles[nDrive].driveInfo.NumberOfSectorsPerTrack = 256;   // for SD Card it is always 256 actual sectors (255 usavle)
                sdCardImageFiles[nDrive].driveInfo.NumberOfBytesPerTrack = 0x10000 * sdCardImageFiles[nDrive].driveInfo.bytesPerRead;

                if (fileToLoad.Substring(1, 1) == ":")
                    sdCardImageFiles[nDrive].driveInfo.MountedFilename = fileToLoad;
                else
                    sdCardImageFiles[nDrive].driveInfo.MountedFilename = currentWorkingDirectory + "/" + fileToLoad;

                sdCardImageFiles[nDrive].driveInfo.MountedFilename = sdCardImageFiles[nDrive].driveInfo.MountedFilename.ToUpper();

                if (mf_textBoxSDCardDrives[nDrive] != null && mf_picturBoxTCPIPSDCardDrives[nDrive] != null)
                {
                    sdCardImageFiles[nDrive].Name = fileToLoad;

                    mf_textBoxSDCardDrives[nDrive].Text = sdCardImageFiles[nDrive].Name;
                    if (sdCardImageFiles[nDrive].readOnly)
                        mf_picturBoxTCPIPSDCardDrives[nDrive].Image = Properties.Resources.reddot;
                    else
                        mf_picturBoxTCPIPSDCardDrives[nDrive].Image = Properties.Resources.greendot;

                    Message = Message.Replace("/", @"\");
                    if (Message.Length > 0)
                        Message += " for use by TCPIP connections";

                    Message += $" as SDCard drive # {nDrive}";

                    Program.mainForm.WriteLineToOutputWindow(Message);
                }
            }
            return c;
        }

        # region called by the ProcessTcpIPRequest function and the functions below that are never called only
        public long GetSDCardSectorOffset(int currentDrive)
        {
            // each partition is on a 0x200000000 byte boundary

            // Since all tracks on the SD Card are formatted to 255 usable sector with an extra sector for sector 0 that
            // is not used, but iis allocated, The offset to the cylinder within each partition will be on a 0x20000 byte
            // boundary with each partition on a 0x200000000 byte boundary. This assumes that the format is RAW. If it
            // is not - then the values will be half of that specified.

            long lSectorOffset;
            long lOffsetToStartOfTrack;
            long lOffsetFromTrackStartToSector;

            long track = cylinderHiRegister * 256 + cylinderLowRegister;
            lOffsetToStartOfTrack = ((long)track * sdCardImageFiles[currentDrive].driveInfo.NumberOfBytesPerTrack);

            lOffsetFromTrackStartToSector = (long)sectorNumberRegister * 256L * sdCardImageFiles[currentDrive].driveInfo.bytesPerRead;
            lSectorOffset = lOffsetToStartOfTrack + lOffsetFromTrackStartToSector + ((sizeDriveHeadRegister & 0x0F) * 0x100000000 * sdCardImageFiles[currentDrive].driveInfo.bytesPerRead);

            //Console.Write("[" + lSectorOffset.ToString("X8") + "]");
            //Program.mainForm.WriteLineToOutputWindow("[" + lSectorOffset.ToString("X8") + "]");
            return (lSectorOffset);
        }

        public long GetFloppySectorOffset(int currentDrive)
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
        #endregion
    }
    #endregion
}
