using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

using HackPDM.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.Src.ClientUtils.Types;

public interface IConvert<T>
{
	T ConvertFromHt(Hashtable ht);
}
public interface ICloneable<T>
{
	T Clone();
}
public interface IRowData<T> : ICloneable<T>
{
	
}
public interface ITreeItem
{
	public object? Tag { get; set; }
	public IEnumerable<TreeData>? Children { get; set; }
}
public interface IListItem<T>
{
	public T? Value { get; }
	public bool IsSelected { get; set; }
	public ListViewItem Item { get; set; }
}
public interface ISettingsProvider
{
	T? Get<T>(string key, T? defaultValue = default);
	void Set<T>(string key, T value);
}
public interface IImageProvider
{
	ImageSource? GetImage(string key);
	Task<ImageSource?> GetImageAsync(string key);
	Bitmap? GetBitmap(string key);
	void SetImage(string key, byte[] imgBytes);
	IEnumerable<string> GetAvailableKeys();
}
public interface IItemChangeListener<T>
{
	void OnItemAdded(object sender, ItemChangedEventArgs<T> e);
	void OnItemRemoved(object sender, ItemChangedEventArgs<T> e);
	void OnItemUpdated(object sender, ItemChangedEventArgs<T> e);
	void OnItemSelected(object sender, ItemChangedEventArgs<T> e);
	void OnItemClicked(object sender, ItemChangedEventArgs<T> e);
	void OnItemDoubleClicked(object sender, ItemChangedEventArgs<T> e);
	void OnItemRendering(object sender, ItemChangedEventArgs<T> e);
	void OnItemFocused(object sender, ItemChangedEventArgs<T> e);
	void OnItemHovered(object sender, ItemChangedEventArgs<T> e);
}