using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Mime;
using HackPDM.Shared.GlobalData;
using SolidWorks.Interop.swdocumentmgr;


namespace HackPDM.Infrastructure.SldWrks;

/// <summary>Windows only: reads SolidWorks files through the licensed SolidWorks Document Manager (COM).</summary>
public class SwDocMgr : ISwDocumentReader
{
    private static SwDMApplication _swDocMgr = default;
    /// <summary>Returns the Document Manager application created by the last successful <see cref="SwDocMgr(string)"/> call.</summary>
    /// <returns>The shared application, or null when none could be created (wrong licence key, Document Manager not installed, not Windows).</returns>
    public static SwDMApplication GetApplication() => _swDocMgr;

	// constructor
	/// <summary>Starts the SolidWorks Document Manager with a licence key and stores it for every instance.</summary>
	/// <param name="strLicenseKey">The Document Manager licence key (the server's SwApi setting).</param>
	/// <remarks>Failures are swallowed: check <see cref="GetApplication"/> afterwards. Does nothing outside Windows.</remarks>
	public SwDocMgr(string strLicenseKey)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return;
            var swClassFact = new SwDMClassFactory();
            _swDocMgr = swClassFact.GetApplication(strLicenseKey);
        }
        catch( Exception ) {
            // DialogResult dr = MessageBox.Show("Failed to get an instance of the SolidWorks Document Manager API: " + ex.Message,
            //     "Loading SW",
            //     buttons: MessageBoxButtons.OK,
            //     icon: MessageBoxIcon.Error);
        }
    
    }
    /// <summary>Lists the non-virtual external references of a document through the Document Manager (<c>GetAllExternalReferences4</c>).</summary>
    /// <param name="fileName">Path of the SolidWorks file; opened read-only.</param>
    /// <param name="deep">Ignored.</param>
    /// <param name="noInterrupt">When true, a document that can't be opened yields null instead of an exception.</param>
    /// <returns><c>[file name, stored full path]</c> pairs; null when the file isn't a SolidWorks document, has no reference list, or (with <paramref name="noInterrupt"/>) can't be opened.</returns>
    /// <exception cref="Exception">The document can't be opened and <paramref name="noInterrupt"/> is false.</exception>
    /// <exception cref="NullReferenceException">The Document Manager was not started (see <see cref="GetApplication"/>).</exception>
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
        SwDmDocumentType swDocType = SwDocumentTypes.Of(fileName);
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

    /// <summary>Reads file-level and per-configuration custom properties through the Document Manager.</summary>
    /// <param name="fileName">Path of the SolidWorks file; opened read-only.</param>
    /// <returns>(configuration, name, type, value) tuples as described on <see cref="ISwDocumentReader.GetProperties"/>; drawings have file-level properties only. Null when the file isn't a SolidWorks document.</returns>
    /// <exception cref="Exception">The document can't be opened.</exception>
    /// <exception cref="NullReferenceException">The Document Manager was not started (see <see cref="GetApplication"/>).</exception>
    public List<Tuple<string, string, string, object>>? GetProperties(string fileName)
    {
        SwDMDocument swDoc = default(SwDMDocument);
        SwDMConfigurationMgr swCfgMgr = default(SwDMConfigurationMgr);

        // config name
        // property name
        // property type
        // resolved value (boxed object)
        List<Tuple<string, string, string, object>> lstProps = [];

        // get doc type
        SwDmDocumentType swDocType = SwDocumentTypes.Of(fileName);
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

                var (strPropType, oValue) = ConvertProperty(nPropType, oPropValue);
                lstProps.Add(Tuple.Create<string, string, string, object>("", strPropName, strPropType, oValue));
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

                var (strPropType, oValue) = ConvertProperty(nPropType, oPropValue);
                lstProps.Add(Tuple.Create<string, string, string, object>(strConfigName, strPropName, strPropType, oValue));

            }

        }

        swDoc.CloseDoc();
        return lstProps;

    }

    /// <summary>A Document Manager property's HackPDM type name and its value converted to match.</summary>
    private static (string Type, object Value) ConvertProperty(SwDmCustomInfoType type, object value) => type switch
    {
        SwDmCustomInfoType.swDmCustomInfoDate => ("date", Convert.ToDateTime(value)),
        SwDmCustomInfoType.swDmCustomInfoNumber => ("number", Convert.ToDecimal(value)),
        SwDmCustomInfoType.swDmCustomInfoText => ("text", Convert.ToString(value) ?? ""),
        SwDmCustomInfoType.swDmCustomInfoYesOrNo => ("yesno", value.Equals("Yes")),
        _ => ("", value),
    };

    /// <summary>Reads the saved preview straight from the file through <see cref="SwFileReader"/>; needs no Document Manager call.</summary>
    /// <param name="fileName">Path of the SolidWorks file.</param>
    /// <returns>The preview as PNG bytes, or null when there is none or the file can't be read.</returns>
    public byte[]? GetPreviewPng(string fileName) => new SwFileReader().GetPreviewPng(fileName);





    

    
    


}