using HackPDM.Infrastructure.Odoo;

namespace HackPDM.Infrastructure.SldWrks
{
	/// <summary>Holds the process-wide SolidWorks file reader.</summary>
	public class SolidWorksUtil
	{
		/// <summary>
		/// Reads SolidWorks references and properties. On Windows with a Document Manager licence key
		/// this is the SolidWorks Document Manager; everywhere else (or when it can't start) the
		/// managed <see cref="SwFileReader"/>, which needs no SolidWorks components.
		/// </summary>
		/// <value>
		/// Created on first read from <see cref="HackPDM.Infrastructure.Odoo.OdooDefaults.Instance"/>'s SwApi key, so read it only after
		/// the server settings are loaded, or the managed reader is chosen for the rest of the run. Can be replaced (for example in tests).
		/// </value>
		public static ISwDocumentReader? DocMgr
		{
			get => field ??= CreateReader();
			set;
		}

		private static ISwDocumentReader CreateReader()
		{
			if( OperatingSystem.IsWindows() && !string.IsNullOrEmpty( OdooDefaults.Instance.SwApi ) )
			{
				var docMgr = new SwDocMgr( OdooDefaults.Instance.SwApi );
				if( SwDocMgr.GetApplication() is not null )
					return docMgr;
			}
			return new SwFileReader();
		}
	}
}
