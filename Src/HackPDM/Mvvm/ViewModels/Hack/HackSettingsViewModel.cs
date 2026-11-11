using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Core.Hack;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Hack;

/// <summary>The local folders: the vault (PWA) and the temporary folder. Saved only when both can be created.</summary>
public partial class HackSettingsViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;

	[ObservableProperty] public partial string PwaPath { get; set; }
	[ObservableProperty] public partial string TempFolderPath { get; set; }

	/// <summary>Raised after a successful save, so a page in its own window can close it.</summary>
	public event Action? Saved;

	public HackSettingsViewModel( IDialogService dialogs )
	{
		_dialogs = dialogs;
		PwaPath = HackDefaults.Instance?.PwaPathAbsolute is { Length: > 0 } pwa
			? pwa
			: Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), StorageBox.APP_NAME, "pwa" );
		TempFolderPath = StorageBox.TemporaryPath is { Length: > 0 } temp
			? temp
			: Path.Combine( Path.GetTempPath(), StorageBox.APP_NAME );
	}

	/// <summary>Why the paths can't be saved; empty when they can (each folder is created if missing).</summary>
	public List<string> Validate()
	{
		List<string> errors = [];
		if( !TryCreateDirectory( PwaPath ) )
			errors.Add( "invalid pwa directory path" );
		if( !TryCreateDirectory( TempFolderPath ) )
			errors.Add( "invalid temporary directory path" );
		return errors;
	}

	[RelayCommand]
	private async Task SubmitAsync()
	{
		List<string> errors = Validate();
		if( errors.Count > 0 )
		{
			errors.Add( "changes were not saved" );
			await _dialogs.ShowMessageAsync( string.Join( Environment.NewLine, errors ) );
			return;
		}
		HackDefaults.Instance?.PwaPathAbsolute = PwaPath;
		HackDefaults.Instance?.PwaPathRelative = new DirectoryInfo( PwaPath ).Name;
		StorageBox.TemporaryPath = TempFolderPath;
		// the page is usually shown inside the main window, where nothing else shows that it worked
		await _dialogs.ShowMessageAsync( "Hack settings saved" );
		Saved?.Invoke();
	}

	private static bool TryCreateDirectory( string path )
	{
		if( string.IsNullOrWhiteSpace( path ) )
			return false;
		try
		{
			Directory.CreateDirectory( path );
			return true;
		}
		catch
		{
			return false;
		}
	}
}
