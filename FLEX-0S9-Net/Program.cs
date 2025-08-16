using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using System.IO;
using System.IO.Ports;

using System.Diagnostics;
using System.Reflection;

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
public enum FileFormat
{
    fileformat_UNKNOWN = -1,
    fileformat_OS9,         // mode where track 0 has same number of sectors as the rest of the disk.
    fileformat_OS9_IMA,     // special mode where track zero (both sides) is always only 10 sectors     (Not Yet Implemented)
    fileformat_FLEX,        // mode where track 0 has same number of sectors as the rest of the disk.
    fileformat_FLEX_IMA,    // special mode where track zero (both sides) is always only 10 sectors     (Not Yet Implemented)
    fileformat_UniFLEX,
    fileformat_FLEX_IDE,
    fileformat_MINIX_68K,
    fileformat_MINIX_IBM,
    fileformat_IMD
}
public class OS9_DD_PATH_OPTS
{
    public byte[] pd_dtp    = new byte[1];      // 0x3F     IT.DTP RMB 1 DEVICE TYPE(0=SCF 1=RBF 2=PIPE 3=SBF)
    public byte[] pd_drv    = new byte[1];      // 0x40     IT.DRV RMB 1 DRIVE NUMBER
    public byte[] pd_stp    = new byte[1];      // 0x41     IT.STP RMB 1 STEP RATE (see table below)
                                                //                          Step Code   FD1771     FD179X Family
                                                //                                      5"   8"   5"   8"
                                                //                              0       40ms 20ms 30ms 15ms
                                                //                              1       20ms 10ms 20ms 10ms
                                                //                              2       12ms  6ms 12ms  6ms
                                                //                              3       12ms  6ms  6ms  3ms

    public byte[] pd_typ    = new byte[1];      // 0x42     IT.TYP RMB 1 DEVICE TYPE(See RBFMAN path descriptor)
                                                //                          bit 0   0 = 5"
                                                //                                  1 = 8"
                                                //                          bit 6   0 = Standard OS9 format
                                                //                                  1 = Non-standard format
                                                //                          bit 7   0 = Floppy disk
                                                //                                  1 = Hard disk

    public byte[] pd_dns    = new byte[1];      // 0x43     IT.DNS RMB 1 MEDIA DENSITY(0 - SINGLE, 1-DOUBLE)
                                                //                          bit 0   0 = Single density (FM)
                                                //                                  1 = Double density (MFM)
                                                //                          bit 1   0 = Single track density (5" 48 TPI)
                                                //                                  1 = Double track density (5" 96 TPI)

    public byte[] pd_cyl    = new byte[2];      // 0x44     IT.CYL RMB 2 NUMBER OF CYLINDERS(TRACKS)
    public byte[] pd_sid    = new byte[1];      // 0x46     IT.SID RMB 1 NUMBER OF SURFACES(SIDES)
    public byte[] pd_vfy    = new byte[1];      // 0x47     IT.VFY RMB 1 0 = VERIFY DISK WRITES
    public byte[] pd_sct    = new byte[2];      // 0x48     IT.SCT RMB 2 Default Sectors/Track
    public byte[] pd_t0s    = new byte[2];      // 0x4A     IT.T0S RMB 2 Default Sectors/Track (Track 0)
    public byte[] pd_ilv    = new byte[1];      // 0x4C     IT.ILV RMB 1 SECTOR INTERLEAVE FACTOR
    public byte[] pd_sas    = new byte[1];      // 0x4D     IT.SAS RMB 1 SEGMENT ALLOCATION SIZE
    public byte[] pd_tfm    = new byte[1];      // 0x4E
    public byte[] pd_exten  = new byte[2];      // 0x4F
    public byte[] pd_stoff  = new byte[1];      // 0x51
    public byte[] pd_att    = new byte[1];      // 0x52     File attributes (D S PE PW PR E W R)
    public byte[] pd_fd     = new byte[3];      // 0x53     File descriptor PSN (physical sector #)
    public byte[] pd_dfd    = new byte[3];      // 0x56     Directory file descriptor PSN
    public byte[] pd_dcp    = new byte[4];      // 0x59     File’s directory entry pointer
    public byte[] pd_dvt    = new byte[2];      // 0x5D     Address of device table entry
}
#endregion
#region supporting classes
public class OS9_ID_SECTOR
{
    public byte[] cTOT = new byte[3];      // 0x00  Total Number of sector on media
    public byte[] cTKS = new byte[1];      // 0x03  Number of tracks
    public byte[] cMAP = new byte[2];      // 0x04  Number of bytes in allocation map
    public byte[] cBIT = new byte[2];      // 0x06  Number of sectors per cluster
    public byte[] cDIR = new byte[3];      // 0x08  Starting sector of root directory
    public byte[] cOWN = new byte[2];      // 0x0B  Owners user number
    public byte[] cATT = new byte[1];      // 0x0D  Disk attributes
    public byte[] cDSK = new byte[2];      // 0x0E  Disk Identification
    public byte[] cFMT = new byte[1];      // 0x10  Disk Format: density, number of sides
    public byte[] cSPT = new byte[2];      // 0x11  Number of sectors per track
    public byte[] cRES = new byte[2];      // 0x13  Reserved for future use
    public byte[] cBT  = new byte[3];      // 0x15  Starting sector of bootstrap file
    public byte[] cBSZ = new byte[2];      // 0x18  Size of bootstrap file (in bytes)
    public byte[] cDAT = new byte[5];      // 0x1A  Time of creation Y:M:D:H:M
    public byte[] cNAM = new byte[32];     // 0x1F  Volume name (last char has sign bit set)
    // these are new 

    //public byte[] cDD_OPT       = new byte[32];                                   //  0x3F 32 bytes of DD_OPT Path descriptor options             0x3F 01 00 02 25 03 00 00 FE 02 00 00 38 00 38 00 08 02 00 00 00 01 00 00 10 00 00 00 00 00 00 00 00 
    public OS9_DD_PATH_OPTS cOPTS = new OS9_DD_PATH_OPTS();                         //  All zeros 0x3F through 0x5E (see above class)

    // these are used by OS9/68K - on EmuOS9Boot.Dsk these are all zeros

    public byte[] cDD_RES       = new byte[1];                                      //   $5F 1 Reserved                                             0x5F 00 
    public byte[] cDD_SYNC      = new byte[4];                                      //   $60 4 DD_SYNC Media integrity code                         0x60 43 72 75 7A (Cruz)
    public byte[] cDD_MapLSN    = new byte[4];                                      //   $64 4 DD_MapLSN Bitmap starting sector number(0=LSN 1)     0x64 00 00 00 01  
    public byte[] cDD_LSNSize   = new byte[2];                                      //   $68 2 DD_LSNSize Media logical sector size(0=256)          0x68 01 00      <= 512 bytes per sector
    public byte[] cDD_VersID    = new byte[2];                                      //   $6A 2 DD_VersID Sector 0 Version ID                        0x6A 00 01 

    // ...

    public byte[] cLSS = new byte[2];       // logical sector size at offset 0x68
}
public class dir_entry
{
    public byte[] m_fdn_number = new byte[2];
    public byte[] m_fdn_name = new byte[14];

    public int nfdnNumber;
}
public class blockList
{
    public byte[] block = new byte[3];
}
public class fdn
{
    //unsigned char m_ffwdl[2]  ;// rmb 2 forward list link               0x00
    //unsigned char m_fstat     ;// rmb 1 * see below *                   0x02
    //unsigned char m_fdevic[2] ;// rmb 2 device where fdn resides        0x03
    //unsigned char m_fnumbr[2] ;// rmb 2 fdn number (device address)     0x05
    //unsigned char m_frefct    ;// rmb 1 reference count                 0x07
    //unsigned char m_fmode     ;// rmb 1 * see below *                   0x08
    //unsigned char m_facces    ;// rmb 1 * see below *                   0x09
    //unsigned char m_fdirlc    ;// rmb 1 directory entry count           0x0A
    //unsigned char m_fouid[2]  ;// rmb 2 owner's user id                 0x0B
    //unsigned char m_fsize[4]  ;// rmb 4 file size                       0x0D
    //unsigned char m_ffmap[48] ;// rmb MAPSIZ*DSKADS file map            0x10

    public byte[] m_mode      = new byte[1];    //  file mode
    public byte[] m_perms     = new byte[1];    //  file security
    public byte[] m_links     = new byte[1];    //  number of links
    public byte[] m_owner     = new byte[2];    //  file owner's ID
    public byte[] m_size      = new byte[4];    //  file size
    public blockList[] m_blks = new blockList[13];   //  block list
    public byte[] m_time      = new byte[4];    //  file time
    public byte[] m_fd_pad    = new byte[12];   //  padding
}
public class UniFLEX_SIR
{
    public byte[] m_supdt  = new byte[1];        //rmb 1       sir update flag                                         0x0200        -> 00 
    public byte[] m_swprot = new byte[1];        //rmb 1       mounted read only flag                                  0x0201        -> 00 
    public byte[] m_slkfr  = new byte[1];        //rmb 1       lock for free list manipulation                         0x0202        -> 00 
    public byte[] m_slkfdn = new byte[1];        //rmb 1       lock for fdn list manipulation                          0x0203        -> 00 
    public byte[] m_sintid = new byte[4];        //rmb 4       initializing system identifier                          0x0204        -> 00 
    public byte[] m_scrtim = new byte[4];        //rmb 4       creation time                                           0x0208        -> 11 44 F3 FC
    public byte[] m_sutime = new byte[4];        //rmb 4       date of last update                                     0x020C        -> 11 44 F1 51
    public byte[] m_sszfdn = new byte[2];        //rmb 2       size in blocks of fdn list                              0x0210        -> 00 4A          = 74
    public byte[] m_ssizfr = new byte[3];        //rmb 3       size in blocks of volume                                0x0212        -> 00 08 1F       = 2079
    public byte[] m_sfreec = new byte[3];        //rmb 3       total free blocks                                       0x0215        -> 00 04 9C       = 
    public byte[] m_sfdnc  = new byte[2];        //rmb 2       free fdn count                                          0x0218        -> 01 B0
    public byte[] m_sfname = new byte[14];       //rmb 14      file system name                                        0x021A        -> 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
    public byte[] m_spname = new byte[14];       //rmb 14      file system pack name                                   0x0228        -> 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
    public byte[] m_sfnumb = new byte[2];        //rmb 2       file system number                                      0x0236        -> 00 00
    public byte[] m_sflawc = new byte[2];        //rmb 2       flawed block count                                      0x0238        -> 00 00
    public byte[] m_sdenf  = new byte[1];        //rmb 1       density flag - 0=single                                 0x023A        -> 01
    public byte[] m_ssidf  = new byte[1];        //rmb 1       side flag - 0=single                                    0x023B        -> 01
    public byte[] m_sswpbg = new byte[3];        //rmb 3       swap starting block number                              0x023C        -> 00 08 20
    public byte[] m_sswpsz = new byte[2];        //rmb 2       swap block count                                        0x023F        -> 01 80
    public byte[] m_s64k   = new byte[1];        //rmb 1       non-zero if swap block count is multiple of 64K         0x0241        -> 00
    public byte[] m_swinc  = new byte[11];       //rmb 11      Winchester configuration info                           0x0242        -> 00 00 00 00 00 00 2A 00 99 00 9A
    public byte[] m_sspare = new byte[11];       //rmb 11      spare bytes - future use                                0x024D        -> 00 9B 00 9C 00 9D 00 9E 00 9F 00
    public byte[] m_snfdn  = new byte[1];        //rmb 1       number of in core fdns                                  0x0258        -> A0           *snfdn * 2 = 320
    public byte[] m_scfdn  = new byte[512];      //rmb CFDN*2  in core free fdns                                       0x0259        variable (*snfdn * 2)
    public byte[] m_snfree = new byte[1];        //rmb 1       number of in core free blocks                           0x03B9        -> 03
    public byte[] m_sfree  = new byte[16384];    //rmb         CDBLKS*DSKADS in core free blocks                       0x03BA        -> 

