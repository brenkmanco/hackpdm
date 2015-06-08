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

        private SldWorks.SldWorks swApp;
        private SldWorks.SldWorks swRunApp;
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

        public List<Tuple<string, string, string, string, object>> GetProperties(string FileName)
        {
            // check for solidworks instance
            if (swApp == null) return null;
            SwDMDocument17 swDoc = default(SwDMDocument17);
            SwDMConfigurationMgr swCfgMgr = default(SwDMConfigurationMgr);
            SwDMConfiguration14 swCfg = default(SwDMConfiguration14);

            // config name
            // property name
            // property type
            // definition
            // resolved value (boxed object)
            List<Tuple<string, string, string, string, object>> lstProps = new List<Tuple<string, string, string, string, object>>();

            // get doc type
            SwDmDocumentType swDocType = GetTypeFromString(FileName);
            if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
            {
                return null;
            }

            // get the document
            SwDmDocumentOpenError nRetVal = 0;
            swDoc = (SwDMDocument17)swDocMgr.GetDocument(FileName, swDocType, false, out nRetVal);
            if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
            {
                DialogResult dr = MessageBox.Show("Failed to open solidworks file: " + FileName,
                    "Loading SW File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
            }

            // get document custom properties (file level properties)
            string[] vDocPropNames = (string[])swDoc.GetCustomPropertyNames();
            foreach (string vCustPropName in vCustPropNameArr)
            {
                sCustPropStr = swDoc.GetCustomProperty(vCustPropName, out nPropType);
                Debug.Print("   Prefaced      = " + vCustPropName + " <" + nPropType + "> = " + sCustPropStr);

                sCustPropStr = swDoc.GetCustomProperty2(vCustPropName, out nPropType);
                Debug.Print("   Not prefaced  = " + vCustPropName + " <" + nPropType + "> = " + sCustPropStr);

                Debug.Print("");
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

                object oPropNames = null;
                object oPropTypes = null;
                object oPropValues = null;
                //object oResolved = null;

                string[] strPropNames = swCfg.GetCustomPropertyNames();
                foreach (string strPropName in strPropNames)
                {
                    SwDmCustomInfoType nPropType = 0;
                    var vCustPropVal = swCfg.GetCustomProperty2(strPropName, out nPropType);

                    // property type
                    string strPropType = "";
                    switch (nPropType)
                    {
                        case SwDmCustomInfoType.swDmCustomInfoDate:
                            strPropType = "date";
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoNumber:
                            strPropType = "number";
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoText:
                            strPropType = "text";
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoUnknown:
                            strPropType = "";
                            break;
                        case SwDmCustomInfoType.swDmCustomInfoYesOrNo:
                            strPropType = "yesno";
                            break;
                    }

                    // property definition
                    string strPropDef = vCustPropVal; // definition

                    // property value
                    //object oPropValue = ((object[])oResolved)[i]; // resolved value, with GetAll2()
                    object oPropValue = vCustPropVal; // resolved value, with GetAll()
                    oPropValue.GetType();
                    if (oPropValue.GetType() == typeof(System.Double))
                    {
                        oPropValue = (Decimal)oPropValue;
                    }

                    // add to list
                    lstProps.Add(Tuple.Create<string, string, string, string, object>(strConfigName, strPropName, strPropType, strPropDef, oPropValue));

                }

            }

            return lstProps;

        }

        SwDmDocumentType GetTypeFromString(string ModelPathName)
        {

            //------------------------------------------------------------
            //--     ModelPathName = fully qualified name of file
            //------------------------------------------------------------
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
