using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

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

	//		if (swRunApp == null)
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

			// test for dependencies
			int size = swApp.IGetDocumentDependenciesCount2(FileName, Deep, false, true);
			if (size == 0) return null;

			// get array of dependency info (one-dimensional)
			string[] varDepends = (string[])swApp.GetDocumentDependencies2(FileName, false, false, true);

			string strTempPath = Path.GetTempPath();
			for (int i = 0; i < varDepends.Length/3; i++)
			{

				// file name with absolute path
				string strFullName = varDepends[3 * i + 1];

				// short file name with extension
				string strName = strFullName.Substring(strFullName.LastIndexOf("\\") + 1);

				// read only status
				string strReadOnly = varDepends[3 * i + 2];

				// only return non-virtual components
				if (strTempPath != strFullName.Substring(0,strTempPath.Length))
				{
					string[] strDepend = new string[3] {strName, strFullName, strReadOnly};
					listDepends.Add(strDepend);
				}

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
			try
			{
				swApp.ExitApp();
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					"Failed to close SolidWorks instance: " + ex.Message,
					"Failed Closing SolidWork",
					MessageBoxButtons.OK
				);
			}
		}

	}
}
