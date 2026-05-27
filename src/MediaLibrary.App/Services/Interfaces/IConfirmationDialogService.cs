namespace MediaLibrary.App.Services.Interfaces;

public enum ConfirmationDialogVariant
{
    Normal,
    Warning,
    Danger
}

public interface IConfirmationDialogService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText,
        ConfirmationDialogVariant variant = ConfirmationDialogVariant.Normal);
}
