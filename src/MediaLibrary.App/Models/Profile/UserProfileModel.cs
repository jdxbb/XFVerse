namespace MediaLibrary.App.Models.Profile;

public sealed class UserProfileModel
{
    public string UserName { get; set; } = string.Empty;

    public string Account { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Gender { get; set; } = "\u5973";

    public string Age { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public string AvatarPath { get; set; } = string.Empty;
}
