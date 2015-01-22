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

		private SldWorks.ISldWorks swApp;

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
