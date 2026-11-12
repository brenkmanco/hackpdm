namespace HackPDM.Infrastructure.Odoo.Models;

/// <summary>Picks the Odoo field names to read or send for a model.</summary>
public static class OdooFieldSelector
{
	/// <summary>Filters a model's field names by an exclude list and an include list, then appends extra names.</summary>
	/// <param name="modelFields">All the model's field names, in order.</param>
	/// <param name="excluded">Names to drop; <see langword="null"/> drops none.</param>
	/// <param name="included">When not <see langword="null"/>, only these names are kept (exclusion still applies).</param>
	/// <param name="insert">Names appended after the filtered fields if not already present; they need not be model fields.</param>
	/// <returns>A new array: the kept fields in <paramref name="modelFields"/> order, then the inserted ones.</returns>
	public static string[] Select(
		string[] modelFields,
		string[]? excluded = null,
		string[]? included = null,
		string[]? insert = null )
	{
		var excludedSet = excluded is null ? null : new HashSet<string>(excluded);
		var includedSet = included is null ? null : new HashSet<string>(included);

		var result = new List<string>();
		var useInclude = includedSet is not null;

		foreach( var field in modelFields )
		{
			var isExcluded = excludedSet is not null && excludedSet.Contains(field);
			var isIncluded = !useInclude || includedSet!.Contains(field);

			if( !isExcluded && isIncluded )
				result.Add( field );
		}

		if( insert is not null )
		{
			foreach( var extra in insert )
			{
				if( !result.Contains( extra ) )
					result.Add( extra );
			}
		}

		return [.. result];
	}
}