    public fdn[] m_fdn = new fdn[592];
}
// [Serializable()]
public class RAW_SIR
{
    public const int sizeofVolumeLabel = 11;
    public const int sizeofDirEntry = 24;
    public const int sizeofSystemInformationRecord = 24;

    public RAW_SIR()
    {
        Array.Clear(caVolumeLabel, 0, caVolumeLabel.Length);
        cVolumeNumberHi  = 0x00;
        cVolumeNumberLo  = 0x00;
        cFirstUserTrack  = 0x00;
        cFirstUserSector = 0x00;
        cLastUserTrack   = 0x00;
        cLastUserSector  = 0x00;
        cTotalSectorsHi  = 0x00;
        cTotalSectorsLo  = 0x00;
        cMonth           = 0x00;
        cDay             = 0x00;
        cYear            = 0x00;
        cMaxTrack        = 0x00;
        cMaxSector       = 0x00;
    }

    public byte[] caVolumeLabel = new byte[sizeofVolumeLabel];    // $50 - $5A
    public byte cVolumeNumberHi;                    // $5B
    public byte cVolumeNumberLo;                    // $5C
    public byte cFirstUserTrack;                    // $5D
    public byte cFirstUserSector;                   // $5E
    public byte cLastUserTrack;                     // $5F
    public byte cLastUserSector;                    // $60
    public byte cTotalSectorsHi;                    // $61
    public byte cTotalSectorsLo;                    // $62
    public byte cMonth;                             // $63
    public byte cDay;                               // $64
    public byte cYear;                              // $65
    public byte cMaxTrack;                          // $66
    public byte cMaxSector;                         // $67
}
public class DIR_ENTRY
{
    public DIR_ENTRY()
    {
        Array.Clear(caFileName, 0, caFileName.Length);
        Array.Clear(caFileExtension, 0, caFileExtension.Length);

        cPad1           = 0x00;
        cPad2           = 0x00;
        cStartTrack     = 0x00;
        cStartSector    = 0x00;
        cEndTrack       = 0x00;
        cEndSector      = 0x00;
        cTotalSectorsHi = 0x00;
        cTotalSectorsLo = 0x00;
        cRandomFileInd  = 0x00;
        cPad3           = 0x00;
        cMonth          = 0x00;
        cDay            = 0x00;
        cYear           = 0x00;
    }

    public byte[] caFileName = new byte[8];
    public byte[] caFileExtension = new byte[3];
    public byte cPad1;
    public byte cPad2;
    public byte cStartTrack;
    public byte cStartSector;
    public byte cEndTrack;
    public byte cEndSector;
    public byte cTotalSectorsHi;
    public byte cTotalSectorsLo;
    public byte cRandomFileInd;
    public byte cPad3;
    public byte cMonth;
    public byte cDay;
    public byte cYear;
}
#endregion
namespace FLEXNetSharp
{
    public class DriveInfo
    {
        public int      mode;
        public string   MountedFilename;
        public int      NumberOfTracks;
        public byte[]   cNumberOfSectorsPerTrack = new byte[1];
        public long     NumberOfBytesPerTrack;
        public long     NumberOfSectorsPerTrack;
        public int      NumberOfSectorsPerCluster;
        public long     TotalNumberOfSectorOnMedia;
        public int      LogicalSectorSize;
    }

    public class ImageFile
    {
        public string     Name;
        public bool       readOnly;
        public Stream     stream;
        public DriveInfo  driveInfo = new DriveInfo();
        public FileFormat fileFormat;
        public bool trackAndSectorAreTrackAndSector = true;
    }

    public class Ports
    {
        public FileFormat currentFileFileFormat = FileFormat.fileformat_UNKNOWN;

        public Stream streamDir = null;
        public System.IO.Ports.SerialPort sp;
        public int          port;
        public int          state;
        public int          createState;
        public int          rate;
        public string       speed;
        public string       verbose;
        public string       autoMount;
        public ImageFile[]  imageFile = new ImageFile[4];
        public string       dirFilename;
        public int          currentDrive = 0;

        public int[] track = new int[4];
        public int[] sector = new int[4];

        public int sectorIndex = 0;
        public int calculatedCRC = 0;

        public int checksumIndex = 0;
        public int checksum = 0;

        public byte[] sectorBuffer = new byte[1024];        // allow for up to 1024 byte sectors

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

        private long m_lPartitionBias = -1;

        public const int sizeofVolumeLabel = 11;
        public const int sizeofDirEntry = 24;
        public const int sizeofSystemInformationRecord = 24;

        private int m_nTotalSectors = 0;
        private FileFormat m_nCurrentFileFormat;

        private int m_nSectorBias;

        public int SectorBias
        {
            get { return m_nSectorBias; }
            set { m_nSectorBias = value; }
        }

        private long lCurrentDirOffset = 0L, lOffset;
        private DIR_ENTRY stDirEntry;
        private byte[] szTargetFileTitle = new byte[8];
        private byte[] szTargetFileExt = new byte[3];

        public int sectorSize = 256;
        public int nLSNBlockSize = 256;

