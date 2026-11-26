using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Common;

/// <summary>
/// One destination of the shell's navigation rail (Penpot: Navigation rail): an icon in a 56×32 indicator pill
/// with a label under it. Its look is HackNavRailItemStyle in HackControls.xaml; the selected item's pill fills.
/// </summary>
public sealed partial class NavRailItem : Button
{
	public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
		nameof( Glyph ), typeof( string ), typeof( NavRailItem ), new PropertyMetadata( string.Empty ) );

	public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
		nameof( Label ), typeof( string ), typeof( NavRailItem ), new PropertyMetadata( string.Empty ) );

	public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
		nameof( IsSelected ), typeof( bool ), typeof( NavRailItem ),
		new PropertyMetadata( false, ( d, _ ) => ( (NavRailItem)d ).UpdateSelectionState( true ) ) );

	public string Glyph
	{
		get => (string)GetValue( GlyphProperty );
		set => SetValue( GlyphProperty, value );
	}

	public string Label
	{
		get => (string)GetValue( LabelProperty );
		set => SetValue( LabelProperty, value );
	}

	public bool IsSelected
	{
		get => (bool)GetValue( IsSelectedProperty );
		set => SetValue( IsSelectedProperty, value );
	}

	protected override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		UpdateSelectionState( false );
	}

	private void UpdateSelectionState( bool useTransitions )
		=> VisualStateManager.GoToState( this, IsSelected ? "Selected" : "Unselected", useTransitions );
}
