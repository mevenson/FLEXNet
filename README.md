FLEXNetSharp

  For Windows 10 platform (this is a work in progress). It has been tested with Don Coates' DC68HC11 system. I have COM ports (USB to serial adapter cables from Belkin) at ports 3 and 4. I have the terminal port of the 68HC11 system hooked to COM3 at 57600 baud and the other port (for FLEXNET usage) on port 4 (also at 57600 baud). The xml configuration file included in the zip is set up for these parameters. This file needs to be in the execution directory of the FlexNetSharp.exe file (or the working directory of a shortcut that starts it). I would also recommend putting a DISKS directory in the same folder and putting your .dsk files in the DISKS folder. Or just unzip this archive into your existing C:/Users/ /AppData/Roaming/EvensonConsultingServices/SWTPCmemulator directory and run it from there. This only works with track 0 padded sectors for doble density images. It uses a very simplistic offset calculator. If you want a more robust version - try the FLEX-OS9-Net project below.

FLEX-OS9-Net

  A version of the above program that will work with either FLEX or Microware OS-9. Same rules apply. Both of these downloads are complete Visual Studio (I use VS 2019) source projects with compiled programs in the bin directories for both Release and debug builds. Microsoft offers Visual Studio community edition for free in case you do not have a licensed copy. I know you can get 2022, but 2019 may still be available.
  
  The OS9 sector read and write access is through the same lower case commands (r and s) as the FLEX version with the high order bit of the command on. The OS9 implementation does not expect track and sector, but rather it expects logical block numbers where you would normally supply track and sector for FLEX. This was done in order to make it easy to implement the OS9 machine's implementation of the FNETDRV driver for OS9 as well as the utilities. This gets rid of the requirement to determine the geometry of the OS9 diskette image. No conversion from track and sector to LSN is required. The program just multiplies the LSN times the LSN block size (usually 256 for floppies) to get the offset into the diskette image file of the LSN.
  
  Since the values passed for the 's' and 'r' commands are drive, track and sector, the LSN can only be a 16 bit value. Where OS9 can handle 24 bit LSN values, FLEX-OS9-Net will only handle 16 bit LSN's.
  
FLEXWire

  All the features of FLEX-OS9-Net with TCPIP added to support the Raspberry Pi pico 6800 emulator
