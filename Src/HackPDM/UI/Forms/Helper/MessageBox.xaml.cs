using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Abstractions;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Hack;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

using AliasColumns = System.Collections.Generic.IEnumerable<CommunityToolkit.WinUI.UI.Controls.DataGridColumn>;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Helper
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MessageBox : Page
	{
		private string? _message;
		private string? _caption;
		private MessageBoxButtons _button;
		private MessageBoxIcon _icon;
		private MessageBoxType _type;
		private ContentDialogButtonRepresentation focusButton;
		private MessageBoxWidth _windowWidthWrap = MessageBoxWidth.DynamicWidthTextWithThreshold;
		private object[]? _parameters;

		public TaskCompletionSource<DialogResult> TCS { get; private set; }
		public DialogResult? Result { get; private set; } = DialogResult.None;
		
		private Window? _messageWindow;

		public ScrollView? ScrollContainer = null;
		public TextBlock? TextBlockImpl = null;
		public ListBox? ListBoxImpl = null;
		public DataGrid? DataGridImpl = null;
		public MessageBox()
		{
			InitializeComponent();
		}
		public MessageBox(ArrayList parameters) => ParseParameters(parameters);
		private void ParseParameters(ArrayList parameters)
		{
			if (parameters is not null)
			{
				var details = parameters.FirstOrDefaultSelect(p => p is MessageBoxDetails mbox ? (true, mbox) : (false, null));
				_messageWindow ??= parameters.FirstOrDefaultSelect(p => p is Window win ? (true, win) : (false, null));
				TCS = parameters.FirstOrDefaultSelect(p => p is TaskCompletionSource<DialogResult> tcs ? (true, tcs) : (false, null)) ?? new();
				if (details is not null)
				{
					Init(details);
					ShowInternal();
				}
			}
		}
		public void Init( MessageBoxDetails? details )
		{
			if( details is null )
				return;

			_message = details.Message;
			_caption = details.Caption;
			_button = details.Buttons;
			_icon = details.Icon;
			_type = details.Type;
			_parameters = details.Parameters;
		}
		private void ShowInternal()
		{
			GeneralBoxConfig();
			ApplyPageConfig();
#if DEBUG
			// TestConfig();
#endif
		}
		private void TestConfig()
		{
			for( int i = 0; i < 4; i++ )
			{
				for( int j = 0; j < 4; j++ )
				{
					if( ( i == 3 && j == 3 ) || ( i == 0 && j == 0 ) )
						continue;

					TextBlock newBlock = new()
					{
						Text = $"(row: {i}, col: {j})",
						TextAlignment = TextAlignment.Justify,
						FontSize = 12,
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center
					};
					gridRoot.Children.Add( newBlock );
					newBlock.SetGrid( i, j, 1, 1 );
				}
			}
		}
		protected override void OnNavigatedTo( NavigationEventArgs e )
		{
			base.OnNavigatedTo( e );
			var arr = ( ( ArrayList )e.Parameter );
			ParseParameters(arr);
		}
		private void GeneralBoxConfig()
		{
			gridRoot.Margin = new Thickness( 15, 15, 15, 5 );
			gridRoot.UseLayoutRounding = true;
			gridRoot.CornerRadius = new CornerRadius( 10, 10, 10, 10 );
		}
		private void ApplyPageConfig()
			=> ( _type switch
			{
				MessageBoxType.ListDetail => ( Action )ListImpl,
				MessageBoxType.GridDetail => GridImpl,
				MessageBoxType.ContentDetail => ContentImpl,
				MessageBoxType.ToolTip => ToolTipImpl,
				MessageBoxType.Notification => NotifyImpl,
				MessageBoxType.Default => DefaultImpl,
				_ => DefaultImpl,
			} )();

		// buttons on bottom / text box on top
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X																	X
		//		X																	X
		//		X																	X
		//		X							TEXTBOX									X
		//		X																	X
		//		X																	X
		//		X																	X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X                 			Buttons									X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		private void DefaultImpl()
		{
			ScrollContainer = new();
			
			TextBlockImpl = new()
			{
				FontSize = 15,
				TextAlignment = TextAlignment.DetectFromContent,
				FontFamily = new FontFamily("Calibri"),
			};
			switch( _windowWidthWrap )
			{
				case MessageBoxWidth.DynamicWidthText:
				{
					var strs = $"- {_message}".SplitBy( ( str, ch ) => ch is '\n');
					TextBlockImpl.Text = string.Join( "\n", strs );

					break;
				}
				case MessageBoxWidth.DynamicWidthTextWithThreshold:
				{
					var strs = $"- {_message}".SplitBy( ( str, ch ) => ch is '\n' || str.Length >= 60);
					TextBlockImpl.Text = string.Join( "\n", strs );

					break;
				}
				default:
				case MessageBoxWidth.Default:
					TextBlockImpl.TextWrapping = TextWrapping.WrapWholeWords;
					TextBlockImpl.Text = $"- {_message}";
					break;
			}

			ScrollContainer.Content = TextBlockImpl;
			gridRoot.Children.Add( ScrollContainer );
			ScrollContainer.SetGrid( 0, 0, 3, 4 );

			var (primary, secondary, close) = DefaultButtons();
		}
		// buttons on bottom / list above
		//		
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X							TEXTBOX									X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X																	X
		//		X																	X
		//		X							LISTBOX									X
		//		X																	X
		//		X																	X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X                 			Buttons									X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		private void ListImpl()
		{
			ScrollContainer = new();
			TextBlockImpl = new()
			{
				Text = _message,
			};

			gridRoot.Children.Add( TextBlockImpl );
			TextBlockImpl.SetGrid( 0, 0, 1, 4 );
			ListBoxImpl = new()
			{
				ItemsSource = _parameters,
			};
			ScrollContainer.Content = ListBoxImpl;
			gridRoot.Children.Add( ScrollContainer );
			ScrollContainer.SetGrid( 1, 0, 2, 4 );
			var (primary, secondary, close) = DefaultButtons();
		}
		// buttons on bottom / grid above
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X							TEXTBOX									X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X																	X
		//		X																	X
		//		X							DATAGRID								X
		//		X																	X
		//		X																	X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X                 			Buttons									X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		private void GridImpl()
		{
			ScrollContainer = new();
			TextBlockImpl = new()
			{
				Text = _message,
			};
			gridRoot.Children.Add( TextBlockImpl );
			TextBlockImpl.SetGrid( 0, 0, 1, 4 );

			(var template, IEnumerable<object?> data) =
				 ( (AliasColumns template, IEnumerable<object?> data) )
				 ( _parameters?.SegmentSelectDiffWhere<object, DataGridColumn?, object?>(
					( p, index ) => p is DataGridColumn g ? (true, g, null) : (false, null, p), true ) )!;

			DataGridImpl = new()
			{
				AutoGenerateColumns = false,
				IsReadOnly = false,
				CanUserReorderColumns = false,
				CanUserSortColumns = false,
				RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed,
				Margin = new Thickness( 0.5, 0.5, 0.5, 0.5 ),
				ItemsSource = data,
			};
			DataGridImpl.Columns.AddRange( template );

			ScrollContainer.Content = DataGridImpl;
			gridRoot.Children.Add( ScrollContainer );
			ScrollContainer.SetGrid( 1, 0, 2, 4 );
			var (primary, secondary, close) = DefaultButtons();
		}
		private void ContentImpl()
		{
			ScrollContainer = new();
			TextBlockImpl = new()
			{
				Text = _message,
			};
			gridRoot.Children.Add( TextBlockImpl );
			TextBlockImpl.SetGrid( 0, 0, 1, 4 );
			ScrollContainer.Content = _parameters?.FirstOrDefaultSelect( p => p is UIElement ui ? (true, ui) : (false, null) );

			gridRoot.Children.Add( ScrollContainer );
			ScrollContainer.SetGrid( 1, 0, 2, 4 );
			var (primary, secondary, close) = DefaultButtons();
		}
		// close button top right / text box below
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X															close	X
		//		X																	X
		//		X																	X
		//		X							TEXTBOX									X
		//		X																	X
		//		X																	X
		//		X																	X
		//		X																	X
		//		X                 													X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		private void ToolTipImpl()
		{
			// var (primary, secondary, close) = DefaultButtons();
		}
		// close button top right / text box below / placement bottom right
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		//		X															close	X
		//		X																	X
		//		X																	X
		//		X							TEXTBOX									X
		//		X																	X
		//		X																	X
		//		X																	X
		//		X																	X
		//		X                 													X
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		private void NotifyImpl()
		{
			//var (primary, secondary, close) = DefaultButtons();
		}
		private (Button? primary, Button? secondary, Button? close) DefaultButtons()
		{
			Button? primary = null;
			Button? secondary = null;
			Button? close = null;

			Vector4<int>? primaryGrid = null;
			Vector4<int>? secondaryGrid = null;
			Vector4<int>? closeGrid = null;

			switch( _button )
			{
				case MessageBoxButtons.OK:
				{
					close = new()
					{
						Content = new TextBlock() { Text = "OK" },
					};
					closeGrid = new( 3, 3, 1, 1 );
					focusButton = ContentDialogButtonRepresentation.Close;
					break;
				}
				case MessageBoxButtons.OKCancel:
				{
					primary = new()
					{
						Content = new TextBlock() { Text = "OK" }
					};
					close = new()
					{
						Content = new TextBlock() { Text = "Cancel" }
					};
					primaryGrid = new( 3, 3, 1, 1 );
					closeGrid = new( 3, 2, 1, 1 );
					focusButton = ContentDialogButtonRepresentation.Primary;
					break;
				}
				case MessageBoxButtons.AbortRetryIgnore:
				{
					primary = new()
					{
						Content = new TextBlock() { Text = "Abort" }
					};
					secondary = new()
					{
						Content = new TextBlock() { Text = "Retry" }
					};
					close = new()
					{
						Content = new TextBlock() { Text = "Ignore" }
					};
					primaryGrid = new( 3, 0, 1, 1 );
					secondaryGrid = new( 3, 2, 1, 1 );
					closeGrid = new( 3, 3, 1, 1 );
					focusButton = ContentDialogButtonRepresentation.Secondary;
					break;
				}
				case MessageBoxButtons.YesNoCancel:
				{
					primary = new()
					{
						Content = new TextBlock() { Text = "Yes" }
					};
					secondary = new()
					{
						Content = new TextBlock() { Text = "No" }
					};
					close = new()
					{
						Content = new TextBlock() { Text = "Cancel" }
					};
					primaryGrid = new( 3, 3, 1, 1 );
					secondaryGrid = new( 3, 2, 1, 1 );
					closeGrid = new( 3, 0, 1, 1 );
					focusButton = ContentDialogButtonRepresentation.Primary;
					break;
				}
				case MessageBoxButtons.YesNo:
				{
					primary = new()
					{
						Content = new TextBlock() { Text = "Yes" }
					};
					close = new()
					{
						Content = new TextBlock() { Text = "No" }
					};
					primaryGrid = new( 3, 3, 1, 1 );
					closeGrid = new( 3, 2, 1, 1 );
					focusButton = ContentDialogButtonRepresentation.Primary;
					break;
				}
				case MessageBoxButtons.RetryCancel:
				{
					primary = new()
					{
						Content = new TextBlock() { Text = "Retry" }
					};
					close = new()
					{
						Content = new TextBlock() { Text = "Cancel" }
					};
					primaryGrid = new( 3, 3, 1, 1 );
					closeGrid = new( 3, 2, 1, 1 );
					focusButton = ContentDialogButtonRepresentation.Primary;
					break;
				}
				case MessageBoxButtons.CancelTryContinue:
				{
					primary = new()
					{
						Content = new TextBlock() { Text = "Cancel" }
					};
					secondary = new()
					{
						Content = new TextBlock() { Text = "Try" }
					};
					close = new()
					{
						Content = new TextBlock() { Text = "Continue" }
					};
					primaryGrid = new( 3, 0, 1, 1 );
					secondaryGrid = new( 3, 2, 1, 1 );
					closeGrid = new( 3, 3, 1, 1 );
					focusButton = ContentDialogButtonRepresentation.Close;
					break;
				}
			}

			if( primary != null )
			{
				gridRoot.Children.Add( primary );
				if( primaryGrid is { } g )
					primary.SetGrid( g.x, g.y, g.z, g.w );
				primary.Style = (Style)this.Resources[focusButton is ContentDialogButtonRepresentation.Primary 
					? "DarkButtonActiveStyle1" 
					: "DarkButtonStyle1"];
				primary.HorizontalAlignment = HorizontalAlignment.Stretch;
				primary.VerticalAlignment = VerticalAlignment.Center;
				primary.Height = 30;
				primary.Click += PrimaryPress;
			}
			if( secondary != null )
			{
				gridRoot.Children.Add( secondary );
				if( secondaryGrid is { } g )
					secondary.SetGrid( g.x, g.y, g.z, g.w );
				secondary.Style = (Style)this.Resources[focusButton is ContentDialogButtonRepresentation.Secondary
					? "DarkButtonActiveStyle1"
					: "DarkButtonStyle1"];
				secondary.HorizontalAlignment = HorizontalAlignment.Stretch;
				secondary.VerticalAlignment = VerticalAlignment.Center;
				secondary.Height = 30;
				secondary.Click += SecondaryPress;
			}
			if( close != null )
			{
				gridRoot.Children.Add( close );
				if( closeGrid is { } g )
					close.SetGrid( g.x, g.y, g.z, g.w );
				close.Style = (Style)this.Resources[focusButton is ContentDialogButtonRepresentation.Close
					? "DarkButtonActiveStyle1"
					: "DarkButtonStyle1"];
				close.HorizontalAlignment = HorizontalAlignment.Stretch;
				close.VerticalAlignment = VerticalAlignment.Center;
				close.Height = 30;
				close.Click += ClosePress;
			}

			return (primary, secondary, close);
		}

		private void PrimaryPress(object sender, RoutedEventArgs e) => SetToClose(ContentDialogResult.Primary);
		private void SecondaryPress(object sender, RoutedEventArgs e) => SetToClose(ContentDialogResult.Secondary);
		private void ClosePress(object sender, RoutedEventArgs e) => SetToClose(ContentDialogResult.None);
		private void SetToClose(ContentDialogResult buttonPress)
		{
			Result = MapContentToDialogResult(buttonPress, _button);
			TCS.TrySetResult(Result ?? DialogResult.None);
			_messageWindow?.Close();
		}
		private static DialogResult MapContentToDialogResult( ContentDialogResult result, MessageBoxButtons buttons ) =>
		(buttons: buttons, result) switch
		{
			(MessageBoxButtons.OK, ContentDialogResult.Primary ) => DialogResult.OK,
			(MessageBoxButtons.OK, ContentDialogResult.Secondary ) => DialogResult.OK,
			(MessageBoxButtons.OK, ContentDialogResult.None ) => DialogResult.OK,

			(MessageBoxButtons.OKCancel, ContentDialogResult.Primary ) => DialogResult.OK,
			(MessageBoxButtons.OKCancel, ContentDialogResult.Secondary ) => DialogResult.Cancel,
			(MessageBoxButtons.OKCancel, ContentDialogResult.None ) => DialogResult.Cancel,

			(MessageBoxButtons.AbortRetryIgnore, ContentDialogResult.Primary ) => DialogResult.Abort,
			(MessageBoxButtons.AbortRetryIgnore, ContentDialogResult.Secondary ) => DialogResult.Retry,
			(MessageBoxButtons.AbortRetryIgnore, ContentDialogResult.None ) => DialogResult.Ignore,

			(MessageBoxButtons.YesNoCancel, ContentDialogResult.Primary ) => DialogResult.Yes,
			(MessageBoxButtons.YesNoCancel, ContentDialogResult.Secondary ) => DialogResult.No,
			(MessageBoxButtons.YesNoCancel, ContentDialogResult.None ) => DialogResult.Cancel,

			(MessageBoxButtons.YesNo, ContentDialogResult.Primary ) => DialogResult.Yes,
			(MessageBoxButtons.YesNo, ContentDialogResult.Secondary ) => DialogResult.Cancel,
			(MessageBoxButtons.YesNo, ContentDialogResult.None ) => DialogResult.No,

			(MessageBoxButtons.RetryCancel, ContentDialogResult.Primary ) => DialogResult.Retry,
			(MessageBoxButtons.RetryCancel, ContentDialogResult.Secondary ) => DialogResult.Cancel,
			(MessageBoxButtons.RetryCancel, ContentDialogResult.None ) => DialogResult.Cancel,

			(MessageBoxButtons.CancelTryContinue, ContentDialogResult.Primary ) => DialogResult.Cancel,
			(MessageBoxButtons.CancelTryContinue, ContentDialogResult.Secondary ) => DialogResult.TryAgain,
			(MessageBoxButtons.CancelTryContinue, ContentDialogResult.None ) => DialogResult.Continue,

			_ => DialogResult.Cancel
		};
		

		public static async Task<DialogResult> ShowAsync( string message ) =>
			await ShowInternalAsync( message: message, caption: string.Empty, buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Information, type: MessageBoxType.Default, parameters: null );
		public static async Task<DialogResult> ShowAsync( string message, string caption, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			await ShowInternalAsync( message: message, caption: caption, buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Information, type: type, parameters: parameters );
		public static async Task<DialogResult> ShowAsync( string message, MessageBoxButtons buttons, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			await ShowInternalAsync( message: message, caption: null, buttons: buttons, icon: MessageBoxIcon.Information, type: type, parameters: parameters);
		public static async Task<DialogResult> ShowAsync( string message, string caption, MessageBoxButtons buttons, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			await ShowInternalAsync( message: message, caption: caption, buttons: buttons, icon: MessageBoxIcon.Information, type: type, parameters: parameters);
		public static async Task<DialogResult> ShowAsync( string message, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			await ShowInternalAsync( message: message, caption: null, buttons: buttons, icon: icon, type: type, parameters: parameters );
		public static async Task<DialogResult> ShowAsync( string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			await ShowInternalAsync( message: message, caption: caption, buttons: buttons, icon: icon, type: type, parameters: parameters );


		private static DialogResult ShowInternal( string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			ShowInternalAsync( message: message, caption: caption, buttons: buttons, icon: icon, type: type, parameters: parameters ).Result;
		private static Task<DialogResult> ShowInternalAsync( string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxType type = MessageBoxType.Default, params object[]? parameters )
		{
			var tcs = new TaskCompletionSource<DialogResult>();
			SafeHelper.SafeInvokerAsync(() =>
			{
				MessageBox mbox = HackApp.Services?.GetRequiredService<MessageBox>();
				
				Window window = WindowHelper.CreateWindow<Window>("MessageBox");
				(window.Content as Frame)?.Content = mbox;
				mbox?.ParseParameters([
					new MessageBoxDetails(
						message,
						caption,
						buttons,
						icon,
						type,
						MessageBoxWidth.Default,
						parameters ),
						window,
						tcs
				]);

				//WindowHelper.CreateWindowAndPage( out MessageBox page, out var window, true,
				//	[
				//		new MessageBoxDetails(
				//			message,
				//			caption,
				//			buttons,
				//			icon,
				//			type,
				//			MessageBoxWidth.Default,
				//			parameters ),
				//			tcs
				//	] );
			
			});
			return tcs.Task;
		}		
		public record MessageBoxDetails(
			string? Message,
			string? Caption,
			MessageBoxButtons Buttons,
			MessageBoxIcon Icon,
			MessageBoxType Type,
			MessageBoxWidth DynamicWidth = MessageBoxWidth.Default,
			params object[]? Parameters );
	}
}
