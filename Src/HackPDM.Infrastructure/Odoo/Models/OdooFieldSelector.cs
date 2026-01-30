namespace HackPDM.Infrastructure.Odoo.Models;

public static class OdooFieldSelector
{
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
