using VcvPatchDiagram.Core.Catalog;

namespace VcvPatchDiagram.Core.Render;

/// <summary>The stylesheet and script embedded in this assembly, shared by every renderer.</summary>
public static class Resources
{
    public static string Css { get; } = PortCatalog.ReadResource("render.diagram.css") ?? "";

    /// <summary>Static HTML script: pan/zoom and library preview helpers first, then the page wiring that uses them.</summary>
    public static string Script { get; } = (PortCatalog.ReadResource("render.panzoom.js") ?? "") + "\n" + (PortCatalog.ReadResource("render.library.js") ?? "") + "\n" + (PortCatalog.ReadResource("render.diagram.js") ?? "");

    /// <summary>Readable zoom range: below 70 % box titles get too small, so you pan instead.</summary>
    public const double MinZoom = 0.7;

    public const double MaxZoom = 2.0;

    /// <summary>On a phone (see vpdCompact in panzoom.js): low enough to see a whole diagram, pinching in to read it.</summary>
    public const double MinZoomCompact = 0.35;
}