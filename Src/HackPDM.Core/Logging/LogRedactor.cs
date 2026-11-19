using System.Text.RegularExpressions;

namespace HackPDM.Core.Logging;

/// <summary>
/// Redacts secrets (passwords, API keys, cookies, tokens) and large base64 file payloads from log text
/// so credentials and binary file bodies never reach log files or crash dumps.
/// </summary>
public static partial class LogRedactor
{
	/// <summary>Minimum contiguous base64 run length treated as a binary payload and redacted.</summary>
	public const int Base64RedactionThreshold = 256;

	// Matches JSON-style secret fields: "password": "secret", "api_key":"abc", "Cookie": "..."
	[GeneratedRegex(
		@"(?<prefix>[""']?(?:password|passwd|pass|odoo_?pass|admin_passwd|api_?key|secret|token|authorization|cookie|session_?id)[""']?\s*[:=]\s*)(?:""(?<qval>(?:\\.|[^""\\])*)""|'(?<sval>(?:\\.|[^'\\])*)'|(?<uval>[^\s,;\]\}""']+))",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant )]
	private static partial Regex SecretFieldRegex();

	// Matches Bearer / Basic authorization headers
	[GeneratedRegex(
		@"(?<prefix>\b(?:Bearer|Basic)\s+)[A-Za-z0-9\-._~+/]+=*",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant )]
	private static partial Regex AuthHeaderRegex();

	// Matches contiguous base64 payloads (>= 256 chars) such as file_contents or preview_image
	[GeneratedRegex(
		@"[A-Za-z0-9+/]{256,}={0,2}",
		RegexOptions.CultureInvariant )]
	private static partial Regex LargeBase64Regex();

	/// <summary>Returns a redacted copy of <paramref name="input"/> safe for writing to logs.</summary>
	public static string Redact( string? input )
	{
		if( string.IsNullOrEmpty( input ) )
			return string.Empty;

		// Redact Bearer/Basic auth headers first so "Authorization: Bearer <token>" masks the token, not just "Bearer"
		string result = AuthHeaderRegex().Replace( input, "${prefix}[REDACTED]" );

		result = SecretFieldRegex().Replace( result, m =>
		{
			string prefix = m.Groups[ "prefix" ].Value;
			if( m.Groups[ "qval" ].Success )
				return $"{prefix}\"[REDACTED]\"";
			if( m.Groups[ "sval" ].Success )
				return $"{prefix}'[REDACTED]'";
			return $"{prefix}[REDACTED]";
		} );

		result = LargeBase64Regex().Replace( result, m => $"[REDACTED_BASE64:{m.Length}chars]" );

		return result;
	}
}
