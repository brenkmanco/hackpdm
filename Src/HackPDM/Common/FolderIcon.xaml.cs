using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace HackPDM.Common;

public sealed partial class FolderIcon : UserControl
{
	private static readonly SolidColorBrush SyncedClosedBrush = new( Color.FromArgb( 255, 0xC6, 0x97, 0x49 ) );
	private static readonly SolidColorBrush SyncedHighlightBrush = new( Color.FromArgb( 255, 0xE5, 0xC0, 0x7B ) );
	private static readonly SolidColorBrush SyncedOpenBackBrush = new( Color.FromArgb( 255, 0x9E, 0x74, 0x32 ) );
	private static readonly SolidColorBrush SyncedOpenFrontBrush = new( Color.FromArgb( 255, 0xDF, 0xB4, 0x68 ) );

	private static readonly SolidColorBrush RemoteClosedBrush = new( Color.FromArgb( 255, 0x3F, 0x8F, 0xB5 ) );
	private static readonly SolidColorBrush RemoteHighlightBrush = new( Color.FromArgb( 255, 0x6C, 0xB8, 0xDC ) );
	private static readonly SolidColorBrush RemoteOpenBackBrush = new( Color.FromArgb( 255, 0x2D, 0x6B, 0x8A ) );
	private static readonly SolidColorBrush RemoteOpenFrontBrush = new( Color.FromArgb( 255, 0x56, 0xA8, 0xCF ) );

	private static readonly SolidColorBrush LocalClosedBrush = new( Color.FromArgb( 255, 0xB8, 0x94, 0x4E ) );
	private static readonly SolidColorBrush LocalHighlightBrush = new( Color.FromArgb( 255, 0xDC, 0xB7, 0x6F ) );
	private static readonly SolidColorBrush LocalOpenBackBrush = new( Color.FromArgb( 255, 0x91, 0x70, 0x36 ) );
	private static readonly SolidColorBrush LocalOpenFrontBrush = new( Color.FromArgb( 255, 0xD2, 0xAD, 0x64 ) );

	public static readonly DependencyProperty OriginProperty = DependencyProperty.Register(
		nameof( Origin ), typeof( FolderOriginKind ), typeof( FolderIcon ),
		new PropertyMetadata( FolderOriginKind.Synced, OnOriginChanged ) );

	public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
		nameof( IsOpen ), typeof( bool ), typeof( FolderIcon ),
		new PropertyMetadata( false, OnIsOpenChanged ) );

	public static readonly DependencyProperty IsRootProperty = DependencyProperty.Register(
		nameof( IsRoot ), typeof( bool ), typeof( FolderIcon ),
		new PropertyMetadata( false, OnIsRootChanged ) );

	public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
		nameof( IconSize ), typeof( double ), typeof( FolderIcon ),
		new PropertyMetadata( 18.0, OnSizeChanged ) );

	private FolderOriginKind? _appliedOrigin;

	public FolderOriginKind Origin
	{
		get => (FolderOriginKind)GetValue( OriginProperty );
		set => SetValue( OriginProperty, value );
	}

	public bool IsOpen
	{
		get => (bool)GetValue( IsOpenProperty );
		set => SetValue( IsOpenProperty, value );
	}

	public bool IsRoot
	{
		get => (bool)GetValue( IsRootProperty );
		set => SetValue( IsRootProperty, value );
	}

	public double IconSize
	{
		get => (double)GetValue( IconSizeProperty );
		set => SetValue( IconSizeProperty, value );
	}

	public FolderIcon()
	{
		InitializeComponent();
		ApplyIsOpen( false );
		ApplyIsRoot( false );
		ApplyOrigin( FolderOriginKind.Synced );
	}

	private static void OnOriginChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		=> ( (FolderIcon)d ).ApplyOrigin( (FolderOriginKind)e.NewValue );

	private static void OnIsOpenChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		=> ( (FolderIcon)d ).ApplyIsOpen( (bool)e.NewValue );

	private static void OnIsRootChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		=> ( (FolderIcon)d ).ApplyIsRoot( (bool)e.NewValue );

	private static void OnSizeChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		FolderIcon control = (FolderIcon)d;
		double size = e.NewValue is double v && v > 0 ? v : 18.0;
		control.IconBox.Width = size;
		control.IconBox.Height = size;
	}

	private void ApplyIsOpen( bool open )
	{
		ClosedGroup.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
		OpenGroup.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
	}

	private void ApplyIsRoot( bool root )
		=> RootAccent.Visibility = root ? Visibility.Visible : Visibility.Collapsed;

	private void ApplyOrigin( FolderOriginKind origin )
	{
		if( _appliedOrigin == origin )
			return;
		_appliedOrigin = origin;

		LocalBadge.Visibility = origin == FolderOriginKind.LocalOnly ? Visibility.Visible : Visibility.Collapsed;
		RemoteBadge.Visibility = origin == FolderOriginKind.RemoteOnly ? Visibility.Visible : Visibility.Collapsed;

		var (closed, highlight, openBack, openFront) = origin switch
		{
			FolderOriginKind.RemoteOnly => ( RemoteClosedBrush, RemoteHighlightBrush, RemoteOpenBackBrush, RemoteOpenFrontBrush ),
			FolderOriginKind.LocalOnly => ( LocalClosedBrush, LocalHighlightBrush, LocalOpenBackBrush, LocalOpenFrontBrush ),
			_ => ( SyncedClosedBrush, SyncedHighlightBrush, SyncedOpenBackBrush, SyncedOpenFrontBrush ),
		};

		ClosedFolderBody.Fill = closed;
		ClosedFolderHighlight.Fill = highlight;
		OpenFolderBack.Fill = openBack;
		OpenFolderFront.Fill = openFront;
	}
}
