namespace HackPDM.Infrastructure.SldWrks;

/// <summary>A SolidWorks file's references, each with where it is on this machine.</summary>
public static class SwDependencies
{
	/// <summary>One reference: the path stored in the file (from the machine that saved it), and the local file it resolves to, if found.</summary>
	public readonly record struct Reference( string Stored, string? Local );

	/// <summary>
	/// The non-virtual references of <paramref name="file"/>, resolved against the working folder.
	/// Empty when it has none or no reader is available; throws when the file can't be read.
	/// </summary>
	public static List<Reference> Of( string file, SwReferenceResolver resolver )
	{
		if( SolidWorksUtil.DocMgr?.GetDependencies( file ) is not { Count: > 0 } dependencies )
			return [];
		return [ .. dependencies.Select( d => new Reference( d[ 1 ], resolver.Resolve( d[ 1 ], file ) ) ) ];
	}
}
