using SheetStudio.Core.Create;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Revit;

public sealed partial class RevitStudioGateway
{
    public Task<CreateReport> CreateAsync(IReadOnlyList<CreateRequest> reqs, IProgress<(int Done, int Total, string Title)> progress, CancellationToken ct) =>
        throw new NotSupportedException("Pembuatan lembar menyusul (Task 10)");

    public Task<CreateReport> DuplicateAsync(IReadOnlyList<string> ids, DuplicateMode mode) =>
        throw new NotSupportedException("Duplikat menyusul (Task 10)");

    public Task<byte[]?> ThumbnailAsync(string uniqueId) => Task.FromResult<byte[]?>(null);
}
