using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using MessageBox = System.Windows.Forms.MessageBox;

namespace HackPDM.Src.Extensions.General
{
	public static class ExtensionsAsync
	{
		extension(MessageBox box)
		{
			public static Task<DialogResult> ShowAsync(string message)
				=> Task.Run(() => MessageBox.Show(message));
			
		}
		public static async Task<CancellationTokenSource> RenewTokenSourceAsync(this CancellationTokenSource? source)
		{
			if (source is not null) await source.CancelAsync();
			
			source = new();
			return source;
		}
	}
}
