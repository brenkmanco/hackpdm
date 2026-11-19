using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HackPDM.Core.Configuration;

public enum HackLogLevel
{
	Trace = 0,
	Debug = 1,
	Information = 2,
	Warning = 3,
	Error = 4,
	Critical = 5,
	None = 6,
}

/// <summary>Redacts secrets and large base64 payloads from log text.</summary>
public static partial class LogRedactor
{
	public const int Base64RedactionThreshold = 256;

	[GeneratedRegex(
		@"(?<prefix>[""']?(?:password|passwd|pass|odoo_?pass|admin_passwd|api_?key|secret|token|authorization|cookie|session_?id)[""']?\s*[:=]\s*)(?:""(?<qval>(?:\\.|[^""\\])*)""|'(?<sval>(?:\\.|[^'\\])*)'|(?<uval>[^\s,;\]\}""']+))",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant )]
	private static partial Regex SecretFieldRegex();

	[GeneratedRegex(
		@"(?<prefix>\b(?:Bearer|Basic)\s+)[A-Za-z0-9\-._~+/]+=*",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant )]
	private static partial Regex AuthHeaderRegex();

	[GeneratedRegex( @"[A-Za-z0-9+/]{256,}={0,2}", RegexOptions.CultureInvariant )]
	private static partial Regex LargeBase64Regex();

	public static string Redact( string? input )
	{
		if( string.IsNullOrEmpty( input ) )
			return string.Empty;

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
		return LargeBase64Regex().Replace( result, m => $"[REDACTED_BASE64:{m.Length}chars]" );
	}
}

/// <summary>Thread-safe rolling-file logger with secret redaction and crash dumps.</summary>
public static class HackLogger
{
	public const long DefaultMaxFileBytes = 10L * 1024 * 1024;
	public const int DefaultMaxRetainedFiles = 10;
	public const string LogLevelEnvVar = "HACKPDM_LOG_LEVEL";

	private static readonly object Gate = new();
	private static string? _logDirectoryOverride;
	private static long _maxFileBytes = DefaultMaxFileBytes;
	private static int _maxRetainedFiles = DefaultMaxRetainedFiles;
	private static int _recentErrorCount;
	private static bool _prunedOnStart;

	public static HackLogLevel MinimumLevel { get; set; } = ResolveDefaultLevel();
	public static string LogDirectory => _logDirectoryOverride ?? Path.Combine( AppDataPaths.Data, "logs" );
	public static int RecentErrorCount => Volatile.Read( ref _recentErrorCount );

	public static void Configure(
		string? logDirectory = null,
		HackLogLevel? minimumLevel = null,
		long maxFileBytes = DefaultMaxFileBytes,
		int maxRetainedFiles = DefaultMaxRetainedFiles )
	{
		lock( Gate )
		{
			_logDirectoryOverride = logDirectory;
			_maxFileBytes = Math.Max( 1024, maxFileBytes );
			_maxRetainedFiles = Math.Max( 1, maxRetainedFiles );
			_prunedOnStart = false;
			if( minimumLevel is { } level )
				MinimumLevel = level;
		}
	}

	public static void ResetToDefaults()
	{
		lock( Gate )
		{
			_logDirectoryOverride = null;
			_maxFileBytes = DefaultMaxFileBytes;
			_maxRetainedFiles = DefaultMaxRetainedFiles;
			_prunedOnStart = false;
			MinimumLevel = ResolveDefaultLevel();
			Volatile.Write( ref _recentErrorCount, 0 );
		}
	}

	public static void Trace( string category, string message, Exception? ex = null, string? correlationId = null )
		=> Write( HackLogLevel.Trace, category, message, ex, correlationId );

	public static void Debug( string category, string message, Exception? ex = null, string? correlationId = null )
		=> Write( HackLogLevel.Debug, category, message, ex, correlationId );

	public static void Info( string category, string message, Exception? ex = null, string? correlationId = null )
		=> Write( HackLogLevel.Information, category, message, ex, correlationId );

	public static void Warn( string category, string message, Exception? ex = null, string? correlationId = null )
		=> Write( HackLogLevel.Warning, category, message, ex, correlationId );

	public static void Error( string category, string message, Exception? ex = null, string? correlationId = null )
		=> Write( HackLogLevel.Error, category, message, ex, correlationId );

	public static void Critical( string category, string message, Exception? ex = null, string? correlationId = null )
		=> Write( HackLogLevel.Critical, category, message, ex, correlationId );

	public static void Write(
		HackLogLevel level,
		string category,
		string message,
		Exception? ex = null,
		string? correlationId = null )
	{
		if( level >= HackLogLevel.Error )
			Interlocked.Increment( ref _recentErrorCount );
		if( level < MinimumLevel || MinimumLevel == HackLogLevel.None )
			return;

		string line = FormatLine( DateTimeOffset.UtcNow, level, category, message, ex, correlationId );
		AppendLineSafe( line );
	}

	public static string? WriteCrashDump(
		string source,
		Exception? exception,
		string? appVersion = null,
		string? context = null )
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		StringBuilder sb = new();
		sb.AppendLine( "=== HackPDM Crash Dump ===" );
		sb.AppendLine( $"TimestampUtc: {now:O}" );
		sb.AppendLine( $"Source: {LogRedactor.Redact( source )}" );
		if( !string.IsNullOrWhiteSpace( appVersion ) )
			sb.AppendLine( $"ClientVersion: {appVersion}" );
		sb.AppendLine( $"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})" );
		sb.AppendLine( $"Runtime: {RuntimeInformation.FrameworkDescription}" );
		if( !string.IsNullOrWhiteSpace( context ) )
			sb.AppendLine( $"Context: {LogRedactor.Redact( context )}" );
		sb.AppendLine( $"RecentErrorCount: {RecentErrorCount}" );
		sb.AppendLine();
		sb.AppendLine( "Exception:" );
		sb.AppendLine( exception is null ? "(none)" : LogRedactor.Redact( exception.ToString() ) );

