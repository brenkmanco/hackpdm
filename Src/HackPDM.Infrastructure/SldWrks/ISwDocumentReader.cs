namespace HackPDM.Infrastructure.SldWrks;

/// <summary>Reads references and custom properties from SolidWorks files.</summary>
public interface ISwDocumentReader
{
	/// <summary>
	/// Lists the files this document references (assembly components, derived/base parts, drawing models),
	/// excluding virtual components.
	/// </summary>
	/// <param name="fileName">Path of the SolidWorks file to read.</param>
	/// <param name="deep">Reserved for recursive lookup; the current implementations ignore it and return direct references only.</param>
	/// <param name="noInterrupt">When true, a file that can't be opened yields null instead of an exception.</param>
	/// <returns>
	/// One <c>[file name, full path as stored in the document]</c> pair per reference; the stored path is a Windows path from
	/// the machine that saved the file (see <see cref="SwReferenceResolver"/>). Null when the file isn't a SolidWorks document
	/// (by extension) or, with <paramref name="noInterrupt"/>, when it can't be opened.
	/// </returns>
	/// <exception cref="Exception">The file can't be opened or read and <paramref name="noInterrupt"/> is false. The exact type depends on the implementation.</exception>
	List<string[]>? GetDependencies( string fileName, bool deep = false, bool noInterrupt = false );

	/// <summary>Reads the document's custom properties: file-level ones first, then each configuration's.</summary>
	/// <param name="fileName">Path of the SolidWorks file to read.</param>
	/// <returns>
	/// (configuration name, property name, type, value) tuples: configuration "" for file-level properties; type "text" (string),
	/// "number" (decimal), "date" (DateTime), "yesno" (bool), or "" when unknown (raw value). Null when the file isn't a
	/// SolidWorks document.
	/// </returns>
	/// <exception cref="Exception">The file can't be opened or read. The exact type depends on the implementation.</exception>
	List<Tuple<string, string, string, object>>? GetProperties( string fileName );

	/// <summary>Reads the preview image SolidWorks saved in the document.</summary>
	/// <param name="fileName">Path of the SolidWorks file to read.</param>
	/// <returns>The preview as PNG bytes, or null when the file has none, isn't a SolidWorks file, or can't be read.</returns>
	byte[]? GetPreviewPng( string fileName );
}
