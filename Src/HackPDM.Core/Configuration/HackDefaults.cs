namespace HackPDM.Core.Configuration;

/// <summary>Process-wide local workspace (PWA) settings.</summary>
public sealed class HackDefaults
{
	private string? _relativeFor;

	public static HackDefaults Instance { get; set; } = new();

	public ISettingsProvider? SettingsProvider { get; set; }

	public string PwaPathAbsolute
	{
		get => SettingsProvider?.Get<string>( "PWAPathAbsolute" ) ?? "";
		set => SettingsProvider?.Set( "PWAPathAbsolute", value );
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

	public string? CurrentPath { get; set; }

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
