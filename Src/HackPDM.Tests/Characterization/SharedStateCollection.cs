using System;
using System.IO;

using HackPDM.Abstractions;
using HackPDM.Core.Hack;
using HackPDM.Tests.Integration;

using Xunit;

namespace HackPDM.Tests.Characterization;

/// <summary>
/// Tests that change process-wide statics (<see cref="HackDefaults.Instance"/>) run here, one at a
/// time and never alongside other collections.
/// </summary>
[CollectionDefinition( Name, DisableParallelization = true )]
public sealed class SharedStateCollection
{
	public const string Name = "Process-wide state";
}

/// <summary>
/// Points <see cref="HackDefaults.Instance"/> at a fake working folder for one test and restores the
/// previous settings afterwards. The folder is not created on disk unless <paramref name="create"/>.
/// </summary>
internal sealed class PwaScope : IDisposable
{
	private readonly ISettingsProvider? _previousSettings;
	private readonly string _previousRelative;

	public string Absolute { get; }
	public string Relative { get; }

	private readonly bool _ownsFolder;

	public PwaScope( bool create = false )
		: this( Path.Combine( Path.GetTempPath(), $"hackpdm-char-{Guid.NewGuid():N}", "PWA" ), owns: true )
	{
		if( create ) Directory.CreateDirectory( Absolute );
	}

	/// <summary>Uses an existing folder as the working folder; it is left in place afterwards.</summary>
	public PwaScope( string existingFolder ) : this( existingFolder, owns: false ) { }

	private PwaScope( string absolute, bool owns )
	{
		_previousSettings = HackDefaults.Instance.SettingsProvider;
		_previousRelative = HackDefaults.Instance.PwaPathRelative;
		_ownsFolder = owns;

		Absolute = absolute;
		Relative = Path.GetFileName( absolute );

		HackDefaults.Instance.SettingsProvider = new InMemorySettingsProvider();
		HackDefaults.Instance.PwaPathAbsolute = Absolute;
		HackDefaults.Instance.PwaPathRelative = Relative;
	}

	public void Dispose()
	{
		HackDefaults.Instance.SettingsProvider = _previousSettings;
		HackDefaults.Instance.PwaPathRelative = _previousRelative;
		if( _ownsFolder )
			try { Directory.Delete( Path.GetDirectoryName( Absolute )!, true ); } catch { }
	}
}
