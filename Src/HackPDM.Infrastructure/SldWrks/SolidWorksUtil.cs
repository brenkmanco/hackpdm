using HackPDM.Infrastructure.Odoo;

namespace HackPDM.Infrastructure.SldWrks
{
	public class SolidWorksUtil
	{
		/// <summary>
		/// Reads SolidWorks references and properties. On Windows with a Document Manager licence key
		/// this is the SolidWorks Document Manager; everywhere else (or when it can't start) the
		/// managed <see cref="SwFileReader"/>, which needs no SolidWorks components.
		/// </summary>
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
