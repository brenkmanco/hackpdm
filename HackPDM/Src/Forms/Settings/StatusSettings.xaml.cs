using HackPDM.Src.ClientUtils.Types;

using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Forms.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class StatusSettings : Page, ISingletonPage<StatusSettings>
{
    public StatusSettings()
    {
        InitializeComponent();
    }
}