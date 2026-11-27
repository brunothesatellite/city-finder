using CityFinder.Core.Models;

namespace CityFinder.App.Services;

public interface IDialogService
{
    bool ConfirmRename(RenamePlan plan, string folderPath);

    /// <summary>Shows the result and returns true when the user asks to undo this batch.</summary>
    bool ShowRenameResult(RenameExecutionResult result);

    bool ConfirmUndo(int count);

    void ShowInfo(string title, string message, DialogTone tone = DialogTone.Info);

    void ShowError(string title, string message);
}
