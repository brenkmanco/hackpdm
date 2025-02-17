namespace XmlRpc.Goober
{
	public class Logger
	{
		public delegate void LoggerDelegate( string message, LogLevel level );

		public static LoggerDelegate Delegate;
		
		public static void WriteEntry( string message, LogLevel level )
		{
			if ( Delegate != null )
			{
				Delegate( message, level );
			}
		}
	}
}