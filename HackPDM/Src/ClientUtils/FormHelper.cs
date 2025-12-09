using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.ClientUtils;

internal static class FormHelper
{
    internal static async Task ExecuteUI(this DispatcherQueue dispatcher, Func<Task> function)
    {
        if (dispatcher.HasThreadAccess)
        {
            await function();
        }
        else
        {
            dispatcher.TryEnqueue(async void ()=>
            {
                try
                {
                    await function();
                }
                catch (Exception e)
                {
                    Debug.Fail(e.Message, e.StackTrace);
                }
            });
        }
    }
    internal static void ExecuteUI(this DispatcherQueue dispatcher, Action function)
    {
        if (dispatcher.HasThreadAccess)
        {
            function();
        }
        else
        {
            dispatcher.TryEnqueue(()=>function());
        }
    }
	static ScrollViewer? GetScrollViewer(DependencyObject parent)
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			if (child is ScrollViewer sv)
				return sv;

			var result = GetScrollViewer(child);
			if (result != null)
				return result;
		}
		return null;
	}

}