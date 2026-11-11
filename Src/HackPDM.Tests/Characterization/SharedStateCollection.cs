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

	public PwaScope( bool create = false )
	{
		_previousSettings = HackDefaults.Instance.SettingsProvider;
		_previousRelative = HackDefaults.Instance.PwaPathRelative;

		Relative = "PWA";
		Absolute = Path.Combine( Path.GetTempPath(), $"hackpdm-char-{Guid.NewGuid():N}", Relative );
		if( create ) Directory.CreateDirectory( Absolute );

		HackDefaults.Instance.SettingsProvider = new InMemorySettingsProvider();
		HackDefaults.Instance.PwaPathAbsolute = Absolute;
		HackDefaults.Instance.PwaPathRelative = Relative;
	}

	public void Dispose()
	{
		HackDefaults.Instance.SettingsProvider = _previousSettings;
		HackDefaults.Instance.PwaPathRelative = _previousRelative;
		try { Directory.Delete( Path.GetDirectoryName( Absolute )!, true ); } catch { }
	}
}
