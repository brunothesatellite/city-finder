using CityFinder.App.ViewModels;
using CityFinder.Core.Models;
using CityFinder.Infrastructure.Updates;

namespace CityFinder.App.Services;

public interface IDialogService
{
    bool ConfirmRename(RenamePlan plan, string folderPath);

    /// <summary>Shows the result and returns true when the user asks to undo this batch.</summary>
    bool ShowRenameResult(RenameExecutionResult result);

    bool ConfirmUndo(int count);

    BulkEditResult? ShowBulkEdit(
        BulkEditKind kind,
        IReadOnlyList<PhotoViewModel> selected,
        IReadOnlyList<PhotoViewModel> visible);

    bool ConfirmBulkOverwrite(BulkEditKind kind, int count, string value);

    void ShowPhotoDetails(PhotoViewModel photo);

    /// <summary>Shows an available update and returns true when it was postponed.</summary>
    bool ShowUpdateAvailable(ReleaseInfo release, UpdateService updates);

    void ShowInfo(string title, string message, DialogTone tone = DialogTone.Info);

    void ShowError(string title, string message);
}
