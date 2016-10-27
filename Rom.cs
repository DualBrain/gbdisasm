using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gbdisasm
{

    enum RomType
    {
        ROM = 0x00,
        ROM_MBC1 = 0x01,
        ROM_MBC1_RAM = 0x02,
        ROM_MBC1_RAM_BATT = 0x03,
        ROM_MBC2 = 0x05,
        ROM_MBC2_BATTERY = 0x06,
        ROM_RAM = 0x08,
        ROM_RAM_BATTERY = 0x09,
        ROM_MMM01 = 0x0B,
        ROM_MMM01_SRAM = 0x0C,
        ROM_MMM01_SRAM_BATT = 0x0D,
        ROM_MBC3_TIMER_BATT = 0x0F,
        ROM_MBC3_TIMER_RAM_BATT = 0x10,
        ROM_MBC3 = 0x11,
        ROM_MBC3_RAM = 0x12,
        ROM_MBC3_RAM_BATT = 0x13,
        ROM_MBC5 = 0x19,
        ROM_MBC5_RAM = 0x1A,
        ROM_MBC5_RAM_BATT = 0x1B,
        ROM_MBC5_RUMBLE = 0x1C,
        ROM_MBC5_RUMBLE_SRAM = 0x1D,
        ROM_MBC5_RUMBLE_SRAM_BATT = 0x1E,
        PocketCamera = 0x1F,
        BandaiTAMA5 = 0xFD,
        HudsonHuC3 = 0xFE,
        HudsonHuC1 = 0xFF,
    }


    class RomLoader
    {
        public Rom Load(string fileName) {

            FileInfo fileInfo = new FileInfo(fileName);
            byte[] fileData = new byte[fileInfo.Length];
            FileStream fileStream = fileInfo.OpenRead();
            fileStream.Read(fileData, 0, fileData.Length);
            fileStream.Close();

            return new Rom(fileData); 

        }
    }

    public class Rom {

        public string title;
        public int romSize;
        public int romBanks;
        public int ramSize;
        public int ramBanks;
        public string romType;
        public byte[] romData;

        public Rom(byte[] fileData)
        {
            title = getInternalTitle(fileData);
            romData = fileData;

            switch ((RomType)fileData[0x147])
            {
                case RomType.ROM:
                case RomType.ROM_RAM:
                case RomType.ROM_RAM_BATTERY:
                    romType = "ROM32K";
                    break;
                case RomType.ROM_MBC1:
                case RomType.ROM_MBC1_RAM:
                case RomType.ROM_MBC1_RAM_BATT:
                case RomType.HudsonHuC1:
                    romType = "MBC1";
                    break;
                case RomType.ROM_MBC2:
                case RomType.ROM_MBC2_BATTERY:
                    romType = "MBC2";
                    break;
                case RomType.ROM_MBC3:
                case RomType.ROM_MBC3_RAM:
                case RomType.ROM_MBC3_RAM_BATT:
                case RomType.ROM_MBC3_TIMER_BATT:
                case RomType.ROM_MBC3_TIMER_RAM_BATT:
                    romType = "MBC3";
                    break;
                case RomType.ROM_MBC5:
                case RomType.ROM_MBC5_RAM:
                case RomType.ROM_MBC5_RAM_BATT:
                case RomType.ROM_MBC5_RUMBLE:
                case RomType.ROM_MBC5_RUMBLE_SRAM:
                case RomType.ROM_MBC5_RUMBLE_SRAM_BATT:
                    romType = "MBC5";
                    break;
                default:
                    romType = "Unsupported";
                    break;
            }

            switch (fileData[0x148])
            { 
                case 0x00:
                    romSize = 32 * 1024;
                    romBanks = 2;
                    break;
                case 0x01:
                    romSize = 64 * 1024;
                    romBanks = 4;
                    break;
                case 0x02:
                    romSize = 128 * 1024;
                    romBanks = 8;
                    break;
                case 0x03:
                    romSize = 256 * 1024;
                    romBanks = 16;
                    break;
                case 0x04:
                    romSize = 512 * 1024;
                    romBanks = 32;
                    break;
                case 0x05:
                    romSize = 1024 * 1024;
                    romBanks = 64;
                    break;
                case 0x06:
                    romSize = 2048 * 1024;
                    romBanks = 128;
                    break;
                case 0x07:
                    romSize = 4096 * 1024;
                    romBanks = 256;
                    break;
                case 0x52:
                    romSize = 1152 * 1024;
                    romBanks = 72;
                    break;
                case 0x53:
                    romSize = 1280 * 1024;
                    romBanks = 80;
                    break;
                case 0x54:
                    romSize = 1536 * 1024;
                    romBanks = 96;
                    break;
            
            }

            switch (fileData[0x149])
            {
                case 0x00:
                    ramSize = 0;
                    ramBanks = 0;
                    break;
                case 0x01:
                    ramSize = 2 * 1024;
                    ramBanks = 1;
                    break;
                case 0x02:
                    ramSize = 8 * 1024;
                    ramBanks = 1;
                    break;
                case 0x03:
                    ramSize = 32 * 1024;
                    ramBanks = 4;
                    break;
                case 0x04:
                    ramSize = 128 * 1024;
                    ramBanks = 16;
                    break;
                case 0x05:
                    ramSize = 64 * 1024;
                    ramBanks = 8;
                    break;
            }

        
        }

        private String getInternalTitle(byte[] fileData) {

            for (int i = 0x134; i < 0x143; i++)
            {
                title += (char)fileData[i];
            }

            return title;
        }

        public String getTitle() {
            return this.title;
        }

        public int ReadByte(int address)
        {
            return romData[address];
        }

    }


}
