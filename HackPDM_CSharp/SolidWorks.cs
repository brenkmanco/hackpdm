using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;


namespace HackPDM
{
	class SWHelper
	{

		private Object swApp;

		// constructor
		public SWHelper()
		{

			// attach to running instance
			if (System.Diagnostics.Process.GetProcessesByName("sldworks").Length != 0)
			{
				swApp = System.Runtime.InteropServices.Marshal.GetActiveObject("SldWorks.Application");
			}
			else
			{
				swApp = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
			}

			// start background instance
			DialogResult dr = MessageBox.Show("Failed to get a SolidWorks instance",
				"Loading SW",
				MessageBoxButtons.OK,
				MessageBoxIcon.Exclamation,
				MessageBoxDefaultButton.Button1);

			return;

		}

	}
}
