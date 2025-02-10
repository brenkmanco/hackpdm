using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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

            HackFileManager.DownloadBatchSize = batchSize;
            StatusDialog.HistoryLength = historyLength;
            StatusDialog.SkipText = skipped;
            StatusErrorMessage.ForeColor = Color.Green;
            StatusErrorMessage.Text = "Successfully saved\nnew settings";
        }
    }
}
