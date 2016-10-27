using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace gbdisasm
{

    public sealed class CPU
    {

        private Rom rom;
        private int A, F, B, C, D, E, H, L, PC, SP;
        private int bank = 0x01;
        private int bankSwitchFunction;
        private int currentLocation;

        private byte[] memory = new byte[64 * 1024];

        private List<string> functionStack = new List<string>();
        private List<string> labelBuffer = new List<string>();
        //private Dictionary<int, int> functionBankBuffer = new Dictionary<int, int>();
        private Dictionary<string, string> outputDict = new Dictionary<string, string>();

        public void initCartRom() {
            
            for (int i = 0x0; i < 0x7FFF; i++)
            {
                memory[i] = (byte)rom.ReadByte(i);
            }

        }

        public void setBank(int _bank, bool switchBank = false) {
            this.bank = _bank;

            if (switchBank && bank > 0x00)
                switchRomBank();
        }

        public void clearOutputBuffer(){
            outputDict.Clear();
        }

        private byte readMemByte(int Address) {
            return memory[0xFFFF & Address];
        }

        private void switchRomBank(int? memAddress = null) {

            if (memAddress != null && memAddress < 0x4000)
                return;

            string romType = rom.romType;
            int romBanks = rom.romBanks;

            if (bank > romBanks)
                bank = romBanks;

            switch (romType) { 
                case "ROM32K":
                    return;
                case "MBC1":
                    if (bank == 0x00 || bank == 0x20 || bank == 0x40 || bank == 0x60)
                        bank += 1;
                    break;
                case "MBC2":
                    break;
                case "MBC3":
                    if (bank == 0x00)
                        bank = 0x01;
                    break;
                case "MBC5":
                    break;
                case "Unsupported":
                    if (bank == 0x00)
                        bank = 0x01;
                    break;
            }

            int romAddress = bank * 0x4000;

            for (int i = 0x4000; i < 0x7FFF; i++, romAddress++)
            {
                memory[i] = (byte)rom.ReadByte(romAddress);
            }

        }

        private int getJrLabel(int bb, int address) {

            int nextAddr = address + getInstructionLength(address);
            //return getRomAddrFromMemAddr( nextAddr + (sbyte)bb );
            return nextAddr + (sbyte)bb;

        }

        private string getLinkerAddress(int address, int _bank) {

            if (address < 0x4000 || address > 0x7FFF)
                _bank = 0x00;

            return _bank.ToString("X").PadLeft(2, '0') + ":" + address.ToString("X").PadLeft(4, '0');
        }

        private int[] splitLinkerAddress(string linkerAddress) {

            int[] ret = new int[2];

            ret[0] = Convert.ToInt32(linkerAddress.Substring(0, 2), 16);
            ret[1] = Convert.ToInt32(linkerAddress.Substring(3, 4), 16);

            return ret;

        }

        public Dictionary<string, string> disassembleOutput(int startAddress)
        {
                outputDict = disasemble(startAddress);

                for (int i = 0; i < functionStack.Count; i++)
                {
                    if (labelBuffer.Contains(functionStack[i]))
                        continue;

                    int[] splitAddr = splitLinkerAddress(functionStack[i]);

                    this.bank = splitAddr[0];
                    switchRomBank(splitAddr[1]);

                    outputDict = disasemble(splitAddr[1]);
                }

                return outputDict;
        }

        public Dictionary<string, string> disasemble(int startAddress) {

            functionStack.Add(getLinkerAddress(startAddress, bank));
            labelBuffer.Add(getLinkerAddress(startAddress, bank));

            currentLocation = startAddress;
            int lastAddressBuffer;

            while (true) {

                string output = "";
                string linkerAddr = getLinkerAddress(currentLocation, bank);

                if (currentLocation == startAddress)
                    output += "\n\rLabel" + getRomAddrFromMemAddr(currentLocation).ToString("X") + ": ;" + linkerAddr + "\n";

                if (functionStack.Contains(linkerAddr) && !labelBuffer.Contains(linkerAddr))
                {
                    output += "Label" + getRomAddrFromMemAddr(currentLocation).ToString("X") + "\n";
                    labelBuffer.Add(linkerAddr);
                }

                string i = disasembleInstruction(currentLocation);
                output += "\t" + i + "\r\n";

                addOrUpdateOutput(linkerAddr, output);

                lastAddressBuffer = currentLocation;
                currentLocation += getInstructionLength(currentLocation);

                if (i == "ret" || i == "reti" || i == "ret" || i.StartsWith(";unknown opcode"))
                {
                    addOrUpdateOutput(linkerAddr, ";" + currentLocation.ToString("X"), true);
                    break;
                }
                    
            
            }

            

            return outputDict;
            
        }

        private void addOrUpdateOutput(string linkerAddress, string output, bool isSuffix = false, bool isPrefix = false)
        {

            if (outputDict.ContainsKey(linkerAddress))
            {
                if (isSuffix) {
                    outputDict[linkerAddress] = outputDict[linkerAddress] + output;
                }
                else if (isPrefix) {
                    outputDict[linkerAddress] = output + outputDict[linkerAddress];
                }
                else {
                    outputDict[linkerAddress] = output;
                }

            }
            else {
                outputDict.Add(linkerAddress, output);
            }
        
        }

        private void checkFromLabel(int memAddr) {

            string linkerAddr = getLinkerAddress(memAddr, bank);

            if (outputDict.ContainsKey(linkerAddr) && !labelBuffer.Contains(linkerAddr))
            {

                string label = "Label" + getRomAddrFromMemAddr(memAddr).ToString("X");

                if (outputDict[linkerAddr].IndexOf(label) != -1)
                {
                    labelBuffer.Add(linkerAddr);
                    return;
                }

                outputDict[linkerAddr] = label + "\n" + outputDict[linkerAddr];
                labelBuffer.Add(linkerAddr);
                
            }
        
        }

        private int getMemAddrFromRomAddr(int romAddress)
        {

            if (romAddress < 0x4000) {
                bank = 0x00;
                return romAddress;
            }

            if (romAddress < 0x7FFF)
            {
                bank = 0x01;
                return romAddress;
            }

            bank = (int)(romAddress / 0x4000);
            int baseAddress = bank * 0x4000;
            int addrOffset = romAddress - baseAddress;

            return addrOffset + 0x4000;
        }

        private int getRomAddrFromMemAddr(int memAddress) {

            if (memAddress < 0x4000 || memAddress  > 0x7FFF)
                return memAddress;

            int addrOffset = memAddress >= 0x4000 ? memAddress - 0x4000 : memAddress;
            int baseAddress = bank * 0x4000;

            return baseAddress + addrOffset;

        }


        private string getMemAddressWithBank(int memAddress)
        {
            int bankBuffer = bank;

            if (memAddress > 0x7FFF)
                bankBuffer = 0x00;

            return bankBuffer.ToString("X") + ":" + memAddress.ToString("X").PadLeft(4, '0');

        }

        private string getByteString(int address) {

            int _byte = readMemByte(address);

            return _byte.ToString("X").PadLeft(2, '0');
        }

        private string getWordString(int address)
        {

            int lowByte = readMemByte(address);
            int highByte = readMemByte(address + 1);

            int word = (highByte << 8) | lowByte;

            return word.ToString("X");
        }

        private void checkBankSwitchFunction(int address) {

            if (address == bankSwitchFunction)
            {
                bank = A;
                switchRomBank();
            }
        
        }

        private string disasembleInstruction(int currentLocation)
        {

            int opcode = readMemByte(currentLocation);

            string bb = "";
            string word = "";
            int ibb = 0;
            int iword = 0;
            int jrAddr = 0;

            switch (opcode)
            {
                case 0x00:
                    return "nop";

                case 0x01:
                    return "ld bc, $" + getWordString(currentLocation+1);

                case 0x02:
                    return "ld [bc], a";

                case 0x03:
                    return "inc bc";

                case 0x04:
                    return "inc b";

                case 0x05:
                    return "dec b";

                case 0x06:
                    return "ld b, $" + getByteString(currentLocation + 1);

                case 0x07:
                    return "rlca";

                case 0x08:
                    return "ld [$" + getWordString(currentLocation + 1) + "], sp";

                case 0x09:
                    return "add hl, bc";

                case 0x0A:
                    return "ld a, [bc]";

                case 0x0B:
                    return "dec bc";

                case 0x0C:
                    return "inc c";

                case 0x0D:
                    return "dec c";

                case 0x0E:
                    return "ld c, $" + getByteString(currentLocation + 1);

                case 0x0F:
                    return "rrca";

                case 0x10:
                    if (getByteString(currentLocation + 1) != "00")
                        break;
                    return "stop";

                case 0x11:
                    return "ld de, $" + getWordString(currentLocation + 1);

                case 0x12:
                    return "ld [de], a";

                case 0x13:
                    return "inc de";

                case 0x14:
                    return "inc d";

                case 0x15:
                    return "dec d";

                case 0x16:
                    return "ld d, $" + getByteString(currentLocation + 1);

                case 0x17:
                    return "rla";

                case 0x18:
                    ibb = Convert.ToInt32(getByteString(currentLocation + 1), 16);
                    jrAddr = getJrLabel(ibb, currentLocation);
                    functionStack.Add(getLinkerAddress(jrAddr, bank));
                    checkFromLabel(jrAddr);
                    return "jr Label" + getRomAddrFromMemAddr(jrAddr).ToString("X");

                case 0x19:
                    return "add hl, de";

                case 0x1A:
                    return "ld a, [de]";

                case 0x1B:
                    return "dec de";

                case 0x1C:
                    return "inc e";

                case 0x1D:
                    return "dec e";

                case 0x1E:
                    return "ld e, $" + getByteString(currentLocation + 1);

                case 0x1F:
                    return "rra";

                case 0x20:
                    ibb = Convert.ToInt32(getByteString(currentLocation + 1), 16);
                    jrAddr = getJrLabel(ibb, currentLocation);
                    functionStack.Add(getLinkerAddress(jrAddr, bank));
                    checkFromLabel(jrAddr);
                    return "jr nz, Label" + getRomAddrFromMemAddr(jrAddr).ToString("X");

                case 0x21:
                    return "ld hl, $" + getWordString(currentLocation + 1);

                case 0x22:
                    return "ld [hli], a";

                case 0x23:
                    return "inc hl";

                case 0x24:
                    return "inc h";

                case 0x25:
                    return "dec h";

                case 0x26:
                    return "ld h, $" + getByteString(currentLocation + 1);

                case 0x27:
                    return "daa";

                case 0x28:
                    ibb = Convert.ToInt32(getByteString(currentLocation + 1), 16);
                    jrAddr = getJrLabel(ibb, currentLocation);
                    functionStack.Add(getLinkerAddress(jrAddr, bank));
                    checkFromLabel(jrAddr);
                    return "jr z, Label" + getRomAddrFromMemAddr(jrAddr).ToString("X");

                case 0x29:
                    return "add hl, hl";

                case 0x2A:
                    return "ld a, [hli]";

                case 0x2B:
                    return "dec hl";

                case 0x2C:
                    return "inc l";

                case 0x2D:
                    return "dec l";

                case 0x2E:
                    return "ld l, $" + getByteString(currentLocation + 1);

                case 0x2F:
                    return "cpl";

                case 0x30:
                    ibb = Convert.ToInt32(getByteString(currentLocation + 1), 16);
                    jrAddr = getJrLabel(ibb, currentLocation);
                    functionStack.Add(getLinkerAddress(jrAddr, bank));
                    checkFromLabel(jrAddr);
                    return "jr nc, Label" + getRomAddrFromMemAddr(jrAddr).ToString("X");

                case 0x31:
                    return "ld sp, $" + getWordString(currentLocation + 1);

                case 0x32:
                    return "ldd [hl], a";

                case 0x33:
                    return "inc sp";

                case 0x34:
                    return "inc [hl]";

                case 0x35:
                    return "dec [hl]";

                case 0x36:
                    return "ld [hl], $" + getByteString(currentLocation + 1);

                case 0x37:
                    return "scf";

                case 0x38:
                    ibb = Convert.ToInt32(getByteString(currentLocation + 1), 16);
                    jrAddr = getJrLabel(ibb, currentLocation);
                    functionStack.Add(getLinkerAddress(jrAddr, bank));
                    checkFromLabel(jrAddr);
                    return "jr c, Label" + getRomAddrFromMemAddr(jrAddr).ToString("X");

                case 0x39:
                    return "add hl, sp";

                case 0x3A:
                    return "ldd a, [hl]";

                case 0x3B:
                    return "dec sp";

                case 0x3C:
                    return "inc a";

                case 0x3D:
                    return "dec a";

                case 0x3E:
                    bb = getByteString(currentLocation + 1);
                    A = Convert.ToInt32(bb, 16);
                    return "ld a, $" + bb;

                case 0x3F:
                    return "ccf";

                case 0x40:
                    return "ld b, b";

                case 0x41:
                    return "ld b, c";

                case 0x42:
                    return "ld b, d";

                case 0x43:
                    return "ld b, e";

                case 0x44:
                    return "ld b, h";

                case 0x45:
                    return "ld b, l";

                case 0x46:
                    return "ld b, [hl]";

                case 0x47:
                    return "ld b, a";

                case 0x48:
                    return "ld c, b";

                case 0x49:
                    return "ld c, c";

                case 0x4A:
                    return "ld c, d";

                case 0x4B:
                    return "ld c, e";

                case 0x4C:
                    return "ld c, h";

                case 0x4D:
                    return "ld c, l";

                case 0x4E:
                    return "ld c, [hl]";

                case 0x4F:
                    return "ld c, a";

                case 0x50:
                    return "ld d, b";

                case 0x51:
                    return "ld d, c";

                case 0x52:
                    return "ld d, d";

                case 0x53:
                    return "ld d, e";

                case 0x54:
                    return "ld d, h";

                case 0x55:
                    return "ld d, l";

                case 0x56:
                    return "ld d, [hl]";

                case 0x57:
                    return "ld d, a";

                case 0x58:
                    return "ld e, b";

                case 0x59:
                    return "ld e, c";

                case 0x5A:
                    return "ld e, d";

                case 0x5B:
                    return "ld e, e";

                case 0x5C:
                    return "ld e, h";

                case 0x5D:
                    return "ld e, l";

                case 0x5E:
                    return "ld e, [hl]";

                case 0x5F:
                    return "ld e, a";

                case 0x60:
                    return "ld h, b";

                case 0x61:
                    return "ld h, c";

                case 0x62:
                    return "ld h, d";

                case 0x63:
                    return "ld h, e";

                case 0x64:
                    return "ld h, h";

                case 0x65:
                    return "ld h, l";

                case 0x66:
                    return "ld h, [hl]";

                case 0x67:
                    return "ld h, a";

                case 0x68:
                    return "ld l, b";

                case 0x69:
                    return "ld l, c";

                case 0x6A:
                    return "ld l, d";

                case 0x6B:
                    return "ld l, e";

                case 0x6C:
                    return "ld l, h";

                case 0x6D:
                    return "ld l, l";

                case 0x6E:
                    return "ld l, [hl]";

                case 0x6F:
                    return "ld l, a";

                case 0x70:
                    return "ld [hl], b";

                case 0x71:
                    return "ld [hl], c";

                case 0x72:
                    return "ld [hl], d";

                case 0x73:
                    return "ld [hl], e";

                case 0x74:
                    return "ld [hl], h";

                case 0x75:
                    return "ld [hl], l";

                case 0x76:
                    return "halt";

                case 0x77:
                    return "ld [hl], a";

                case 0x78:
                    return "ld a, b";

                case 0x79:
                    return "ld a, c";

                case 0x7A:
                    return "ld a, d";

                case 0x7B:
                    return "ld a, e";

                case 0x7C:
                    return "ld a, h";

                case 0x7D:
                    return "ld a, l";

                case 0x7E:
                    return "ld a, [hl]";

                case 0x7F:
                    return "ld a";

                case 0x87:
                    return "add a";

                case 0x80:
                    return "add a, b";

                case 0x81:
                    return "add a, c";

                case 0x82:
                    return "add a, d";

                case 0x83:
                    return "add a, e";

                case 0x84:
                    return "add a, h";

                case 0x85:
                    return "add a, l";

                case 0x86:
                    return "add a, [hl]";

                case 0x88:
                    return "adc a, b";

                case 0x89:
                    return "adc a, c";

                case 0x8A:
                    return "adc a, d";

                case 0x8B:
                    return "adc a, e";

                case 0x8C:
                    return "adc a, h";

                case 0x8D:
                    return "adc a, l";

                case 0x8E:
                    return "adc a, [hl]";

                case 0x8F:
                    return "adc a";

                case 0x90:
                    return "sub a, b";

                case 0x91:
                    return "sub a, c";

                case 0x92:
                    return "sub a, d";

                case 0x93:
                    return "sub a, e";

                case 0x94:
                    return "sub a, h";

                case 0x95:
                    return "sub a, l";

                case 0x96:
                    return "sub a, [hl]";

                case 0x97:
                    return "sub a";

                case 0x98:
                    return "sbc a, b";

                case 0x99:
                    return "sbc a, c";

                case 0x9A:
                    return "sbc a, d";

                case 0x9B:
                    return "sbc a, e";

                case 0x9C:
                    return "sbc a, h";

                case 0x9D:
                    return "sbc a, l";

                case 0x9E:
                    return "sbc a, [hl]";

                case 0x9F:
                    return "sbc a";

                case 0xA0:
                    return "and a, b";

                case 0xA1:
                    return "and a, c";

                case 0xA2:
                    return "and a, d";

                case 0xA3:
                    return "and a, e";

                case 0xA4:
                    return "and a, h";

                case 0xA5:
                    return "and a, l";

                case 0xA6:
                    return "and a, [hl]";

                case 0xA7:
                    return "and a";

                case 0xA8:
                    return "xor a, b";

                case 0xA9:
                    return "xor a, c";

                case 0xAA:
                    return "xor a, d";

                case 0xAB:
                    return "xor a, e";

                case 0xAC:
                    return "xor a, h";

                case 0xAD:
                    return "xor a, l";

                case 0xAE:
                    return "xor a, [hl]";

                case 0xAF:
                    return "xor a";

                case 0xB0:
                    return "or a, b";

                case 0xB1:
                    return "or a, c";

                case 0xB2:
                    return "or a, d";

                case 0xB3:
                    return "or a, e";

                case 0xB4:
                    return "or a, h";

                case 0xB5:
                    return "or a, l";

                case 0xB6:
                    return "or a, [hl]";

                case 0xB7:
                    return "or a";

                case 0xB8:
                    return "cp a, b";

                case 0xB9:
                    return "cp a, c";

                case 0xBA:
                    return "cp a, d";

                case 0xBB:
                    return "cp a, e";

                case 0xBC:
                    return "cp a, h";

                case 0xBD:
                    return "cp a, l";

                case 0xBE:
                    return "cp a, [hl]";

                case 0xBF:
                    return "cp a";

                case 0xC0:
                    return "ret nz";

                case 0xC1:
                    return "pop bc";

                case 0xC2:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    return "jp nz, Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xC3:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    checkBankSwitchFunction(iword);
                    return "jp Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xC4:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    return "call nz, Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xC5:
                    return "push bc";

                case 0xC6:
                    return "add a, $" + getByteString(currentLocation + 1);

                case 0xC7:
                    functionStack.Add(getLinkerAddress(0x00, 0x00));
                    return "rst Label0";

                case 0xC8:
                    return "ret z";

                case 0xC9:
                    return "ret";

                case 0xCA:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    return "jp z, Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xCB:
                    byte newOpcode = (byte)Convert.ToInt32(getByteString(currentLocation + 1), 16);
                    switch (newOpcode)
                    {
                        case 0x00:
                            return "rlc b";

                        case 0x01:
                            return "rlc c";

                        case 0x02:
                            return "rlc d";

                        case 0x03:
                            return "rlc e";

                        case 0x04:
                            return "rlc h";

                        case 0x05:
                            return "rlc l";

                        case 0x06:
                            return "rlc [hl]";

                        case 0x07:
                            return "rlc a";

                        case 0x08:
                            return "rrc b";

                        case 0x09:
                            return "rrc c";

                        case 0x0A:
                            return "rrc d";

                        case 0x0B:
                            return "rrc e";

                        case 0x0C:
                            return "rrc h";

                        case 0x0D:
                            return "rrc l";

                        case 0x0E:
                            return "rrc [hl]";

                        case 0x0F:
                            return "rrc a";

                        case 0x10:
                            return "rl b";

                        case 0x11:
                            return "rl c";

                        case 0x12:
                            return "rl d";

                        case 0x13:
                            return "rl e";

                        case 0x14:
                            return "rl h";

                        case 0x15:
                            return "rl l";

                        case 0x16:
                            return "rl [hl]";

                        case 0x17:
                            return "rl a";

                        case 0x18:
                            return "rr b";

                        case 0x19:
                            return "rr c";

                        case 0x1A:
                            return "rr d";

                        case 0x1B:
                            return "rr e";

                        case 0x1C:
                            return "rr h";

                        case 0x1D:
                            return "rr l";

                        case 0x1E:
                            return "rr [hl]";

                        case 0x1F:
                            return "rr a";

                        case 0x20:
                            return "sla b";

                        case 0x21:
                            return "sla c";

                        case 0x22:
                            return "sla d";

                        case 0x23:
                            return "sla e";

                        case 0x24:
                            return "sla h";

                        case 0x25:
                            return "sla l";

                        case 0x26:
                            return "sla [hl]";

                        case 0x27:
                            return "sla a";

                        case 0x28:
                            return "sra b";

                        case 0x29:
                            return "sra c";

                        case 0x2A:
                            return "sra d";

                        case 0x2B:
                            return "sra e";

                        case 0x2C:
                            return "sra h";

                        case 0x2D:
                            return "sra l";

                        case 0x2E:
                            return "sra [hl]";

                        case 0x2F:
                            return "sra a";

                        case 0x30:
                            return "swap b";

                        case 0x31:
                            return "swap c";

                        case 0x32:
                            return "swap d";

                        case 0x33:
                            return "swap e";

                        case 0x34:
                            return "swap h";

                        case 0x35:
                            return "swap l";

                        case 0x36:
                            return "swap [hl]";

                        case 0x37:
                            return "swap a";

                        case 0x38:
                            return "srl b";

                        case 0x39:
                            return "srl c";

                        case 0x3A:
                            return "srl d";

                        case 0x3B:
                            return "srl e";

                        case 0x3C:
                            return "srl h";

                        case 0x3D:
                            return "srl l";

                        case 0x3E:
                            return "srl [hl]";

                        case 0x3F:
                            return "srl a";

                        case 0x40:
                            return "bit 0, b";

                        case 0x41:
                            return "bit 0, c";

                        case 0x42:
                            return "bit 0, d";

                        case 0x43:
                            return "bit 0, e";

                        case 0x44:
                            return "bit 0, h";

                        case 0x45:
                            return "bit 0, l";

                        case 0x46:
                            return "bit 0, [hl]";

                        case 0x47:
                            return "bit 0, a";

                        case 0x48:
                            return "bit 1, b";

                        case 0x49:
                            return "bit 1, c";

                        case 0x4A:
                            return "bit 1, d";

                        case 0x4B:
                            return "bit 1, e";

                        case 0x4C:
                            return "bit 1, h";

                        case 0x4D:
                            return "bit 1, l";

                        case 0x4E:
                            return "bit 1, [hl]";

                        case 0x4F:
                            return "bit 1, a";

                        case 0x50:
                            return "bit 2, b";

                        case 0x51:
                            return "bit 2, c";

                        case 0x52:
                            return "bit 2, d";

                        case 0x53:
                            return "bit 2, e";

                        case 0x54:
                            return "bit 2, h";

                        case 0x55:
                            return "bit 2, l";

                        case 0x56:
                            return "bit 2, [hl]";

                        case 0x57:
                            return "bit 2, a";

                        case 0x58:
                            return "bit 3, b";

                        case 0x59:
                            return "bit 3, c";

                        case 0x5A:
                            return "bit 3, d";

                        case 0x5B:
                            return "bit 3, e";

                        case 0x5C:
                            return "bit 3, h";

                        case 0x5D:
                            return "bit 3, l";

                        case 0x5E:
                            return "bit 3, [hl]";

                        case 0x5F:
                            return "bit 3, a";

                        case 0x60:
                            return "bit 4, b";

                        case 0x61:
                            return "bit 4, c";

                        case 0x62:
                            return "bit 4, d";

                        case 0x63:
                            return "bit 4, e";

                        case 0x64:
                            return "bit 4, h";

                        case 0x65:
                            return "bit 4, l";

                        case 0x66:
                            return "bit 4, [hl]";

                        case 0x67:
                            return "bit 4, a";

                        case 0x68:
                            return "bit 5, b";

                        case 0x69:
                            return "bit 5, c";

                        case 0x6A:
                            return "bit 5, d";

                        case 0x6B:
                            return "bit 5, e";

                        case 0x6C:
                            return "bit 5, h";

                        case 0x6D:
                            return "bit 5, l";

                        case 0x6E:
                            return "bit 5, [hl]";

                        case 0x6F:
                            return "bit 5, a";

                        case 0x70:
                            return "bit 6, b";

                        case 0x71:
                            return "bit 6, c";

                        case 0x72:
                            return "bit 6, d";

                        case 0x73:
                            return "bit 6, e";

                        case 0x74:
                            return "bit 6, h";

                        case 0x75:
                            return "bit 6, l";

                        case 0x76:
                            return "bit 6, [hl]";

                        case 0x77:
                            return "bit 6, a";

                        case 0x78:
                            return "bit 7, b";

                        case 0x79:
                            return "bit 7, c";

                        case 0x7A:
                            return "bit 7, d";

                        case 0x7B:
                            return "bit 7, e";

                        case 0x7C:
                            return "bit 7, h";

                        case 0x7D:
                            return "bit 7, l";

                        case 0x7E:
                            return "bit 7, [hl]";

                        case 0x7F:
                            return "bit 7, a";

                        case 0x80:
                            return "res 0, b";

                        case 0x81:
                            return "res 0, c";

                        case 0x82:
                            return "res 0, d";

                        case 0x83:
                            return "res 0, e";

                        case 0x84:
                            return "res 0, h";

                        case 0x85:
                            return "res 0, l";

                        case 0x86:
                            return "res 0, [hl]";

                        case 0x87:
                            return "res 0, a";

                        case 0x88:
                            return "res 1, b";

                        case 0x89:
                            return "res 1, c";

                        case 0x8A:
                            return "res 1, d";

                        case 0x8B:
                            return "res 1, e";

                        case 0x8C:
                            return "res 1, h";

                        case 0x8D:
                            return "res 1, l";

                        case 0x8E:
                            return "res 1, [hl]";

                        case 0x8F:
                            return "res 1, a";

                        case 0x90:
                            return "res 2, b";

                        case 0x91:
                            return "res 2, c";

                        case 0x92:
                            return "res 2, d";

                        case 0x93:
                            return "res 2, e";

                        case 0x94:
                            return "res 2, h";

                        case 0x95:
                            return "res 2, l";

                        case 0x96:
                            return "res 2, [hl]";

                        case 0x97:
                            return "res 2, a";

                        case 0x98:
                            return "res 3, b";

                        case 0x99:
                            return "res 3, c";

                        case 0x9A:
                            return "res 3, d";

                        case 0x9B:
                            return "res 3, e";

                        case 0x9C:
                            return "res 3, h";

                        case 0x9D:
                            return "res 3, l";

                        case 0x9E:
                            return "res 3, [hl]";

                        case 0x9F:
                            return "res 3, a";

                        case 0xA0:
                            return "res 4, b";

                        case 0xA1:
                            return "res 4, c";

                        case 0xA2:
                            return "res 4, d";

                        case 0xA3:
                            return "res 4, e";

                        case 0xA4:
                            return "res 4, h";

                        case 0xA5:
                            return "res 4, l";

                        case 0xA6:
                            return "res 4, [hl]";

                        case 0xA7:
                            return "res 4, a";

                        case 0xA8:
                            return "res 5, b";

                        case 0xA9:
                            return "res 5, c";

                        case 0xAA:
                            return "res 5, d";

                        case 0xAB:
                            return "res 5, e";

                        case 0xAC:
                            return "res 5, h";

                        case 0xAD:
                            return "res 5, l";

                        case 0xAE:
                            return "res 5, [hl]";

                        case 0xAF:
                            return "res 5, a";

                        case 0xB0:
                            return "res 6, b";

                        case 0xB1:
                            return "res 6, c";

                        case 0xB2:
                            return "res 6, d";

                        case 0xB3:
                            return "res 6, e";

                        case 0xB4:
                            return "res 6, h";

                        case 0xB5:
                            return "res 6, l";

                        case 0xB6:
                            return "res 6, [hl]";

                        case 0xB7:
                            return "res 6, a";

                        case 0xB8:
                            return "res 7, b";

                        case 0xB9:
                            return "res 7, c";

                        case 0xBA:
                            return "res 7, d";

                        case 0xBB:
                            return "res 7, e";

                        case 0xBC:
                            return "res 7, h";

                        case 0xBD:
                            return "res 7, l";

                        case 0xBE:
                            return "res 7, [hl]";

                        case 0xBF:
                            return "res 7, a";

                        case 0xC0:
                            return "set 0, b";

                        case 0xC1:
                            return "set 0, c";

                        case 0xC2:
                            return "set 0, d";

                        case 0xC3:
                            return "set 0, e";

                        case 0xC4:
                            return "set 0, h";

                        case 0xC5:
                            return "set 0, l";

                        case 0xC6:
                            return "set 0, [hl]";

                        case 0xC7:
                            return "set 0, a";

                        case 0xC8:
                            return "set 1, b";

                        case 0xC9:
                            return "set 1, c";

                        case 0xCA:
                            return "set 1, d";

                        case 0xCB:
                            return "set 1, e";

                        case 0xCC:
                            return "set 1, h";

                        case 0xCD:
                            return "set 1, l";

                        case 0xCE:
                            return "set 1, [hl]";

                        case 0xCF:
                            return "set 1, a";

                        case 0xD0:
                            return "set 2, b";

                        case 0xD1:
                            return "set 2, c";

                        case 0xD2:
                            return "set 2, d";

                        case 0xD3:
                            return "set 2, e";

                        case 0xD4:
                            return "set 2, h";

                        case 0xD5:
                            return "set 2, l";

                        case 0xD6:
                            return "set 2, [hl]";

                        case 0xD7:
                            return "set 2, a";

                        case 0xD8:
                            return "set 3, b";

                        case 0xD9:
                            return "set 3, c";

                        case 0xDA:
                            return "set 3, d";

                        case 0xDB:
                            return "set 3, e";

                        case 0xDC:
                            return "set 3, h";

                        case 0xDD:
                            return "set 3, l";

                        case 0xDE:
                            return "set 3, [hl]";

                        case 0xDF:
                            return "set 3, a";

                        case 0xE0:
                            return "set 4, b";

                        case 0xE1:
                            return "set 4, c";

                        case 0xE2:
                            return "set 4, d";

                        case 0xE3:
                            return "set 4, e";

                        case 0xE4:
                            return "set 4, h";

                        case 0xE5:
                            return "set 4, l";

                        case 0xE6:
                            return "set 4, [hl]";

                        case 0xE7:
                            return "set 4, a";

                        case 0xE8:
                            return "set 5, b";

                        case 0xE9:
                            return "set 5, c";

                        case 0xEA:
                            return "set 5, d";

                        case 0xEB:
                            return "set 5, e";

                        case 0xEC:
                            return "set 5, h";

                        case 0xED:
                            return "set 5, l";

                        case 0xEE:
                            return "set 5, [hl]";

                        case 0xEF:
                            return "set 5, a";

                        case 0xF0:
                            return "set 6, b";

                        case 0xF1:
                            return "set 6, c";

                        case 0xF2:
                            return "set 6, d";

                        case 0xF3:
                            return "set 6, e";

                        case 0xF4:
                            return "set 6, h";

                        case 0xF5:
                            return "set 6, l";

                        case 0xF6:
                            return "set 6, [hl]";

                        case 0xF7:
                            return "set 6, a";

                        case 0xF8:
                            return "set 7, b";

                        case 0xF9:
                            return "set 7, c";

                        case 0xFA:
                            return "set 7, d";

                        case 0xFB:
                            return "set 7, e";

                        case 0xFC:
                            return "set 7, h";

                        case 0xFD:
                            return "set 7, l";

                        case 0xFE:
                            return "set 7, [hl]";

                        case 0xFF:
                            return "set 7, a";
                    }

                    return ";unknown BC opcode " + newOpcode.ToString("X").PadLeft(2, '0');

                case 0xCC:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    return "call z, Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xCD:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    checkBankSwitchFunction(iword);
                    return "call Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xCE:
                    return "adc a, $" + getByteString(currentLocation + 1);

                case 0xCF:
                    functionStack.Add(getLinkerAddress(0x08, 0x00));
                    return "rst Label8";

                case 0xD0:
                    return "ret nc";

                case 0xD1:
                    return "pop de";

                case 0xD2:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    return "jp nc, Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xD4:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    return "call nc, Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xD5:
                    return "push de";

                case 0xD6:
                    return "sub a, $" + getByteString(currentLocation + 1);

                case 0xD7:
                    functionStack.Add(getLinkerAddress(0x10, 0x00));
                    return "rst Label10";

                case 0xD8:
                    return "ret c";

                case 0xD9:
                    return "reti";

                case 0xDA:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    return "jp c, Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xDC:
                    word = getWordString(currentLocation + 1);
                    iword = Convert.ToInt32(word, 16);
                    functionStack.Add(getLinkerAddress(iword, bank));
                    checkFromLabel(iword);
                    return "call c, Label" + getRomAddrFromMemAddr(iword).ToString("X") + (iword > 0x7FFF ? " ;Warning - RAM-only procedure" : "");

                case 0xDE:
                    return "sbc a, $" + getByteString(currentLocation + 1);

                case 0xDF:
                    functionStack.Add(getLinkerAddress(0x18, 0x00));
                    return "rst Label18";

                case 0xE0:
                    return "ld [$FF" + getByteString(currentLocation + 1) + "], a" + getInstructionHint(opcode, Convert.ToInt32("FF" + getByteString(currentLocation + 1), 16));

                case 0xE1:
                    return "pop hl";

                case 0xE2:
                    return "ld [$FF0C], a";

                case 0xE5:
                    return "push hl";

                case 0xE6:
                    return "and a, $" + getByteString(currentLocation + 1);

                case 0xE7:
                    functionStack.Add(getLinkerAddress(0x20, 0x00));
                    return "rst Label20";

                case 0xE8:
                    return "add sp, $" + getByteString(currentLocation + 1);

                case 0xE9:
                    return "jp [hl]";

                case 0xEA:
                    word = getWordString(currentLocation + 1);
                    int intWord = Convert.ToInt32(word, 16);
                    if (intWord >= 0x2000 && intWord <= 0x2FFF)
                    {
                        bank = A;
                        switchRomBank();
                    }
                    return "ld [$" + word + "], a" + getInstructionHint(opcode, intWord);

                case 0xEE:
                    return "xor a, $" + getByteString(currentLocation + 1);

                case 0xEF:
                    functionStack.Add(getLinkerAddress(0x28, 0x00));
                    return "rst Label28";

                case 0xF0:
                    return "ld a, [$FF" + getByteString(currentLocation + 1) + "] " + getInstructionHint(opcode, Convert.ToInt32("FF" + getByteString(currentLocation + 1), 16));

                case 0xF1:
                    return "pop af";

                case 0xF2:
                    return "ld a, [$FF0C]";

                case 0xF3:
                    return "di";

                case 0xF5:
                    return "push af";

                case 0xF6:
                    return "or a, $" + getByteString(currentLocation + 1);

                case 0xF7:
                    functionStack.Add(getLinkerAddress(0x30, 0x00));
                    return "rst Label30";

                case 0xF8:
                    return "ld hl, sp+" + getByteString(currentLocation + 1);

                case 0xF9:
                    return "ld sp, hl";

                case 0xFA:
                    return "ld a, [$" + getWordString(currentLocation + 1) + "]";

                case 0xFB:
                    return "ei";

                case 0xFE:
                    return "cp a, $" + getByteString(currentLocation + 1);

                case 0xFF:
                    functionStack.Add(getLinkerAddress(0x38, 0x00));
                    return "rst Label38";
            }

            return ";unknown opcode " + opcode.ToString("X").PadLeft(2, '0');
        
        }

        private int getInstructionLength(int currentLocation)
        {

            int opcode = readMemByte(currentLocation);

            switch (opcode)
            {
                case 0x01:
                    return 3;
                case 0x08:
                    return 3;
                case 0x11:
                    return 3;
                case 0x21:
                    return 3;
                case 0x31:
                    return 3;
                case 0xC2:
                    return 3;
                case 0xC3:
                    return 3;
                case 0xC4:
                    return 3;
                case 0xCA:
                    return 3;
                case 0xCC:
                    return 3;
                case 0xCD:
                    return 3;
                case 0xD2:
                    return 3;
                case 0xD4:
                    return 3;
                case 0xDA:
                    return 3;
                case 0xDC:
                    return 3;
                case 0xEA:
                    return 3;
                case 0xFA:
                    return 3;
                case 0x06:
                    return 2;
                case 0x0E:
                    return 2;
                case 0x10:
                    return 2;
                case 0x16:
                    return 2;
                case 0x18:
                    return 2;
                case 0x1E:
                    return 2;
                case 0x20:
                    return 2;
                case 0x26:
                    return 2;
                case 0x28:
                    return 2;
                case 0x2E:
                    return 2;
                case 0x30:
                    return 2;
                case 0x36:
                    return 2;
                case 0x38:
                    return 2;
                case 0x3E:
                    return 2;
                case 0xC6:
                    return 2;
                case 0xCB:
                    return 2;
                case 0xCE:
                    return 2;
                case 0xD6:
                    return 2;
                case 0xDE:
                    return 2;
                case 0xE0:
                    return 2;
                case 0xE6:
                    return 2;
                case 0xE8:
                    return 2;
                case 0xEE:
                    return 2;
                case 0xF0:
                    return 2;
                case 0xF6:
                    return 2;
                case 0xFE:
                    return 2;
                default:
                    return 1;
            }


        }


        private string getInstructionHint(int opcode, int value = 0) {

            switch (opcode)
            {
                case 0xEA:
                case 0XE0:
                    if (value >= 0x2000 && value <= 0x2FFF)
                        return " ;bank Switch";
                    else if(value == 0xFF00)
                        return " ;write joypad info";
                    else if (value == 0xFF01)
                        return " ;serial transfer data";
                    else if (value == 0xFF02)
                        return " ;serial I/O control";
                    else if (value == 0xFF04)
                        return " ;timer divider";
                    else if (value == 0xFF05)
                        return " ;timer counter";
                    else if (value == 0xFF06)
                        return " ;timer modulo";
                    else if (value == 0xFF07)
                        return " ;timer control";
                    else if (value == 0xFF0F)
                        return " ;interrupt flag";
                    else if (value == 0xFF40)
                        return " ;lcd control";
                    else if (value == 0xFF41)
                        return " ;lcd status";
                    else if (value == 0xFF42)
                        return " ;scroll screen Y";
                    else if (value == 0xFF43)
                        return " ;scroll screen X";
                    else if (value == 0xFF44)
                        return " ;lcdc Y-coord";
                    else if (value == 0xFF45)
                        return " ;LY compare";
                    else if (value == 0xFF46)
                        return " ;DMA transfer";
                    else if (value == 0xFF47)
                        return " ;bg pallete data";
                    else if (value == 0xFF48)
                        return " ;obj pallete 0 data";
                    else if (value == 0xFF49)
                        return " ;obj pallete 1 data";
                    else if (value == 0xFF4A)
                        return " ;window Y pos";
                    else if (value == 0xFF4B)
                        return " ;window X pos";
                    else if (value == 0xFF4D)
                        return " ;cpu speed select";
                    else if (value == 0xFF4F)
                        return " ;vram bank select";
                    else if (value == 0xFF51)
                        return " ;HBL general DMA 1";
                    else if (value == 0xFF52)
                        return " ;HBL general DMA 2";
                    else if (value == 0xFF53)
                        return " ;HBL general DMA 3";
                    else if (value == 0xFF54)
                        return " ;HBL general DMA 4";
                    else if (value == 0xFF55)
                        return " ;HBL general DMA 5";
                    else if (value == 0xFF56)
                        return " ;infrared comms";
                    else if (value == 0xFF68)
                        return " ;bg color index";
                    else if (value == 0xFF69)
                        return " ;bg color data";
                    else if (value == 0xFF6A)
                        return " ;obj color index";
                    else if (value == 0xFF6B)
                        return " ;obj color data";
                    else if (value == 0xFF70)
                        return " ;ram bank select";
                    else if (value == 0xFFFF)
                        return " ;interrupt enable";
                    else if (value == 0xFF10)
                        return " ;NR10 audio sweep";
                    else if (value == 0xFF11)
                        return " ;NR11 audio channel #1";
                    else if (value == 0xFF12)
                        return " ;NR12 envelope channel #1";
                    else if (value == 0xFF13)
                        return " ;NR13 sound frequency #1";
                    else if (value == 0xFF14)
                        return " ;NR14 sound frequency #1";
                    else if (value == 0xFF16)
                        return " ;NR21 audio channel #2";
                    else if (value == 0xFF17)
                        return " ;NR22 envelope channel #2";
                    else if (value == 0xFF18)
                        return " ;NR23 sound frequency #2";
                    else if (value == 0xFF19)
                        return " ;NR24 sound frequency #2";
                    else if (value == 0xFF1A)
                        return " ;NR30 audio channel #3";
                    else if (value == 0xFF1B)
                        return " ;NR31 sound length #2";
                    else if (value == 0xFF1C)
                        return " ;NR32 volume #3";
                    else if (value == 0xFF1D)
                        return " ;NR33 sound frequency #3";
                    else if (value == 0xFF1E)
                        return " ;NR34 sound frequency #3";
                    else if (value == 0xFF20)
                        return " ;NR41 sound length #4";
                    else if (value == 0xFF21)
                        return " ;NR42 envelope channel #4";
                    else if (value == 0xFF22)
                        return " ;NR43 audio counter";
                    else if (value == 0xFF23)
                        return " ;NR44 audio control";
                    else if (value == 0xFF24)
                        return " ;NR50 channel control";
                    else if (value == 0xFF25)
                        return " ;NR51 sound output";
                    else if (value == 0xFF26)
                        return " ;NR52 sound on/off";
                    else if (value == 0xFF3F)
                        return " ;sound sample ram";
                    else
                        return "";
                case 0XF0:
                    if (value == 0xFF00)
                        return " ;read joypad info";
                    else if (value == 0xFF01)
                        return " ;serial transfer data";
                    else if (value == 0xFF02)
                        return " ;serial I/O control";
                    else if (value == 0xFF04)
                        return " ;timer divider";
                    else if (value == 0xFF05)
                        return " ;timer counter";
                    else if (value == 0xFF06)
                        return " ;timer modulo";
                    else if (value == 0xFF07)
                        return " ;timer control";
                    else if (value == 0xFF0F)
                        return " ;interrupt flag";
                    else if (value == 0xFF40)
                        return " ;lcd control";
                    else if (value == 0xFF41)
                        return " ;lcd status";
                    else if (value == 0xFF42)
                        return " ;scroll screen Y";
                    else if (value == 0xFF43)
                        return " ;scroll screen X";
                    else if (value == 0xFF44)
                        return " ;lcdc Y-coord";
                    else if (value == 0xFF45)
                        return " ;LY compare";
                    else if (value == 0xFF46)
                        return " ;DMA transfer";
                    else if (value == 0xFF47)
                        return " ;bg pallete data";
                    else if (value == 0xFF48)
                        return " ;obj pallete 0 data";
                    else if (value == 0xFF49)
                        return " ;obj pallete 1 data";
                    else if (value == 0xFF4A)
                        return " ;window Y pos";
                    else if (value == 0xFF4B)
                        return " ;window X pos";
                    else if (value == 0xFF4D)
                        return " ;cpu speed select";
                    else if (value == 0xFF4F)
                        return " ;vram bank select";
                    else if (value == 0xFF51)
                        return " ;HBL general DMA 1";
                    else if (value == 0xFF52)
                        return " ;HBL general DMA 2";
                    else if (value == 0xFF53)
                        return " ;HBL general DMA 3";
                    else if (value == 0xFF54)
                        return " ;HBL general DMA 4";
                    else if (value == 0xFF55)
                        return " ;HBL general DMA 5";
                    else if (value == 0xFF56)
                        return " ;infrared comms";
                    else if (value == 0xFF68)
                        return " ;bg color index";
                    else if (value == 0xFF69)
                        return " ;bg color data";
                    else if (value == 0xFF6A)
                        return " ;obj color index";
                    else if (value == 0xFF6B)
                        return " ;obj color data";
                    else if (value == 0xFF70)
                        return " ;ram bank select";
                    else if (value == 0xFFFF)
                        return " ;interrupt enable";
                    else if (value == 0xFF10)
                        return " ;NR10 audio sweep";
                    else if (value == 0xFF11)
                        return " ;NR11 audio channel #1";
                    else if (value == 0xFF12)
                        return " ;NR12 envelope channel #1";
                    else if (value == 0xFF13)
                        return " ;NR13 sound frequency #1";
                    else if (value == 0xFF14)
                        return " ;NR14 sound frequency #1";
                    else if (value == 0xFF16)
                        return " ;NR21 audio channel #2";
                    else if (value == 0xFF17)
                        return " ;NR22 envelope channel #2";
                    else if (value == 0xFF18)
                        return " ;NR23 sound frequency #2";
                    else if (value == 0xFF19)
                        return " ;NR24 sound frequency #2";
                    else if (value == 0xFF1A)
                        return " ;NR30 audio channel #3";
                    else if (value == 0xFF1B)
                        return " ;NR31 sound length #2";
                    else if (value == 0xFF1C)
                        return " ;NR32 volume #3";
                    else if (value == 0xFF1D)
                        return " ;NR33 sound frequency #3";
                    else if (value == 0xFF1E)
                        return " ;NR34 sound frequency #3";
                    else if (value == 0xFF20)
                        return " ;NR41 sound length #4";
                    else if (value == 0xFF21)
                        return " ;NR42 envelope channel #4";
                    else if (value == 0xFF22)
                        return " ;NR43 audio counter";
                    else if (value == 0xFF23)
                        return " ;NR44 audio control";
                    else if (value == 0xFF24)
                        return " ;NR50 channel control";
                    else if (value == 0xFF25)
                        return " ;NR51 sound output";
                    else if (value == 0xFF26)
                        return " ;NR52 sound on/off";
                    else if (value == 0xFF3F)
                        return " ;sound sample ram";
                    else
                        return "";
                default:
                    return "";
            
            }
        }


        public void setRom(Rom romData) {
            this.rom = romData;
        }

        public void setBankSwitchfunction(int addr)
        {
            this.bankSwitchFunction = addr;
        }

        
        

    }
}
