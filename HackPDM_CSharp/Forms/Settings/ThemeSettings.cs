using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HackPDM.Forms.Settings
{
    public partial class ThemeSettings : Form
    {
        string ThemeName = "";

        public ThemeSettings()
        {
            InitializeComponent();
        }

        private void NameTXT_TextChanged(object sender, EventArgs e)
        {
            ThemeName = NameTXT.Text;
            
        }

        private void FontTXT_TextChanged(object sender, EventArgs e)
        {

        }

        private void ForegroundColorTXT_TextChanged(object sender, EventArgs e)
        {
            var diagResult = ColorPicker.ShowDialog();
            if (diagResult is DialogResult.OK or DialogResult.Yes)
            {

            }
        }

        private void BackgroundColorTXT_TextChanged(object sender, EventArgs e)
        {
            var diagResult = ColorPicker.ShowDialog();
            if (diagResult is DialogResult.OK or DialogResult.Yes)
            {

            }
        }

        private void SecondBackgroundColorTXT_TextChanged(object sender, EventArgs e)
        {
            var diagResult = ColorPicker.ShowDialog();
            if (diagResult is DialogResult.OK or DialogResult.Yes)
            {

            }
        }
    }
}
