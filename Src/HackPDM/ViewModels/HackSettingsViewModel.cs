using CommunityToolkit.Mvvm.ComponentModel;

namespace HackPDM.ViewModels;

public partial class HackSettingsViewModel : ObservableObject
{
	public string PwaPath { get; set; } = string.Empty;
	public string TempFolderPath { get; set; } = string.Empty;
}
