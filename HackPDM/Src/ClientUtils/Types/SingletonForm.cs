using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using HackPDM.Extensions.Controls;
using HackPDM.Helper;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Src.ClientUtils.Types;

public interface ISingletonForm<T>
    where T : Form, new()
{
    public static abstract T? Singleton { get; set; }   
}
public interface ISingletonPage<T>
	where T : Page, new()
{
	public static T? Singleton 
	{ 
		get => InstanceManager.TryGet<T>(out var instance) 
			? instance 
			: null;
	}
	public static Window? ParentWindow
	{
		get => InstanceManager.TryGet<T, Window>(out _, out var window)
			? window
			: null;
	}
	public static bool GetOrAddSingleton(out T instance, bool activated = true)
	{
		if (Singleton is T single)
		{
			instance = single;
			return true;
		}
		else
		{
			WindowHelper.CreateWindowAndPage(out T page, out _, activated);
			instance = page;
			return false;
		}
	}
}