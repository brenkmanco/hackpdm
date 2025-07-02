using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace XmlRpc.Goober
{
	public class SimpleHttpRequest
	{
		private string __filePath;

		private string _filePathDir;

		private string _filePathFile;

		private Hashtable _headers;

		public StreamWriter Output
		{
			get;
		}

		public StreamReader Input
		{
			get;
		}

		public TcpClient Client
		{
			get;
		}

		private string _filePath
		{
			get
			{
				return __filePath;
			}
			set
			{
				__filePath = value;
				_filePathDir = null;
				_filePathFile = null;
			}
		}

		public string HttpMethod
		{
			get; private set;
		}

		public string Protocol
		{
			get; private set;
		}

		public string FilePath => _filePath;

		public string FilePathFile
		{
			get
			{
				if ( _filePathFile != null )
				{
					return _filePathFile;
				}

				int num = FilePath.LastIndexOf("/", StringComparison.Ordinal);
				if ( num == -1 )
				{
					return "";
				}

				num++;
				_filePathFile = FilePath.Substring( num, FilePath.Length - num );
				return _filePathFile;
			}
		}

		public string FilePathDir
		{
			get
			{
				if ( _filePathDir != null )
				{
					return _filePathDir;
				}

				int num = FilePath.LastIndexOf("/", StringComparison.Ordinal);
				if ( num == -1 )
				{
					return "";
				}

				num++;
				_filePathDir = FilePath.Substring( 0, num );
				return _filePathDir;
			}
		}

		public SimpleHttpRequest( TcpClient client )
		{
			Client = client;
			Output = new StreamWriter( client.GetStream() );
			Input = new StreamReader( client.GetStream() );
			GetRequestMethod();
			GetRequestHeaders();
		}

		private void GetRequestMethod()
		{
			string text = Input.ReadLine();
			if ( text == null )
			{
				throw new ApplicationException( "Void request." );
			}

			if ( string.Compare( "GET ", text.Substring( 0, 4 ), StringComparison.Ordinal ) == 0 )
			{
				HttpMethod = "GET";
			}
			else
			{
				if ( string.Compare( "POST ", text.Substring( 0, 5 ), StringComparison.Ordinal ) != 0 )
				{
					throw new InvalidOperationException( "Unrecognized method in query: " + text );
				}

				HttpMethod = "POST";
			}

			text = text.TrimEnd();
			int num = text.IndexOf(' ') + 1;
			if ( num >= text.Length )
			{
				throw new ApplicationException( "What do you want?" );
			}

			string text2 = text.Substring(num);
			int num2 = text2.IndexOf(' ');
			if ( num2 == -1 )
			{
				num2 = text2.Length;
			}

			_filePath = text2.Substring( 0, num2 ).Trim();
			Protocol = text2.Substring( num2 ).Trim();
		}

		private void GetRequestHeaders()
		{
			_headers = [];
			string text;
			while ( ( text = Input.ReadLine() ) != "" && text != null )
			{
				int num = text.IndexOf(':');
				if ( num == -1 || num == text.Length - 1 )
				{
					Logger.WriteEntry( "Malformed header line: " + text, LogLevel.Information );
					continue;
				}

				string key = text.Substring(0, num);
				string value = text.Substring(num + 1);
				try
				{
					_headers.Add( key, value );
				}
				catch ( Exception )
				{
					Logger.WriteEntry( "Duplicate header key in line: " + text, LogLevel.Information );
				}
			}
		}

		public override string ToString()
		{
			return HttpMethod + " " + FilePath + " " + Protocol;
		}

		public void Close()
		{
			Output.Flush();
			Output.Close();
			Input.Close();
			Client.Close();
		}
	}
}
