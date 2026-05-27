using System.Windows;
using System.Windows.Input;

namespace MediaLibrary.App.Views.Dialogs;

public partial class UserProfileDialogWindow : Window
{
    private readonly LocalProfileDraft _profile = new()
    {
        UserName = "James",
        Account = "local_user",
        PhoneNumber = string.Empty,
        Email = string.Empty,
        Gender = string.Empty,
        Age = string.Empty,
        Signature = string.Empty
    };

    private bool _isEditMode;

    public UserProfileDialogWindow()
    {
        InitializeComponent();
        RefreshProfileDisplay();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        EditSaveButton.Focus();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse state changes during the initial click.
        }
    }

    private void EditSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isEditMode)
        {
            SaveEditorValues();
            SetEditMode(false);
            ProfileStatusBlock.Text = "资料已更新为当前弹窗中的本地展示值。";
            return;
        }

        LoadEditorValues();
        SetEditMode(true);
        ProfileStatusBlock.Text = "编辑内容仅用于本地资料形态展示，不会触发登录、注册或云同步。";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void SetEditMode(bool isEditMode)
    {
        _isEditMode = isEditMode;
        ProfileDisplayGrid.Visibility = isEditMode ? Visibility.Collapsed : Visibility.Visible;
        ProfileEditGrid.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
        EditSaveButton.Content = isEditMode ? "确定" : "编辑资料";
    }

    private void LoadEditorValues()
    {
        UserNameTextBox.Text = _profile.UserName;
        AccountTextBox.Text = _profile.Account;
        PhoneTextBox.Text = _profile.PhoneNumber;
        EmailTextBox.Text = _profile.Email;
        GenderTextBox.Text = _profile.Gender;
        AgeTextBox.Text = _profile.Age;
        SignatureTextBox.Text = _profile.Signature;
    }

    private void SaveEditorValues()
    {
        _profile.UserName = UserNameTextBox.Text.Trim();
        _profile.Account = AccountTextBox.Text.Trim();
        _profile.PhoneNumber = PhoneTextBox.Text.Trim();
        _profile.Email = EmailTextBox.Text.Trim();
        _profile.Gender = GenderTextBox.Text.Trim();
        _profile.Age = AgeTextBox.Text.Trim();
        _profile.Signature = SignatureTextBox.Text.Trim();
        RefreshProfileDisplay();
    }

    private void RefreshProfileDisplay()
    {
        SummaryNameBlock.Text = _profile.UserName;
        AvatarInitialBlock.Text = BuildAvatarInitial(_profile.UserName);

        DisplayUserNameBlock.Text = _profile.UserName;
        DisplayAccountBlock.Text = _profile.Account;
        DisplayPhoneBlock.Text = _profile.PhoneNumber;
        DisplayEmailBlock.Text = _profile.Email;
        DisplayGenderBlock.Text = _profile.Gender;
        DisplayAgeBlock.Text = _profile.Age;
        DisplaySignatureBlock.Text = _profile.Signature;
    }

    private static string BuildAvatarInitial(string userName)
    {
        return string.IsNullOrWhiteSpace(userName)
            ? "X"
            : userName.Trim().Substring(0, 1).ToUpperInvariant();
    }

    private sealed class LocalProfileDraft
    {
        public string UserName { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
