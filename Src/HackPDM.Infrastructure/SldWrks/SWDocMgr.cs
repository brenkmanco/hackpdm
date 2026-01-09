using System.Drawing;
using System.Net.Mime;
using HackPDM.Shared.GlobalData;
using SolidWorks.Interop.swdocumentmgr;


namespace HackPDM.Infrastructure.SldWrks;

public class SwDocMgr
{
    private static SwDMApplication _swDocMgr = default;
    public static SwDMApplication GetApplication() => _swDocMgr;

	// constructor
	public SwDocMgr(string strLicenseKey)
    {
        try
        {
            var swClassFact = new SwDMClassFactory();
            _swDocMgr = swClassFact.GetApplication(strLicenseKey);
        }
        catch (Exception ex)
        {
            // DialogResult dr = MessageBox.Show("Failed to get an instance of the SolidWorks Document Manager API: " + ex.Message,
            //     "Loading SW",
            //     buttons: MessageBoxButtons.OK,
            //     icon: MessageBoxIcon.Error);
        }
    
    }
	public void ReplaceDependencies(string filepath, string newReference, SwDmDocumentType docType)
	{
		ISwDMDocument _swDMDoc;
		_swDMDoc = _swDocMgr.GetDocument(filepath, docType, true, out var docResult);
		_swDMDoc.ReplaceReference(filepath, newReference);
	}
    public List<string[]>? GetDependencies(string fileName, bool deep=false, bool noInterrupt=false)
    {
        // external references for assembly files (GetAllExternalReferences4)
        // external references for part files (GetExternalFeatureReferences)
        SwDMDocument19 swDoc = default;
        SwDMSearchOption swSearchOpt = default;

        // returns list of string arrays
        // 0: short file name
        // 1: long file name
        List<string[]> listDepends = [];

        // get doc type
        SwDmDocumentType swDocType = GetTypeFromString(fileName);
        if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
        {
            return null;
        }

        // get the document
        SwDmDocumentOpenError nRetVal = 0;
        swDoc = (SwDMDocument19)_swDocMgr.GetDocument(fileName, swDocType, true, out nRetVal);
        if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
        {
            return !noInterrupt ? throw new Exception("Failed to open solidworks file: " + fileName) : null;
        }

        // get arrays of dependency info (one-dimensional)
        object oBrokenRefVar;
        object oIsVirtual;
        object oTimeStamp;
        swSearchOpt = _swDocMgr.GetSearchOptionObject();
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
                string[] strDepend = [strName, strFullName];
                listDepends.Add(strDepend);
            }

        }

        swDoc.CloseDoc();
        return listDepends;

    }

    public List<Tuple<string, string, string, object>> GetProperties(string fileName)
    {
        SwDMDocument swDoc = default(SwDMDocument);
        SwDMConfigurationMgr swCfgMgr = default(SwDMConfigurationMgr);

        // config name
        // property name
        // property type
        // resolved value (boxed object)
        List<Tuple<string, string, string, object>> lstProps = [];

        // get doc type
        SwDmDocumentType swDocType = GetTypeFromString(fileName);
        if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
        {
            return null;
        }

        // get the document
        SwDmDocumentOpenError nRetVal = 0;
        swDoc = (SwDMDocument)_swDocMgr.GetDocument(fileName, swDocType, true, out nRetVal);
        if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
        {
            throw new Exception("Failed to open solidworks file: " + fileName);
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
                        oPropValue = Convert.ToDateTime(oPropValue);
                        break;
                    case SwDmCustomInfoType.swDmCustomInfoNumber:
                        strPropType = "number";
                        oPropValue = Convert.ToDecimal(oPropValue);
                        break;
                    case SwDmCustomInfoType.swDmCustomInfoText:
                        strPropType = "text";
                        oPropValue = Convert.ToString(oPropValue);
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
            string[] strCfgPropNames = (string[])swCfg.GetCustomPropertyNames();
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
                        oPropValue = Convert.ToDateTime(oPropValue);
                        break;
                    case SwDmCustomInfoType.swDmCustomInfoNumber:
                        strPropType = "number";
                        oPropValue = Convert.ToDecimal(oPropValue);
                        break;
                    case SwDmCustomInfoType.swDmCustomInfoText:
                        strPropType = "text";
                        oPropValue = Convert.ToString(oPropValue);
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

    public static SwDmDocumentType GetTypeFromString(string modelPathName)
    {

        // ModelPathName = fully qualified name of file
        SwDmDocumentType nDocType = 0;

        // Determine type of SOLIDWORKS file based on file extension
        if (modelPathName.ToLower().EndsWith("sldprt"))
        {
            nDocType = SwDmDocumentType.swDmDocumentPart;
        }
        else if (modelPathName.ToLower().EndsWith("sldasm"))
        {
            nDocType = SwDmDocumentType.swDmDocumentAssembly;
        }
        else if (modelPathName.ToLower().EndsWith("slddrw"))
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