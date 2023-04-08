using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Trader
{
    public partial class Preview : Form
    {
        string crt = "";
        public Preview()
        {
            InitializeComponent();
        }
        public Preview(string a)  
        {
           crt = a;
            InitializeComponent();
        }

        private void Preview_Load(object sender, EventArgs e)
        {
            richTextBox1.Text = crt;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.DialogResult= DialogResult.OK;
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
