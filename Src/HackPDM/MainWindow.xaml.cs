using Microsoft.UI.Xaml;

using HackPDM.Shared.GlobalData;

using WindowHelper = HackPDM.UI.Controls.WindowHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
		WindowHelper.ResizeWindow(this, StorageBox.PROFILE_MANAGER_WIDTH, StorageBox.PROFILE_MANAGER_HEIGHT);
	}
}