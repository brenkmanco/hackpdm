using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Core.Hack;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Hack;

/// <summary>The local folders: the vault (PWA) and the temporary folder. Saved only when both can be created.</summary>
public partial class HackSettingsViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;
	private readonly IFilePickerService? _pickers;
	private readonly LoginSession? _session;

	[ObservableProperty] public partial string PwaPath { get; set; }
	[ObservableProperty] public partial string TempFolderPath { get; set; }

	/// <summary>Raised after a successful save, so a page in its own window can close it.</summary>
	public event Action? Saved;

	/// <summary>Fills the fields with the current folders, or default locations under Documents and the system temp folder.</summary>
	/// <param name="dialogs">Shows validation errors and the saved confirmation.</param>
	/// <param name="pickers">Optional folder picker service for the Browse buttons.</param>
	/// <param name="session">Optional session status log shared with the Profile Manager.</param>
	public HackSettingsViewModel( IDialogService dialogs, IFilePickerService? pickers = null, LoginSession? session = null )
	{
		_dialogs = dialogs;
		_pickers = pickers;
		_session = session;
		PwaPath = HackDefaults.Instance?.PwaPathAbsolute is { Length: > 0 } pwa
			? pwa
			: Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), StorageBox.APP_NAME, "pwa" );
		TempFolderPath = StorageBox.TemporaryPath is { Length: > 0 } temp
			? temp
			: Path.Combine( Path.GetTempPath(), StorageBox.APP_NAME );
	}

	[RelayCommand]
	private async Task BrowsePwaAsync()
	{
		if( _pickers is not null && await _pickers.PickFolderAsync() is { Length: > 0 } chosen )
			PwaPath = chosen;
	}

	[RelayCommand]
	private async Task BrowseTempAsync()
	{
		if( _pickers is not null && await _pickers.PickFolderAsync() is { Length: > 0 } chosen )
			TempFolderPath = chosen;
	}

	/// <summary>Checks both folders, creating each one if missing.</summary>
	/// <returns>Why the paths can't be saved; empty when they can.</returns>
	/// <remarks>Has a side effect: valid folders are created on disk even when the other one fails.</remarks>
	public List<string> Validate()
	{
		List<string> errors = [];
		if( !TryCreateDirectory( PwaPath ) )
			errors.Add( "invalid pwa directory path" );
		if( !TryCreateDirectory( TempFolderPath ) )
			errors.Add( "invalid temporary directory path" );
		return errors;
	}

	/// <summary>Validates the folders, then applies them to the app settings and raises <see cref="Saved"/>.</summary>
	[RelayCommand]
	private async Task SubmitAsync()
	{
		List<string> errors = Validate();
		if( errors.Count > 0 )
		{
			_session?.LogError( ProfileStatusCategory.Configuration, $"Local folder configuration error: {string.Join( "; ", errors )}; changes were not saved." );
			errors.Add( "changes were not saved" );
			await _dialogs.ShowMessageAsync( string.Join( Environment.NewLine, errors ) );
			return;
		}
		HackDefaults.Instance?.PwaPathAbsolute = PwaPath;
		HackDefaults.Instance?.PwaPathRelative = new DirectoryInfo( PwaPath ).Name;
		StorageBox.TemporaryPath = TempFolderPath;
		HackDefaults.Instance?.SettingsProvider?.Set( "TemporaryPath", TempFolderPath );
		_session?.LogInfo( ProfileStatusCategory.Configuration, $"Saved local workspace folders: PWA '{PwaPath}', Temp '{TempFolderPath}'." );
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
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException )
		{
			HackPDM.Core.Logging.HackLogger.Warn( "HackSettings", $"Invalid or inaccessible directory '{path}': {ex.Message}", ex );
			return false;
		}
	}
}
