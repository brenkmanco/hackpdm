using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

using MessageBox = System.Windows.Forms.MessageBox;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

using SolidWorks.Interop.swdocumentmgr;


namespace HackPDM.Hack;

public class SwDocMgr
{

    private SwDMApplication _swDocMgr = default;

	// constructor
	public SwDocMgr(string strLicenseKey)
    {

        SwDMClassFactory swClassFact;
        swClassFact = new SwDMClassFactory();

        try
        {
            _swDocMgr = swClassFact.GetApplication(strLicenseKey);
        }
        catch (Exception ex)
        {
            DialogResult dr = MessageBox.Show("Failed to get an instance of the SolidWorks Document Manager API: " + ex.Message,
                "Loading SW",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
        }

    }
	public void ReplaceDependencies(string filepath, string newReference, SwDmDocumentType docType)
	{
		ISwDMDocument _swDMDoc;
		_swDMDoc = _swDocMgr.GetDocument(filepath, docType, true, out var docResult);
		_swDMDoc.ReplaceReference(filepath, newReference);
	}
    public List<string[]> GetDependencies(string fileName, bool deep=false, bool noInterrupt=false)
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
            if (!noInterrupt)
            {
                DialogResult dr = MessageBox.Show("Failed to open solidworks file: " + fileName,
                    "Loading SW File",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
            }
            return null;
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
            DialogResult dr = MessageBox.Show("Failed to open solidworks file: " + fileName,
                "Loading SW File",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
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

    public Bitmap GetPreview(string fileName, bool deep = false)
    {
        // external references for assembly files (GetAllExternalReferences4)
        // external references for part files (GetExternalFeatureReferences)
        SwDMDocument11 swDoc = default(SwDMDocument11);

        // get doc type
        SwDmDocumentType swDocType = GetTypeFromString(fileName);
        if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
        {
            return null;
        }

        // get the document
        SwDmDocumentOpenError nRetVal = 0;
        swDoc = (SwDMDocument11)_swDocMgr.GetDocument(fileName, swDocType, true, out nRetVal);
        if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
        {
            DialogResult dr = MessageBox.Show("Failed to open solidworks file: " + fileName,
                "Loading SW File",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
            return null;
        }

        SwDmPreviewError ePrevError = SwDmPreviewError.swDmPreviewErrorNone;
        try
        {
            byte[] bPreview = (byte[])swDoc.GetPreviewBitmapBytes(out ePrevError);
            MemoryStream ms = new(bPreview);
            Bitmap bmp = (Bitmap)Bitmap.FromStream(ms);

            // crop and pad the image to 640x480
            Bitmap bmp2 = ResizeImage(bmp);
            return bmp2;
        }
        catch
        {
            //DialogResult dr = MessageBox.Show("Failed to get solidworks preview image: " + FileName + ": " + ePrevError.ToString(),
            //    "Loading SW Preview",
            //    MessageBoxButtons.OK,
            //    MessageBoxIcon.Exclamation,
            //    MessageBoxDefaultButton.Button1);
            return null;
        }

    }

    //public stdole.IPictureDisp GetPreview2(string fileName, bool deep = false)
    //{
    //    // This method is only supported for in-process execution.  We need this the execute in a multi-threaded environment.
    //    // I tried starting a new instance of the SWDocMgr class for each thread, and
    //    // that seemed to work once, then didn't work, no explanation.
    //    // The error, when in a thread, is "Catastrophic Failure."
    //    // Use GetPreview() instead

    //    // get doc type
    //    SwDmDocumentType swDocType = GetTypeFromString(fileName);
    //    if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
    //    {
    //        return null;
    //    }

    //    // get the document
    //    SwDMDocument19 swDoc;
    //    SwDmDocumentOpenError nRetVal = 0;
    //    swDoc = (SwDMDocument19)_swDocMgr.GetDocument(fileName, swDocType, true, out nRetVal);
    //    if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
    //    {
    //        DialogResult dr = MessageBox.Show("Failed to open solidworks file: " + fileName,
    //            "Loading SW File",
    //            type: MessageBoxType.Ok,
    //            icon: MessageBoxIcon.Error);
    //        return null;
    //    }

    //    SwDmPreviewError ePrevError = SwDmPreviewError.swDmPreviewErrorNone;
    //    stdole.IPictureDisp ipicPreview;
    //    try
    //    {
    //        ipicPreview = swDoc.GetPreviewPNGBitmap(out ePrevError);
    //        swDoc.CloseDoc();
    //        return ipicPreview;
    //    }
    //    catch
    //    {
    //        //DialogResult dr = MessageBox.Show("Failed to get solidworks preview image: " + FileName + ": " + ePrevError.ToString(),
    //        //    "Loading SW Preview",
    //        //    MessageBoxButtons.OK,
    //        //    MessageBoxIcon.Exclamation,
    //        //    MessageBoxDefaultButton.Button1);
    //        swDoc.CloseDoc();
    //        return null;
    //    }

    //}

    SwDmDocumentType GetTypeFromString(string modelPathName)
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




    // methods for processing preview images

    public static byte[][] GetRgb(Bitmap bmp)
    {

        BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        IntPtr ptr = bmpData.Scan0;
        int numPixels = bmp.Width * bmp.Height;
        int numBytes = bmpData.Stride * bmp.Height;
        int padding = bmpData.Stride - bmp.Width * 3;
        int i = 0;
        int ct = 1;

        byte[] r = new byte[numPixels];
        byte[] g = new byte[numPixels];
        byte[] b = new byte[numPixels];
        byte[] rgb = new byte[numBytes];
        System.Runtime.InteropServices.Marshal.Copy(ptr, rgb, 0, numBytes);

        for (int x = 0; x < numBytes - 3; x += 3)
        {
            if (x == (bmpData.Stride * ct - padding))
            {
                x += padding; ct++;
            }
            r[i] = rgb[x];
            g[i] = rgb[x + 1];
            b[i] = rgb[x + 2];
            i++;
        }

        bmp.UnlockBits(bmpData);
        return [r, g, b];

    }

    public static Bitmap AutoCrop(Bitmap bmp)
    {

        //Get an array containing the R,G,B components of each pixel
        var pixels = GetRgb(bmp);

        int h = bmp.Height - 1;
        int w = bmp.Width;
        int top = 0;
        int bottom = h;
        int left = bmp.Width;
        int right = 0;
        int white = 0;
        int tolerance = 95; // 95%

        bool prevColor = false;
        for (int i = 0; i < pixels[0].Length; i++)
        {
            int x = (i % (w));
            int y = (int)(Math.Floor((decimal)(i / w)));
            int tol = 255 * tolerance / 100;

            if (pixels[0][i] >= tol && pixels[1][i] >= tol && pixels[2][i] >= tol)
            {
                white++; right = (x > right && white == 1) ? x : right;
            }
            else
            {
                left = (x < left && white >= 1) ? x : left;
                right = (x == w - 1 && white == 0) ? w - 1 : right;
                white = 0;
            }
            if (white == w)
            {
                top = (y - top < 3) ? y : top;
                bottom = (prevColor && x == w - 1 && y > top + 1) ? y : bottom;
            }
            left = (x == 0 && white == 0) ? 0 : left;
            bottom = (y == h && x == w - 1 && white != w && prevColor) ? h + 1 : bottom;
            if (x == w - 1)
            {
                prevColor = (white < w) ? true : false; white = 0;
            }
        }
        right = (right == 0) ? w : right;
        left = (left == w) ? 0 : left;

        //Crop the image
        if (bottom - top > 0)
        {
            Bitmap bmpCrop = bmp.Clone(new Rectangle(left, top, right - left + 1, bottom - top), bmp.PixelFormat);

            return (Bitmap)(bmpCrop);
        }
        else
        {
            return bmp;
        }

    }

    private static Bitmap ResizeImage(Bitmap bmpOrig)
    {

        bmpOrig.Save("C:\\temp\\test_orig.png", System.Drawing.Imaging.ImageFormat.Png);
        Image sourceImage = (Image)AutoCrop(bmpOrig);
        sourceImage.Save("C:\\temp\\test_cropped.png", System.Drawing.Imaging.ImageFormat.Png);

        //return (Bitmap)sourceImage;

        System.Drawing.Size sizeOut = new(640, 480);
        int sourceWidth = sourceImage.Width;
        int sourceHeight = sourceImage.Height;

        // percent size change
        float nPercent = 0;
        float nPercentW = 0;
        float nPercentH = 0;
        nPercentW = ((float)sizeOut.Width / (float)sourceWidth);
        nPercentH = ((float)sizeOut.Height / (float)sourceHeight);

        // keeping aspect ratio
        if (nPercentH < nPercentW)
            nPercent = nPercentH;
        else
            nPercent = nPercentW;


        int destWidth = (int)(sourceWidth * nPercent);
        int destHeight = (int)(sourceHeight * nPercent);
        int leftOffset = (int)((sizeOut.Width - destWidth) / 2);
        int topOffset = (int)((sizeOut.Height - destHeight) / 2);

        var destRect = new Rectangle(leftOffset, topOffset, destWidth, destHeight);


        Bitmap destImage = new(sizeOut.Width, sizeOut.Height);
        destImage.SetResolution(sourceImage.HorizontalResolution, sourceImage.VerticalResolution);

        using (var graphics = Graphics.FromImage((Image)destImage))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.White);

            //graphics.DrawImage(sourceImage, destRect, -leftOffset, -topOffset, sourceImage.Width, sourceImage.Height, GraphicsUnit.Pixel);
            graphics.DrawImage(sourceImage, destRect, 0, 0, sourceImage.Width, sourceImage.Height, GraphicsUnit.Pixel);

            //using (var wrapMode = new System.Drawing.Imaging.ImageAttributes())
            //{
            //    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
            //    graphics.DrawImage(sourceImage, destRect, leftOffset, topOffset, sourceImage.Width, sourceImage.Height, GraphicsUnit.Pixel, wrapMode);
            //}
        }
        destImage.Save("C:\\temp\\test_resized.png", System.Drawing.Imaging.ImageFormat.Png);

        return (Bitmap)destImage;

    }


}