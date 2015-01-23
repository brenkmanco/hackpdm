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

		// constructor
		public SWHelper()
		{

			// attach to running instance
			if (System.Diagnostics.Process.GetProcessesByName("sldworks").Length != 0)
			{
				swApp = (SldWorks.SldWorks)System.Runtime.InteropServices.Marshal.GetActiveObject("SldWorks.Application");
			}
			else
			{
				swApp = new SldWorks.SldWorks();
				//swApp = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
			}

			// start background instance
			DialogResult dr = MessageBox.Show("Failed to get a SolidWorks instance",
				"Loading SW",
				MessageBoxButtons.OK,
				MessageBoxIcon.Exclamation,
				MessageBoxDefaultButton.Button1);

			return;

		}

		public List<string[]> GetDependenciesShallow(string FileName)
		{
			List<string[]> listDepends = new List<string[]>();
			int size = swApp.IGetDocumentDependenciesCount2(FileName, false, false, true);
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

	}
}
