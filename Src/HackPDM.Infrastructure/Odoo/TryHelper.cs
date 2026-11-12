namespace HackPDM.Infrastructure.Odoo;

/// <summary>Runs code and turns any exception into a false result. Every exception is swallowed, cancellation included.</summary>
public static class TryHelper
{
	/// <summary>Runs an action, swallowing any exception.</summary>
	/// <param name="act">The action to run.</param>
	/// <returns>True when <paramref name="act"/> completed; false when it threw.</returns>
	public static bool Try(Action act)
	{
		try
		{
			act();
			return true;
		}
		catch { return false; }
	}
	/// <summary>Runs a function, swallowing any exception.</summary>
	/// <typeparam name="T">The function's result type.</typeparam>
	/// <param name="func">The function to run.</param>
	/// <param name="value">The function's result, or <see langword="default"/> when it threw.</param>
	/// <returns>True when <paramref name="func"/> completed; false when it threw.</returns>
	public static bool TryGet<T>(Func<T?> func, out T? value)
	{
		try
		{
			value = func();
			return true;
		}
		catch { value = default; return false; }
	}
	/// <summary>Runs and awaits an asynchronous action, swallowing any exception.</summary>
	/// <param name="act">The asynchronous action to run.</param>
	/// <returns>True when the task completed; false when starting or awaiting it threw (including <see cref="OperationCanceledException"/>).</returns>
	public static async Task<bool> TryAsync(Func<Task> act)
	{
		try
		{
			await act();
			return true;
		}
		catch { return false; }
	}
	/// <summary>Runs and awaits an asynchronous function, swallowing any exception.</summary>
	/// <typeparam name="T">The function's result type.</typeparam>
	/// <param name="func">The asynchronous function to run.</param>
	/// <returns>(true, result) when the task completed; (false, <see langword="default"/>) when it threw.</returns>
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