using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace HackPDM.Common;

internal enum NavigationSlideAxis
{
	None,
	Up,
	Down,
	Left,
	Right,
}

/// <summary>Smooth page entrance animation shared by top-level and tabbed navigation frames.</summary>
internal static class NavigationTransitions
{
	private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds( 180 );

	/// <summary>Plays a subtle fade and directional slide on <paramref name="content"/> after navigation.</summary>
	public static void Play( object? content, NavigationSlideAxis direction )
	{
		if( content is not UIElement element || direction == NavigationSlideAxis.None )
			return;

		TranslateTransform transform = new();
		element.RenderTransform = transform;

		ExponentialEase ease = new() { EasingMode = EasingMode.EaseOut, Exponent = 5 };
		Storyboard story = new();

		DoubleAnimation fade = new()
		{
			From = 0.0,
			To = 1.0,
			Duration = new Duration( Duration ),
			EasingFunction = ease,
		};
		element.Opacity = 0.0;
		Storyboard.SetTarget( fade, element );
		Storyboard.SetTargetProperty( fade, nameof( UIElement.Opacity ) );
		story.Children.Add( fade );

		(string axis, double offset) = direction switch
		{
			NavigationSlideAxis.Up => ( nameof( TranslateTransform.Y ), 18.0 ),
			NavigationSlideAxis.Down => ( nameof( TranslateTransform.Y ), -18.0 ),
			NavigationSlideAxis.Left => ( nameof( TranslateTransform.X ), 24.0 ),
			NavigationSlideAxis.Right => ( nameof( TranslateTransform.X ), -24.0 ),
			_ => ( nameof( TranslateTransform.Y ), 0.0 ),
		};

		if( offset != 0.0 )
		{
			if( axis == nameof( TranslateTransform.Y ) )
				transform.Y = offset;
			else
				transform.X = offset;

			DoubleAnimation slide = new()
			{
				From = offset,
				To = 0.0,
				Duration = new Duration( Duration ),
				EasingFunction = ease,
			};
			Storyboard.SetTarget( slide, transform );
			Storyboard.SetTargetProperty( slide, axis );
			story.Children.Add( slide );
		}

		story.Completed += ( _, _ ) =>
		{
			element.Opacity = 1.0;
			element.RenderTransform = null;
		};
		story.Begin();
	}
}
