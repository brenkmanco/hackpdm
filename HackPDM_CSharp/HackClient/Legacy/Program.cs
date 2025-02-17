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
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

using HackPDM.ClientUtils;
using HackPDM.Forms.Settings;
using HackPDM.HackClient;

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using NUnit.Framework;

using Octokit;

using SolidWorks.Interop.sldworks;

using Application = System.Windows.Forms.Application;

namespace HackPDM
{
	/// <summary>
	/// Class with program entry point.
	/// </summary>
	internal sealed class Program
	{
		/// <summary>
		/// Program entry point.
		/// </summary>
		[STAThread]
		private async static Task Main(string[] args)
		{
			if (!await HackUpdater.EnsureUpdated()) return;
			
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
