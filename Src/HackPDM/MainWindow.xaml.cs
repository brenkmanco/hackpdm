using HackPDM.Common;

using Microsoft.UI.Xaml;

namespace HackPDM;

public sealed partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		SetTitleBar( AppTitleBar );
		WindowHelper.ApplyAppIcon( this );
		WindowHelper.ResizeWindow( this, 820, 580 );
	}
}
