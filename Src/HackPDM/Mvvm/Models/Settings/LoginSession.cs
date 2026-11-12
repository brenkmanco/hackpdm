using System;
using System.Collections.Generic;

using HackPDM.Domain.Representation;

namespace HackPDM.Mvvm.Models.Settings;

/// <summary>
/// Whether this session is logged in to Odoo, kept for the whole run (one instance, held by the
/// composition root) while the Profile Manager page and its view model come and go.
/// </summary>
public sealed class LoginSession
{
	public bool IsLoggedIn { get; set; }

	/// <summary>Why the last login failed; shown again when the Profile Manager page is reopened.</summary>
	public IReadOnlyList<BasicStatusMessage> LastErrors { get; set; } = [];

	/// <summary>The server, database and user of the last successful login.</summary>
	public string? Account { get; private set; }

	/// <summary>Records a successful login and remembers which account it was.</summary>
	/// <param name="account">Identifies the account logged in to (server, database and user).</param>
	/// <returns><see langword="true"/> when an earlier login in this run was to a different account (compared ignoring case),
	/// meaning what was kept for that account no longer applies; <see langword="false"/> on the first login or the same account.</returns>
	/// <remarks>Sets <see cref="IsLoggedIn"/>; it does not clear <see cref="LastErrors"/>.</remarks>
	public bool LoggedInAs( string account )
	{
		IsLoggedIn = true;
		bool changed = Account is not null && !string.Equals( Account, account, StringComparison.OrdinalIgnoreCase );
		Account = account;
		return changed;
	}
}
