using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>The system's open and save file pickers, starting in the documents folder.</summary>
public interface IFilePickerService
{
	/// <summary>Lets the user choose an existing file to open.</summary>
	/// <param name="extension">The only file type offered, with its dot (".json").</param>
	/// <returns>The chosen file's path; null when the user cancelled.</returns>
	Task<string?> PickOpenFileAsync( string extension );

	/// <summary>Lets the user choose an existing file matching any of the given extensions.</summary>
	/// <param name="extensions">Allowed extensions with leading dots (e.g. ".png", ".jpg").</param>
	/// <returns>The chosen file's path; null when the user cancelled.</returns>
	Task<string?> PickOpenFileAsync( IReadOnlyList<string> extensions );

	/// <summary>Lets the user choose where to save a file.</summary>
	/// <param name="typeLabel">The name shown for the file type ("JSON file").</param>
	/// <param name="extension">The file type's extension, with its dot (".json").</param>
	/// <param name="suggestedName">The file name filled in at first.</param>
	/// <returns>The chosen file's path; null when the user cancelled. Nothing is written.</returns>
	Task<string?> PickSaveFileAsync( string typeLabel, string extension, string suggestedName );

	/// <summary>Lets the user choose an existing directory.</summary>
	/// <returns>The chosen folder's path; null when the user cancelled.</returns>
	Task<string?> PickFolderAsync();
}
