using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

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
		@"(?<prefix>[""']?(?:password|passwd|pass|pwd|odoo_?pass|admin_passwd|api_?key|secret|token|authorization|cookie|session_?id|swdocmgr_key|sw_?license_?key)[""']?\s*[:=]\s*)(?:""(?<qval>(?:\\.|[^""\\])*)""|'(?<sval>(?:\\.|[^'\\])*)'|(?<uval>[^\s,;\]\}""']+))",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant )]
	private static partial Regex SecretFieldRegex();

	[GeneratedRegex(
		@"(?<prefix>\b(?:Bearer|Basic)\s+)[A-Za-z0-9\-._~+/]+=*",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant )]
	private static partial Regex AuthHeaderRegex();

	[GeneratedRegex(
		@"://[^/\s:@]+:[^/\s@]+@",
		RegexOptions.CultureInvariant )]
	private static partial Regex UrlCredentialsRegex();

	[GeneratedRegex(
		@"(?:[A-Za-z0-9+/\-_]{256,}|(?:[A-Za-z0-9+/\-_]{64,}\r?\n)+[A-Za-z0-9+/\-_]+)={0,2}",
		RegexOptions.CultureInvariant )]
	private static partial Regex LargeBase64Regex();

	public static string Redact( string? input )
	{
		if( string.IsNullOrEmpty( input ) )
			return string.Empty;

		string result = UrlCredentialsRegex().Replace( input, "://[REDACTED]@" );
		result = AuthHeaderRegex().Replace( result, "${prefix}[REDACTED]" );
		result = SecretFieldRegex().Replace( result, static m =>
		{
			string prefix = m.Groups[ "prefix" ].Value;
			if( m.Groups[ "qval" ].Success )
				return $"{prefix}\"[REDACTED]\"";
			if( m.Groups[ "sval" ].Success )
				return $"{prefix}'[REDACTED]'";
			return $"{prefix}[REDACTED]";
		} );
		return result.Length >= Base64RedactionThreshold
			? LargeBase64Regex().Replace( result, static m =>
				m.Length >= Base64RedactionThreshold ? $"[REDACTED_BASE64:{m.Length}chars]" : m.Value )
			: result;
	}
}

/// <summary>Thread-safe rolling-file logger with secret redaction and crash dumps.</summary>
public static class HackLogger
{
	public const long DefaultMaxFileBytes = 10L * 1024 * 1024;
	public const int DefaultMaxRetainedFiles = 10;
	public const string LogLevelEnvVar = "HACKPDM_LOG_LEVEL";

	private readonly record struct PendingLogEntry(
		long Epoch,
		DateTimeOffset TimestampUtc,
		HackLogLevel Level,
		string Category,
		string Message,
		Exception? Exception,
		string? CorrelationId,
		TaskCompletionSource? FlushBarrier = null );

	private const int MaxBatchLines = 64;

