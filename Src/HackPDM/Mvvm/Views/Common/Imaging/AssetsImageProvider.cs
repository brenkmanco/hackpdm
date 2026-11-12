using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HackPDM.Mvvm.Views.Common.Imaging;

/// <summary>Registers image assets (key to URI) in one map shared by the whole app.</summary>
public class AssetsImageProvider
{
	internal static readonly Dictionary<string, string> AssetMap;
	// one decoded image per key, shared by every row that shows it (about 20 distinct icons)
	/// <summary>Creates a provider without registering any assets.</summary>
	public AssetsImageProvider() : this([]) {}
	/// <summary>Creates a provider and adds the given assets to the shared map.</summary>
	/// <param name="assetMap">Image key to URI; keys already registered keep their first URI.</param>
	public AssetsImageProvider(Dictionary<string, string>? assetMap)
	{
		if (assetMap is not { Count: > 0 }) return;
		foreach (var item in assetMap)
		{
			if (!AssetMap.ContainsKey(item.Key)) AssetMap.Add(item.Key, item.Value);
		}
	}
	static AssetsImageProvider()
	{
		AssetMap = new();
	}
	/// <summary>Lists the registered image keys.</summary>
	/// <returns>Every key in the shared map (a live view, not a copy).</returns>
	public IEnumerable<string> GetAvailableKeys() => AssetMap.Keys;

}