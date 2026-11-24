#if !WINDOWS
using System;
using System.Threading;

using Microsoft.UI.Xaml.Controls;

using Uno.UI.DataBinding;

using WinUI.TableView;

namespace HackPDM.Common;

/// <summary>
/// Wraps Uno's generated <see cref="IBindableMetadataProvider"/> to supply static, zero-reflection
/// binding metadata for public framework and third-party types that lack <c>[Bindable]</c>.
/// </summary>
internal sealed class SupplementalBindableMetadataProvider : IBindableMetadataProvider
{
	private static readonly string? ToolTipServiceFullName = typeof( ToolTipService ).FullName;
	private static readonly string? TableViewFilterItemFullName = typeof( TableViewFilterItem ).FullName;

	private readonly IBindableMetadataProvider _inner;
	private IBindableType? _toolTipServiceType;
	private IBindableType? _tableViewFilterItemType;

	private SupplementalBindableMetadataProvider( IBindableMetadataProvider inner ) => _inner = inner;

	internal static void Install()
	{
		if( BindableMetadata.Provider is { } existing && existing is not SupplementalBindableMetadataProvider )
			BindableMetadata.Provider = new SupplementalBindableMetadataProvider( existing );
	}

	public IBindableType? GetBindableTypeByType( Type type )
	{
		if( type == typeof( ToolTipService ) )
			return GetOrBuild( ref _toolTipServiceType, BuildToolTipServiceType );
		if( type == typeof( TableViewFilterItem ) )
			return GetOrBuild( ref _tableViewFilterItemType, BuildTableViewFilterItemType );
		return _inner.GetBindableTypeByType( type );
	}

	public IBindableType? GetBindableTypeByFullName( string fullName )
	{
		if( string.Equals( fullName, ToolTipServiceFullName, StringComparison.Ordinal ) )
			return GetOrBuild( ref _toolTipServiceType, BuildToolTipServiceType );
		if( string.Equals( fullName, TableViewFilterItemFullName, StringComparison.Ordinal ) )
			return GetOrBuild( ref _tableViewFilterItemType, BuildTableViewFilterItemType );
		return _inner.GetBindableTypeByFullName( fullName );
	}

	private static IBindableType GetOrBuild( ref IBindableType? cache, Func<IBindableType> build )
	{
		if( Volatile.Read( ref cache ) is { } existing )
			return existing;
		Interlocked.CompareExchange( ref cache, build(), null );
		return Volatile.Read( ref cache )!;
	}

	private static BindableType BuildToolTipServiceType()
	{
		BindableType type = new( 3, typeof( ToolTipService ) );
		type.AddProperty( ToolTipService.ToolTipProperty );
		type.AddProperty( ToolTipService.PlacementProperty );
		type.AddProperty( ToolTipService.PlacementTargetProperty );
		return type;
	}

	private static BindableType BuildTableViewFilterItemType()
	{
		BindableType type = new( 5, typeof( TableViewFilterItem ) );
		type.AddProperty<bool>(
			nameof( TableViewFilterItem.IsSelected ),
			static ( instance, _ ) => ( (TableViewFilterItem)instance ).IsSelected,
			static ( instance, value, _ ) =>
			{
				if( value is bool b )
					( (TableViewFilterItem)instance ).IsSelected = b;
			} );
		type.AddProperty<object>(
			nameof( TableViewFilterItem.Value ),
			static ( instance, _ ) => ( (TableViewFilterItem)instance ).Value!,
			null );
		type.AddProperty<string>(
			nameof( TableViewFilterItem.ValueText ),
			static ( instance, _ ) => ( (TableViewFilterItem)instance ).ValueText,
			null );
		type.AddProperty<int>(
			nameof( TableViewFilterItem.Count ),
			static ( instance, _ ) => ( (TableViewFilterItem)instance ).Count,
			static ( instance, value, _ ) =>
			{
				if( value is int c )
					( (TableViewFilterItem)instance ).Count = c;
			} );
		type.AddProperty<bool>(
			nameof( TableViewFilterItem.ShowCount ),
			static ( instance, _ ) => ( (TableViewFilterItem)instance ).ShowCount,
			null );
		return type;
	}
}
#endif
