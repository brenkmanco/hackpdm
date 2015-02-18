using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swdocumentmgr;
//using stdole;
//using Microsoft.VisualBasic.Compatibility.VB6;

namespace HackPDM
{
	class SWHelper
	{

		private SldWorks.SldWorks swApp;
		private SldWorks.SldWorks swRunApp;

		// constructor
		public SWHelper()
		{
			//if (System.Diagnostics.Process.GetProcessesByName("sldworks").Length > 1)
			//{
			//    DialogResult dr = MessageBox.Show("Multiple SolidWorks Instances Detected",
			//        "Loading SW",
			//        MessageBoxButtons.OK,
			//        MessageBoxIcon.Exclamation,
			//        MessageBoxDefaultButton.Button1);
			//    return;
			//}

			try
			{
				// start background instance
				swRunApp = (SldWorks.SldWorks)System.Runtime.InteropServices.Marshal.GetActiveObject("SldWorks.Application");
			}
			catch { }

			if (swRunApp == null)
			{
				try
				{
					swApp = new SldWorks.SldWorks();
				}
				catch
				{
					DialogResult dr = MessageBox.Show("Failed to get a SolidWorks instance",
						"Loading SW",
						MessageBoxButtons.OK,
						MessageBoxIcon.Exclamation,
						MessageBoxDefaultButton.Button1);
				}
			}
			return;

		}

		public List<string[]> GetDependencies(string FileName, bool Deep=false)
		{
			// returns list of string arrays
			// 0: short file name
			// 1: long file name
			// 2: loaded read only
			List<string[]> listDepends = new List<string[]>();
			int size = swApp.IGetDocumentDependenciesCount2(FileName, Deep, false, true);
			if (size == 0) return null;
			string[] varDepends = (string[])swApp.GetDocumentDependencies2(FileName, false, false, true);
			for (int i = 0; i < varDepends.Length/3; i++)
			{
				string[] strDepend = new string[3] {varDepends[3 * i], varDepends[3 * i + 1], varDepends[3 * i + 2]};
				listDepends.Add(strDepend);
			}
			return listDepends;
		}

		//private swDocumentTypes_e GetDocType(string FileName)
		//{
		//    ModelDoc2 swDoc = (ModelDoc2)swApp.GetDocuments();
		//    return (swDocumentTypes_e)swDoc.GetType();
		//}

		//public string GetActiveDocName()
		//{
		//    ModelDoc2 swDoc = (ModelDoc2)swApp.ActiveDoc;
		//    return strModelFile = swDoc.GetPathName();
		//}


		// class destructor
		~SWHelper()
		{
			// cleanup statements
			swApp.ExitApp();
		}

	}
}
