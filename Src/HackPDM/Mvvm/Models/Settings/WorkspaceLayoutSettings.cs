using System.Text.Json.Serialization;

namespace HackPDM.Mvvm.Models.Settings;

/// <summary>Which side of the workspace holds the Folders sidebar.</summary>
[JsonConverter( typeof( JsonStringEnumConverter<FolderDockSide> ) )]
public enum FolderDockSide
{
	Left,
	Right,
}

/// <summary>Where the Details inspector (History, Where Used, Dependents, Properties, Info) docks relative to Entries.</summary>
[JsonConverter( typeof( JsonStringEnumConverter<DetailsDockPosition> ) )]
public enum DetailsDockPosition
{
	Bottom,
	Right,
}

/// <summary>Where the Preview panel sits relative to the Details inspector.</summary>
[JsonConverter( typeof( JsonStringEnumConverter<PreviewDockPlacement> ) )]
public enum PreviewDockPlacement
{
	/// <summary>Right of Details when Bottom-docked, or below Details when Right-docked.</summary>
	AfterDetails,
	/// <summary>Left of Details when Bottom-docked, or above Details when Right-docked.</summary>
	BeforeDetails,
}

/// <summary>Named layout arrangements users can switch between in one click.</summary>
public enum WorkspaceLayoutPreset
{
	Balanced,
	RightInspector,
	RightSidebar,
	FocusEntries,
}

/// <summary>Saved docking & pane sizing configuration for the file manager and dependency graph workspaces.</summary>
public sealed class WorkspaceLayoutSettings
{
	public bool ShowFoldersPane { get; set; } = true;
	public FolderDockSide FoldersDock { get; set; } = FolderDockSide.Left;
	public double FoldersPaneWidth { get; set; } = 278;

	public bool ShowDetailsPane { get; set; } = true;
	public DetailsDockPosition DetailsDock { get; set; } = DetailsDockPosition.Bottom;
	public double DetailsPaneHeight { get; set; } = 250;
	public double DetailsRightWidth { get; set; } = 460;

	public bool ShowPreviewPane { get; set; } = true;
	public PreviewDockPlacement PreviewPlacement { get; set; } = PreviewDockPlacement.AfterDetails;
	public double PreviewPaneSize { get; set; } = 264;

	public double GraphFoldersWidth { get; set; } = 264;
	public double GraphFilesWidth { get; set; } = 260;

	public FolderDockSide SearchDock { get; set; } = FolderDockSide.Right;
	public double SearchDrawerWidth { get; set; } = 460;
	public double StatusDrawerHeight { get; set; } = 280;
	public bool SearchMinimalMode { get; set; }
	public bool StatusMinimalMode { get; set; }

	public void ApplyPreset( WorkspaceLayoutPreset preset )
	{
		switch( preset )
		{
			case WorkspaceLayoutPreset.Balanced:
				ShowFoldersPane = true;
				FoldersDock = FolderDockSide.Left;
				FoldersPaneWidth = 278;
				ShowDetailsPane = true;
				DetailsDock = DetailsDockPosition.Bottom;
				DetailsPaneHeight = 250;
				ShowPreviewPane = true;
				PreviewPlacement = PreviewDockPlacement.AfterDetails;
				PreviewPaneSize = 264;
				break;
			case WorkspaceLayoutPreset.RightInspector:
				ShowFoldersPane = true;
				FoldersDock = FolderDockSide.Left;
				FoldersPaneWidth = 256;
				ShowDetailsPane = true;
				DetailsDock = DetailsDockPosition.Right;
				DetailsRightWidth = 460;
				ShowPreviewPane = true;
				PreviewPlacement = PreviewDockPlacement.BeforeDetails;
				PreviewPaneSize = 220;
				break;
			case WorkspaceLayoutPreset.RightSidebar:
				ShowFoldersPane = true;
				FoldersDock = FolderDockSide.Right;
				FoldersPaneWidth = 278;
				ShowDetailsPane = true;
				DetailsDock = DetailsDockPosition.Bottom;
				DetailsPaneHeight = 250;
				ShowPreviewPane = true;
				PreviewPlacement = PreviewDockPlacement.AfterDetails;
				PreviewPaneSize = 264;
				break;
			case WorkspaceLayoutPreset.FocusEntries:
				ShowFoldersPane = false;
				ShowDetailsPane = true;
				DetailsDock = DetailsDockPosition.Bottom;
				DetailsPaneHeight = 220;
				ShowPreviewPane = false;
				break;
		}
	}
}
