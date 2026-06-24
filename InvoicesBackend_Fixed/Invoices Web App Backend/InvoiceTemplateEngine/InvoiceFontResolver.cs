using PdfSharpCore.Fonts;

namespace InvoiceTemplateEngine;

/// <summary>
/// Cross-platform font resolver for PdfSharpCore. The library ships with the
/// "Work Sans" font (SIL Open Font License) embedded as a resource so that
/// invoice generation works identically on Windows, Linux and macOS without
/// relying on system-installed fonts.
/// </summary>
public class InvoiceFontResolver : IFontResolver
{
    public const string FamilyName = "InvoiceSans";

    public string DefaultFontName => FamilyName;

    private static readonly Lazy<byte[]> RegularBytes = new(() => LoadResource("WorkSans-Regular.ttf"));
    private static readonly Lazy<byte[]> BoldBytes = new(() => LoadResource("WorkSans-Bold.ttf"));

    private static byte[] LoadResource(string fileName)
    {
        var assembly = typeof(InvoiceFontResolver).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            throw new InvalidOperationException($"Embedded font resource '{fileName}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public byte[] GetFont(string faceName)
    {
        return faceName == $"{FamilyName}#Bold" ? BoldBytes.Value : RegularBytes.Value;
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var faceName = isBold ? $"{FamilyName}#Bold" : $"{FamilyName}#Regular";
        return new FontResolverInfo(faceName);
    }

    /// <summary>Registers this resolver globally. Safe to call multiple times.</summary>
    public static void EnsureRegistered()
    {
        if (GlobalFontSettings.FontResolver is InvoiceFontResolver) return;
        GlobalFontSettings.FontResolver = new InvoiceFontResolver();
    }
}
