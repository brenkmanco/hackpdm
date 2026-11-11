using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Helpers;
using HackPDM.Shared.GlobalData;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.Mvvm.Views.Dialogs;

/// <summary>
/// The app's message box: the message above, a row of buttons below, in its own window. Use it
/// through <see cref="IDialogService"/>. When the window can't be built it falls back to the
/// native Windows message box.
/// </summary>
public sealed partial class MessageBox : Page
{
	private const int ButtonRow = 3;

	/// <param name="Column">Grid column (0-3) on the button row.</param>
	/// <param name="IsDefault">The highlighted button.</param>
	private sealed record ButtonSpec( string Label, DialogResult Result, int Column, bool IsDefault = false );

	/// <summary>The buttons each <see cref="MessageBoxButtons"/> shows, left to right as placed.</summary>
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

	/// <summary>The native box's default-button flag (MB_DEFBUTTONn) for the fallback.</summary>
	private static readonly Dictionary<MessageBoxButtons, MessageBoxRepresentation> NativeDefaultButton = new()
	{
		[ MessageBoxButtons.OKCancel ] = MessageBoxRepresentation.Secondary,
		[ MessageBoxButtons.YesNoCancel ] = MessageBoxRepresentation.Tertiary,
		[ MessageBoxButtons.YesNo ] = MessageBoxRepresentation.Secondary,
		[ MessageBoxButtons.RetryCancel ] = MessageBoxRepresentation.Secondary,
	};

	private TaskCompletionSource<DialogResult> _result = new();
	private Window? _window;

	public MessageBox()
	{
		InitializeComponent();
	}

	/// <summary>Fills the page and completes <paramref name="result"/> (and closes <paramref name="window"/>) on a button press.</summary>
	private void Show( string message, MessageBoxButtons buttons, Window window, TaskCompletionSource<DialogResult> result )
	{
		_window = window;
		_result = result;
		gridRoot.Margin = new Thickness( 15, 15, 15, 5 );
		gridRoot.UseLayoutRounding = true;
		gridRoot.CornerRadius = new CornerRadius( 10 );

		ScrollView text = new()
		{
			Content = new TextBlock
			{
				Text = $"- {message}",
				FontSize = 15,
				FontFamily = new FontFamily( "Calibri" ),
				TextAlignment = TextAlignment.DetectFromContent,
				TextWrapping = TextWrapping.WrapWholeWords,
			},
		};
		gridRoot.Children.Add( text );
		text.SetGrid( 0, 0, 3, 4 );

		foreach( ButtonSpec spec in Layouts.GetValueOrDefault( buttons ) ?? Layouts[ MessageBoxButtons.OK ] )
		{
			Button button = new()
			{
				Content = new TextBlock { Text = spec.Label },
				Style = (Style)Resources[ spec.IsDefault ? "DarkButtonActiveStyle1" : "DarkButtonStyle1" ],
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				Height = 30,
			};
			button.Click += ( _, _ ) => Close( spec.Result );
			gridRoot.Children.Add( button );
			button.SetGrid( ButtonRow, spec.Column );
		}
	}

	private void Close( DialogResult result )
	{
		_result.TrySetResult( result );
		_window?.Close();
	}

	internal static Task<DialogResult> ShowAsync( string message )
		=> ShowAsync( message, null, MessageBoxButtons.OK, MessageBoxIcon.Information );

	internal static async Task<DialogResult> ShowAsync( string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon )
	{
		var result = new TaskCompletionSource<DialogResult>( TaskCreationOptions.RunContinuationsAsynchronously );
		try
		{
			// awaited, so a failure building the window reaches the native fallback below instead of
			// leaving the caller waiting forever
			await UiDispatcher.Current.RunAsync( () =>
			{
				MessageBox page = new();
				Window window = WindowHelper.CreateWindow<Window>( "MessageBox" );
				( window.Content as Frame )?.Content = page;
				page.Show( message, buttons, window, result );
			} );
		}
		catch( Exception )
		{
			result.TrySetResult( ShowNative( message, caption ?? "Information",
				// bitmask for the native box: buttons | icon | default button | modality
				(uint)buttons
				| (uint)icon
				| (uint)NativeDefaultButton.GetValueOrDefault( buttons, MessageBoxRepresentation.Primary )
				| (uint)MessageBoxModal.AppModal
				| (uint)MessageBoxTypeFallback.TopMost ) );
		}
		return await result.Task;
	}

	private static DialogResult ShowNative( string message, string title, uint type )
	{
#if WINDOWS
		return (DialogResult)MsgBox( HackApp.Window?.IntPtrHandle ?? 0, message, title, type );
#else
		Debug.WriteLine( $"[Native MessageBox Fallback] {title}: {message}" );
		return DialogResult.OK;
#endif
	}

#if WINDOWS
	[LibraryImport( "User32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16 )]
	private static partial int MsgBox( IntPtr hWnd, string lpText, string lpCaption, uint uType );
#endif
}
