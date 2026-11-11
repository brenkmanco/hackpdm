using CommunityToolkit.Mvvm.ComponentModel;

namespace HackPDM.ViewModels;

public partial class OdooSettingsViewModel : ObservableObject
{
	public string OdooAddress { get; set; } = string.Empty;
	public string OdooPort { get; set; } = string.Empty;
	public string OdooDb { get; set; } = string.Empty;
	public string OdooSwKey { get; set; } = string.Empty;
	public string OdooAreaFactor { get; set; } = "1";
	public string OdooUser { get; set; } = string.Empty;
	public string OdooPass { get; set; } = string.Empty;
	public string OdooUrl { get; set; } = string.Empty;
}
