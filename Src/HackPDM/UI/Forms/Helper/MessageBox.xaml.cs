using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using HackPDM.Abstractions;
using HackPDM.Core.Helper.Xaml;
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
		public DialogResult? Result { get; private set; }
		public MessageBox()
		{
			InitializeComponent();
		}
		public MessageBox(string message, string? caption, MessageBoxButtons buttons,  MessageBoxIcon icon) : this()
		{
			_message = message;
			_caption = caption;
			_button = buttons;
			_icon = icon;

			var info = AlterMessageBoxLayout(message, caption, buttons, icon);

			ContentDialog popup = new()
			{
				Title = info.Caption,
				Content = info.Message
			};
			if (info.CloseText is not null) popup.CloseButtonText = info.CloseText;
			if (info.PrimaryText is not null) popup.PrimaryButtonText = info.PrimaryText;
			if (info.SecondaryText is not null) popup.SecondaryButtonText = info.SecondaryText;

			Result = DialogResult.None;
			
			Init();
			//MessageBox.ShowAsync(_message, _caption, _button, _icon);
		}
		private async void Init()
		{
			await ShowAsync(_message, _caption, _button, _icon);
		}
		public async Task<DialogResult> ShowAsync(string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, Window window)
		{

			//var result = await popup.ShowAsync();
			//return MapContentToDialogResult(result, buttons);
			return DialogResult.None;
		}
		public DialogResult Show(string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, Window window)
		{
			var info = AlterMessageBoxLayout(message, caption, buttons, icon);
		
			ContentDialog popup = new()
			{
				XamlRoot = window.Content.XamlRoot,
				Title = info.Caption,
				Content = info.Message
			};
			if (info.CloseText is not null) popup.CloseButtonText = info.CloseText;
			if (info.PrimaryText is not null) popup.PrimaryButtonText = info.PrimaryText;
			if (info.SecondaryText is not null) popup.SecondaryButtonText = info.SecondaryText;
			var result = popup.ShowAsync().AsTask().Result;
			return MapContentToDialogResult(result, buttons);
		}
		private static DialogResult MapContentToDialogResult(ContentDialogResult result, MessageBoxButtons buttons) =>
		(buttons: buttons, result) switch
		{
			(MessageBoxButtons.OK, ContentDialogResult.Primary) => DialogResult.OK,
			(MessageBoxButtons.OK, ContentDialogResult.Secondary) => DialogResult.OK,
			(MessageBoxButtons.OK, ContentDialogResult.None) => DialogResult.OK,

			(MessageBoxButtons.OKCancel, ContentDialogResult.Primary) => DialogResult.OK,
			(MessageBoxButtons.OKCancel, ContentDialogResult.Secondary) => DialogResult.Cancel,
			(MessageBoxButtons.OKCancel, ContentDialogResult.None) => DialogResult.Cancel,

			(MessageBoxButtons.AbortRetryIgnore, ContentDialogResult.Primary) => DialogResult.Abort,
			(MessageBoxButtons.AbortRetryIgnore, ContentDialogResult.Secondary) => DialogResult.Retry,
			(MessageBoxButtons.AbortRetryIgnore, ContentDialogResult.None) => DialogResult.Ignore,

			(MessageBoxButtons.YesNoCancel, ContentDialogResult.Primary) => DialogResult.Yes,
			(MessageBoxButtons.YesNoCancel, ContentDialogResult.Secondary) => DialogResult.No,
			(MessageBoxButtons.YesNoCancel, ContentDialogResult.None) => DialogResult.Cancel,

			(MessageBoxButtons.YesNo, ContentDialogResult.Primary) => DialogResult.Yes,
			(MessageBoxButtons.YesNo, ContentDialogResult.Secondary) => DialogResult.Cancel,
			(MessageBoxButtons.YesNo, ContentDialogResult.None) => DialogResult.No,

			(MessageBoxButtons.RetryCancel, ContentDialogResult.Primary) => DialogResult.Retry,
			(MessageBoxButtons.RetryCancel, ContentDialogResult.Secondary) => DialogResult.Cancel,
			(MessageBoxButtons.RetryCancel, ContentDialogResult.None) => DialogResult.Cancel,

			(MessageBoxButtons.CancelTryContinue, ContentDialogResult.Primary) => DialogResult.Cancel,
			(MessageBoxButtons.CancelTryContinue, ContentDialogResult.Secondary) => DialogResult.TryAgain,
			(MessageBoxButtons.CancelTryContinue, ContentDialogResult.None) => DialogResult.Continue,

			_ => DialogResult.Cancel
		};
		private static ContentDialogInfo AlterMessageBoxLayout(string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon)
		{
			ContentDialogInfo popupInfo = new()
			{
				Message = message,
				Caption = caption ?? "Info",
			};

			switch (buttons)
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

		public static DialogResult Show(string message) =>
		ShowInternal(message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
		public static DialogResult Show(string message, string caption) =>
			ShowInternal(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
		public static DialogResult Show(string message, MessageBoxButtons buttons) =>
			ShowInternal(message, null, buttons, MessageBoxIcon.Information);
		public static DialogResult Show(string message, string caption, MessageBoxButtons buttons) =>
			ShowInternal(message, caption, buttons, MessageBoxIcon.Information);
		public static DialogResult Show(string message, MessageBoxButtons buttons, MessageBoxIcon icon) =>
			ShowInternal(message, null, buttons, icon);
		public static DialogResult Show(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
			ShowInternal(message, caption, buttons, icon);

		public static async Task<DialogResult> ShowAsync(string message) =>
			await ShowInternalAsync(message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
		public static async Task<DialogResult> ShowAsync(string message, string caption) =>
			await ShowInternalAsync(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
		public static async Task<DialogResult> ShowAsync(string message, MessageBoxButtons buttons) =>
			await ShowInternalAsync(message, null, buttons, MessageBoxIcon.Information);
		public static async Task<DialogResult> ShowAsync(string message, string caption, MessageBoxButtons buttons) =>
			await ShowInternalAsync(message, caption, buttons, MessageBoxIcon.Information);
		public static async Task<DialogResult> ShowAsync(string message, MessageBoxButtons buttons, MessageBoxIcon icon) =>
			await ShowInternalAsync(message, null, buttons, icon);
		public static async Task<DialogResult> ShowAsync(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
			await ShowInternalAsync(message, caption, buttons, icon);


		private static DialogResult ShowInternal(string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
			ShowInternalAsync(message, caption, buttons, icon).Result;
		private static async Task<DialogResult> ShowInternalAsync(string message, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon)
		{
			return await SafeHelper.SafeInvokerAsync(()=>
			{
				WindowHelper.CreateWindowAndPage(out MessageBox page, out var window);
				return page.Show(message, caption, buttons, icon, window);
			});
		}
	}
}
