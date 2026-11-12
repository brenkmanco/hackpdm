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

public class AssetsImageProvider
{
	internal static readonly Dictionary<string, string> AssetMap;
	// one decoded image per key, shared by every row that shows it (about 20 distinct icons)
	public AssetsImageProvider() : this([]) {}
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
	public IEnumerable<string> GetAvailableKeys() => AssetMap.Keys;

}