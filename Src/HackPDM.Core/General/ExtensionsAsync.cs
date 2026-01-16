using System.Threading;
using System.Threading.Tasks;

namespace HackPDM.Core.General
{
	public static class ExtensionsAsync
	{
		public static async Task<CancellationTokenSource> RenewTokenSourceAsync(this CancellationTokenSource? source)
		{
			if (source is not null) await source.CancelAsync();
			
			source = new();
			return source;
		}
	}
}
