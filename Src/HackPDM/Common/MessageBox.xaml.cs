using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Common;

public sealed partial class MessageBox : Page
{
	private const int ButtonRow = 3;

	private sealed record ButtonSpec( string Label, DialogResult Result, int Column, bool IsDefault = false );

	private static readonly Dictionary<MessageBoxButtons, ButtonSpec[]> Layouts = new()
	{
		[ MessageBoxButtons.OK ] = [ new( "OK", DialogResult.OK, 3, true ) ],
		[ MessageBoxButtons.OKCancel ] = [ new( "Cancel", DialogResult.Cancel, 2 ), new( "OK", DialogResult.OK, 3, true ) ],
		[ MessageBoxButtons.AbortRetryIgnore ] = [ new( "Abort", DialogResult.Abort, 0 ), new( "Retry", DialogResult.Retry, 2, true ), new( "Ignore", DialogResult.Ignore, 3 ) ],
		[ MessageBoxButtons.YesNoCancel ] = [ new( "Cancel", DialogResult.Cancel, 0 ), new( "No", DialogResult.No, 2 ), new( "Yes", DialogResult.Yes, 3, true ) ],
		[ MessageBoxButtons.YesNo ] = [ new( "No", DialogResult.No, 2 ), new( "Yes", DialogResult.Yes, 3, true ) ],
		[ MessageBoxButtons.RetryCancel ] = [ new( "Cancel", DialogResult.Cancel, 2 ), new( "Retry", DialogResult.Retry, 3, true ) ],
		[ MessageBoxButtons.CancelTryContinue ] = [ new( "Cancel", DialogResult.Cancel, 0 ), new( "Try", DialogResult.TryAgain, 2 ), new( "Continue", DialogResult.Continue, 3, true ) ],
	};

	private TaskCompletionSource<DialogResult> _result = new();
	private Window? _window;

	public MessageBox()
	{
		InitializeComponent();
	}

	private void Populate( string message, MessageBoxButtons buttons, Window window, TaskCompletionSource<DialogResult> result )
	{
		_window = window;
		_result = result;

		ScrollView text = new()
		{
			Content = new TextBlock
			{
				Text = message,
				FontSize = 14,
				TextWrapping = TextWrapping.WrapWholeWords,
				IsTextSelectionEnabled = true,
			},
		};
		Grid.SetRow( text, 0 );
		Grid.SetColumn( text, 0 );
		Grid.SetRowSpan( text, 3 );
		Grid.SetColumnSpan( text, 4 );
		RootGrid.Children.Add( text );

		ButtonSpec[] specs = Layouts.GetValueOrDefault( buttons ) ?? Layouts[ MessageBoxButtons.OK ];
		ButtonSpec defaultSpec = Array.Find( specs, s => s.IsDefault ) ?? specs[ ^1 ];
		foreach( ButtonSpec spec in specs )
		{
			Button button = new()
			{
				Content = spec.Label,
				Style = (Style)Resources[ spec.IsDefault ? "ChoiceDefaultButtonStyle" : "ChoiceButtonStyle" ],
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				Height = 32,
			};
			button.Click += ( _, _ ) => Close( spec.Result );
			Grid.SetRow( button, ButtonRow );
			Grid.SetColumn( button, spec.Column );
			RootGrid.Children.Add( button );
		}

		window.Closed += ( _, _ ) => result.TrySetResult( DialogResult.Cancel );
		KeyDown += ( _, e ) =>
		{
			if( e.Key == Windows.System.VirtualKey.Escape )
			{
				e.Handled = true;
				Close( DialogResult.Cancel );
			}
			else if( e.Key == Windows.System.VirtualKey.Enter )
			{
				e.Handled = true;
				Close( defaultSpec.Result );
			}
		};
	}

	private void Close( DialogResult result )
	{
		_result.TrySetResult( result );
		_window?.Close();
	}

	internal static async Task<DialogResult> ShowAsync( string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon )
	{
		TaskCompletionSource<DialogResult> result = new( TaskCreationOptions.RunContinuationsAsynchronously );
		try
		{
			await UiDispatcher.Current.RunAsync( () =>
			{
				MessageBox page = new();
				Window window = WindowHelper.CreateWindow<Window>( "MessageBox" );
				window.Title = string.IsNullOrWhiteSpace( caption ) ? "Info" : caption;
				( window.Content as Frame )?.Content = page;
				page.Populate( message, buttons, window, result );
			} );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "MessageBox", $"XAML MessageBox failed; falling back to native dialog: {ex.Message}", ex );
			result.TrySetResult( ShowNative( message, caption ?? "Information", (uint)buttons | (uint)icon | 0x00040000u ) );
		}
		return await result.Task;
	}

	private static DialogResult ShowNative( string message, string title, uint type )
	{
#if WINDOWS
		return (DialogResult)MsgBox( HackApp.Window?.IntPtrHandle ?? 0, message, title, type );
#else
		HackLogger.Info( "MessageBox", $"[Native MessageBox Fallback] {title}: {message}" );
		return DialogResult.OK;
#endif
	}

#if WINDOWS
	[LibraryImport( "User32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16 )]
	private static partial int MsgBox( IntPtr hWnd, string lpText, string lpCaption, uint uType );
#endif
}
