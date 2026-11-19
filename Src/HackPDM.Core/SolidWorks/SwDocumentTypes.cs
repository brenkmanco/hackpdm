using System.Diagnostics.CodeAnalysis;
using SolidWorks.Interop.swdocumentmgr;

namespace HackPDM.Core.SolidWorks;

/// <summary>Which files are SolidWorks documents, and of what kind: the one extension map.</summary>
public static class SwDocumentTypes
{
	/// <summary>Determines the SolidWorks document type of a file from its extension (any case).</summary>
	/// <param name="pathOrName">A file path or name; null is allowed.</param>
	/// <returns>Part, assembly or drawing for .sldprt / .sldasm / .slddrw; <see cref="SwDmDocumentType.swDmDocumentUnknown"/> otherwise.</returns>
	public static SwDmDocumentType Of( string? pathOrName )
	{
		if( string.IsNullOrEmpty( pathOrName ) )
			return SwDmDocumentType.swDmDocumentUnknown;
		ReadOnlySpan<char> ext = Path.GetExtension( pathOrName.AsSpan() );
		if( ext.Length != 7 || ext[ 0 ] != '.' )
			return SwDmDocumentType.swDmDocumentUnknown;
		return MatchBareExtension( ext[ 1.. ] );
	}

	/// <summary>Checks whether a file is a SolidWorks part, assembly or drawing, by extension (any case). The file isn't opened.</summary>
	/// <param name="pathOrName">A file path or name; null is allowed.</param>
	/// <returns>True for .sldprt, .sldasm and .slddrw.</returns>
	public static bool IsSolidWorks( [NotNullWhen( true )] string? pathOrName ) => Of( pathOrName ) != SwDmDocumentType.swDmDocumentUnknown;

	/// <summary>Checks whether an extension ("sldprt", ".SLDASM", ...) is a SolidWorks document's.</summary>
	/// <param name="extension">The extension, with or without the leading dot, any case.</param>
	/// <returns>True for sldprt, sldasm and slddrw; false for null or empty.</returns>
	public static bool IsSolidWorksExtension( [NotNullWhen( true )] string? extension )
	{
		if( string.IsNullOrEmpty( extension ) )
			return false;
		ReadOnlySpan<char> span = extension.AsSpan();
		if( span[ 0 ] == '.' )
			span = span[ 1.. ];
		return span.Length == 6 && MatchBareExtension( span ) != SwDmDocumentType.swDmDocumentUnknown;
	}

	private static SwDmDocumentType MatchBareExtension( ReadOnlySpan<char> ext )
	{
		if( ext.Equals( "sldprt", StringComparison.OrdinalIgnoreCase ) )
			return SwDmDocumentType.swDmDocumentPart;
		if( ext.Equals( "sldasm", StringComparison.OrdinalIgnoreCase ) )
			return SwDmDocumentType.swDmDocumentAssembly;
		if( ext.Equals( "slddrw", StringComparison.OrdinalIgnoreCase ) )
			return SwDmDocumentType.swDmDocumentDrawing;
		return SwDmDocumentType.swDmDocumentUnknown;
	}
}
