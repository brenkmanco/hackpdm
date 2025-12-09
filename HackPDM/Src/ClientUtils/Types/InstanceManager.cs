using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using HackPDM.Extensions.General;

namespace HackPDM.Src.ClientUtils.Types
{
    public static class InstanceManager
    {
        private static readonly Dictionary<Page, Window> _window = []; 
        private static readonly Dictionary<Type, List<Page>?> _pages = [];
        public static TWin? GetAWindow<TPage, TWin>(TPage page) where TPage : Page where TWin : Window
        {
            if (page is null) return null;
			return _window.TryGetValue(page, out var window) ? (TWin)window : null;
		}
        public static void RegisterWindow<TPage, TWin>(TPage page, TWin win) where TPage : Page where TWin : Window
        {
            if (page is null || win is null) return;
            if (!_window.ContainsKey(page))
            {
                _window.Add(page, win);
            }
            Register(page);
        }
		public static T GetAPage<T>() where T : Page, new()
        {
			if (_pages.TryGetValue(typeof(T), out var page) && page?.Count > 0)
			{
				return (T)page[0];
			}

            T newPage = new();
            List<Page> newPages = [newPage];
            _pages[typeof(T)] = newPages;
            return newPage;
        }
        
		public static List<T> GetPages<T>() where T : Page, new()
		{
			if (_pages.TryGetValue(typeof(T), out var page) && page?.Count > 0)
				return [.. page.Cast<T>()];

			T newPage = new();
			List<T> newPages = [newPage];
			_pages[typeof(T)] = [.. newPages.OfType<Page>()];
			return newPages;
		}

        public static void Register<T>(T page) where T : Page
        {
            if (_pages.TryGetValue(typeof(T), out var val) && val?.Count > 0)
            {
                if (!val?.Contains(page) == false) val!.Add(page);
            }
            else
            {
                _pages[typeof(T)] = [page];
            }
            //_pages[typeof(T)] = page;
        }

        public static bool TryGet<T>(out T? page) where T : Page
        {
            if (_pages.TryGetValue(typeof(T), out var result) && result?[0] is T fpage)
            {
				page = fpage;
                return true;
            }
            page = null;
            return false;
        }
		public static bool TryGet<TPage, TWin>(out TPage? page, out TWin? win) where TPage : Page where TWin : Window
		{
			if (_pages.TryGetValue(typeof(TPage), out var result) && result?[0] is TPage fpage)
			{
				page = fpage;
				win = _window.TryGetValue(fpage, out var window)
					? window as TWin
					: null;
				return true;
			}
			page = null;
			win = null;
			return false;
		}

		public static bool TryGetPages<T>(out List<T>? pages) where T : Page
		{
			if (_pages.TryGetValue(typeof(T), out var result))
			{
				if (result.Count != 0)
				{
					pages = result.Cast<T>().ToList();
					return true;
				}
			}
			pages = null;
			return false;
		}
    }

}
