using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace gbdisasm
{
    public partial class disamModal : Form
    {

        public int disasmAddress { get; set; }
        public int bank { get; set; }
        public int bankSwitchFunction { get; set; }

        public disamModal()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            String addrTxt = addrTextBox.Text.Trim();
            String bankTxt = bankTxtBox.Text.Trim();

            String bsfunc = bankSwitchAddrTxt.Text.Trim();

            if(bsfunc != ""){
                bankSwitchFunction = Convert.ToInt32(bsfunc, 16);
            }

            if (addrTxt == "")
            {
                this.DialogResult = DialogResult.None;
                return;
            }

            if (bankTxt == "")
            {
                this.DialogResult = DialogResult.None;
                return;
            }

            try
            {
                this.disasmAddress = Convert.ToInt32(addrTxt, 16);
                this.bank = Convert.ToInt32(bankTxt, 16);

            }
            catch (System.FormatException)
            {
                MessageBox.Show("Start Address must be a valid Hex number.");
                this.DialogResult = DialogResult.None;
                return;
            }


        }

        private void bankTxtBox_TextChanged(object sender, EventArgs e)
        {

        }

  
    }
}