        public RAW_SIR ReadRAW_FLEX_SIR(Stream fs)
        {
            long currentPosition = fs.Position;

            RAW_SIR systemInformationRecord = new RAW_SIR();

            if (m_lPartitionBias >= 0)
                fs.Seek(m_lPartitionBias + 0x0310 - (0x100 * SectorBias), SeekOrigin.Begin);     // fseek(m_fp, m_lPartitionBias + 0x0310 - (0x100 * m_nSectorBias), SEEK_SET);
            else
                fs.Seek(0x0210, SeekOrigin.Begin);                                                  // fseek (fs, 0x0210, SEEK_SET);

            fs.Read(systemInformationRecord.caVolumeLabel, 0, 11);                          // fread (stSystemInformationRecord.caVolumeLabel      , 1, sizeof (stSystemInformationRecord.caVolumeLabel), fs);  // $50 - $5A
            systemInformationRecord.cVolumeNumberHi     = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cVolumeNumberHi   , 1, 1, fs);      // $5B
            systemInformationRecord.cVolumeNumberLo     = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cVolumeNumberLo   , 1, 1, fs);      // $5C
            systemInformationRecord.cFirstUserTrack     = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cFirstUserTrack   , 1, 1, fs);      // $5D
            systemInformationRecord.cFirstUserSector    = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cFirstUserSector  , 1, 1, fs);      // $5E
            systemInformationRecord.cLastUserTrack      = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cLastUserTrack    , 1, 1, fs);      // $5F
            systemInformationRecord.cLastUserSector     = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cLastUserSector   , 1, 1, fs);      // $60
            systemInformationRecord.cTotalSectorsHi     = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cTotalSectorsHi   , 1, 1, fs);      // $61
            systemInformationRecord.cTotalSectorsLo     = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cTotalSectorsLo   , 1, 1, fs);      // $62
            systemInformationRecord.cMonth              = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cMonth            , 1, 1, fs);      // $63
            systemInformationRecord.cDay                = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cDay              , 1, 1, fs);      // $64
            systemInformationRecord.cYear               = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cYear             , 1, 1, fs);      // $65
            systemInformationRecord.cMaxTrack           = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cMaxTrack         , 1, 1, fs);      // $66
            systemInformationRecord.cMaxSector          = (byte)fs.ReadByte();              // fread (&stSystemInformationRecord.cMaxSector        , 1, 1, fs);      // $67

            fs.Seek(currentPosition, SeekOrigin.Begin);

            return systemInformationRecord;
        }

        public UniFLEX_SIR ReadUNIFLEX_SIR(Stream fs)
        {
            long currentPosition = fs.Position;

            UniFLEX_SIR drive_SIR = new UniFLEX_SIR();

            fs.Seek(512, SeekOrigin.Begin);         // fseek (fs, 512, SEEK_SET);

            drive_SIR.m_supdt[0]    = (byte)fs.ReadByte();  // fread (&drive_SIR.m_supdt,  1,   1, fs);    //rmb 1       sir update flag                                         0x0200        -> 00 
            drive_SIR.m_swprot[0]   = (byte)fs.ReadByte();  // fread (&drive_SIR.m_swprot, 1,   1, fs);    //rmb 1       mounted read only flag                                  0x0201        -> 00 
            drive_SIR.m_slkfr[0]    = (byte)fs.ReadByte();  // fread (&drive_SIR.m_slkfr,  1,   1, fs);    //rmb 1       lock for free list manipulation                         0x0202        -> 00 
            drive_SIR.m_slkfdn[0]   = (byte)fs.ReadByte();  // fread (&drive_SIR.m_slkfdn, 1,   1, fs);    //rmb 1       lock for fdn list manipulation                          0x0203        -> 00 

            fs.Read(drive_SIR.m_sintid, 0, 4);              // fread (&drive_SIR.m_sintid, 1,   4, fs);    //rmb 4       initializing system identifier                          0x0204        -> 00 

            fs.Read(drive_SIR.m_scrtim, 0, 4);              // fread (&drive_SIR.m_scrtim, 1,   4, fs);    //rmb 4       creation time                                           0x0208        -> 11 44 F3 FC
            fs.Read(drive_SIR.m_sutime, 0, 4);              // fread (&drive_SIR.m_sutime, 1,   4, fs);    //rmb 4       date of last update                                     0x020C        -> 11 44 F1 51
            fs.Read(drive_SIR.m_sszfdn, 0, 2);              // fread (&drive_SIR.m_sszfdn, 1,   2, fs);    //rmb 2       size in blocks of fdn list                              0x0210        -> 00 4A          = 74
            fs.Read(drive_SIR.m_ssizfr, 0, 3);              // fread (&drive_SIR.m_ssizfr, 1,   3, fs);    //rmb 3       size in blocks of volume                                0x0212        -> 00 08 1F       = 2079
            fs.Read(drive_SIR.m_sfreec, 0, 3);              // fread (&drive_SIR.m_sfreec, 1,   3, fs);    //rmb 3       total free blocks                                       0x0215        -> 00 04 9C       = 
            fs.Read(drive_SIR.m_sfdnc , 0, 2);              // fread (&drive_SIR.m_sfdnc,  1,   2, fs);    //rmb 2       free fdn count                                          0x0218        -> 01 B0
            fs.Read(drive_SIR.m_sfname, 0, 14);             // fread (&drive_SIR.m_sfname, 1,  14, fs);    //rmb 14      file system name                                        0x021A        -> 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
            fs.Read(drive_SIR.m_spname, 0, 14);             // fread (&drive_SIR.m_spname, 1,  14, fs);    //rmb 14      file system pack name                                   0x0228        -> 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
            fs.Read(drive_SIR.m_sfnumb, 0, 2);              // fread (&drive_SIR.m_sfnumb, 1,   2, fs);    //rmb 2       file system number                                      0x0236        -> 00 00
            fs.Read(drive_SIR.m_sflawc, 0, 2);              // fread (&drive_SIR.m_sflawc, 1,   2, fs);    //rmb 2       flawed block count                                      0x0238        -> 00 00


            drive_SIR.m_sdenf[0] = (byte)fs.ReadByte();     // fread (&drive_SIR.m_sdenf,  1,   1, fs);    //rmb 1       density flag - 0=single                                 0x023A        -> 01
            drive_SIR.m_ssidf[0] = (byte)fs.ReadByte();     // fread (&drive_SIR.m_ssidf,  1,   1, fs);    //rmb 1       side flag - 0=single                                    0x023B        -> 01

            fs.Read(drive_SIR.m_sswpbg, 0, 3);              // fread (&drive_SIR.m_sswpbg, 1,   3, fs);    //rmb 3       swap starting block number                              0x023C        -> 00 08 20
            fs.Read(drive_SIR.m_sswpsz, 0, 2);              // fread (&drive_SIR.m_sswpsz, 1,   2, fs);    //rmb 2       swap block count                                        0x023F        -> 01 80

            drive_SIR.m_s64k[0] = (byte)fs.ReadByte();      // fread (&drive_SIR.m_s64k,   1,   1, fs);    //rmb 1       non-zero if swap block count is multiple of 64K         0x0241        -> 00

            fs.Read(drive_SIR.m_swinc, 0, 11);              // fread (&drive_SIR.m_swinc,  1,  11, fs);    //rmb 11      Winchester configuration info                           0x0242        -> 00 00 00 00 00 00 2A 00 99 00 9A
            fs.Read(drive_SIR.m_sspare, 0, 11);             // fread (&drive_SIR.m_sspare, 1,  11, fs);    //rmb 11      spare bytes - future use                                0x024D        -> 00 9B 00 9C 00 9D 00 9E 00 9F 00

            drive_SIR.m_snfdn[0] = (byte)fs.ReadByte();     // fread (&drive_SIR.m_snfdn,  1,   1, fs);    //rmb 1       number of in core fdns                                  0x0278        -> A0     *snfdn * 2 = 320

            fs.Read(drive_SIR.m_scfdn, 0, 169);             // fread (&drive_SIR.m_scfdn,  1, 160, fs);    //rmb CFDN*2  in core free fdns                                       0x0279        variable (*snfdn * 2)

            drive_SIR.m_snfree[0] = (byte)fs.ReadByte();    // fread (&drive_SIR.m_snfree, 1,   1, fs);    //rmb 1       number of in core free blocks                           0x03B9        -> 03

            fs.Read(drive_SIR.m_sfree, 0, 300);             // fread (&drive_SIR.m_sfree,  1, 300, fs);    //rmb         CDBLKS*DSKADS in core free blocks                       0x03BA        -> 

            fs.Seek(currentPosition, SeekOrigin.Begin);

            return drive_SIR;
        }

        public uint ConvertToInt16(byte[] value)
        {
            return (uint)(value[0] * 256) + (uint)(value[1]);
        }

        public uint ConvertToInt24(byte[] value)
        {
            return (uint)(value[0] * 256 * 256) + (uint)(value[1] * 256) + (uint)(value[2]);
        }

        // Valid FLEX diskette image formats supported by SWTPC DC-4 and DMAF disk controllers (DSK sizes are simply FLEX max sector * FLEX max track * 1 (number of cylinders)
        //
        //      description                         IMA size    DSK size    cyl hd  sector per side track 0     sectors per side (the rest) 
        //      ----------------------------------- -------     -------     --- --- --------------------------- --------------------------- 
        //                                                                              FLEX max sector             FLEX max track
        //          5 1/4" 80 track                                                     ---------------             --------------
        //																	                                                how to calculate the IMA size.
        //																	                                                ------------------------------------------
        //      DS/DD with SD (FM) cylinder 0.      733184      737280      80  2   10      36                  18      79  ((79 * 18 * 2) + (10 * 2)) * 256 = IMA size
        //      DS/SD with SD (FM) cylinder 0.      409600      409600      80  2   10      20                  10      79  ((79 * 10 * 2) + (10 * 2)) * 256 = IMA size
        //      SS/DD with SD (FM) cylinder 0.      366592      368640      80  1   10      18                  18      79  ((79 * 18 * 1) + (10 * 1)) * 256 = IMA size
        //      SS/SD with SD (FM) cylinder 0.      204800      204800      80  1   10      10                  10      79  ((79 * 10 * 1) + (10 * 1)) * 256 = IMA size
        //
        //          5 1/4" 40 track 
        //
        //      DS/DD with SD (FM) cylinder 0.      364544      368640      40  2   10      36                  18      39  ((39 * 18 * 2) + (10 * 2)) * 256 = IMA size
        //      DS/SD with SD (FM) cylinder 0       204800      204800      40  2   10      20                  10      39  ((39 * 10 * 2) + (10 * 2)) * 256 = IMA size
        //      SS/DD with SD (FM) cylinder 0.      182272      184320      40  1   10      18                  18      39  ((39 * 18 * 1) + (10 * 1)) * 256 = IMA size
        //      SS/SD with SD (FM) cylinder 0.      102400      102400      40  1   10      10                  10      39  ((39 * 10 * 1) + (10 * 1)) * 256 = IMA size
        //
        //          5 1/4" 35 track
        //
        //      DS/DD with SD (FM) cylinder 0.      318464      322560      35  2   10      36                  18      34  ((34 * 18 * 2) + (10 * 2)) * 256 = IMA size
        //      DS/SD with SD (FM) cylinder 0       179200      179200      35  2   10      20                  10      34  ((34 * 10 * 2) + (10 * 2)) * 256 = IMA size
        //      SS/DD with SD (FM) cylinder 0.      159232      161280      35  1   10      18                  18      34  ((34 * 18 * 1) + (10 * 1)) * 256 = IMA size
        //      SS/SD with SD (FM) cylinder 0.       89600       89600      35  1   10      10                  10      34  ((34 * 10 * 1) + (10 * 1)) * 256 = IMA size
        //
        //          8" 77 track FLEX DMAF-2
        //
        //      DS/DD with SD (FM) cylinder 0.     1019392     1025024      77  2   15      52                  26      76  ((76 * 26 * 2) + (15 * 2)) * 256 = IMA size
        //      DS/SD with SD (FM) cylinder 0       591360      591360      77  2   15      30                  15      76  ((76 * 15 * 2) + (15 * 2)) * 256 = IMA size 
        //      SS/DD with SD (FM) cylinder 0.      509696      512512      77  1   15      26                  26      76  ((76 * 26 * 1) + (15 * 1)) * 256 = IMA size 
        //      SS/SD with SD (FM) cylinder 0.      295680      295680      77  1   15      15                  15      76  ((76 * 15 * 1) + (15 * 1)) * 256 = IMA size
        //
        //          These are only supported by GoTek       1.44MB format 80 Track DSHD

        //      DS/HD with SD (FM) cylinder 0.     1461248     1474560      80  2   10      72                  36      79  ((79 * 36 * 2) + (10 * 2)) * 256 = IMA size (5688 + 20 = 5708) * 256 = 1,461,248


        public enum ValidFLEXGeometries
        {
            UNKNOWN = 0,
            SSSD35T,
            SSDD35T,
            DSSD35T,
            DSDD35T,
            SSSD40T,
            SSDD40T,
            DSSD40T,
            DSDD40T,
            SSSD77T,
            SSDD77T,
            DSSD77T,
            DSDD77T,
            SSSD80T,
            SSDD80T,
            DSSD80T,
            DSDD80T,
            DSHD80T
        }

        // these are the public variable that are availabe and are set by GetFLEXGeometry

        public bool singleSided = true;
        public bool isFiveInch = true;
        public int currentDiskDiameter = 0;
        public int maxSector = 0;
        public int maxTrack = 0;
        public int sectorOnTrackZero = 0;
        public int sectorsToEndOfDirectory = 0;
        public bool trackZeroIsBigEnough = false;
        public int sectorsToAddToFreeChain = 0;
        public string getGeometryErrorMessage = "";

        public ValidFLEXGeometries currentDiskGeometry = ValidFLEXGeometries.UNKNOWN;

        public FileStream currentlyOpenedImageFileStream = null;

        public void GetFLEXGeometry(Stream fs)
        {
            currentDiskDiameter = 0;
            currentDiskGeometry = ValidFLEXGeometries.UNKNOWN;
            getGeometryErrorMessage = "";

            if (currentFileFileFormat == FileFormat.fileformat_FLEX || currentFileFileFormat == FileFormat.fileformat_FLEX_IMA)
            {
                // only do this for FLEX .DSK files

                fs.Seek(m_lPartitionBias + 0x0310 - (sectorSize * SectorBias), SeekOrigin.Begin);
                RAW_SIR stSystemInformationRecord = ReadRAW_FLEX_SIR(fs);

                // use the sector and track count to calculate the expected file size for a .DSK file

                maxSector = stSystemInformationRecord.cMaxSector;
                maxTrack = stSystemInformationRecord.cMaxTrack;

                if (maxSector == 10 || maxSector == 20 || maxSector == 15)
                {
                    getGeometryErrorMessage = "This appears to be a single density image so no converion id required.";
                }
                //else      // commented out so we can set geometry on single density.
                {
                    if ((maxSector * maxTrack * 256) != fs.Length)
                    {
                        // get the number of sectors on track 0 - start at sector 5 to position to the start of the directory

                        sectorOnTrackZero = 5;
                        sectorsToEndOfDirectory = 5;
                        byte[] trackLinkBytes = new byte[17];

                        for (int i = 5; i < maxSector; i++)
                        {
                            fs.Seek(i * 256, SeekOrigin.Begin);
                            fs.Read(trackLinkBytes, 0, 17);
                            if (trackLinkBytes[0] == 0)
                            {
                                sectorOnTrackZero++;
                                if (trackLinkBytes[16] != 0)
                                {
                                    sectorsToEndOfDirectory++;
                                }
                            }
                            else
                                break;
                        }

                        // see if the directory is has more sectors than track 0 on an IMA image can handle

                        sectorsToAddToFreeChain = 0;
                        trackZeroIsBigEnough = false;

                        if (sectorsToEndOfDirectory <= 20)
                        {
                            trackZeroIsBigEnough = true;
                        }
                        else
                        {
                            // set up to move the extra directory sectors to the end of the free chain.

                            sectorsToAddToFreeChain = sectorsToEndOfDirectory - 20;
                            trackZeroIsBigEnough = true;
                        }

                        //if (trackZeroIsBigEnough)   // this will always be true
                        {
                            // see if this could be a modified image that had sectors added to track 0 to make it orthogonal

                            if ((maxSector * (maxTrack + 1) * 256) == fs.Length)      // if the calculated file size = actaul file image size, this is either sssd or dssd or modified ssdd or modified dsdd
                            {
                                // could be, but also could be Single Density diskette original

                                if (maxTrack == 34 || maxTrack == 39 || maxTrack == 76 || maxTrack == 79)   // these are the valid number of tracks for an actual diskette
                                {
                                    if (maxTrack != 76) // 5 1/4"
                                    {
                                        currentDiskDiameter = 5;

                                        // these are the valid number of sectors on an actual 5 1/4" diskettes (72 is QUAD density)
                                        if (maxSector == 10 || maxSector == 20)
                                        {
                                            // this is single density
                                            if (maxSector == 10)
                                            {
                                                // this is single sided
                                                switch (maxTrack)
                                                {
                                                    case 34:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSSD35T;
                                                        break;
                                                    case 39:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSSD40T;
                                                        break;
                                                    case 76:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSSD77T;
                                                        break;
                                                    case 79:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSSD80T;
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                // this is double sided
                                                switch (maxTrack)
                                                {
                                                    case 34:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSSD35T;
                                                        break;
                                                    case 39:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSSD40T;
                                                        break;
                                                    case 76:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSSD77T;
                                                        break;
                                                    case 79:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSSD80T;
                                                        break;
                                                }
                                            }
                                        }
                                        else if (maxSector == 18 || maxSector == 36)
                                        {
                                            // this is double density
                                            if (maxSector == 18)
                                            {
                                                // this is single sided
                                                switch (maxTrack)
                                                {
                                                    case 34:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSDD35T;
                                                        break;
                                                    case 39:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSDD40T;
                                                        break;
                                                    case 76:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSDD77T;
                                                        break;
                                                    case 79:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSDD80T;
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                // this is double sided
                                                switch (maxTrack)
                                                {
                                                    case 34:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSDD35T;
                                                        break;
                                                    case 39:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSDD40T;
                                                        break;
                                                    case 76:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSDD77T;
                                                        break;
                                                    case 79:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSDD80T;
                                                        break;
                                                }
                                            }
                                        }
                                        else if (maxSector == 72)
                                        {
                                            if (maxTrack == 79)
                                                currentDiskGeometry = ValidFLEXGeometries.DSHD80T;
                                        }
                                    }
                                    else                // 8"
                                    {
                                        currentDiskDiameter = 8;

                                        // these are the valid number of sectors on an actual 8" diskettes
                                        if (maxSector == 15 || maxSector == 30)
                                        {
                                            if (maxSector == 15)
                                            {
                                                // this is single sided
                                                switch (maxTrack)
                                                {
                                                    case 34:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSSD35T;
                                                        break;
                                                    case 39:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSSD40T;
                                                        break;
                                                    case 76:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSSD77T;
                                                        break;
                                                    case 79:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSSD80T;
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                // this is double sided
                                                switch (maxTrack)
                                                {
                                                    case 34:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSSD35T;
                                                        break;
                                                    case 39:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSSD40T;
                                                        break;
                                                    case 76:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSSD77T;
                                                        break;
                                                    case 79:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSSD80T;
                                                        break;
                                                }
                                            }
                                        }
                                        else if (maxSector == 26 || maxSector == 52)
                                        {
                                            // this is double density
                                            if (maxSector == 26)
                                            {
                                                // this is single sided
                                                switch (maxTrack)
                                                {
                                                    case 34:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSDD35T;
                                                        break;
                                                    case 39:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSDD40T;
                                                        break;
                                                    case 76:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSDD77T;
                                                        break;
                                                    case 79:
                                                        currentDiskGeometry = ValidFLEXGeometries.SSDD80T;
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                // this is double sided
                                                switch (maxTrack)
                                                {
                                                    case 34:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSDD35T;
                                                        break;
                                                    case 39:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSDD40T;
                                                        break;
                                                    case 76:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSDD77T;
                                                        break;
                                                    case 79:
                                                        currentDiskGeometry = ValidFLEXGeometries.DSDD80T;
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                }

                                if (currentDiskDiameter != 0 && currentDiskGeometry != ValidFLEXGeometries.UNKNOWN)
                                {
                                    switch (maxSector)
                                    {
                                        case 18:
                                            isFiveInch = true;
                                            singleSided = true;
                                            break;
                                        case 26:
                                            isFiveInch = false;
                                            singleSided = true;
                                            break;
                                        case 36:
                                            singleSided = false;
                                            break;
                                        case 52:
                                            isFiveInch = false;
                                            singleSided = false;
                                            break;
                                        case 72:
                                            isFiveInch = true;
                                            singleSided = false;
                                            break;
                                    }
                                }
                                else
                                {
                                    string message = string.Format("    Cylinders: {0}\r\n    Sectors on track 0: {1}\r\n    Max Sectors: {2}", maxTrack + 1, sectorOnTrackZero, maxSector);
                                    getGeometryErrorMessage = string.Format("This iamge does not match any FLEX standard formats\r\n\r\n{0}", message);
                                }
                            }
                            else
                                getGeometryErrorMessage = "This .DSK image does not seem to be valid. The calcutated expected size does not match the actual size.";
                        }
                    }
                }
            }

            switch (currentFileFileFormat)
            {
                case FileFormat.fileformat_FLEX_IMA:
                    getGeometryErrorMessage = "This image is already in .IMA format. A direct copy will be made.";
                    break;

                case FileFormat.fileformat_FLEX_IDE:
                    getGeometryErrorMessage = "We cannot convert IDE images to .IMA.";
                    break;

                default:
                    if (currentDiskGeometry == ValidFLEXGeometries.UNKNOWN)
                        getGeometryErrorMessage = "The current diskette image does not match any known FLEX IMA formats";
                    break;
            }
        }

        public FileFormat GetFileFormat(Stream fs)
        {
            FileFormat ff = FileFormat.fileformat_UNKNOWN;

            if (fs != null)
            {
                // save current position so we can restore it later.

                long currentPosition = fs.Position;
                long fileLength = fs.Length;        // int fd = _fileno (fs);   long fileLength = _filelength(fd);

                byte[] imdBytes = new byte[4];

                fs.Seek(0, SeekOrigin.Begin);
                fs.Read(imdBytes, 0, 4);
                ASCIIEncoding ascii = new ASCIIEncoding();
                string imdString = ascii.GetString(imdBytes);
                fs.Seek(currentPosition, SeekOrigin.Begin);

                if (imdString == "IMD ")
                {
                    // read past the header - it ends with 0x1A.

                    for (int i = 0; i < fileLength; i++)
                    {
                        byte b = (byte)fs.ReadByte();
                        if (b == 0x1A)
                        {
                            // we have reached the end of the header
                            break;
                        }
                    }

                    if (fs.Position != fileLength)
                        ff = FileFormat.fileformat_IMD;
                }
                else
                {
                    // First Check for OS9 format

                    OS9_ID_SECTOR stIDSector = new OS9_ID_SECTOR();

                    fs.Seek(0, SeekOrigin.Begin);

                    stIDSector.cTOT[0] = (byte)fs.ReadByte();         // fread (&stIDSector.cTOT[0], 1, 1, fs);   // Total Number of sector on media
                    stIDSector.cTOT[1] = (byte)fs.ReadByte();         // fread (&stIDSector.cTOT[1], 1, 1, fs);
                    stIDSector.cTOT[2] = (byte)fs.ReadByte();         // fread(&stIDSector.cTOT[2], 1, 1, fs);
                    stIDSector.cTKS[0] = (byte)fs.ReadByte();         // fread (&stIDSector.cTKS[0], 1, 1, fs);   // Sectors Per Track (not track 0)

                    fs.Seek(16, SeekOrigin.Begin);                    // fseek(fs, 16, SEEK_SET);

                    stIDSector.cFMT[0] = (byte)fs.ReadByte();         // fread (&stIDSector.cFMT[0], 1, 1, fs);     // Disk Format Byte
                    stIDSector.cSPT[0] = (byte)fs.ReadByte();         // fread (&stIDSector.cSPT[0], 1, 1, fs);     // Sectors per track on track 0 high byte
                    stIDSector.cSPT[1] = (byte)fs.ReadByte();         // fread (&stIDSector.cSPT[1], 1, 1, fs);     // Sectors per track on track 0 low  byte

                    fs.Seek(104, SeekOrigin.Begin);                   // Get the LSNSize from 68K Os9 disk

                    stIDSector.cDD_LSNSize[0] = (byte)fs.ReadByte();  // fread (&stIDSector.cSPT[1], 1, 1, fs);     // Sectors per track on track 0 high  byte
                    stIDSector.cDD_LSNSize[1] = (byte)fs.ReadByte();  // fread (&stIDSector.cSPT[1], 1, 1, fs);     // Sectors per track on track 0 low   byte

                    // calculate nLSNBlockSize from sector size in SIR

                    nLSNBlockSize = stIDSector.cDD_LSNSize[1] + (stIDSector.cDD_LSNSize[0] * 256);

                    // these are used by OS9/68K - on EmuOS9Boot.Dsk these are all zeros if nLSNBlockSize = 0

                    sectorSize = 256;                   // default sector size if the SIR does not set it
                    if (nLSNBlockSize == 0)
                        nLSNBlockSize = 256;
                    else
                        sectorSize = nLSNBlockSize;

                    //if (nLSNBlockSize != 0)
                    //{
                    //    sectorSize = nLSNBlockSize;
                    //}

                    // There's no point in going any further if the file size if not right

                    int nNumberOfTracks = (int)stIDSector.cTKS[0];
                    int nSectorsPerTrack = stIDSector.cSPT[1] + (stIDSector.cSPT[0] * 256);
                    int nTotalSectors = stIDSector.cTOT[2] + (stIDSector.cTOT[1] * 256) + (stIDSector.cTOT[0] * 65536);

                    // get disk size based on reported number of sectors in the first three bytes of the SIR

                    long nDSKDiskSize = (long)(nTotalSectors * nLSNBlockSize) & 0x00000000FFFFFF00;
                    long nIMAdiskSize = 0;

                    if (nDSKDiskSize == (fileLength & 0x00000000FFFFFF00))
                    {
                        ff = FileFormat.fileformat_OS9;
                        SectorBias = 0;
                        m_lPartitionBias = 0;

                        currentFileFileFormat = ff;
                    }
                    else
                    {
                        sectorSize = 256;

                        //let's see if this is a 128 byte sector diskette

                        //{
                        //    get current offset so we can set it back when we are done

                        //    long currentOffset = fs.Position;

                        //    the System Information Record on a mini FLEX diskette is at
                        //    offset 0x0080 in the diskette image file.


                        //      an example from the DISK31_2.DSK file looks like this:

                        //     00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 07 0A 22  12 01 EF 00 00 00 00 00
                        //     00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00
                        //     00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00
                        //     00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00


                        //     the one from the MFLXSYS.DSK looks like this:

                        //     00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 10 0D 10  0B 00 3D 00 00 00 00 00
                        //     00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00
                        //     00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00
                        //     00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00


                        //      The directory starts at offset 0x0100.The first 8 bytes of the directory sector are unused except
                        //      for sector linkage. Each entry consists of 24 bytes.

                        //      A single sided 35 track diskette
                        //          35 tracks * 17 sectors per track = 595 sectors * 128 bytes per sector = 76, 160 bytes
                        //          605 sectors = 77, 440
                        //          614 sectors = 78, 592


                        //      A single sided 40 track diskette will be 35 tracks * 18 sectors per track * 128 bytes per sector =


                        //     return position to where we were when we got here

                        //    fs.Seek(currentOffset, SeekOrigin.Begin);
                        //}

                        // not OS-9, see if this image conforms to a valid FLEX diskette image

                        int nMaxSector;
                        int nMaxTrack;

                        SectorBias = 1;

                        RAW_SIR stSystemInformationRecord = ReadRAW_FLEX_SIR(fs);

                        nMaxSector = stSystemInformationRecord.cMaxSector;
                        nMaxTrack = stSystemInformationRecord.cMaxTrack;
                        nTotalSectors = stSystemInformationRecord.cTotalSectorsHi * 256 + stSystemInformationRecord.cTotalSectorsLo;

                        nDSKDiskSize = (long)(nMaxTrack + 1) * (long)nMaxSector * (long)sectorSize;           // Track is 0 based, sector is 1 based
                        nIMAdiskSize = 0;
                        switch (nMaxSector)
                        {
                            // these are the only valid max sector values for IMA (10 or 18 = single side, 20 or 36 = double sided)

                            case 10:
                                nIMAdiskSize = (long)(((nMaxTrack) * (long)nMaxSector * (long)sectorSize) + (sectorSize * 10));           // single sided
                                break;
                            case 20:
                                nIMAdiskSize = (long)(((nMaxTrack) * (long)nMaxSector * (long)sectorSize) + (sectorSize * 10 * 2));       // double sided
                                break;
                            case 18:
                                nIMAdiskSize = (long)(((nMaxTrack) * (long)nMaxSector * (long)sectorSize) + (sectorSize * 10));           // single sided
                                break;
                            case 36:
                                nIMAdiskSize = (long)(((nMaxTrack) * (long)nMaxSector * (long)sectorSize) + (sectorSize * 10 * 2));       // double sided
                                break;

                            case 72:
                                nIMAdiskSize = (long)(((nMaxTrack) * (long)nMaxSector * (long)sectorSize) + (sectorSize * 10 * 2));       // double sided
                                break;

                            // 8" images

                            case 15:        // 8" single sided single density
                                nIMAdiskSize = (long)(((nMaxTrack + 1) * (long)nMaxSector * (long)sectorSize));
                                break;
                            case 26:        // 8" single sided double density
                                nIMAdiskSize = (long)(((nMaxTrack) * (long)nMaxSector * (long)sectorSize) + (sectorSize * 15));
                                break;
                            case 52:        // 8" double sided double density
                                nIMAdiskSize = (long)(((nMaxTrack) * (long)nMaxSector * (long)sectorSize) + (sectorSize * 15 * 2));
                                break;

                            // special GoTek images
                            case 255:        // 
                                nIMAdiskSize = (long)(((nMaxTrack) * (long)nMaxSector * (long)sectorSize) + (sectorSize * 15 * 2));
                                break;
                        }

                        if (fileLength != nIMAdiskSize)
                        {
                            if (nDSKDiskSize == (fileLength & 0xFFFFFF00))
                            {
                                ff = FileFormat.fileformat_FLEX;
                                SectorBias = 1;
                                m_lPartitionBias = 0;

                                currentFileFileFormat = ff;

                                GetFLEXGeometry(fs);
                            }
                            else if (nIMAdiskSize == (fileLength & 0xFFFFFF00))
                            {
                                ff = FileFormat.fileformat_FLEX_IMA;
                                SectorBias = 1;
                                m_lPartitionBias = 0;

                                currentFileFileFormat = ff;
                            }
                            else
                            {
                                // not OS-9 or FLEX, see if this image conforms to a valid UniFLEX diskette image

                                UniFLEX_SIR drive_SIR = ReadUNIFLEX_SIR(fs);

                                uint nFDNSize = ConvertToInt16(drive_SIR.m_sszfdn);
                                uint nVolumeSize = ConvertToInt24(drive_SIR.m_ssizfr);
                                uint nSwapSize = ConvertToInt16(drive_SIR.m_sswpsz);

                                nDSKDiskSize = (nVolumeSize + nSwapSize + 1) * 512;
                                if (nDSKDiskSize == (fileLength & 0xFFFFFF00) || ((nVolumeSize + nSwapSize) * 512) == (fileLength & 0xFFFFFF00))
                                {
                                    ff = FileFormat.fileformat_UniFLEX;
                                    currentFileFileFormat = ff;
                                }
                                else
                                {
                                    // see if this an IDE drive with multiple partitions

                                    if ((fileLength % 256) > 0)
                                    {
                                        // could be - get the drive info and see if it makes sense

                                        byte[] cInfoSize = new byte[2];
                                        uint nInfoSize = 0;

                                        fs.Seek(-2, SeekOrigin.End);            // (fs, -2, SEEK_END);
                                        fs.Read(cInfoSize, 0, 2);               // fread (cInfoSize, 1, 2, fs);

                                        nInfoSize = ConvertToInt16(cInfoSize);
                                        if (nInfoSize == (fileLength % 256))
                                        {
                                            // Not FLEX, OS-9 or UniFLEX, assume it is FLEX IDE (multiple partitions) format used on
                                            // the driver for the PIA IDE interface board if we get this far.

                                            ff = FileFormat.fileformat_FLEX_IDE;
                                            SectorBias = 0;
                                            m_lPartitionBias = 0;

                                            currentFileFileFormat = ff;
                                        }
                                    }
                                    else
                                    {
                                        // see if this is a minix floppy
                                        //
                                        //      read the two bytes at 0x0410 - if it is 0x13, 0x7F - this is probably a MINIX diskette image

                                        byte[] magic = new byte[2];
                                        fs.Seek(0x410, SeekOrigin.Begin);       // seek to magic word
                                        fs.Read(magic, 0, 2);                   // get magic bytes

                                        if (magic[0] == 0x13 && magic[1] == 0x7F)
                                        {
                                            sectorSize = 512;

                                            ff = FileFormat.fileformat_MINIX_68K;       // big endian
                                            currentFileFileFormat = ff;
                                        }
                                        else if (magic[1] == 0x13 && magic[0] == 0x7F)
                                        {
                                            sectorSize = 512;

                                            ff = FileFormat.fileformat_MINIX_IBM;       // little endian
                                            currentFileFileFormat = ff;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // this is already an IMA disk image (probably 8" for DMAF controller

                            ff = FileFormat.fileformat_FLEX_IMA;
                            SectorBias = 1;
                            m_lPartitionBias = 0;

                            currentFileFileFormat = ff;
                        }
                        fs.Seek(currentPosition, SeekOrigin.Begin);
                    }
                }

                fs.Seek(currentPosition, SeekOrigin.Begin);
            }

            m_nCurrentFileFormat = ff;

            return ff;
        }

        /// <summary>
        /// Determine the file format OS9 - FLEX or UniFLEX
        /// </summary>
        /// <param name="fs"></param>
        /// <returns></returns>
        //public FileFormat GetFileFormat(Stream fs)
        //{
        //    FileFormat ff = FileFormat.fileformat_UNKNOWN;

        //    if (fs != null)
        //    {
        //        long currentPosition = fs.Position;
        //        long fileLength = fs.Length;        // int fd = _fileno (fs);   long fileLength = _filelength(fd);

        //        // First Check for OS9 format

        //        OS9_ID_SECTOR stIDSector = new OS9_ID_SECTOR();

        //        fs.Seek(0, SeekOrigin.Begin);

        //        stIDSector.cTOT[0] = (byte)fs.ReadByte();         // fread (&stIDSector.cTOT[0], 1, 1, fs);   // Total Number of sector on media
        //        stIDSector.cTOT[1] = (byte)fs.ReadByte();         // fread (&stIDSector.cTOT[1], 1, 1, fs);
        //        stIDSector.cTOT[2] = (byte)fs.ReadByte();         // fread (&stIDSector.cTOT[2], 1, 1, fs);
        //        stIDSector.cTKS[0] = (byte)fs.ReadByte();         // fread (&stIDSector.cTKS[0], 1, 1, fs);   // Sectors Per Track (not track 0)

        //        fs.Seek(16, SeekOrigin.Begin);                    // fseek(fs, 16, SEEK_SET);

        //        stIDSector.cFMT[0] = (byte)fs.ReadByte();         // fread (&stIDSector.cFMT[0], 1, 1, fs);     // Disk Format Byte
        //        stIDSector.cSPT[0] = (byte)fs.ReadByte();         // fread (&stIDSector.cSPT[0], 1, 1, fs);     // Sectors per track on track 0 high byte
        //        stIDSector.cSPT[1] = (byte)fs.ReadByte();         // fread (&stIDSector.cSPT[1], 1, 1, fs);     // Sectors per track on track 0 low  byte

        //        // There's no point in going any further if the file size if not right

        //        int nSectorsPerTrack = (int)stIDSector.cTKS[0];
        //        int nSectorsPerTrackZero = stIDSector.cSPT[1] + (stIDSector.cSPT[0] * 256);
        //        int nTotalSectors = stIDSector.cTOT[2] + (stIDSector.cTOT[1] * 256) + (stIDSector.cTOT[0] * 1024);

        //        long nDiskSize = (long)(nTotalSectors * 256);
        //        nDiskSize += (long)((nSectorsPerTrack - nSectorsPerTrackZero) * 256);

        //        if (nDiskSize == (fileLength & 0xFFFFFF00))
        //        {
        //            ff = FileFormat.fileformat_OS9;
        //            SectorBias = 0;
        //            m_lPartitionBias = 0;
        //        }
        //        else
        //        {
        //            int nMaxSector;
        //            int nMaxTrack;

        //            RAW_SIR stSystemInformationRecord = ReadRAW_SIR(fs);

        //            nMaxSector = stSystemInformationRecord.cMaxSector;
        //            nMaxTrack = stSystemInformationRecord.cMaxTrack;
        //            nTotalSectors = stSystemInformationRecord.cTotalSectorsHi * 256 + stSystemInformationRecord.cTotalSectorsLo;

        //            nDiskSize = (long)(nMaxTrack + 1) * (long)nMaxSector * (long)256;   // Track is 0 based, sector is 1 based

        //            if (nDiskSize == (fileLength & 0xFFFFFF00))
        //            {
        //                ff = FileFormat.fileformat_FLEX;
        //                SectorBias = 1;
        //                m_lPartitionBias = 0;
        //            }
        //            else
        //            {
        //                UniFLEX_SIR drive_SIR = ReadUNIFLEX_SIR(fs);

        //                uint nFDNSize = ConvertToInt16(drive_SIR.m_sszfdn);
        //                uint nVolumeSize = ConvertToInt24(drive_SIR.m_ssizfr);
        //                uint nSwapSize = ConvertToInt16(drive_SIR.m_sswpsz);

        //                nDiskSize = (nVolumeSize + nSwapSize + 1) * 512;
        //                if (nDiskSize == (fileLength & 0xFFFFFF00))
        //                {
        //                    ff = FileFormat.fileformat_UniFLEX;
        //                }
        //                else
        //                {
        //                    // see if this an IDE drive with multiple partitions

        //                    if ((fileLength % 256) > 0)
        //                    {
        //                        // could be - get the drive info and see if it makes sence

        //                        byte[] cInfoSize = new byte[2];
        //                        uint nInfoSize = 0;

        //                        fs.Seek(-2, SeekOrigin.End);            // (fs, -2, SEEK_END);
        //                        fs.Read(cInfoSize, 0, 2);               // fread (cInfoSize, 1, 2, fs);

        //                        nInfoSize = ConvertToInt16(cInfoSize);
        //                        if (nInfoSize == (fileLength % 256))
        //                        {
        //                            ff = FileFormat.fileformat_FLEX_IDE;
        //                            SectorBias = 0;
        //                            m_lPartitionBias = 0;
        //                        }
        //                    }
        //                }
        //            }
        //            fs.Seek(currentPosition, SeekOrigin.Begin);
        //        }
        //    }

        //    m_nCurrentFileFormat = ff;

        //    return ff;
        //}

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
                SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                Console.Write(byteToWrite.ToString("X2", ci) + " ");
                SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);
            }
        }

        public void SetAttribute(int attr)
        {
            if (attr == (int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE)
            {
                //Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            else if (attr == (int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE)
            {
                //Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.White;
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
                case (int)CONNECTION_STATE.CONNECTED:                   statusLine = "\nState is CONNECTED"; break;
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

            Console.WriteLine("\n" + statusLine);
        }

        public byte MountImageFile(string fileName, int nDrive)
        {
            byte c = 0x06;
            string Message = "";

            try
            {
                Directory.SetCurrentDirectory(currentWorkingDirectory);

                string fileToLoad = fileName;

                try
                {
                    if (imageFile[nDrive] == null)
                        imageFile[nDrive] = new ImageFile();

                    imageFile[nDrive].stream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                    imageFile[nDrive].fileFormat = GetFileFormat(imageFile[nDrive].stream);
                    imageFile[nDrive].readOnly = false;
                }
                catch
                {
                    try
                    {
                        imageFile[nDrive].stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        imageFile[nDrive].fileFormat = GetFileFormat(imageFile[nDrive].stream);
                        imageFile[nDrive].readOnly = true;
                    }
                    catch
                    {
                        c = 0x15;
                    }
                }

                if (c == 0x06)
                {
                    if (fileToLoad.Substring(1, 1) == ":")
                    {
                        Message = string.Format("Loaded imagefile {0} from directory {1}", fileToLoad.PadRight(16), Directory.GetParent(fileToLoad));
                    }
                    else
                    {
                        Message = string.Format("Loaded imagefile {0} from directory {1}", fileToLoad.PadRight(16), currentWorkingDirectory);
                    }

                    imageFile[nDrive].driveInfo = new DriveInfo();

                    switch (imageFile[nDrive].fileFormat)
                    {
                        case FileFormat.fileformat_FLEX:
                        case FileFormat.fileformat_FLEX_IMA:
                            imageFile[nDrive].stream.Seek(512 + 39, SeekOrigin.Begin);
                            imageFile[nDrive].stream.Read(imageFile[nDrive].driveInfo.cNumberOfSectorsPerTrack, 0, 1);
                            imageFile[nDrive].driveInfo.NumberOfSectorsPerTrack = Convert.ToInt16(imageFile[nDrive].driveInfo.cNumberOfSectorsPerTrack[0]);
                            imageFile[nDrive].driveInfo.NumberOfBytesPerTrack = imageFile[nDrive].driveInfo.NumberOfSectorsPerTrack * 256;
                            imageFile[nDrive].driveInfo.NumberOfBytesPerTrack = (long)(imageFile[nDrive].driveInfo.NumberOfSectorsPerTrack * 256L);
                            imageFile[nDrive].driveInfo.LogicalSectorSize = 0; //set sector size = 256 bytes for FLEX.
                            break;

                        case FileFormat.fileformat_OS9:
                            {
                                OS9_ID_SECTOR stIDSector = new OS9_ID_SECTOR();

                                // Point to DD_TOT

                                imageFile[nDrive].stream.Seek(0, SeekOrigin.Begin);

                                stIDSector.cTOT[0] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cTOT[0], 1, 1, fs);   // Total Number of sector on media
                                stIDSector.cTOT[1] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cTOT[1], 1, 1, fs);
                                stIDSector.cTOT[2] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cTOT[2], 1, 1, fs);
                                stIDSector.cTKS[0] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cTKS[0], 1, 1, fs);   // Sectors Per Track (not track 0)

                                // point to DD_BIT

                                imageFile[nDrive].stream.Seek(6, SeekOrigin.Begin);                    // fseek(fs, 6, SEEK_SET);

                                stIDSector.cBIT[0] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cBIT[0], 1, 1, fs);     // cluster size high byte
                                stIDSector.cBIT[1] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cBIT[1], 1, 1, fs);     // cluster size low  byte

                                // POINT to DD_FMT

                                imageFile[nDrive].stream.Seek(16, SeekOrigin.Begin);                    // fseek(fs, 0x10, SEEK_SET);

                                stIDSector.cFMT[0] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cFMT[0], 1, 1, fs);     // Disk Format Byte
                                stIDSector.cSPT[0] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cSPT[0], 1, 1, fs);     // Sectors per track on track 0 high byte
                                stIDSector.cSPT[1] = (byte)imageFile[nDrive].stream.ReadByte();         // fread (&stIDSector.cSPT[1], 1, 1, fs);     // Sectors per track on track 0 low  byte

                                // POINT to DD_LSNSize

                                imageFile[nDrive].stream.Seek(0x68, SeekOrigin.Begin);                    // fseek(fs, 0x68, SEEK_SET);

                                stIDSector.cLSS[0] = (byte)imageFile[nDrive].stream.ReadByte();
                                stIDSector.cLSS[1] = (byte)imageFile[nDrive].stream.ReadByte();

                                // There's no point in going any further if the file size if not right

                                int nSectorsPerTrack        = stIDSector.cSPT[1] + (stIDSector.cSPT[0] * 256);
                                int nSectorsPerTrackZero    = stIDSector.cSPT[1] + (stIDSector.cSPT[0] * 256);
                                int nTotalSectors           = stIDSector.cTOT[2] + (stIDSector.cTOT[1] * 256) + (stIDSector.cTOT[0] * 1024);
                                int nClusterSize            = stIDSector.cBIT[1] + (stIDSector.cBIT[0] * 256);
                                int nLogicalSectorSize      = stIDSector.cLSS[1] + (stIDSector.cLSS[0] * 256);

                                long nDiskSize = (long)(nTotalSectors * 256);
                                nDiskSize += (long)((nSectorsPerTrack - nSectorsPerTrackZero) * 256);

                                imageFile[nDrive].driveInfo.NumberOfSectorsPerTrack     = nSectorsPerTrack;
                                imageFile[nDrive].driveInfo.TotalNumberOfSectorOnMedia  = nTotalSectors;
                                imageFile[nDrive].driveInfo.NumberOfBytesPerTrack       = nSectorsPerTrack * (nLogicalSectorSize + 1) * 256;
                                imageFile[nDrive].driveInfo.NumberOfSectorsPerCluster   = nClusterSize;
                                imageFile[nDrive].driveInfo.LogicalSectorSize           = nLogicalSectorSize;       // 0 = 256 bytes per sector
                            }
                            break;

                        default:
                            break;
                    }

                    if (fileToLoad.Substring(1, 1) == ":")
                        imageFile[nDrive].driveInfo.MountedFilename = fileToLoad;
                    else
                        imageFile[nDrive].driveInfo.MountedFilename = currentWorkingDirectory + "/" + fileToLoad;

                    imageFile[nDrive].driveInfo.MountedFilename = imageFile[nDrive].driveInfo.MountedFilename.ToUpper();
                }
                else
                {
                    if (fileToLoad.Substring(1, 1) == ":")
                    {
                        Message = string.Format("Unable to load {0} from directory {1}", fileToLoad.PadRight(16), Directory.GetParent(fileToLoad));
                    }
                    else
                    {
                        Message = string.Format("Unable to load {0} from directory {1}", fileToLoad.PadRight(16), currentWorkingDirectory);
                    }
                }

                Message = Message.Replace("/", @"\");
                Console.WriteLine(Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Unable to open directory: {0} - please change your configuration file", currentWorkingDirectory));
            }

            return (c);
        }

        public long GetSectorOffset()
        {
            long lSectorOffset = 0;
            long lOffsetToStartOfTrack;
            long lOffsetFromTrackStartToSector;

            if (imageFile[currentDrive].trackAndSectorAreTrackAndSector)
            {
                // actual track and sector are in the track[currentDrive] and sector[currentDrive] variables

                // This is FLEX asking -
                //
                // we need to check if the diskette is an OS9 or FLEX diskette image
                //  
                //      Since this is FLEX requesting the sector - the data in the track and sector are actual track and sector - not LBN
                //
                //          -   if the diskette image format is FLEX - proceed as we normally would
                //          -   if the diskette image format is OS9  - we need to convert the track and sector to an LBN
                //              using the information in the driveInfo data of the imageFIle class

                switch (imageFile[currentDrive].fileFormat)
                {
                    case FileFormat.fileformat_FLEX:
                    case FileFormat.fileformat_FLEX_IMA:
                        {
                            lOffsetToStartOfTrack = ((long)track[currentDrive] * imageFile[currentDrive].driveInfo.NumberOfBytesPerTrack);

                            if (sector[currentDrive] == 0)
                                lOffsetFromTrackStartToSector = (long)sector[currentDrive] * 256L;
                            else
                                lOffsetFromTrackStartToSector = (long)(sector[currentDrive] - 1) * 256L;

                            lSectorOffset = lOffsetToStartOfTrack + lOffsetFromTrackStartToSector;

                            Console.Write("[" + lSectorOffset.ToString("X8") + "]");
                        }
                        break;

                    case FileFormat.fileformat_OS9:     
                        {
                            // we need to convert the track and sector as LBN to Track and Sector to be able to calculate the offset used to access the diskette image sector

                            int lbn = (track[currentDrive] * 256) + sector[currentDrive];

                            try
                            {
                                int _track = lbn / (int)imageFile[currentDrive].driveInfo.NumberOfSectorsPerTrack;
                                int _sector = lbn % (int)imageFile[currentDrive].driveInfo.NumberOfSectorsPerTrack;
                                if (lbn <= imageFile[currentDrive].driveInfo.NumberOfSectorsPerTrack)
                                    _sector = lbn;

                                // now we can access the diskette iamge with T/S calcualted from LBN

                                lOffsetToStartOfTrack = ((long)_track * imageFile[currentDrive].driveInfo.NumberOfBytesPerTrack);

                                if (sector[currentDrive] == 0)
                                    lOffsetFromTrackStartToSector = (long)_sector * 256L;
                                else
                                    lOffsetFromTrackStartToSector = (long)(_sector - 1) * 256L;

                                lSectorOffset = lOffsetToStartOfTrack + lOffsetFromTrackStartToSector;
                            }
                            catch (Exception e)
                            {
                                string message = e.Message;
                            }
                            Console.Write("[" + lSectorOffset.ToString("X8") + "]");
                        }
                        break;
                }
            }
            else
            {
                // track[currentDrive] and sector[currentDrive] variables contain the LBN to retrieve

                // This is OS9 asking
                //
                // we need to check if the diskette is an OS9 or FLEX diskette image
                //  
                //      Since this is OS9 requesting the sector 
                //          -   if the diskette image format is FLEX - convert the LBN to T/S 
                //              using data in the driveInfo data of the imageFIle class
                //          -   if the diskette image format is OS9  - just use the LBN to calculate the offset
                //              
                switch (imageFile[currentDrive].fileFormat)
                {
                    case FileFormat.fileformat_FLEX:
                    case FileFormat.fileformat_FLEX_IMA:
                        break;

                    case FileFormat.fileformat_OS9:
                        break;
                }

                lSectorOffset = ((imageFile[currentDrive].driveInfo.LogicalSectorSize + 1) * 256) * (track[currentDrive] * 256 + sector[currentDrive]);
                Console.Write("[" + lSectorOffset.ToString("X8") + "]");
            }

            return (lSectorOffset);
        }

        public byte WriteSector()
        {
            byte status = 0x15;

            if (calculatedCRC == checksum)
            {
                long lSectorOffset = GetSectorOffset();

                imageFile[currentDrive].stream.Seek(lSectorOffset, SeekOrigin.Begin);
                try
                {
                    int bytesPerSector = (imageFile[currentDrive].driveInfo.LogicalSectorSize + 1) * 256;
                    imageFile[currentDrive].stream.Write(sectorBuffer, 0, bytesPerSector);
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


        public void SendCharToFLEX(byte c, bool displayOnScreen)
        {
            WriteByte(c, displayOnScreen);
        }

        public void SendCharToFLEX(string str, bool displayOnScreen)
        {
            if (displayOnScreen)
            {
                SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                Console.Write(str);
                SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);
            }

            sp.Write(str);
        }

        public void SendSector()
        {
            int checksum = 0;

            long lSectorOffset = GetSectorOffset();

            if (imageFile[currentDrive].stream != null)
            {
                int bytesPerSector = (imageFile[currentDrive].driveInfo.LogicalSectorSize + 1) * 256;

                imageFile[currentDrive].stream.Seek(lSectorOffset, SeekOrigin.Begin);
                imageFile[currentDrive].stream.Read(sectorBuffer, 0, bytesPerSector);

                for (int nIndex = 0; nIndex < bytesPerSector; nIndex++)
                {
                    if (Program.verboseOutput)
                    {
                        // cause new line after every 32 characters
                        if (nIndex % 32 == 0)
                        {
                            Console.WriteLine();
                        }
                    }

                    //WriteByte(sectorBuffer[nIndex], g_displaySectorData);
                    WriteByte(sectorBuffer[nIndex], Program.verboseOutput);
                    checksum += (char)(sectorBuffer[nIndex] & 0xFF);
                }

                WriteByte((byte)((checksum / 256) & 0xFF));
                WriteByte((byte)((checksum % 256) & 0xFF));
            }
            else
            {
                int bytesPerSector = (imageFile[currentDrive].driveInfo.LogicalSectorSize + 1) * 256;
                for (int nIndex = 0; nIndex < bytesPerSector; nIndex++)
                {
                    WriteByte(0x00);
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
                                                    {
                                                        int bytesPerSector = (imageFile[currentDrive].driveInfo.LogicalSectorSize + 1) * 256;
                                                        for (int i = 0; i < bytesPerSector; i++)
                                                            sectorBuffer[i] = 0x00;
                                                    }
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

    class Program
    {
        //int             nNumberOfPorts;
        //int             FocusWindow = -1;
        //int             Done        = 0;
        //int             Helping     = 1;

        public static bool verboseOutput;

        static string shutDown;
        static bool done = false;

        //long            NextStatusUpdate = 0;

        static List<Ports> listPorts = new List<Ports>();
        static ArrayList ports = new ArrayList();

        static CultureInfo ci = new CultureInfo("en-us");

        static void ParseConfigFile()
        {
            string ApplicationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            XmlDocument doc = new XmlDocument();
            doc.Load(Path.Combine(ApplicationPath, "fnconfig.xml"));

            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                if (node.Name == "Ports")
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
                                        p.imageFile[index] = new ImageFile();
                                        p.imageFile[index].Name = imageFile.InnerText;
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
                                foreach (ImageFile imageFile in p.imageFile)
                                {
                                    if (listPorts[i].imageFile[imageFileIndex].Name != null && imageFile.Name != null)
                                        listPorts[i].imageFile[imageFileIndex].Name = imageFile.Name;

                                    imageFileIndex++;
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
            }
        }


        // Waiting for Sync Byte

        static void StateConnectionStateNotConnected(Ports serialPort, int c)
        {
            if (c == 0x55)
            {
                serialPort.SetState((int)CONNECTION_STATE.SYNCRONIZING);
                serialPort.WriteByte((byte)0x55);
            }
        }

        // send ack to sync

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

        // this is the main loop while we are connected waiting for commands

        static void StateConnectionStateConnected(Ports serialPort, int c)
        {
            if (c == 'U')
            {
                serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
                Console.Write(string.Format("{0} ", c.ToString("X2")));
                serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);

                serialPort.SetState((int)CONNECTION_STATE.SYNCRONIZING);
                serialPort.WriteByte((byte)0x55);
            }
            else if (c == '?')
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
            else if (c == 'S')      // used by FLEX to read a sector from the PC
            {
                // 'S'end Sector Request

                if (serialPort.imageFile[serialPort.currentDrive] == null)
                    serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

                serialPort.imageFile[serialPort.currentDrive].trackAndSectorAreTrackAndSector = true;
                serialPort.imageFile[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.S_MODE;
                serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
            }
            else if (c == 'R')      // used by FLEX to write a sector to the PC
            {
                // 'R'eceive Sector Request

                if (serialPort.imageFile[serialPort.currentDrive] == null)
                    serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

                serialPort.imageFile[serialPort.currentDrive].trackAndSectorAreTrackAndSector = true;
                serialPort.imageFile[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.R_MODE;
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

            // now the Extended multi drive versions - these are what makes FLEXNet different from NETPC.
            //
            //      NETPC only allowed one drive to be mounted at a time. FLEXnet allows all four FLEX
            //      drives to be remote by providing mount points withinh FLEXNet for 4 image files per
            //      serial port.

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
            else if (c == ('s' | 0x80))     // used by OS9 to read a sector from the PC - track and sector are LBN
            {
                // 'S'end Sector Request

                if (serialPort.imageFile[serialPort.currentDrive] == null)
                    serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

                serialPort.imageFile[serialPort.currentDrive].trackAndSectorAreTrackAndSector = false;

                serialPort.SetState((int)CONNECTION_STATE.GET_READ_DRIVE);
            }
            else if (c == ('r' | 0x80))      // used by OS9 to write a sector to the PC- track and sector are LBN
            {
                // 'R'eceive Sector Request

                if (serialPort.imageFile[serialPort.currentDrive] == null)
                    serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

                serialPort.imageFile[serialPort.currentDrive].trackAndSectorAreTrackAndSector = false;

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

        // 'd' command recieved - Report which disk image is mounted to requested drive 

        static void StateConnectionStateGetRequestedMountDrive(Ports serialPort, int c)
        {
            // Report which disk image is mounted to requested drive

            serialPort.currentDrive = c;

            serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.REVERSE_ATTRIBUTE);
            Console.Write(serialPort.currentWorkingDirectory);
            Console.Write("\r");
            Console.Write("\n");
            serialPort.SetAttribute((int)CONSOLE_ATTRIBUTE.NORMAL_ATTRIBUTE);

            if (serialPort.imageFile[serialPort.currentDrive] == null)
                serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

            serialPort.sp.Write(serialPort.imageFile[serialPort.currentDrive].driveInfo.MountedFilename);

            serialPort.WriteByte(0x0D, false);
            serialPort.WriteByte(0x06);

            serialPort.SetState((int)CONNECTION_STATE.CONNECTED);
        }

        // 's'end Sector Request with drive - this state gets the drive number

        static void StateConnectionStateGetReadDrive(Ports serialPort, int c)
        {
            serialPort.currentDrive = c;

            if (serialPort.imageFile[serialPort.currentDrive] == null)
                serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

            serialPort.imageFile[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.S_MODE;
            serialPort.SetState((int)CONNECTION_STATE.GET_TRACK);
        }

        static void StateConnectionStateGetWriteDrive(Ports serialPort, int c)
        {
            serialPort.currentDrive = c;

            if (serialPort.imageFile[serialPort.currentDrive] == null)
                serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

            serialPort.imageFile[serialPort.currentDrive].driveInfo.mode = (int)SECTOR_ACCESS_MODE.R_MODE;
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

            if (serialPort.imageFile[serialPort.currentDrive] == null)
                serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

            if (serialPort.imageFile[serialPort.currentDrive].driveInfo.mode == (int)SECTOR_ACCESS_MODE.S_MODE)
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
            if (serialPort.imageFile[serialPort.currentDrive] == null)
                serialPort.imageFile[serialPort.currentDrive] = new ImageFile();

            if (c != 0x0d)
            {
                // just add the character to the filename

                serialPort.commandFilename += (char)c;
            }
            else
            {
                serialPort.commandFilename += ".DSK";

                // this should close any file that is currently open for this port/drive

                if (serialPort.imageFile[serialPort.currentDrive] != null)
                {
                    if (serialPort.imageFile[serialPort.currentDrive].stream != null)
                    {
                        serialPort.imageFile[serialPort.currentDrive].stream.Close();
                        serialPort.imageFile[serialPort.currentDrive].stream = null;
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
                if (serialPort.imageFile[serialPort.currentDrive] != null)
                {
                    if (serialPort.imageFile[serialPort.currentDrive].readOnly)
                    {
                        cMode = (byte)'R';
                    }
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

        static void InitializeFromConfigFile()
        {
            Console.WriteLine("FLEX-OS9-Net version 5.0:1");

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

                foreach (ImageFile imageFile in serialPort.imageFile)
                {
                    if (imageFile != null)
                    {
                        serialPort.MountImageFile(imageFile.Name + ".DSK", nIndex);
                    }
                    nIndex++;
                }

                Console.WriteLine(string.Format("COM{0} parameters:", serialPort.port));
                Console.WriteLine(string.Format("    Rate:              {0}", serialPort.rate));
                Console.WriteLine(string.Format("    CpuSpeed:          {0}", serialPort.speed));
                Console.WriteLine(string.Format("    Verbose:           {0}", serialPort.verbose));
                Console.WriteLine(string.Format("    AutoMount:         {0}", serialPort.autoMount));
                Console.WriteLine(string.Format("    DefaultDirectory   {0}", serialPort.defaultStartDirectory));
                Console.WriteLine(string.Format("    ImageFiles"));

                for (int imageFileIndex = 0; imageFileIndex < serialPort.imageFile.Length; imageFileIndex++)
                {
                    if (serialPort.imageFile[imageFileIndex] == null)
                        serialPort.imageFile[imageFileIndex] = new ImageFile();

                    Console.WriteLine(string.Format("        {0} - {1}", imageFileIndex, serialPort.imageFile[imageFileIndex].Name));
                }
                Console.WriteLine(string.Format("    Current Working Directory: {0}", serialPort.currentWorkingDirectory));

                serialPort.sp.DtrEnable = true;
                serialPort.sp.RtsEnable = true;
            }
        }

        static void ProcessRequests()
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
                                case (int)CONNECTION_STATE.NOT_CONNECTED:               StateConnectionStateNotConnected            (serialPort, c); break;
                                case (int)CONNECTION_STATE.SYNCRONIZING:                StateConnectionStateSynchronizing           (serialPort, c); break;
                                case (int)CONNECTION_STATE.CONNECTED:                   StateConnectionStateConnected               (serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_REQUESTED_MOUNT_DRIVE:   StateConnectionStateGetRequestedMountDrive  (serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_READ_DRIVE:              StateConnectionStateGetReadDrive            (serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_WRITE_DRIVE:             StateConnectionStateGetWriteDrive           (serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_MOUNT_DRIVE:             StateConnectionStateGetMountDrive           (serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_CREATE_DRIVE:            StateConnectionStateGetCreateDrive          (serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_TRACK:                   StateConnectionStateGetTrack                (serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_SECTOR:                  StateConnectionStateGetSector               (serialPort, c); break;
                                case (int)CONNECTION_STATE.RECEIVING_SECTOR:            StateConnectionStateRecievingSector         (serialPort, c); break;
                                case (int)CONNECTION_STATE.GET_CRC:                     StateConnectionStateGetCRC                  (serialPort, c); break;
                                case (int)CONNECTION_STATE.MOUNT_GETFILENAME:           StateConnectionStateMountGetFilename        (serialPort, c); break;
                                case (int)CONNECTION_STATE.DELETE_GETFILENAME:          StateConnectionStateDeleteGetFilename       (serialPort, c); break;
                                case (int)CONNECTION_STATE.DIR_GETFILENAME:             StateConnectionStateDirGetFilename          (serialPort, c); break;
                                case (int)CONNECTION_STATE.CD_GETFILENAME:              StateConnectionStateCDGetFilename           (serialPort, c); break;
                                case (int)CONNECTION_STATE.DRIVE_GETFILENAME:           StateConnectionStateDriveGetFilename        (serialPort, c); break;
                                case (int)CONNECTION_STATE.SENDING_DIR:                 StateConnectionStateSendingDir              (serialPort, c); break;
                                case (int)CONNECTION_STATE.CREATE_GETPARAMETERS:        StateConnectionStateCreateGetParameters     (serialPort, c); break;
                                case (int)CONNECTION_STATE.WAIT_ACK:                    StateConnectionStateWaitACK                 (serialPort, c); break;
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
                done = false;
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
                        verboseOutput = !verboseOutput;
                        Console.WriteLine(string.Format("verbose is {0}", verboseOutput ? "on" : "off"));
                        return;

                    case ConsoleKey.F9:
                        //clsConnection[nPort].nDisplayOutputBytes = !clsConnection[nPort].nDisplayOutputBytes;
                        //*StatusLine << "Output Display is : " << (clsConnection[nPort].nDisplayOutputBytes ? "ON" : "OFF") << " Status: ";
                        return;

                    case ConsoleKey.F10:
                        foreach (Ports serialPort in listPorts)
                        {
                            serialPort.SetState((int)CONNECTION_STATE.NOT_CONNECTED);
                            Console.WriteLine("Serial Port " + serialPort.port.ToString() + " is reset to NOT CONNECTED");
                        }
                        return;

                }
            }
            //    *StatusLine << clsConnection[FocusWindow].rsPort->ErrorName( status );
        }

        static void Main(string[] args)
        {
            InitializeFromConfigFile();
            ProcessRequests();
        }
    }
}
