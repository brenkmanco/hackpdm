using HackPDM.Common;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM;

public sealed partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		WindowHelper.ApplyAppIcon( this );
	}

	internal Frame ContentFrame => RootFrame;
}
