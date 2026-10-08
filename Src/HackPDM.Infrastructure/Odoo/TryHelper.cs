namespace HackPDM.Infrastructure.Odoo;

public static class TryHelper
{
	public static bool Try(Action act)
	{
		try
		{
			act();
			return true;
		}
		catch { return false; }
	}
	public static bool TryGet<T>(Func<T?> func, out T? value)
	{
		try
		{
			value = func();
			return true;
		}
		catch { value = default; return false; }
	}
	public static async Task<bool> TryAsync(Func<Task> act)
	{
		try
		{
			await act();
			return true;
		}
		catch { return false; }
	}
	public static async Task<(bool, T?)> TryGetAsync<T>(Func<Task<T?>> func)
	{
		try
		{
			var value = await func();
			return (true, value);
		}
		catch { return (false, default); }
	}
}