using CommunityToolkit.Mvvm.ComponentModel;
using Windows.UI;

namespace HackPDM.ViewModels;

public partial class ApplicationSettingsViewModel : ObservableObject
{
	public Color SelectedColor { get; set; } = Color.FromArgb(255, 0, 120, 215);
}
