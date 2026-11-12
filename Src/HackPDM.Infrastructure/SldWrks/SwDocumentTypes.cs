using System.Diagnostics.CodeAnalysis;
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

	/// <summary>Determines the SolidWorks document type of a file from its extension (any case).</summary>
	/// <param name="pathOrName">A file path or name; null is allowed.</param>
	/// <returns>Part, assembly or drawing for .sldprt / .sldasm / .slddrw; <see cref="SwDmDocumentType.swDmDocumentUnknown"/> otherwise.</returns>
	public static SwDmDocumentType Of( string? pathOrName )
		=> ByExtension.GetValueOrDefault( Path.GetExtension( pathOrName ) ?? "", SwDmDocumentType.swDmDocumentUnknown );

	/// <summary>Checks whether a file is a SolidWorks part, assembly or drawing, by extension (any case). The file isn't opened.</summary>
	/// <param name="pathOrName">A file path or name; null is allowed.</param>
	/// <returns>True for .sldprt, .sldasm and .slddrw.</returns>
	public static bool IsSolidWorks( [NotNullWhen( true )] string? pathOrName ) => Of( pathOrName ) != SwDmDocumentType.swDmDocumentUnknown;

	/// <summary>Checks whether an extension ("sldprt", ".SLDASM", ...) is a SolidWorks document's.</summary>
	/// <param name="extension">The extension, with or without the leading dot, any case.</param>
	/// <returns>True for sldprt, sldasm and slddrw; false for null or empty.</returns>
	public static bool IsSolidWorksExtension( [NotNullWhen( true )] string? extension )
		=> !string.IsNullOrEmpty( extension ) && ByExtension.ContainsKey( extension.StartsWith( '.' ) ? extension : "." + extension );
}