		Critical( source, $"Unhandled exception: {exception?.Message ?? "unknown"}", exception );
		try
		{
			string dir = LogDirectory;
			Directory.CreateDirectory( dir );
			string path = Path.Combine( dir, $"crash-{now:yyyyMMdd-HHmmss}.log" );
			File.WriteAllText( path, sb.ToString(), Encoding.UTF8 );
			return path;
		}
		catch
		{
			return null;
		}
	}

	public static string BuildDiagnosticSummary(
		string? clientVersion = null,
		string? odooUrl = null,
		string? odooDb = null,
		string? credentialStore = null )
	{
		StringBuilder sb = new();
		sb.AppendLine( "HackPDM Diagnostic Summary" );
		sb.AppendLine( $"Timestamp (UTC): {DateTimeOffset.UtcNow:O}" );
		if( !string.IsNullOrWhiteSpace( clientVersion ) )
			sb.AppendLine( $"Client Version: {clientVersion}" );
		sb.AppendLine( $"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})" );
		sb.AppendLine( $"Runtime: {RuntimeInformation.FrameworkDescription}" );
		sb.AppendLine( $"Log Level: {MinimumLevel}" );
		sb.AppendLine( $"Logs Directory: {LogDirectory}" );
		sb.AppendLine( $"Session Errors Logged: {RecentErrorCount}" );
		if( !string.IsNullOrWhiteSpace( credentialStore ) )
			sb.AppendLine( $"Credential Store: {credentialStore}" );
		if( !string.IsNullOrWhiteSpace( odooUrl ) )
			sb.AppendLine( $"Odoo URL: {LogRedactor.Redact( odooUrl )}" );
		if( !string.IsNullOrWhiteSpace( odooDb ) )
			sb.AppendLine( $"Odoo DB: {odooDb}" );
		return sb.ToString().TrimEnd();
	}

	internal static string FormatLine(
		DateTimeOffset timestampUtc,
		HackLogLevel level,
		string category,
		string message,
		Exception? ex,
		string? correlationId )
	{
		string lvl = level switch
		{
			HackLogLevel.Trace => "TRC",
			HackLogLevel.Debug => "DBG",
			HackLogLevel.Information => "INF",
			HackLogLevel.Warning => "WRN",
			HackLogLevel.Error => "ERR",
			HackLogLevel.Critical => "CRT",
			_ => "LOG",
		};
		string safeCategory = string.IsNullOrWhiteSpace( category ) ? "General" : category;
		string safeMessage = LogRedactor.Redact( message );
		string corr = string.IsNullOrWhiteSpace( correlationId ) ? "-" : correlationId;

		StringBuilder sb = new();
		sb.Append( '[' ).Append( timestampUtc.ToString( "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture ) ).Append( "] " );
		sb.Append( '[' ).Append( lvl ).Append( "] " );
		sb.Append( '[' ).Append( safeCategory ).Append( "] " );
		sb.Append( '[' ).Append( corr ).Append( "] " );
		sb.Append( safeMessage );

		if( ex is not null )
		{
			sb.AppendLine();
			sb.Append( "  " ).Append( LogRedactor.Redact( ex.ToString() ).Replace( "\n", "\n  " ) );
		}
		return sb.ToString();
	}

	private static void AppendLineSafe( string line )
	{
		lock( Gate )
		{
			try
			{
				string dir = LogDirectory;
				Directory.CreateDirectory( dir );
				if( !_prunedOnStart )
				{
					_prunedOnStart = true;
					PruneOldLogs( dir );
				}

				string basePath = Path.Combine( dir, $"hackpdm-{DateTimeOffset.UtcNow:yyyyMMdd}.log" );
				if( File.Exists( basePath ) && new FileInfo( basePath ).Length >= _maxFileBytes )
				{
					RotateLogFile( dir, basePath );
					PruneOldLogs( dir );
				}
				File.AppendAllText( basePath, line + Environment.NewLine, Encoding.UTF8 );
			}
			catch
			{
			}
		}
	}

	private static void RotateLogFile( string dir, string basePath )
	{
		string stem = Path.GetFileNameWithoutExtension( basePath );
		int suffix = 1;
		string rotated;
		do
		{
			rotated = Path.Combine( dir, $"{stem}.{suffix++}.log" );
		}
		while( File.Exists( rotated ) );
		File.Move( basePath, rotated );
	}

	private static void PruneOldLogs( string dir )
	{
		FileInfo[] files = new DirectoryInfo( dir )
			.GetFiles( "hackpdm-*.log" )
			.OrderByDescending( f => f.LastWriteTimeUtc )
			.ThenByDescending( f => f.Name, StringComparer.Ordinal )
			.ToArray();

		for( int i = _maxRetainedFiles; i < files.Length; i++ )
		{
			try { files[ i ].Delete(); }
			catch { }
		}
	}

	private static HackLogLevel ResolveDefaultLevel()
	{
		if( Environment.GetEnvironmentVariable( LogLevelEnvVar ) is { Length: > 0 } env
			&& Enum.TryParse<HackLogLevel>( env, ignoreCase: true, out var parsed ) )
			return parsed;
#if DEBUG
		return HackLogLevel.Debug;
#else
		return HackLogLevel.Information;
#endif
	}
}
