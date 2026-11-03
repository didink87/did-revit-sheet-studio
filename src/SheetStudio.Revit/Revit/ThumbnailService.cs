using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;

namespace SheetStudio.Revit.Revit;

/// <summary>Small PNG previews of sheets, cached per sheet version so unchanged sheets are not re-exported.</summary>
public static class ThumbnailService
{
    public static string CacheDir(Document doc)
    {
        var key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(doc.PathName is { Length: > 0 } p ? p : doc.Title)))[..12];
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SheetStudio", "thumbs", key);
    }

    public static byte[]? Render(Document doc, string uniqueId, string cacheDir)
    {
        if (doc.GetElement(uniqueId) is not ViewSheet sheet) return null;
        Directory.CreateDirectory(cacheDir);
        var version = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(sheet.SheetNumber + "|" + sheet.Name)))[..8];
        var target = Path.Combine(cacheDir, $"{uniqueId}-{version}.png");
        if (File.Exists(target)) return File.ReadAllBytes(target);

        var prefix = Path.Combine(cacheDir, $"tmp-{Guid.NewGuid():N}");
        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = prefix,
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = 120,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG,
            ImageResolution = ImageResolution.DPI_72,
        };
        options.SetViewsAndSheets([sheet.Id]);
        doc.ExportImage(options);

        var produced = Directory.GetFiles(cacheDir, Path.GetFileName(prefix) + "*.png").FirstOrDefault();
        if (produced is null) return null;
        File.Move(produced, target, overwrite: true);
        return File.ReadAllBytes(target);
    }
}
