using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

using HackPDM.Mvvm.Models.Common;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>A view model whose commands run PDM operations that report into the status window.</summary>
/// <param name="dialogs">Shows messages to the user.</param>
/// <param name="reporter">The status window the operations log into.</param>
public abstract class OperationViewModel( IDialogService dialogs, IOperationReporter reporter ) : ObservableObject
{
	protected IDialogService Dialogs { get; } = dialogs;
	protected IOperationReporter Reporter { get; } = reporter;

	/// <summary>Opens the status window and runs <paramref name="operation"/>; then shows <paramref name="completedMessage"/>, if any.</summary>
	protected async Task RunOperationAsync( Func<Task> operation, string? completedMessage = null )
	{
		Reporter.ShowWindow();
		await operation();
		if( completedMessage is not null )
			await Dialogs.ShowMessageAsync( completedMessage );
	}

	/// <summary>As <see cref="RunOperationAsync"/> on <paramref name="ids"/>; does nothing (opens no window) when there are none.</summary>
	protected Task RunOnIdsAsync( IReadOnlyList<int> ids, Func<IReadOnlyList<int>, Task> operation, string? completedMessage = null )
		=> ids.Count == 0 ? Task.CompletedTask : RunOperationAsync( () => operation( ids ), completedMessage );

	/// <summary>The server ids among <paramref name="ids"/> (local-only rows have none, or 0).</summary>
	protected static int[] RemoteIds( IEnumerable<int?> ids ) => [ .. ids.OfType<int>().Where( id => id != 0 ) ];
}
