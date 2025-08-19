using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using HackPDM.Forms.Settings;

namespace HackPDM
{
    public partial class StatusSettings : Form
    {
        public StatusSettings()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StringBuilder sb = new();
            var SD = StatusData.StaticData;
            bool skipped = skippedSetting.Checked;
            bool wasHistoryParsed = int.TryParse(StatusHistoryLengthTextbox.Text, out int historyLength);
            bool wasBatchParsed = int.TryParse(batchSizeTextbox.Text, out int batchSize);

            if (!wasHistoryParsed)
            {
                sb.AppendLine($"History Length ({StatusHistoryLengthTextbox.Text}) Invalid");
            }
            if (!wasBatchParsed)
            {
                sb.AppendLine($"Batch Length ({batchSizeTextbox.Text}) Invalid");
            }


            if (!wasBatchParsed || !wasHistoryParsed)
            {
                StatusErrorMessage.ForeColor = Color.Red;
                StatusErrorMessage.Text = sb.ToString();
                return;
            }

            //OdooDefaults.DownloadBatchSize = batchSize;
            //StatusDialog.MaxHistoryLength = historyLength;
            //SkipText = skipped;
            StatusErrorMessage.ForeColor = Color.Green;
            StatusErrorMessage.Text = "Successfully saved\nnew settings";
        }

		private void button2_Click( object sender, EventArgs e )
		{

		}
	}
}
