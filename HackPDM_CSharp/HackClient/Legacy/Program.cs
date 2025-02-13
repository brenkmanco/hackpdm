/*
 * 
 * (C) 2013 Matt Taylor
 * Date: 2/18/2013
 * 
 * This file is part of HackPDM.
 * 
 * HackPDM is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * HackPDM is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with HackPDM.  If not, see <http://www.gnu.org/licenses/>.
 * 
 */

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HackPDM.Forms.Settings;

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using NUnit.Framework;
using SolidWorks.Interop.sldworks;

namespace HackPDM
{
	/// <summary>
	/// Class with program entry point.
	/// </summary>
	internal sealed class Program
	{
#if DEBUG
		private static string CurrentVersion()
		{
			// git log --format="%H | %cd" --date=iso
			var assembly = Assembly.GetExecutingAssembly();
			
			var Test = assembly.GetCustomAttribute<AssemblyMetadataAttribute>()?.Value;
            // var commitHash = assembly.GetCustomAttribute<AssemblyMetadataAttribute>("CommitHash")?.Value;
            // var commitDate = assembly.GetCustomAttribute<AssemblyMetadataAttribute>("CommitDate")?.Value;
			return "";
		}		
#endif






		/// <summary>
		/// Program entry point.
		/// </summary>
		[STAThread]
		private static void Main(string[] args)
		{
			#if DEBUG
				CurrentVersion();
			#endif
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			#if DEBUG
				Application.Run(new DebugForm());
			#elif RELEASE || GITRELEASE
				Application.Run(new HackFileManager());
			#elif SERVER
				string printer = string.Join( "\n", args);
				MessageBox.Show(printer);
			// .\HackPDM.exe test this -s out --help with -h me "and" -wompwomp
			#else
				
			#endif
		}
		
	}
}
