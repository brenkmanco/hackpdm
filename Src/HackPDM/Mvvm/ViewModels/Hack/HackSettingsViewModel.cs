using CommunityToolkit.Mvvm.ComponentModel;

namespace HackPDM.Mvvm.ViewModels.Hack;

public partial class HackSettingsViewModel : ObservableObject
{
	public string PwaPath { get; set; } = string.Empty;
	public string TempFolderPath { get; set; } = string.Empty;
}
