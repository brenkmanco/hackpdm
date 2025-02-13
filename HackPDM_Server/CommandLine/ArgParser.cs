using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using CommandLine;

namespace HackPDM_Server.CommandLine
{
	abstract class BaseOptions
	{
		[Option('v', "verbose", Default=false,Required=false, HelpText="Set output to verbose messages.")]
		public bool Verbose { get; set; }
			
		[Option('u', "update", Default=10, Required=false, HelpText="Set the interval in minutes that you'd like to commit to odoo")]
		public int UpdateOdoo { get; set; }
		[Option('r', "refresh", Default=10, Required=false, HelpText="Set the interval in minutes that you'd like to pull from odoo to update hack")]
		public int UpdateHack { get; set; }
		[Option('c', "config", Required=false, HelpText="File path to oodo/hack configuration")]
		public string HackConfigPath { get; set; }

		private static string GetDefaultConfig()
		{
			string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string directoryPath = Path.Combine(appDataPath, $"HackPDM_CSharp\\Settings");
			string fullPath = Path.Combine(directoryPath, "HackOdooSettings.json");
			return fullPath;
		}
	}
	class Options : BaseOptions {}
	
	[Verb("shell", HelpText="Start a shell instance for hack server")]
	class ShellOptions : BaseOptions {}
	public static class ProgramOptions
	{
		public static void Parse(string[] args)
		{
			//string printer = string.Join( "\n", args);
			// shell -v -u 1 -r 1
			
			ParserResult<object> parse =  Parser.Default.ParseArguments<Options, ShellOptions, ConfigSchema>(args);
			parse.WithParsed<Options>(  ParsingActions  );
			parse.WithParsed<ShellOptions>( ParsingShellActions );
			parse.WithParsed<ConfigSchema>( ParsingConfigActions );
			parse.WithNotParsed( InvalidActions );
		}
		private static void ParsingActions(Options opt) 
		{
			DisplayValues(opt);
		}
		private static void ParsingShellActions( ShellOptions opt ) 
		{
			DisplayValues(opt);
		}
		private static void ParsingConfigActions( ConfigSchema opt )
		{
			string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string directoryPath = Path.Combine(appDataPath, $"HackPDM_CSharp\\Settings");
			string fullPath = Path.Combine(directoryPath, "HackOdooSettings.json");

			FileInfo file = new FileInfo(fullPath);
			if (file.Exists)
			{
				Console.Write("configuration file already exists..\n" +
				"o = overwrite\n" +
				"n = new configuration\n" +
				"q = quit\n => ");
				string? decision = Console.ReadLine();
				switch (decision) 
				{
					case null:
					case "q":
					case "":
						return;
					case "o": 
					{
						using (FileStream stream = file.OpenRead())
						{
							ConfigSchema newConfig = JsonSerializer.Deserialize<ConfigSchema>(stream);
							if (newConfig is null)
							{
								Console.WriteLine("configuration file is invalid\ntype n for new configuration or program exits");
								if (Console.ReadLine() != "n") return;
							}
							else opt = newConfig;
						}
						break;
					}
					case "n": break;
					
				}
			}
			if (SetupConfigValues(ref opt))
			{
				DisplayConfigValues( opt );
				if (!Directory.Exists(directoryPath))
				{
					Directory.CreateDirectory(directoryPath);
				}
				using (var fs = file.Create())
				using (var sw = new StreamWriter(fs))
				{
					sw.Write(JsonSerializer.Serialize(opt));
				}
			}
		}
		private static void DisplayConfigValues(ConfigSchema configSchema)
		{
			Type type = configSchema.GetType();
			PropertyInfo[] props = type.GetProperties();
			var storeColor = Console.ForegroundColor;
			foreach ( PropertyInfo prop in props ) 
			{
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write($"{prop.Name} = ");
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine(prop.GetValue(configSchema));
			}
			Console.ForegroundColor = storeColor;
		}
		private static bool SetupConfigValues(ref ConfigSchema configSchema)
		{
			Type type = configSchema.GetType();
			PropertyInfo[] props = type.GetProperties();
			var storeColor = Console.ForegroundColor;
			Console.WriteLine("for default value type '$d'.\nto quit type 'quit'\n");
			foreach ( PropertyInfo prop in props ) 
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				object? value = prop.GetValue(configSchema);
				if ( value != null )
				{
					Console.WriteLine($"default = {value}");
				}
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write($"{prop.Name}");
				bool inLoop = true;
				while(inLoop)
				{
					Console.Write( " => ");
					string? input = Console.ReadLine();
					bool hasValue = false;
					switch(input)
					{
						case null: continue;
						case "quit": return false;
						case "$d": 
						{
							if ( value == null )
							{
								Console.WriteLine("this variable doesn't have a default value");
								continue;
							}
							inLoop = false;
							continue;
						}
						default:
						{
							if (hasValue)
							{
								inLoop = false; 
								continue;
							}
							int number;
							if (prop.PropertyType == typeof(int)) 
							{
								if (!int.TryParse(input, out number))
								{
									Console.WriteLine("invalid number");
									continue;
								}
								prop.SetValue(configSchema, number);
							}
							else
							{
								prop.SetValue(configSchema, input);
							}
							inLoop = false;
							continue;
						}
					}
				}

				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine();
			}
			Console.ForegroundColor = storeColor;			
			return true;
		}
		private static void DisplayValues(BaseOptions opt)
		{
			Console.WriteLine($"verbose: {opt.Verbose}");
			Console.WriteLine($"update: {opt.UpdateOdoo}");
			Console.WriteLine($"hack: {opt.UpdateHack}");
		}
		private static void InvalidActions( IEnumerable<Error> enumerable )
		{
			foreach (var error in enumerable )
			{
				Console.WriteLine(error);
			}
		}
	}
	[Verb("config", HelpText="Configuration setup for oodo/hack")]
	class ConfigSchema
	{
		[Value(0, HelpText="The name of the Credential target in Windows Credential Manager. Ex: HackPDM-OdooUser")]
		public string OdooCredentialTarget { get; set; } = "HackPDM-OdooUser";
		[Value(1, HelpText="The Odoo database name that contains your models")]
		public string OdooDb { get; set; } = "odoopdm";
		[Value(2, HelpText="Your Odoo IP or domain name. Ex: http://<domain or ip>:8069")]
		public string OdooAddress { get; set; }
		[Value(3, HelpText="Your Odoo Port number. Ex: http://odoodomain:<port>")]
		public int OdooPort { get; set; } = 8069;
		[Value(4, HelpText="The total number of versions to download file contents from in one api call")]
		public int OdooDownloadBatchAmount { get; set; } = 25;
		[Value(5, HelpText="The absolute path to your root pwa. Ex: C:\\ProgramData\\Temp\\hackpdm\\HackPDM_CSharp\\pwa")]
		public string PWAPathAbsolute { get; set; }
		[Value(6, HelpText="The relative path to your root. Ex: pwa")]
		public string PWAPathRelative { get; set; } = "pwa";
		[Value(7, HelpText="The absolute path to your project. Ex: C:\\ProgramData\\Temp\\hackpdm\\HackPDM_CSharp")]
		public string HackProjectDirectory { get; set; }
	}
}
