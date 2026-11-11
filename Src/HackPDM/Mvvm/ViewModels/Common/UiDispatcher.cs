using System;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>
/// Runs view-model updates on the UI thread without the view model knowing about the UI framework.
/// The shell sets <see cref="Post"/> at startup; until then actions run inline.
/// </summary>
public static class UiDispatcher
{
	public static Action<Action> Post { get; set; } = action => action();
}
