using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Runtime.InteropServices;
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

            //try
            //{
            //    // get a running instance
            //    swRunApp = (SldWorks.SldWorks)System.Runtime.InteropServices.Marshal.GetActiveObject("SldWorks.Application");
            //}
            //catch { }

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
                if (!strFullName.Contains(strTempPath))
                {
                    string[] strDepend = new string[3] {strName, strFullName, strReadOnly};
                    listDepends.Add(strDepend);
                }

            }

            return listDepends;

        }

        public List<string[]> GetProperties(string FileName)
        {
            List<string[]> lstProps = new List<string[]>();

            // get doc type
            swDocumentTypes_e swDocType = GetTypeFromString(FileName);

            // document open options
            // swOpenDocOptions_e is a bitmask enumerator
            // use bitwise "and" to select multiple options
            swOpenDocOptions_e swOpenDocOptions = swOpenDocOptions_e.swOpenDocOptions_Silent &
                swOpenDocOptions_e.swOpenDocOptions_DontLoadHiddenComponents &
                swOpenDocOptions_e.swOpenDocOptions_LoadLightweight &
                swOpenDocOptions_e.swOpenDocOptions_ReadOnly;

            // try to load the model file
            int intWarnings = 0;
            int intErrors = 0;
            SldWorks.ModelDoc2 swModelDoc;
            try
            {
                swModelDoc = swApp.OpenDoc6(FileName, (int)swDocType, (int)swOpenDocOptions, "", ref intErrors, ref intWarnings);
            }
            catch (Exception ex)
            {
                return null;
            }
            SldWorks.ModelDocExtension swDocExt = swModelDoc.Extension;

            // get list of configs
			string[] strConfgNames = (string[])swModelDoc.GetConfigurationNames();
            foreach (string strConfigName in strConfgNames)
            {

                SldWorks.CustomPropertyManager swCustPropMgr = swDocExt.get_CustomPropertyManager(strConfigName);

                object oPropNames;
                object oPropTypes;
                object oPropValues;
                object oResolved;

                //swCustPropMgr.GetAll2(ref oPropNames, ref oPropTypes, ref oPropValues, ref oResolved);
                swCustPropMgr.GetAll(ref oPropNames, ref oPropTypes, ref oPropValues);

                int intPropCount = ((string[])oPropNames).Length;
                string[] strProps = new string[5];
                for (int i = 0; i < intPropCount; i++ )
                {
                    strProps[0] = ((string[])oPropNames)[i]; // property name
                    strProps[1] = ((string[])oPropTypes)[i]; // property type
                    strProps[2] = ((string[])oPropValues)[i]; // value with GetAll(), definition with GetAll2()
                    //strProps[3] = ((string[])oResolved)[i]; // resolved value
                    strProps[3] = ((string[])oPropValues)[i]; // resolved value
                    strProps[4] = strConfigName;

                }

                lstProps.Add(strProps);

            }

            return lstProps;

        }

        swDocumentTypes_e GetTypeFromString(string ModelPathName)
        {

            //------------------------------------------------------------
            //--     strModelPathName = fully qualified name of file
            //------------------------------------------------------------
            string strModelName;
            string strFileExt;
            swDocumentTypes_e swDocType;

            strModelName = ModelPathName.Substring(ModelPathName.LastIndexOf("\\") + 1, ModelPathName.Length - ModelPathName.LastIndexOf("\\") - 1);
            strFileExt = ModelPathName.Substring(ModelPathName.LastIndexOf(".") + 1, ModelPathName.Length - ModelPathName.LastIndexOf(".") - 1);

            switch (strFileExt)
            {
                case "SLDASM":
                    swDocType = swDocumentTypes_e.swDocASSEMBLY;
                    break;
                case "SLDPRT":
                    swDocType = swDocumentTypes_e.swDocPART;
                    break;
                case "SLDDRW":
                    swDocType = swDocumentTypes_e.swDocDRAWING;
                    break;
                default:
                    swDocType = swDocumentTypes_e.swDocNONE;
                    break;
            }

            return (swDocType);

        }			

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
                //MessageBox.Show(
                //    "Failed to close SolidWorks instance: " + ex.Message,
                //    "Failed Closing SolidWork",
                //    MessageBoxButtons.OK
                //);
            }
        }

    }
}
