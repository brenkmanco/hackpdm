using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Settings;

[Bindable]
public sealed partial class HackSettingsViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;
	private readonly IFilePickerService? _pickers;
	private readonly LoginSession? _session;
	private readonly Func<Task>? _onVaultPathChanged;

	[ObservableProperty] public partial string PwaPath { get; set; }
	[ObservableProperty] public partial string TempFolderPath { get; set; }

	public HackSettingsViewModel(
		IDialogService dialogs,
		IFilePickerService? pickers = null,
		LoginSession? session = null,
		Func<Task>? onVaultPathChanged = null )
	{
		_dialogs = dialogs;
		_pickers = pickers;
		_session = session;
		_onVaultPathChanged = onVaultPathChanged;
		PwaPath = HackDefaults.Instance.PwaPathAbsolute is { Length: > 0 } pwa
			? pwa
			: Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), PdmConstants.AppName, "pwa" );
		TempFolderPath = PdmConstants.TemporaryPath;
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

	private List<string> Validate()
	{
		PwaPath = PwaPath?.Trim() ?? string.Empty;
		TempFolderPath = TempFolderPath?.Trim() ?? string.Empty;
		List<string> errors = [];
		if( !IsValidDirectoryPath( PwaPath ) )
			errors.Add( "invalid pwa directory path" );
		if( !IsValidDirectoryPath( TempFolderPath ) )
			errors.Add( "invalid temporary directory path" );
		return errors;
	}

	[RelayCommand]
	private async Task SubmitAsync()
	{
		List<string> errors = Validate();
		string pwa = PwaPath;
		string temp = TempFolderPath;
		if( errors.Count == 0 )
		{
			var (pwaOk, tempOk) = await Task.Run( () => ( TryCreateDirectory( pwa ), TryCreateDirectory( temp ) ) );
			if( !pwaOk )
				errors.Add( "invalid pwa directory path" );
			if( !tempOk )
				errors.Add( "invalid temporary directory path" );
		}
		if( errors.Count > 0 )
		{
			_session?.LogError( ProfileStatusCategory.Configuration, $"Local folder configuration error: {string.Join( "; ", errors )}; changes were not saved." );
			errors.Add( "changes were not saved" );
			await _dialogs.ShowMessageAsync( string.Join( Environment.NewLine, errors ) );
			return;
		}

		bool vaultChanged = await Task.Run( () =>
		{
			string previousPwa = HackDefaults.Instance.PwaPathAbsolute;
			HackDefaults.Instance.PwaPathAbsolute = pwa;
			HackDefaults.Instance.PwaPathRelative = new DirectoryInfo( pwa ).Name;
			PdmConstants.TemporaryPath = temp;
			HackDefaults.Instance.SettingsProvider?.Set( nameof( PdmConstants.TemporaryPath ), temp );
			return !string.Equals( previousPwa, pwa, StringComparison.OrdinalIgnoreCase );
		} );

		if( vaultChanged && _onVaultPathChanged is not null )
			await _onVaultPathChanged();

		_session?.LogInfo( ProfileStatusCategory.Configuration, $"Saved local workspace folders: PWA '{pwa}', Temp '{temp}'." );
		await _dialogs.ShowMessageAsync( "Local folder settings saved." );
	}

	private static bool IsValidDirectoryPath( string path )
	{
		if( string.IsNullOrWhiteSpace( path ) || !Path.IsPathFullyQualified( path ) )
			return false;
		try
		{
			_ = Path.GetFullPath( path );
			return true;
		}
		catch( Exception ex ) when( ex is ArgumentException or NotSupportedException or PathTooLongException )
		{
			return false;
		}
	}

	private static bool TryCreateDirectory( string path )
	{
		if( !IsValidDirectoryPath( path ) )
			return false;
		try
		{
			Directory.CreateDirectory( path );
			return true;
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException )
		{
			HackLogger.Warn( "HackSettings", $"Invalid or inaccessible directory '{path}': {ex.Message}", ex );
			return false;
		}
	}
}