	private static readonly object Gate = new();
	private static readonly Channel<PendingLogEntry> LogChannel = Channel.CreateUnbounded<PendingLogEntry>(
		new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false } );
	private static readonly Task WriterTask = StartWriterLoop();
	private static string? _logDirectoryOverride;
	private static string? _cachedLogDir;
	private static int _cachedDayKey;
	private static string? _cachedBasePath;
	private static long _cachedFileBytes;
	private static long _maxFileBytes = DefaultMaxFileBytes;
	private static int _maxRetainedFiles = DefaultMaxRetainedFiles;
	private static long _configEpoch;
	private static int _recentErrorCount;
	private static bool _prunedOnStart;

	static HackLogger()
		=> AppDomain.CurrentDomain.ProcessExit += static ( _, _ ) => Flush( 1000 );

	public static HackLogLevel MinimumLevel { get; set; } = ResolveDefaultLevel();
	public static string LogDirectory => _logDirectoryOverride ?? Path.Combine( AppDataPaths.Data, "logs" );
	public static int RecentErrorCount => Volatile.Read( ref _recentErrorCount );

	public static void Configure(
		string? logDirectory = null,
		HackLogLevel? minimumLevel = null,
		long maxFileBytes = DefaultMaxFileBytes,
		int maxRetainedFiles = DefaultMaxRetainedFiles )
	{
		Flush();
		lock( Gate )
		{
			_logDirectoryOverride = logDirectory;
			_cachedLogDir = null;
			_cachedDayKey = 0;
			_cachedBasePath = null;
			_cachedFileBytes = 0;
			_maxFileBytes = Math.Max( 1024, maxFileBytes );
			_maxRetainedFiles = Math.Max( 1, maxRetainedFiles );
			_prunedOnStart = false;
			_configEpoch++;
			if( minimumLevel is { } level )
				MinimumLevel = level;
		}
	}

	public static void ResetToDefaults()
	{
		Flush();
		lock( Gate )
		{
			_logDirectoryOverride = null;
			_cachedLogDir = null;
			_cachedDayKey = 0;
			_cachedBasePath = null;
			_cachedFileBytes = 0;
			_maxFileBytes = DefaultMaxFileBytes;
			_maxRetainedFiles = DefaultMaxRetainedFiles;
			_prunedOnStart = false;
			_configEpoch++;
			MinimumLevel = ResolveDefaultLevel();
			Volatile.Write( ref _recentErrorCount, 0 );
		}
	}

	public static Task FlushAsync( CancellationToken token = default )
	{
		TaskCompletionSource barrier = new( TaskCreationOptions.RunContinuationsAsynchronously );
		if( !LogChannel.Writer.TryWrite( new PendingLogEntry(
			Volatile.Read( ref _configEpoch ),
			default,
			HackLogLevel.None,
			string.Empty,
			string.Empty,
			null,
			null,
			barrier ) ) )
			return Task.CompletedTask;
		return token.CanBeCanceled ? barrier.Task.WaitAsync( token ) : barrier.Task;
	}

	public static void Flush( int timeoutMs = 5000 )
	{
		try
		{
			FlushAsync().Wait( Math.Max( 0, timeoutMs ) );
		}
		catch
		{
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

		LogChannel.Writer.TryWrite( new PendingLogEntry(
			Volatile.Read( ref _configEpoch ),
			DateTimeOffset.UtcNow,
			level,
			category,
			message,
			ex,
			correlationId ) );
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
		Flush( 1000 );
		lock( Gate )
		{
			try
			{
				string dir = LogDirectory;
				Directory.CreateDirectory( dir );
				string suffix = Guid.NewGuid().ToString( "N" )[ ..8 ];
				string path = Path.Combine( dir, $"crash-{now:yyyyMMdd-HHmmss-fff}-{suffix}.log" );
				File.WriteAllText( path, sb.ToString(), Encoding.UTF8 );
				PruneCrashDumps( dir );
				return path;
			}
			catch
			{
				return null;
			}
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

	private static Task StartWriterLoop()
		=> Task.Factory.StartNew(
			ProcessQueueAsync,
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default ).Unwrap();

	private static async Task ProcessQueueAsync()
	{
		ChannelReader<PendingLogEntry> reader = LogChannel.Reader;
		StringBuilder batch = new( 4096 );
		int batchLines = 0;
		long batchByteCount = 0;
		DateTimeOffset batchTimestamp = default;
		long batchEpoch = -1;

		while( await reader.WaitToReadAsync().ConfigureAwait( false ) )
		{
			while( reader.TryRead( out PendingLogEntry entry ) )
			{
				if( entry.FlushBarrier is { } barrier )
				{
					FlushBatchSafe( batch, ref batchLines, ref batchByteCount, batchTimestamp );
					barrier.TrySetResult();
					continue;
				}

				if( entry.Epoch != Volatile.Read( ref _configEpoch ) )
					continue;

				string line = FormatLine( entry.TimestampUtc, entry.Level, entry.Category, entry.Message, entry.Exception, entry.CorrelationId );
				int lineBytes = Encoding.UTF8.GetByteCount( line ) + Encoding.UTF8.GetByteCount( Environment.NewLine );

				if( batchLines > 0 )
				{
					int prevDay = ( batchTimestamp.Year * 10000 ) + ( batchTimestamp.Month * 100 ) + batchTimestamp.Day;
					int nextDay = ( entry.TimestampUtc.Year * 10000 ) + ( entry.TimestampUtc.Month * 100 ) + entry.TimestampUtc.Day;
					long maxBytes = Volatile.Read( ref _maxFileBytes );
					if( batchEpoch != entry.Epoch
						|| prevDay != nextDay
						|| batchLines >= MaxBatchLines
						|| ( _cachedFileBytes + batchByteCount + lineBytes > maxBytes && batchByteCount > 0 ) )
					{
						FlushBatchSafe( batch, ref batchLines, ref batchByteCount, batchTimestamp );
					}
				}

				if( batchLines == 0 )
				{
					batchTimestamp = entry.TimestampUtc;
					batchEpoch = entry.Epoch;
				}
				batch.Append( line ).AppendLine();
				batchLines++;
				batchByteCount += lineBytes;
			}

			FlushBatchSafe( batch, ref batchLines, ref batchByteCount, batchTimestamp );
		}
	}

	private static void FlushBatchSafe( StringBuilder batch, ref int batchLines, ref long batchByteCount, DateTimeOffset nowUtc )
	{
		if( batchLines == 0 )
			return;
		string payload = batch.ToString();
		long payloadBytes = batchByteCount;
		batch.Clear();
		if( batch.Capacity > 64 * 1024 )
			batch.Capacity = 4096;
		batchLines = 0;
		batchByteCount = 0;
		AppendPayloadSafe( nowUtc, payload, payloadBytes );
	}

	private static void AppendPayloadSafe( DateTimeOffset nowUtc, string payload, long payloadBytes )
	{
		lock( Gate )
		{
			try
			{
				string dir = LogDirectory;
				bool dirChanged = !string.Equals( _cachedLogDir, dir, StringComparison.Ordinal );
				if( dirChanged )
				{
					Directory.CreateDirectory( dir );
					_cachedLogDir = dir;
					_cachedDayKey = 0;
					_cachedBasePath = null;
					_cachedFileBytes = 0;
				}
				bool needsPrune = false;
				if( !_prunedOnStart )
				{
					_prunedOnStart = true;
					needsPrune = true;
				}

				int dayKey = ( nowUtc.Year * 10000 ) + ( nowUtc.Month * 100 ) + nowUtc.Day;
				if( _cachedDayKey != dayKey || _cachedBasePath is null )
				{
					_cachedDayKey = dayKey;
					_cachedBasePath = Path.Combine( dir, $"hackpdm-{nowUtc:yyyyMMdd}.log" );
					FileInfo info = new( _cachedBasePath );
					_cachedFileBytes = info.Exists ? info.Length : 0;
					if( !info.Exists )
						needsPrune = true;
				}

				if( _cachedFileBytes >= _maxFileBytes )
				{
					RotateLogFile( dir, _cachedBasePath );
					_cachedFileBytes = 0;
					needsPrune = true;
				}

				File.AppendAllText( _cachedBasePath, payload, Encoding.UTF8 );
				_cachedFileBytes += payloadBytes;

				if( needsPrune )
					PruneOldLogs( dir );
			}
			catch
			{
				_cachedLogDir = null;
				_cachedDayKey = 0;
				_cachedBasePath = null;
				_cachedFileBytes = 0;
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
			.ThenByDescending( f => ParseLogSortKey( f.Name ).DateKey )
			.ThenByDescending( f => ParseLogSortKey( f.Name ).Rotation )
			.ThenByDescending( f => f.Name, StringComparer.Ordinal )
			.ToArray();

		for( int i = _maxRetainedFiles; i < files.Length; i++ )
		{
			try { files[ i ].Delete(); }
			catch { }
		}
	}

	private static void PruneCrashDumps( string dir )
	{
		FileInfo[] files = new DirectoryInfo( dir )
			.GetFiles( "crash-*.log" )
			.OrderByDescending( f => f.LastWriteTimeUtc )
			.ThenByDescending( f => f.Name, StringComparer.Ordinal )
			.ToArray();

		for( int i = _maxRetainedFiles; i < files.Length; i++ )
		{
			try { files[ i ].Delete(); }
			catch { }
		}
	}

	private static (int DateKey, int Rotation) ParseLogSortKey( string fileName )
	{
		const string prefix = "hackpdm-";
		const string ext = ".log";
		if( !fileName.StartsWith( prefix, StringComparison.Ordinal ) || !fileName.EndsWith( ext, StringComparison.Ordinal ) )
			return (0, 0);

		ReadOnlySpan<char> stem = fileName.AsSpan( prefix.Length, fileName.Length - prefix.Length - ext.Length );
		int dot = stem.IndexOf( '.' );
		if( dot < 0 )
			return int.TryParse( stem, NumberStyles.None, CultureInfo.InvariantCulture, out int day ) ? (day, int.MaxValue) : (0, 0);

		ReadOnlySpan<char> dayPart = stem[ ..dot ];
		ReadOnlySpan<char> rotPart = stem[ ( dot + 1 ).. ];
		if( int.TryParse( dayPart, NumberStyles.None, CultureInfo.InvariantCulture, out int d )
			&& int.TryParse( rotPart, NumberStyles.None, CultureInfo.InvariantCulture, out int r ) )
			return (d, r);
		return (0, 0);
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
