using SolidWorks.Interop.swdocumentmgr;

namespace HackPDM.Infrastructure.SldWrks;

/// <summary>Which files are SolidWorks documents, and of what kind: the one extension map.</summary>
public static class SwDocumentTypes
{
	private static readonly Dictionary<string, SwDmDocumentType> ByExtension = new( StringComparer.OrdinalIgnoreCase )
	{
		[ ".sldprt" ] = SwDmDocumentType.swDmDocumentPart,
		[ ".sldasm" ] = SwDmDocumentType.swDmDocumentAssembly,
		[ ".slddrw" ] = SwDmDocumentType.swDmDocumentDrawing,
	};

	/// <summary>The document type of a file, from its extension (any case).</summary>
	public static SwDmDocumentType Of( string? pathOrName )
		=> ByExtension.GetValueOrDefault( Path.GetExtension( pathOrName ) ?? "", SwDmDocumentType.swDmDocumentUnknown );

	/// <summary>Whether a file is a SolidWorks part, assembly or drawing (by extension, any case).</summary>
	public static bool IsSolidWorks( string? pathOrName ) => Of( pathOrName ) != SwDmDocumentType.swDmDocumentUnknown;

	/// <summary>Whether an extension ("sldprt", ".SLDASM", …) is a SolidWorks document's.</summary>
	public static bool IsSolidWorksExtension( string? extension )
		=> !string.IsNullOrEmpty( extension ) && ByExtension.ContainsKey( extension.StartsWith( '.' ) ? extension : "." + extension );
}
