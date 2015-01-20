using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swdocumentmgr;
using stdole;
using Microsoft.VisualBasic.Compatibility.VB6;

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

		public Image GetPreviewImage(string FileName)
		{

			SwDMClassFactory swClassFact = default(SwDMClassFactory);
			SwDMApplication swDocMgr = default(SwDMApplication);
			SwDMDocument swDoc = default(SwDMDocument);
			SwDMConfigurationMgr swCfgMgr = default(SwDMConfigurationMgr);
			string[] vCfgNameArr = null;
			SwDMConfiguration7 swCfg = default(SwDMConfiguration7);
			IPictureDisp pPreview = default(IPictureDisp);
			SwDmDocumentType nDocType = 0;
			SwDmDocumentOpenError nRetVal = 0;
			SwDmPreviewError nRetVal2 = 0;
			Image image = default(Image);

			//Access to interface
			swClassFact = new SwDMClassFactory();
			swDocMgr = (SwDMApplication)swClassFact.GetApplication("Post your code here");
			swDoc = (SwDMDocument)swDocMgr.GetDocument(FileName, nDocType, false, out nRetVal);
			swCfgMgr = swDoc.ConfigurationManager;
			swCfg = (SwDMConfiguration7)swCfgMgr.GetConfigurationByName("");
			pPreview = (IPictureDisp)swCfg.GetPreviewPNGBitmap(out nRetVal2);
			image = Support.IPictureDispToImage(pPreview);
			swDoc.CloseDoc();
		}

		private swDocumentTypes_e GetDocType(string FileName)
		{
			//ModelDoc2 swDoc = (ModelDoc2)swApp.ActiveDoc;
			//return (swDocumentTypes_e)swDoc.GetType();
		}

		public string GetActiveDocName()
		{
			//ModelDoc2 swDoc = (ModelDoc2)swApp.ActiveDoc;
			//return strModelFile = swDoc.GetPathName();
		}

	}
}
