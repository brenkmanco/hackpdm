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
public partial class HackSettingsViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;
	private readonly IFilePickerService? _pickers;
	private readonly LoginSession? _session;
	private readonly Action? _onVaultPathChanged;

	[ObservableProperty] public partial string PwaPath { get; set; }
	[ObservableProperty] public partial string TempFolderPath { get; set; }

	public event Action? Saved;

	public HackSettingsViewModel(
		IDialogService dialogs,
		IFilePickerService? pickers = null,
		LoginSession? session = null,
		Action? onVaultPathChanged = null )
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
			_session?.LogError( ProfileStatusCategory.Configuration, $"Local folder configuration error: {string.Join( "; ", errors )}; changes were not saved." );
			errors.Add( "changes were not saved" );
			await _dialogs.ShowMessageAsync( string.Join( Environment.NewLine, errors ) );
			return;
		}

		string previousPwa = HackDefaults.Instance.PwaPathAbsolute;
		HackDefaults.Instance.PwaPathAbsolute = PwaPath;
		HackDefaults.Instance.PwaPathRelative = new DirectoryInfo( PwaPath ).Name;
		PdmConstants.TemporaryPath = TempFolderPath;
		HackDefaults.Instance.SettingsProvider?.Set( "TemporaryPath", TempFolderPath );

		if( !string.Equals( previousPwa, PwaPath, StringComparison.OrdinalIgnoreCase ) )
			_onVaultPathChanged?.Invoke();

		_session?.LogInfo( ProfileStatusCategory.Configuration, $"Saved local workspace folders: PWA '{PwaPath}', Temp '{TempFolderPath}'." );
		await _dialogs.ShowMessageAsync( "Local folder settings saved." );
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
			HackLogger.Warn( "HackSettings", $"Invalid or inaccessible directory '{path}': {ex.Message}", ex );
			return false;
		}
	}
}
