using System.Diagnostics.CodeAnalysis;
using HackPDM.Core.Odoo;

namespace HackPDM.Core.SolidWorks
{
	/// <summary>Holds the process-wide SolidWorks file reader.</summary>
	public class SolidWorksUtil
	{
		private static readonly Lock Gate = new();
		private static ISwDocumentReader? _docMgr;
		private static bool _isExplicit;
		private static string? _initializedSwApi;

		/// <summary>
		/// Reads SolidWorks references and properties. On Windows with a Document Manager licence key
		/// this is the SolidWorks Document Manager; everywhere else (or when it can't start) the
		/// managed <see cref="SwFileReader"/>, which needs no SolidWorks components.
		/// </summary>
		/// <value>
		/// Created lazily from <see cref="OdooDefaults.Instance"/>'s SwApi key and refreshed automatically
		/// if SwApi becomes available after an earlier pre-login read. Can be replaced explicitly (for example in tests).
		/// </value>
		[AllowNull]
		public static ISwDocumentReader DocMgr
		{
			get
			{
				lock( Gate )
				{
					if( _isExplicit && _docMgr is not null )
						return _docMgr;

					string currentKey = OperatingSystem.IsWindows() ? ( OdooDefaults.Instance?.SwApi ?? string.Empty ) : string.Empty;
					if( _docMgr is null || !string.Equals( _initializedSwApi, currentKey, StringComparison.Ordinal ) )
					{
						_docMgr = CreateReader( currentKey );
						_initializedSwApi = currentKey;
						_isExplicit = false;
					}
					return _docMgr;
				}
			}
			set
			{
				lock( Gate )
				{
					_docMgr = value;
					_isExplicit = value is not null;
					if( value is null )
						_initializedSwApi = null;
				}
			}
		}

		/// <summary>Clears the cached reader so the next access to <see cref="DocMgr"/> re-evaluates the current <see cref="OdooDefaults.Instance"/> configuration.</summary>
		public static void Reset()
		{
			lock( Gate )
			{
				_docMgr = null;
				_isExplicit = false;
				_initializedSwApi = null;
			}
		}

		private static ISwDocumentReader CreateReader( string swApi )
		{
			if( OperatingSystem.IsWindows() && !string.IsNullOrEmpty( swApi ) )
			{
				var docMgr = new SwDocMgr( swApi );
				if( SwDocMgr.GetApplication() is not null )
					return docMgr;
			}
			return new SwFileReader();
		}
	}
}
