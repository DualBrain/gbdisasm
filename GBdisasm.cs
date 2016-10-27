using FastColoredTextBoxNS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace gbdisasm
{
    public partial class GBdisasm : Form
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        
        
        public static GBdisasm mainForm;

        private Rom rom;
        private CPU cpu;

        private List<string> symFile;
        private string outputText = "";


        //styles
        TextStyle BlueStyle = new TextStyle(Brushes.Blue, null, FontStyle.Regular);
        TextStyle BoldStyle = new TextStyle(null, null, FontStyle.Bold | FontStyle.Underline);
        TextStyle GrayStyle = new TextStyle(Brushes.Gray, null, FontStyle.Regular);
        TextStyle MagentaStyle = new TextStyle(Brushes.Magenta, null, FontStyle.Regular);
        TextStyle OrangeStyle = new TextStyle(Brushes.Orange, null, FontStyle.Regular);
        TextStyle PurpleStyle = new TextStyle(Brushes.Purple, null, FontStyle.Regular);
        TextStyle GreenStyle = new TextStyle(Brushes.Green, null, FontStyle.Italic);
        TextStyle BrownStyle = new TextStyle(Brushes.Brown, null, FontStyle.Regular);
        TextStyle MaroonStyle = new TextStyle(Brushes.Maroon, null, FontStyle.Regular);
        MarkerStyle SameWordsStyle = new MarkerStyle(new SolidBrush(Color.FromArgb(40, Color.Gray)));

        public GBdisasm()
        {
            InitializeComponent();
            mainForm = this;
            
        }

        private void openROMToolStripMenuItem_Click(object sender, EventArgs e)
        {

            openFileDialog.Filter = "Gameboy ROM|*.gb;*.gbc;*.sgb";

            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                string fileName = openFileDialog.FileName;

                RomLoader romLoader = new RomLoader();
                rom = romLoader.Load(fileName);
                symFile = loadSymFile(fileName);

                cpu = new CPU();
                cpu.setRom(rom);
                cpu.initCartRom();

                String romtitle = ";ROM " + rom.getTitle();

                fctb.AppendText(romtitle);
                fctb.AppendText("\n\r");
                //fctb.AppendText(symFile);
            
            }

        }



        private void disassemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {

            Dictionary<string, string> output;

            using (disamModal disasmModal = new disamModal())
            { 
                if (disasmModal.ShowDialog() == DialogResult.OK) {
                    cpu.clearOutputBuffer();
                    cpu.setBank(disasmModal.bank, true);

                    if (disasmModal.bankSwitchFunction != -1) {
                        cpu.setBankSwitchfunction(disasmModal.bankSwitchFunction);
                    }

                    outputText = "";
                    output = cpu.disassembleOutput(disasmModal.disasmAddress);

                    foreach (var o in output)
                    {
                        
                        outputText += o.Value;
                    }

                    applySymfileReplace();

                    fctb.Clear();
                    fctb.AppendText(outputText);
                    
                }
            }

        }



        private void rgbdsSyntaxHighlight(TextChangedEventArgs e)
        {

            fctb.LeftBracket = '[';
            fctb.RightBracket = ']';

            //comment highlighting
            e.ChangedRange.SetStyle(GreenStyle, @";.*$", RegexOptions.Multiline);
            //keyword highlighting
            e.ChangedRange.SetStyle(BlueStyle, @"\b(adc|add|and|bit|call|ccf|cp|cpl|daa|dec|di|ei|halt|inc|jp|jr|ld|ldi|ldh|nop|or|pop|push|res|ret|reti|rl|rla|rlc|rr|rrc|rrca|rst|sbc|scf|set|sla|sra|srl|stop|sub|swap|xor)\b|#region\b|#endregion\b");
            //hex number highlighting
            e.ChangedRange.SetStyle(BrownStyle, @"\$[0-9a-fA-F]+\b");
            //dec number highlighting
            e.ChangedRange.SetStyle(OrangeStyle, @"(?<![\$-%])\b\d+\b");
            //bin number highlighting
            e.ChangedRange.SetStyle(MagentaStyle, @"\%[0-1]+\b");
        
        }

        private void fctb_TextChanged(object sender, TextChangedEventArgs e)
        {
            rgbdsSyntaxHighlight(e);
        }

        private List<string> loadSymFile(string filename) {

            string extension = Path.GetExtension(filename);
            string pathWithoutExt = filename.Substring(0, filename.Length - extension.Length);
            

            string symPath = pathWithoutExt + ".sym";

            string symFile = "";

            try {
                symFile = System.IO.File.ReadAllText(symPath);
            }
            catch (FileNotFoundException ex) {
                return null;
            }

            symFile = Regex.Replace(symFile, @";(.*?)\r?\n", "");
            symFile = Regex.Replace(symFile, @"[\n\r\t ]", "");
            symFile = Regex.Replace(symFile, @"[0-9a-fA-F]{2}:[0-9a-fA-F]{4}", "\n$& ");

            return symFile.Split(Environment.NewLine.ToCharArray()).ToList<string>();

        }

        private void applySymfileReplace() {

            if (this.symFile == null)
                return;

            for (var i = 0; i < symFile.Count; i++) {

                if (symFile[i] == "")
                    continue;

                string[] symArgs = symFile[i].Split(' ');
                string[] addrArgs = symArgs[0].Split(':');
                
                int bank = Convert.ToInt32(addrArgs[0], 16);
                int addr = Convert.ToInt32(addrArgs[1], 16);
                int romAddr;

                if(addr > 0x7FFF){
                    romAddr = addr;
                }else{

                    int addrOffset = addr >= 0x4000 ? addr - 0x4000 : addr;
                    int baseAddr = bank * 0x4000;
                    romAddr = baseAddr + addrOffset;

                }

                this.outputText = Regex.Replace(this.outputText, "\\$" + addrArgs[1], symArgs[1], RegexOptions.IgnoreCase);
                this.outputText = Regex.Replace(this.outputText, "Label" + romAddr.ToString("X"), symArgs[1], RegexOptions.IgnoreCase);
            
            }
        
        
        }


    }
}
