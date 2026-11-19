using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Storage.Pickers;

namespace HackPDM.Common;

public interface IFilePickerService
{
	Task<string?> PickOpenFileAsync( string extension );
	Task<string?> PickOpenFileAsync( IReadOnlyList<string> extensions );
	Task<string?> PickSaveFileAsync( string typeLabel, string extension, string suggestedName );
	Task<string?> PickFolderAsync();
}

public sealed class FilePickerService : IFilePickerService
{
	public Task<string?> PickOpenFileAsync( string extension ) => PickOpenFileAsync( [ extension ] );

	public async Task<string?> PickOpenFileAsync( IReadOnlyList<string> extensions )
	{
		FileOpenPicker picker = new() { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
		AttachToWindow( picker );
		foreach( string ext in extensions )
			picker.FileTypeFilter.Add( ext );
		var file = await picker.PickSingleFileAsync().AsTask();
		return string.IsNullOrEmpty( file?.Path ) ? null : file.Path;
	}

	public async Task<string?> PickSaveFileAsync( string typeLabel, string extension, string suggestedName )
	{
		FileSavePicker picker = new()
		{
			SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
			SuggestedFileName = suggestedName,
			DefaultFileExtension = extension,
		};
		AttachToWindow( picker );
		picker.FileTypeChoices.Add( typeLabel, [ extension ] );
		var file = await picker.PickSaveFileAsync().AsTask();
		return string.IsNullOrEmpty( file?.Path ) ? null : file.Path;
	}

	public async Task<string?> PickFolderAsync()
	{
		FolderPicker picker = new() { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
		AttachToWindow( picker );
		picker.FileTypeFilter.Add( "*" );
		var folder = await picker.PickSingleFolderAsync().AsTask();
		return string.IsNullOrEmpty( folder?.Path ) ? null : folder.Path;
	}

	private static void AttachToWindow( object picker )
	{
#if WINDOWS
		if( HackApp.Window is { } window )
			WinRT.Interop.InitializeWithWindow.Initialize( picker, window.IntPtrHandle );
#else
		_ = picker;
#endif
	}
}
