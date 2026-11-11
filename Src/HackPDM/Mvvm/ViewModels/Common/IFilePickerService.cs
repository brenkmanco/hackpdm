using System.Threading.Tasks;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>The system's open and save file pickers, starting in the documents folder.</summary>
public interface IFilePickerService
{
	/// <summary>The chosen file's path; null when the user cancelled.</summary>
	Task<string?> PickOpenFileAsync( string extension );

	/// <summary>The chosen file's path; null when the user cancelled.</summary>
	Task<string?> PickSaveFileAsync( string typeLabel, string extension, string suggestedName );
}
