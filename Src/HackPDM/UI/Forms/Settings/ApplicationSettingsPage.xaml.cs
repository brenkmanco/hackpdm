using System;
using System.Collections.ObjectModel;

using ABI.Microsoft.UI.Xaml;

using HackPDM.Core;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ApplicationSettingsPage : Page
{
    public ApplicationSettingsPage()
    {
        InitializeComponent();

#if DEBUG
        // ShowIconLibrary();
#endif
    }

#if DEBUG
	private void ShowIconLibrary()
    {
        Symbol[] storedSymbols = Enum.GetValues<Symbol>();
        ObservableCollection<MyIco> icons = [];
        foreach( var Symbol in storedSymbols )
        {
            icons.Add( new() { Icon = new SymbolIcon( Symbol ) } );
        }

        ItemsViewRoot.ItemsSource = icons;
        
	}

    private class MyIco
    {
        public IconElement? Icon { get; set; }
    }
#endif
}