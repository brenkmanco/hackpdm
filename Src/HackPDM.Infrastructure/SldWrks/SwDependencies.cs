namespace HackPDM.Infrastructure.SldWrks;

/// <summary>A SolidWorks file's references, each with where it is on this machine.</summary>
public static class SwDependencies
{
	/// <summary>One reference: the path stored in the file (from the machine that saved it), and the local file it resolves to, if found.</summary>
	/// <param name="Stored">The path as stored in the document, normally an absolute Windows path.</param>
	/// <param name="Local">The local file it resolves to, or null when it can't be found.</param>
	public readonly record struct Reference( string Stored, string? Local );

	/// <summary>Lists the non-virtual references of <paramref name="file"/> and resolves each to a local file.</summary>
	/// <param name="file">Path of the SolidWorks file to read.</param>
	/// <param name="resolver">Finds the local copy of each stored path; reuse one per working folder so its vault index is built once.</param>
	/// <returns>The references in stored order; empty when the file has none, isn't a SolidWorks file, or no reader is available.</returns>
	/// <remarks>Reads through <see cref="SolidWorksUtil.DocMgr"/>.</remarks>
	/// <exception cref="Exception">The file can't be read (the type depends on the reader in <see cref="SolidWorksUtil.DocMgr"/>, e.g. <see cref="IOException"/> or <see cref="InvalidDataException"/>).</exception>
	public static List<Reference> Of( string file, SwReferenceResolver resolver )
	{
		if( SolidWorksUtil.DocMgr?.GetDependencies( file ) is not { Count: > 0 } dependencies )
			return [];
		return [ .. dependencies.Select( d => new Reference( d[ 1 ], resolver.Resolve( d[ 1 ], file ) ) ) ];
	}
}
