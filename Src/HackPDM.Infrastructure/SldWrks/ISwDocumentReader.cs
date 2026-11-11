namespace HackPDM.Infrastructure.SldWrks;

/// <summary>Reads references and custom properties from SolidWorks files.</summary>
public interface ISwDocumentReader
{
	/// <summary>
	/// The files this document references (assembly components, derived/base parts, drawing models),
	/// as [file name, full path as stored in the document]. Null when the file isn't a SolidWorks
	/// document. Throws when the file can't be read, unless <paramref name="noInterrupt"/> is set.
	/// </summary>
	List<string[]>? GetDependencies( string fileName, bool deep = false, bool noInterrupt = false );

	/// <summary>
	/// Custom properties as (configuration name, property name, type, value): configuration ""
	/// for file-level properties; type "text" (string), "number" (decimal), "date" (DateTime),
	/// "yesno" (bool), or "" when unknown. Null when the file isn't a SolidWorks document.
	/// </summary>
	List<Tuple<string, string, string, object>>? GetProperties( string fileName );

	/// <summary>The document's saved preview as PNG bytes, or null when it has none or isn't a SolidWorks file.</summary>
	byte[]? GetPreviewPng( string fileName );
}
