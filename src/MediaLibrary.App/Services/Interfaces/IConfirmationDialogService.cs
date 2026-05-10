namespace MediaLibrary.App.Services.Interfaces;

public interface IConfirmationDialogService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText);
}
