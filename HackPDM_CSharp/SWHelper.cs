﻿using System;
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

        private SolidWorks.Interop.sldworks.SldWorks swApp;
        private SolidWorks.Interop.sldworks.SldWorks swRunApp;

        // constructor
        public SWHelper()
        {

            // ---------------------------------------------------
            // acquire a running process

            //if (System.Diagnostics.Process.GetProcessesByName("sldworks").Length > 1)
            //{
            //    DialogResult dr = MessageBox.Show("Multiple SolidWorks Instances Detected",
            //        "Loading SW",
            //        MessageBoxButtons.OK,
            //        MessageBoxIcon.Exclamation,
            //        MessageBoxDefaultButton.Button1);
            //    return;
            //}

            //if (System.Diagnostics.Process.GetProcessesByName("sldworks").Length < 1)
            //{
            //    DialogResult dr = MessageBox.Show("SolidWorks is not running",
            //        "Loading SW",
            //        MessageBoxButtons.OK,
            //        MessageBoxIcon.Exclamation,
            //        MessageBoxDefaultButton.Button1);
            //    return;
            //}

            //try
            //{
            //    // get a running instance
            //    swApp = (SldWorks.SldWorks)System.Runtime.InteropServices.Marshal.GetActiveObject("SldWorks.Application");

            //    // or try this way, as recommended by someone on stackoverflow.com
            //    //swApp = (SldWorks.SldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
            //}
            //catch (Exception ex)
            //{
            //    DialogResult dr = MessageBox.Show("Failed to get a SolidWorks instance: " + ex.Message,
            //        "Loading SW",
            //        MessageBoxButtons.OK,
            //        MessageBoxIcon.Exclamation,
            //        MessageBoxDefaultButton.Button1);
            //}

            // ---------------------------------------------------
            // start a new process

            //try
            //{
            //    swApp = new SldWorks.SldWorks();
            //    swApp.Visible = false;
            //    swApp.DocumentVisible(false, (int)swDocumentTypes_e.swDocASSEMBLY);
            //    swApp.DocumentVisible(false, (int)swDocumentTypes_e.swDocDRAWING);
            //    swApp.DocumentVisible(false, (int)swDocumentTypes_e.swDocPART);
            //    SldWorks.Frame pFrame = swApp.Frame();
            //    pFrame.KeepInvisible = true;
            //    swApp.UserControl = false;
            //}
            //catch (Exception ex)
            //{
            //    DialogResult dr = MessageBox.Show("Failed to get a SolidWorks instance: " + ex.Message,
            //        "Loading SW",
            //        MessageBoxButtons.OK,
            //        MessageBoxIcon.Exclamation,
            //        MessageBoxDefaultButton.Button1);
            //}

        }

        public List<string[]> GetDependencies(string FileName, bool Deep=false)
        {
            // check for solidworks instance
            if (swApp==null) return null;

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

        public List<Tuple<string, string, string, object>> GetProperties(string FileName)
        {
            // check for solidworks instance
            if (swApp == null) return null;
            if (!File.Exists(FileName)) return null;

            // config name
            // property name
            // property type
            // resolved value (boxed object)
            List<Tuple<string, string, string, object>> lstProps = new List<Tuple<string, string, string, object>>();

            // get doc type
            swDocumentTypes_e swDocType = GetTypeFromString(FileName);
            if (swDocType == swDocumentTypes_e.swDocNONE)
            {
                return null;
            }

            // document open options
            // swOpenDocOptions_e is a bitmask enumerator
            // use bitwise "and" to select multiple options
            swOpenDocOptions_e swOpenDocOptions = swOpenDocOptions_e.swOpenDocOptions_Silent &
                swOpenDocOptions_e.swOpenDocOptions_DontLoadHiddenComponents &
                swOpenDocOptions_e.swOpenDocOptions_LoadLightweight &
                swOpenDocOptions_e.swOpenDocOptions_Silent &
                swOpenDocOptions_e.swOpenDocOptions_ReadOnly;

            // try to load the model file
            int intWarnings = 0;
            int intErrors = 0;
            ModelDoc2 swModelDoc;
            try
            {
                swModelDoc = swApp.OpenDoc6(FileName, (int)swDocType, (int)swOpenDocOptions, "", ref intErrors, ref intWarnings);
            }
            catch
            {
                return null;
            }
            ModelDocExtension swDocExt = swModelDoc.Extension;

            // get list of configs
            //string[] strConfgNames = (string[])swModelDoc.GetConfigurationNames();
            List<string> lstConfigNames = new List<string>();
            if (swDocType != swDocumentTypes_e.swDocDRAWING)
            {
                lstConfigNames = new List<string>((string[])swModelDoc.GetConfigurationNames());
            }
            lstConfigNames.Add("");
            foreach (string strConfigName in lstConfigNames)
            {

                CustomPropertyManager swCustPropMgr = swDocExt.get_CustomPropertyManager(strConfigName);

                object oPropNames = null;
                object oPropTypes = null;
                object oPropValues = null;
                //object oResolved = null;

                //swCustPropMgr.GetAll2(ref oPropNames, ref oPropTypes, ref oPropValues, ref oResolved);
                swCustPropMgr.GetAll(ref oPropNames, ref oPropTypes, ref oPropValues);

                if (oPropNames == null)
                {
                    continue;
                }

                // get list of properties for this config
                int intPropCount = ((string[])oPropNames).Length;
                for (int i = 0; i < intPropCount; i++ )
                {
                    // property name
                    string strPropName = ((string[])oPropNames)[i]; // property name

                    // property type
                    string strPropType = "";
                    swCustomInfoType_e eCustInfoType = ((swCustomInfoType_e[])oPropTypes)[i]; // property type
                    switch (eCustInfoType)
                    {
                        case swCustomInfoType_e.swCustomInfoDate:
                            strPropType = "date";
                            break;
                        case swCustomInfoType_e.swCustomInfoDouble:
                            strPropType = "number";
                            break;
                        case swCustomInfoType_e.swCustomInfoNumber:
                            strPropType = "number";
                            break;
                        case swCustomInfoType_e.swCustomInfoText:
                            strPropType = "text";
                            break;
                        case swCustomInfoType_e.swCustomInfoUnknown:
                            strPropType = "";
                            break;
                        case swCustomInfoType_e.swCustomInfoYesOrNo:
                            strPropType = "yesno";
                            break;
                    }

                    // property value
                    //object oPropValue = ((object[])oResolved)[i]; // resolved value, with GetAll2()
                    object oPropValue = ((object[])oPropValues)[i]; // resolved value, with GetAll()
                    oPropValue.GetType();
                    if (oPropValue.GetType() == typeof(System.Double))
                    {
                        oPropValue = (Decimal)oPropValue;
                    }

                    // add to list
                    lstProps.Add(Tuple.Create<string, string, string, object>(strConfigName, strPropName, strPropType, oPropValue));

                }

            }

            swModelDoc = null;
            swApp.CloseDoc(FileName);
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
        //~SWHelper()
        //{
        //    // cleanup statements
        //    try
        //    {
        //        swApp.ExitApp();
        //    }
        //    catch (Exception ex)
        //    {
        //        //MessageBox.Show(
        //        //    "Failed to close SolidWorks instance: " + ex.Message,
        //        //    "Failed Closing SolidWork",
        //        //    MessageBoxButtons.OK
        //        //);
        //    }
        //}

    }
}
