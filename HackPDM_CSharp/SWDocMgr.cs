using System;
using System.Collections.Generic;
using System.Text;
using SolidWorks.Interop.swdocumentmgr;

using System.Drawing;
using System.Windows.Forms;


namespace HackPDM
{
    class SWDocMgr
    {

        private SwDMApplication swDocMgr = default(SwDMApplication);

        // constructor
        public SWDocMgr(string strLicenseKey)
        {

            SwDMClassFactory swClassFact = default(SwDMClassFactory);
            swClassFact = new SwDMClassFactory();

            try
            {
                swDocMgr = swClassFact.GetApplication(strLicenseKey);
            }
            catch (Exception ex)
            {
                DialogResult dr = MessageBox.Show("Failed to get an instance of the SolidWorks Document Manager API: " + ex.Message,
                    "Loading SW",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
            }

        }

        public List<string[]> GetDependencies(string FileName, bool Deep=false)
        {
            // external references for assembly files (GetAllExternalReferences4)
            // external references for part files (GetExternalFeatureReferences)
            SwDMDocument13 swDoc = default(SwDMDocument13);
            SwDMSearchOption swSearchOpt = default(SwDMSearchOption);

            // returns list of string arrays
            // 0: short file name
            // 1: long file name
            List<string[]> listDepends = new List<string[]>();

            // get doc type
            SwDmDocumentType swDocType = GetTypeFromString(FileName);
            if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
            {
                return null;
            }

            // get the document
            SwDmDocumentOpenError nRetVal = 0;
            swDoc = (SwDMDocument13)swDocMgr.GetDocument(FileName, swDocType, true, out nRetVal);
            if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
            {
                DialogResult dr = MessageBox.Show("Failed to open solidworks file: " + FileName,
                    "Loading SW File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                return null;
            }

            // get arrays of dependency info (one-dimensional)
            object oBrokenRefVar;
            object oIsVirtual;
            object oTimeStamp;
            swSearchOpt = swDocMgr.GetSearchOptionObject();
            string[] varDepends = (string[])swDoc.GetAllExternalReferences4(swSearchOpt, out oBrokenRefVar, out oIsVirtual, out oTimeStamp);
            if (varDepends == null) return null;

            Boolean[] blnIsVirtual = (Boolean[])oIsVirtual;
            for (int i = 0; i < varDepends.Length; i++)
            {

                // file name with absolute path
                string strFullName = varDepends[i];

                // short file name with extension
                string strName = strFullName.Substring(strFullName.LastIndexOf("\\") + 1);

                // only return non-virtual components
                if ((bool)blnIsVirtual[i] != true)
                {
                    string[] strDepend = new string[2] {strName, strFullName};
                    listDepends.Add(strDepend);
                }

            }

            swDoc.CloseDoc();
            return listDepends;

        }

        public List<Tuple<string, string, string, object>> GetProperties(string FileName)
        {
            SwDMDocument swDoc = default(SwDMDocument);
            SwDMConfigurationMgr swCfgMgr = default(SwDMConfigurationMgr);

            // config name
            // property name
            // property type
            // resolved value (boxed object)
            List<Tuple<string, string, string, object>> lstProps = new List<Tuple<string, string, string, object>>();

            // get doc type
            SwDmDocumentType swDocType = GetTypeFromString(FileName);
            if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
            {
                return null;
            }

            // get the document
            SwDmDocumentOpenError nRetVal = 0;
            swDoc = (SwDMDocument)swDocMgr.GetDocument(FileName, swDocType, true, out nRetVal);
            if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
            {
                DialogResult dr = MessageBox.Show("Failed to open solidworks file: " + FileName,
                    "Loading SW File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
            }

            // get document custom properties (file level properties)
            string[] strDocPropNames = (string[])swDoc.GetCustomPropertyNames();
            if (strDocPropNames != null)
            {
                foreach (string strPropName in strDocPropNames)
                {

                    SwDmCustomInfoType nPropType = 0;
                    object oPropValue = swDoc.GetCustomProperty(strPropName, out nPropType);

                    // property type
                    string strPropType = "";
                    switch (nPropType)
                    {
                        case SwDmCustomInfoType.swDmCustomInfoDate:
                            strPropType = "date";
                            oPropValue = (DateTime)oPropValue;
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoNumber:
                            strPropType = "number";
                            oPropValue = (Decimal)oPropValue;
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoText:
                            strPropType = "text";
                            oPropValue = (String)oPropValue;
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoYesOrNo:
                            strPropType = "yesno";
                            oPropValue = (Boolean)oPropValue;
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoUnknown:
                            strPropType = "";
                            break;
                    }

                    // add to list
                    lstProps.Add(Tuple.Create<string, string, string, object>("", strPropName, strPropType, oPropValue));
                }
            }

            // drawings don't have configurations, so we can return here
            if (swDocType == SwDmDocumentType.swDmDocumentDrawing)
            {
                return lstProps;
            }

            // parts and assemblies have configurations
            // get a list of configs
            List<string> lstConfigNames;
            swCfgMgr = swDoc.ConfigurationManager;
            lstConfigNames = new List<string>((string[])swCfgMgr.GetConfigurationNames());

            // get properties
            foreach (string strConfigName in lstConfigNames)
            {

                SwDMConfiguration swCfg = (SwDMConfiguration)swCfgMgr.GetConfigurationByName(strConfigName);
                string[] strCfgPropNames = swCfg.GetCustomPropertyNames();
                if (strCfgPropNames==null) continue;

                foreach (string strPropName in strCfgPropNames)
                {
                    SwDmCustomInfoType nPropType = 0;
                    object oPropValue = swCfg.GetCustomProperty(strPropName, out nPropType);

                    // property type
                    string strPropType = "";
                    switch (nPropType)
                    {
                        case SwDmCustomInfoType.swDmCustomInfoDate:
                            strPropType = "date";
                            oPropValue = (DateTime)oPropValue;
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoNumber:
                            strPropType = "number";
                            oPropValue = (Decimal)oPropValue;
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoText:
                            strPropType = "text";
                            oPropValue = (String)oPropValue;
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoYesOrNo:
                            strPropType = "yesno";
                            oPropValue = oPropValue.Equals("Yes");
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoUnknown:
                            strPropType = "";
                            break;
                    }

                    // add to list
                    lstProps.Add(Tuple.Create<string, string, string, object>(strConfigName, strPropName, strPropType, oPropValue));

                }

            }

            swDoc.CloseDoc();
            return lstProps;

        }

        SwDmDocumentType GetTypeFromString(string ModelPathName)
        {

            // ModelPathName = fully qualified name of file
            SwDmDocumentType nDocType = 0;

            // Determine type of SOLIDWORKS file based on file extension
            if (ModelPathName.ToLower().EndsWith("sldprt"))
            {
                nDocType = SwDmDocumentType.swDmDocumentPart;
            }
            else if (ModelPathName.ToLower().EndsWith("sldasm"))
            {
                nDocType = SwDmDocumentType.swDmDocumentAssembly;
            }
            else if (ModelPathName.ToLower().EndsWith("slddrw"))
            {
                nDocType = SwDmDocumentType.swDmDocumentDrawing;
            }
            else
            {
                // Not a SOLIDWORKS file
                nDocType = SwDmDocumentType.swDmDocumentUnknown;
            }

            return nDocType;

        }

    }
}
