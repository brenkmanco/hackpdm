using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Helper;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

using static HackPDM.UI.Forms.Settings.ProfileManager;

namespace HackPDM.Configuration;

// saveable settings for application
// MAIN WINDOW
// preferred size, anchored (or not)

// ODOOENTRYLIST
// columns layout, order, sort (dir and column), width, 
// group type, grouped (or not), group collapsible types,
// show inactive, ignore filter, type filter
// preferred sublist active (history, parent, child, properties, info)


public class GlobalSettings
{

	public static async void test()
	{
		var hwnd = HackApp.Window?.IntPtrHandle ?? 0;
		var windowId = HackApp.Window?.AppWindow.Id ?? Win32Interop.GetWindowIdFromWindow(hwnd);
		var picker = new FileOpenPicker(windowId);
		picker.FileTypeFilter.Add( ".json" );
		picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

		var file = await picker.PickSingleFileAsync().AsTask();



		if( string.IsNullOrEmpty( file?.Path ) )
			return;

		var savedData = JsonSerializer.Deserialize<SavedData>(await File.ReadAllTextAsync( file.Path, Encoding.UTF8 ));

		if( savedData is null )
		{
			await MessageBox.ShowAsync( "unable to deserialize json file" );
			return;
		}
	}
	public async void SaveSettingsJson_Click( object sender, RoutedEventArgs e )
	{
		var hwnd = HackApp.Window?.IntPtrHandle ?? 0;
		var windowId = HackApp.Window?.AppWindow.Id ?? Win32Interop.GetWindowIdFromWindow(hwnd);
		var picker = new FileSavePicker(windowId);
		picker.FileTypeChoices.Add( "JSON", new List<string>() { ".json" } );
		picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
		picker.SuggestedFileName = "HackPDM_Settings";
		var file = await picker.PickSaveFileAsync().AsTask();
		if( string.IsNullOrEmpty( file?.Path ) )
			return;
		//await File.WriteAllTextAsync( file.Path, JsonSerializer.Serialize( saveData ), Encoding.UTF8 );
	}
}
