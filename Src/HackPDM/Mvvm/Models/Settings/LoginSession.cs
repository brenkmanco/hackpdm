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

	/// <summary>Records a successful login; true when it is another account than the last one (what was kept for that one no longer applies).</summary>
	public bool LoggedInAs( string account )
	{
		IsLoggedIn = true;
		bool changed = Account is not null && !string.Equals( Account, account, StringComparison.OrdinalIgnoreCase );
		Account = account;
		return changed;
	}
}
