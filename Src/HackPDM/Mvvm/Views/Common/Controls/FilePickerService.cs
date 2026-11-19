using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using HackPDM.Mvvm.ViewModels.Common;

using Windows.Storage.Pickers;

namespace HackPDM.Mvvm.Views.Common.Controls;

/// <summary>File pickers owned by the main window.</summary>
public sealed class FilePickerService : IFilePickerService
{
	/// <inheritdoc/>
	public Task<string?> PickOpenFileAsync( string extension )
		=> PickOpenFileAsync( [ extension ] );

	/// <inheritdoc/>
	public async Task<string?> PickOpenFileAsync( IReadOnlyList<string> extensions )
	{
		FileOpenPicker picker = new() { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
		AttachToWindow( picker );
		foreach( string ext in extensions )
			picker.FileTypeFilter.Add( ext );
		var file = await picker.PickSingleFileAsync().AsTask();
		return string.IsNullOrEmpty( file?.Path ) ? null : file.Path;
	}

	/// <inheritdoc/>
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

	/// <inheritdoc/>
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
#endif
	}
}
