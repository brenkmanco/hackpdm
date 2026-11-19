namespace HackPDM.Core.Configuration;

/// <summary>Process-wide local workspace (PWA) settings.</summary>
public sealed class HackDefaults
{
	private ISettingsProvider? _settingsProvider;
	private string? _cachedPwaAbsolute;
	private string? _relativeFor;

	public static HackDefaults Instance { get; set; } = new();

	public ISettingsProvider? SettingsProvider
	{
		get => _settingsProvider;
		set
		{
			if( !ReferenceEquals( _settingsProvider, value ) )
			{
				_settingsProvider = value;
				_cachedPwaAbsolute = null;
			}
		}
	}

	public string PwaPathAbsolute
	{
		get => _cachedPwaAbsolute ??= _settingsProvider?.Get<string>( nameof( Services.SavedProfileData.PWAPathAbsolute ) ) ?? "";
		set
		{
			_cachedPwaAbsolute = value ?? "";
			_settingsProvider?.Set( nameof( Services.SavedProfileData.PWAPathAbsolute ), value );
		}
	}

	public string PwaPathRelative
	{
		get
		{
			string absolute = PwaPathAbsolute;
			if( field is null || _relativeFor != absolute )
			{
				field = Path.GetFileName( Path.TrimEndingDirectorySeparator( absolute ) );
				_relativeFor = absolute;
			}
			return field;
		}
		set
		{
			field = value;
			_relativeFor = PwaPathAbsolute;
		}
	}

	public HackDefaults() { }

	public HackDefaults( ISettingsProvider settingsProvider )
	{
		if( Instance is null )
		{
			SettingsProvider = settingsProvider;
			Instance = this;
			return;
		}
		Instance.SettingsProvider = settingsProvider;
	}
}
