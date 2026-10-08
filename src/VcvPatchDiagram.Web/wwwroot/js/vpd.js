// Small browser helpers for the Blazor app: saving a generated file, and wiring the shared pan/zoom script.
window.vpdDownload = (fileName, mimeType, text) => {
  const url = URL.createObjectURL(new Blob([text], { type: mimeType }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
};

window.vpdScrollIntoView = element => element.scrollIntoView({ block: 'start' });

// Blazor side of panzoom.js: Ctrl+wheel zoom goes back to C# (PatchDiagramView.ZoomBy), which owns the zoom level.
window.vpdPanZoomDotNet = (scroller, view) =>
  window.vpdPanZoom(scroller, factor => view.invokeMethodAsync('ZoomBy', factor));
