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
using HackPDM.Core.General;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;

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
		private MessageBoxWidth _windowWidthWrap = MessageBoxWidth.DynamicWidthText;
		private object[]? _parameters;

		private MessageBoxWindow? _messageWindow;

		public ScrollView? ScrollContainer = null;
		public TextBlock? TextBlockImpl = null;
		public ListBox? ListBoxImpl = null;
		public DataGrid? DataGridImpl = null;
		public DialogResult? Result { get; private set; } = DialogResult.None;
		public MessageBox()
		{
			InitializeComponent();
		}
		public MessageBox( string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon = MessageBoxIcon.Information, MessageBoxType type = MessageBoxType.Default, MessageBoxWidth dynamicWidth = MessageBoxWidth.Default, params object[] parameters )
			=> Init( new MessageBoxDetails( Message: message, Caption: caption, Buttons: buttons, Icon: icon, Type: type, DynamicWidth: dynamicWidth, Parameters: parameters ) );

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
			if( arr is not null )
			{
				var details = arr.FirstOrDefaultSelect(p => p is MessageBoxDetails mbox ? (true, mbox) : (false, null));
				_messageWindow ??= arr.FirstOrDefaultSelect( p => p is MessageBoxWindow win ? (true, win) : (false, null) );
				if( details is not null )
				{
					Init( details );
					ShowInternal();
				}
			}
		}
		private void GeneralBoxConfig()
		{
			gridRoot.Margin = new Thickness( 15, 15, 15, 15 );
			gridRoot.UseLayoutRounding = true;
			gridRoot.CornerRadius = new CornerRadius( 10, 10, 10, 10 );
		}
		//public async Task<DialogResult> ShowAsync(string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, Window window)
		//{
		//	_message = message;
		//	_caption = caption;
		//	_button = buttons;
		//	_icon = icon;

		//	var info = AlterMessageBoxLayout(message, caption, buttons, icon);

		//	ContentDialog popup = new()
		//	{
		//		Title = info.Caption,
		//		Content = info.Message
		//	};
		//	if (info.CloseText is not null) popup.CloseButtonText = info.CloseText;
		//	if (info.PrimaryText is not null) popup.PrimaryButtonText = info.PrimaryText;
		//	if (info.SecondaryText is not null) popup.SecondaryButtonText = info.SecondaryText;

		//	Result = DialogResult.None;
		//	var result = await popup.ShowAsync();
		//	return MapContentToDialogResult(result, buttons);
		//}
		//public DialogResult Show(string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, Window window)
		//{
		//	var info = AlterMessageBoxLayout(message, caption, buttons, icon);

		//	ContentDialog popup = new()
		//	{
		//		XamlRoot = window.Content.XamlRoot,
		//		Title = info.Caption,
		//		Content = info.Message
		//	};
		//	if (info.CloseText is not null) popup.CloseButtonText = info.CloseText;
		//	if (info.PrimaryText is not null) popup.PrimaryButtonText = info.PrimaryText;
		//	if (info.SecondaryText is not null) popup.SecondaryButtonText = info.SecondaryText;
		//	var result = popup.ShowAsync().AsTask().Result;
		//	return MapContentToDialogResult(result, buttons);
		//}
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
				FontSize = 10,
				TextAlignment = TextAlignment.Justify,
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
			ScrollContainer.SetGrid( 0, 0, 4, 4 );
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
					primaryGrid = new( 3, 2, 1, 1 );
					closeGrid = new( 3, 3, 1, 1 );
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
				primary.HorizontalAlignment = HorizontalAlignment.Stretch;
				primary.VerticalAlignment = VerticalAlignment.Center;
				primary.Height = 35;
			}
			if( secondary != null )
			{
				gridRoot.Children.Add( secondary );
				if( secondaryGrid is { } g )
					secondary.SetGrid( g.x, g.y, g.z, g.w );
				secondary.HorizontalAlignment = HorizontalAlignment.Stretch;
				secondary.VerticalAlignment = VerticalAlignment.Center;
				secondary.Height = 35;
			}
			if( close != null )
			{
				gridRoot.Children.Add( close );
				if( closeGrid is { } g )
					close.SetGrid( g.x, g.y, g.z, g.w );
				close.HorizontalAlignment = HorizontalAlignment.Stretch;
				close.VerticalAlignment = VerticalAlignment.Center;
				close.Height = 35;
			}

			return (primary, secondary, close);
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
		private static ContentDialogInfo AlterMessageBoxLayout( string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon )
		{
			ContentDialogInfo popupInfo = new()
			{
				Message = message,
				Caption = caption ?? "Info",
			};

			switch( buttons )
			{
				case MessageBoxButtons.OK:
				{
					popupInfo.ButtonRepresentation = ContentDialogButtonRepresentation.Close;
					popupInfo.CloseText = "OK";
					break;
				}
					;
				case MessageBoxButtons.OKCancel:
				{
					popupInfo.ButtonRepresentation = ContentDialogButtonRepresentation.Primary;
					popupInfo.PrimaryText = "OK";
					popupInfo.CloseText = "Cancel";
					break;
				}
					;
				case MessageBoxButtons.AbortRetryIgnore:
				{
					popupInfo.ButtonRepresentation = ContentDialogButtonRepresentation.Secondary;
					popupInfo.PrimaryText = "Abort";
					popupInfo.SecondaryText = "Retry";
					popupInfo.CloseText = "Ignore";
					break;
				}
				case MessageBoxButtons.YesNoCancel:
				{
					popupInfo.ButtonRepresentation = ContentDialogButtonRepresentation.Primary;
					popupInfo.PrimaryText = "Yes";
					popupInfo.SecondaryText = "No";
					popupInfo.CloseText = "Cancel";
					break;
				}
				case MessageBoxButtons.YesNo:
				{
					popupInfo.ButtonRepresentation = ContentDialogButtonRepresentation.Primary;
					popupInfo.PrimaryText = "Yes";
					popupInfo.CloseText = "No";
					break;
				}
				case MessageBoxButtons.RetryCancel:
				{
					popupInfo.ButtonRepresentation = ContentDialogButtonRepresentation.Primary;
					popupInfo.PrimaryText = "Retry";
					popupInfo.CloseText = "Cancel";
					break;
				}
				case MessageBoxButtons.CancelTryContinue:
				{
					popupInfo.ButtonRepresentation = ContentDialogButtonRepresentation.Close;
					popupInfo.PrimaryText = "Cancel";
					popupInfo.SecondaryText = "Try";
					popupInfo.CloseText = "Continue";
					break;
				}
			}

			return popupInfo;
		}

		public static DialogResult Show( string message ) =>
			ShowInternal( message: message, caption: string.Empty, buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Information, type: MessageBoxType.Default, parameters: null);
		public static DialogResult Show( string message, string caption, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			ShowInternal( message: message, caption: caption, buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Information, type: type, parameters: parameters );
		public static DialogResult Show( string message, MessageBoxButtons buttons, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			ShowInternal( message: message, caption: null, buttons: buttons, icon: MessageBoxIcon.Information, type: type, parameters: parameters );
		public static DialogResult Show( string message, string caption, MessageBoxButtons buttons, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			ShowInternal( message: message, caption: caption, buttons: buttons, icon: MessageBoxIcon.Information, type: type, parameters: parameters);
		public static DialogResult Show( string message, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			ShowInternal( message: message, caption: null, buttons: buttons, icon: icon, type: type, parameters: parameters);
		public static DialogResult Show( string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxType type = MessageBoxType.Default, params object[]? parameters ) =>
			ShowInternal( message: message, caption: caption, buttons: buttons, icon: icon, type: type, parameters: parameters );

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
		private static async Task<DialogResult> ShowInternalAsync( string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxType type = MessageBoxType.Default, params object[]? parameters )
		{
			//MessageBoxWidth dynamicWidth = MessageBoxWidth.Default,
			WindowHelper.CreateWindowAndPage( out MessageBox page, out MessageBoxWindow window, true,
				[
					new MessageBoxDetails(
						message,
						caption,
						buttons,
						icon,
						type,
						MessageBoxWidth.Default,
						parameters )
				] );

			return DialogResult.None;
			//return await SafeHelper.SafeInvokerAsync(()=>
			//{
			//	return page.Show(message, caption, buttons, icon, window);
			//});
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
