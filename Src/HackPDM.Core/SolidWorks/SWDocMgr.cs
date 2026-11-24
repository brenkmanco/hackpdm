using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

using HackPDM.Core.Configuration;

using SolidWorks.Interop.swdocumentmgr;

namespace HackPDM.Core.SolidWorks;

/// <summary>Windows only: reads SolidWorks files through the licensed SolidWorks Document Manager (COM).</summary>
public class SwDocMgr : ISwDocumentReader
{
    private static SwDMApplication? _swDocMgr;
    private static readonly SwFileReader PreviewReader = new();
    /// <summary>Returns the Document Manager application created by the last successful <see cref="SwDocMgr(string)"/> call.</summary>
    /// <returns>The shared application, or null when none could be created (wrong licence key, Document Manager not installed, not Windows).</returns>
    public static SwDMApplication? GetApplication() => _swDocMgr;

    private static SwDMApplication Started
        => _swDocMgr ?? throw new InvalidOperationException("The SolidWorks Document Manager was not started.");

	// constructor
	/// <summary>Starts the SolidWorks Document Manager with a licence key and stores it for every instance.</summary>
	/// <param name="strLicenseKey">The Document Manager licence key (the server's SwApi setting).</param>
	/// <remarks>Failures are swallowed: check <see cref="GetApplication"/> afterwards. Does nothing outside Windows.</remarks>
	public SwDocMgr(string strLicenseKey)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(strLicenseKey)) return;
            var swClassFact = new SwDMClassFactory();
            _swDocMgr = swClassFact.GetApplication(strLicenseKey);
        }
        catch( Exception ex )
        {
            HackLogger.Debug( nameof( SwDocMgr ), $"Could not initialize SolidWorks Document Manager: {ex.Message}", ex );
        }
    
    }
    /// <summary>Lists the non-virtual external references of a document through the Document Manager (<c>GetAllExternalReferences4</c>).</summary>
    /// <param name="fileName">Path of the SolidWorks file; opened read-only.</param>
    /// <param name="deep">Ignored.</param>
    /// <param name="noInterrupt">When true, a document that can't be opened yields null instead of an exception.</param>
    /// <returns><c>[file name, stored full path]</c> pairs (empty when none); null when the file isn't a SolidWorks document or (with <paramref name="noInterrupt"/>) can't be opened.</returns>
    /// <exception cref="Exception">The document can't be opened and <paramref name="noInterrupt"/> is false.</exception>
    /// <exception cref="InvalidOperationException">The Document Manager was not started (see <see cref="GetApplication"/>).</exception>
    public List<string[]>? GetDependencies(string fileName, bool deep=false, bool noInterrupt=false)
    {
        // get doc type
        SwDmDocumentType swDocType = SwDocumentTypes.Of(fileName);
        if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
        {
            return null;
        }

        SwDMDocument? rawDoc = null;
        try
        {
            SwDmDocumentOpenError nRetVal = 0;
            rawDoc = Started.GetDocument(fileName, swDocType, true, out nRetVal);
            if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal || rawDoc is not SwDMDocument19 swDoc)
            {
                return !noInterrupt ? throw new Exception("Failed to open solidworks file: " + fileName) : null;
            }

            SwDMSearchOption swSearchOpt = Started.GetSearchOptionObject();
            object? rawDepends = swDoc.GetAllExternalReferences4(swSearchOpt, out _, out object? oIsVirtual, out _);
            return ExtractDependencies(rawDepends, oIsVirtual);
        }
        catch (Exception ex) when (noInterrupt && ex is COMException or InvalidCastException)
        {
            HackLogger.Debug(nameof(SwDocMgr), $"Could not read SolidWorks dependencies for '{fileName}': {ex.Message}", ex);
            return null;
        }
        finally
        {
            rawDoc?.CloseDoc();
        }
    }

    internal static List<string[]> ExtractDependencies(object? rawDepends, object? oIsVirtual)
    {
        List<string[]> listDepends = [];
        if (rawDepends is not Array depArray || depArray.Length == 0)
            return listDepends;

        listDepends.EnsureCapacity(depArray.Length);
        for (int i = 0; i < depArray.Length; i++)
        {
            // only return non-virtual components
            if (IsVirtualAt(oIsVirtual, i))
                continue;

            string? strFullName = depArray.GetValue(i) as string;
            if (string.IsNullOrEmpty(strFullName))
                continue;

            // short file name with extension
            string strName = SwReferenceResolver.FileName(strFullName);
            listDepends.Add([strName, strFullName]);
        }
        return listDepends;
    }

    internal static bool IsVirtualAt(object? oIsVirtual, int index)
    {
        if (index < 0 || oIsVirtual is null)
            return false;
        if (oIsVirtual is bool[] bools)
            return index < bools.Length && bools[index];
        if (oIsVirtual is short[] shorts)
            return index < shorts.Length && shorts[index] != 0;
        if (oIsVirtual is Array arr && index < arr.Length)
        {
            return arr.GetValue(index) switch
            {
                bool b => b,
                short s => s != 0,
                int i => i != 0,
                sbyte sb => sb != 0,
                byte by => by != 0,
                ushort us => us != 0,
                uint ui => ui != 0,
                long l => l != 0,
                IConvertible conv => SafeToBool(conv),
                _ => false,
            };
        }
        return false;
    }

    private static bool SafeToBool(IConvertible conv)
    {
        try { return conv.ToBoolean(CultureInfo.InvariantCulture); }
        catch { return false; }
    }

    internal static IEnumerable<string> AsStrings(object? value)
    {
        if (value is string[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (!string.IsNullOrEmpty(arr[i]))
                    yield return arr[i];
            }
            yield break;
        }
        if (value is Array genericArr)
        {
            for (int i = 0; i < genericArr.Length; i++)
            {
                if (genericArr.GetValue(i) is string s && !string.IsNullOrEmpty(s))
                    yield return s;
            }
        }
    }

    /// <summary>Reads file-level and per-configuration custom properties through the Document Manager.</summary>
    /// <param name="fileName">Path of the SolidWorks file; opened read-only.</param>
    /// <returns>(configuration, name, type, value) tuples as described on <see cref="ISwDocumentReader.GetProperties"/>; drawings have file-level properties only. Null when the file isn't a SolidWorks document.</returns>
    /// <exception cref="Exception">The document can't be opened.</exception>
    /// <exception cref="InvalidOperationException">The Document Manager was not started (see <see cref="GetApplication"/>).</exception>
    public List<Tuple<string, string, string, object>>? GetProperties(string fileName)
    {
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
        SwDMDocument? swDoc = Started.GetDocument(fileName, swDocType, true, out nRetVal);
        try
        {
            if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal || swDoc == null)
            {
                throw new Exception("Failed to open solidworks file: " + fileName);
            }

            // get document custom properties (file level properties)
            foreach (string strPropName in AsStrings(swDoc.GetCustomPropertyNames()))
            {
                SwDmCustomInfoType nPropType = 0;
                object oPropValue = swDoc.GetCustomProperty(strPropName, out nPropType);

                var (strPropType, oValue) = ConvertProperty(nPropType, oPropValue);
                lstProps.Add(Tuple.Create<string, string, string, object>("", strPropName, strPropType, oValue));
            }

            // drawings don't have configurations, so we can return here
            if (swDocType == SwDmDocumentType.swDmDocumentDrawing)
            {
                return lstProps;
            }

            // parts and assemblies have configurations
            SwDMConfigurationMgr? swCfgMgr = swDoc.ConfigurationManager;
            if (swCfgMgr != null)
            {
                foreach (string strConfigName in AsStrings(swCfgMgr.GetConfigurationNames()))
                {
                    if (swCfgMgr.GetConfigurationByName(strConfigName) is not SwDMConfiguration swCfg)
                        continue;

                    foreach (string strPropName in AsStrings(swCfg.GetCustomPropertyNames()))
                    {
                        SwDmCustomInfoType nPropType = 0;
                        object oPropValue = swCfg.GetCustomProperty(strPropName, out nPropType);

                        var (strPropType, oValue) = ConvertProperty(nPropType, oPropValue);
                        lstProps.Add(Tuple.Create<string, string, string, object>(strConfigName, strPropName, strPropType, oValue));
                    }
                }
            }

            return lstProps;
        }
        finally
        {
            swDoc?.CloseDoc();
        }
    }

    /// <summary>A Document Manager property's HackPDM type name and its value converted to match.</summary>
    private static (string Type, object Value) ConvertProperty(SwDmCustomInfoType type, object? value)
    {
        if (value is null)
            return ("", "");
        return type switch
        {
            SwDmCustomInfoType.swDmCustomInfoDate => value is DateTime dt
                ? ("date", dt)
                : DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                    ? ("date", parsed)
                    : ("", value),
            SwDmCustomInfoType.swDmCustomInfoNumber => decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal dec)
                ? ("number", dec)
                : ("", value),
            SwDmCustomInfoType.swDmCustomInfoText => ("text", Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""),
            SwDmCustomInfoType.swDmCustomInfoYesOrNo => ("yesno", value is bool b ? b : string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), "Yes", StringComparison.OrdinalIgnoreCase)),
            _ => ("", value),
        };
    }

    /// <summary>Reads the saved preview straight from the file through <see cref="SwFileReader"/>; needs no Document Manager call.</summary>
    /// <param name="fileName">Path of the SolidWorks file.</param>
    /// <returns>The preview as PNG bytes, or null when there is none or the file can't be read.</returns>
    public byte[]? GetPreviewPng(string fileName) => PreviewReader.GetPreviewPng(fileName);





    

    
    


}
