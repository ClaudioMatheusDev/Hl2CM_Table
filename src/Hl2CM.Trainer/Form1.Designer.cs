#nullable enable
namespace Hl2CM.Trainer;

partial class Form1
{
    private System.ComponentModel.IContainer? components = null;

    // UI is built entirely in code (see Form1.cs -> BuildUi()) instead of the
    // designer surface, so this stays empty.
    private void InitializeComponent()
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            _trainer?.Dispose();
            foreach (var p in _candidateProcesses) p.Dispose();
        }
        base.Dispose(disposing);
    }
}
