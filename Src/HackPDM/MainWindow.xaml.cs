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
		WindowHelper.ResizeWindow( this, 820, 580 );
	}

	internal Frame ContentFrame => RootFrame;
}
