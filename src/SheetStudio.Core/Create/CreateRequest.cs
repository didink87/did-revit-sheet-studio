using SheetStudio.Core.ViewModels;

namespace SheetStudio.Core.Create;

public enum Kertas { A3, A2, A1 }

public enum ViewMode { Tanpa, PakaiYangAda, BuatBaru }

/// <summary>JenisView: Denah, Plafon, Tampak, Potongan, Drafting, Legend, Schedule, TigaD, Tanpa.</summary>
public sealed record ViewSpec(ViewMode Mode, string? ExistingViewUniqueId, string? JenisView, string? LevelName, int Skala, string? ViewTemplate);

public sealed record CreateRequest(CreateName Name, Kertas Kertas, string? TitleBlockType, IReadOnlyList<ViewSpec> Views);

/// <summary>One batch line. Null KindId = free title; empty NomorDiminta = automatic; Skala 0 = kind default.</summary>
public sealed record BatchRow(string? KindId, string Subjek, string? LevelName, string? NomorDiminta, string? Judul, int Skala, Kertas Kertas, bool BuatView);

public enum PlanStatus { Baru, SudahAda, Duplikat, Galat }

public sealed record PlannedSheet(BatchRow Row, CreateRequest? Request, PlanStatus Status, string? Pesan);

public sealed record Rect(double X, double Y, double W, double H);
