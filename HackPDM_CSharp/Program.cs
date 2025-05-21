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
using System.IO;
using HackPDM.HackClient;

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
		private static void Main(string[] args)
		{
			try
			{
				//#if GITRELEASE || RELEASE
				HackUpdater.EnsureUpdated();
				//#endif
			}
			catch
			{
				return;
			}
			string tempPath = Path.Combine(Path.GetTempPath(), Application.ProductName);
			if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			Application.Run(new HackFileManager());		
		}
		
	}
}
