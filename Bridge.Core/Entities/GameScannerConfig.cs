using Bridge.Core.Enums;

namespace Bridge.Core.Entities;

/// <summary>Matches Playnite's GameScannerConfig field-for-field — see PROJECT_FOUNDATION.md §28.1 for the field list and §28.4 for exactly what each field controls during a scan.</summary>
public class GameScannerConfig : DatabaseObject
{
    public Guid EmulatorId { get; set; }
    public string EmulatorProfileId { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public bool ScanSubfolders { get; set; } = true;
    public bool ScanInsideArchives { get; set; } = true;
    public bool ExcludeOnlineFiles { get; set; }
    public bool UseSimplifiedOnlineFileScan { get; set; }
    public bool ImportWithRelativePaths { get; set; } = true;
    public bool MergeRelatedFiles { get; set; } = true;
    public List<string> ExcludedFiles { get; set; } = [];
    public List<string> ExcludedDirectories { get; set; } = [];
    public List<string> CrcExcludeFileTypes { get; set; } = [];
    public Guid OverridePlatformId { get; set; }
    public ScannerPlayActionMode PlayActionMode { get; set; } = ScannerPlayActionMode.UseScannerSettings;
}
