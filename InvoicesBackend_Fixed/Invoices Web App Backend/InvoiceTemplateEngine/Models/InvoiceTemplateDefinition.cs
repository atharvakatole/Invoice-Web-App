namespace InvoiceTemplateEngine.Models;

/// <summary>
/// Alignment of a piece of overlay text relative to its anchor point.
/// </summary>
public enum TextAlign
{
    Left = 0,
    Center = 1,
    Right = 2
}

/// <summary>
/// Position + style of a single dynamic field that gets drawn on top
/// of the user's uploaded PDF background.
/// Coordinates are stored in PDF space: origin bottom-left, units = points.
/// </summary>
public class FieldSpec
{
    public string Key { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double FontSize { get; set; } = 10;
    public bool Bold { get; set; }
    public string Color { get; set; } = "#1A1A1A"; // hex RGB
    public TextAlign Align { get; set; } = TextAlign.Left;

    /// <summary>Optional max width in points; text longer than this is shrunk to fit.</summary>
    public double? MaxWidth { get; set; }
}

/// <summary>One column of the recurring invoice line-items table.</summary>
public class TableColumnSpec
{
    public string Key { get; set; } = string.Empty; // ItemName | Quantity | Rate | Amount
    public double X { get; set; }
    public double Width { get; set; } = 100;
    public TextAlign Align { get; set; } = TextAlign.Left;
}

/// <summary>Layout of the repeating line-items table.</summary>
public class TableSpec
{
    /// <summary>Y position (PDF space, bottom-left origin) of the first item row.</summary>
    public double FirstRowY { get; set; }

    /// <summary>Vertical distance between successive rows (positive number).</summary>
    public double RowHeight { get; set; } = 18;

    /// <summary>Maximum number of rows that fit on a single page before overflow.</summary>
    public int MaxRowsPerPage { get; set; } = 10;

    public double FontSize { get; set; } = 9.5;
    public string Color { get; set; } = "#1A1A1A";

    public List<TableColumnSpec> Columns { get; set; } = new();
}

/// <summary>
/// A fully analyzed template: page geometry + every field/table position
/// detected from the user's uploaded PDF. Stored as JSON alongside the
/// original PDF bytes (used as the visual background for every page).
/// </summary>
public class InvoiceTemplateDefinition
{
    public double PageWidth { get; set; }
    public double PageHeight { get; set; }

    public Dictionary<string, FieldSpec> Fields { get; set; } = new();

    public TableSpec? Table { get; set; }

    /// <summary>Diagnostics: which known anchors were/weren't found during analysis.</summary>
    public List<string> DetectedAnchors { get; set; } = new();
    public List<string> MissingAnchors { get; set; } = new();
}
