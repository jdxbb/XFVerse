using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MediaLibrary.App.Models.Profile;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Helpers;
using Microsoft.Win32;

namespace MediaLibrary.App.Views.Dialogs;

public partial class UserProfileDialogWindow : Window
{
    private const int SignatureMaxLength = 48;
    private const int AvatarRenderSize = 512;
    private const int ToastDurationMilliseconds = 1600;

    private readonly IUserProfileService _userProfileService;
    private readonly List<string> _temporaryAvatarPaths = [];

    private UserProfileModel _profile = CreateDefaultProfile();
    private string _draftAvatarPath = string.Empty;
    private bool _isEditMode;
    private int _toastVersion;

    public UserProfileDialogWindow(IUserProfileService userProfileService)
    {
        _userProfileService = userProfileService;
        InitializeComponent();
        DataObject.AddPastingHandler(SignatureTextBox, SignatureTextBox_OnPaste);
        RefreshProfileDisplay();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadProfileAsync();
        BringDialogToFront();
        EditSaveButton.Focus();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        DeleteDiscardedTemporaryAvatars(string.Empty);
    }

    private async Task LoadProfileAsync()
    {
        try
        {
            _profile = await _userProfileService.LoadAsync();
            _draftAvatarPath = _profile.AvatarPath;
            RefreshProfileDisplay();
        }
        catch
        {
            ShowToast("资料读取失败，已显示默认本地资料。", ProfileToastKind.Warning);
        }
    }

    private void BringDialogToFront()
    {
        if (Owner is { WindowState: WindowState.Minimized } owner)
        {
            owner.WindowState = WindowState.Normal;
        }

        Topmost = true;
        Activate();
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                Topmost = false;
                Activate();
            }),
            DispatcherPriority.ApplicationIdle);
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

    private async void EditSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isEditMode)
        {
            await SaveEditorValuesAsync();
            return;
        }

        LoadEditorValues();
        SetEditMode(true);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SetEditMode(bool isEditMode)
    {
        _isEditMode = isEditMode;
        ProfileDisplayGrid.Visibility = isEditMode ? Visibility.Collapsed : Visibility.Visible;
        ProfileEditGrid.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
        EditIconPath.Visibility = isEditMode ? Visibility.Collapsed : Visibility.Visible;
        DoneIconPath.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
        EditSaveButton.ToolTip = isEditMode ? "完成" : "编辑";
        RefreshAvatarDisplay();
    }

    private void LoadEditorValues()
    {
        _draftAvatarPath = _profile.AvatarPath;
        UserNameTextBox.Text = _profile.UserName;
        AccountTextBox.Text = _profile.Account;
        PhoneTextBox.Text = _profile.PhoneNumber;
        SignatureTextBox.Text = _profile.Signature;
        GenderTextBox.Text = _profile.Gender;
        AgeTextBox.Text = _profile.Age;
        EmailTextBox.Text = _profile.Email;
        RefreshAvatarDisplay();
    }

    private async Task SaveEditorValuesAsync()
    {
        var previousAvatarPath = _profile.AvatarPath;
        var updated = new UserProfileModel
        {
            UserName = TrimToLength(UserNameTextBox.Text, 24),
            Account = TrimToLength(AccountTextBox.Text, 32),
            PhoneNumber = TrimToLength(PhoneTextBox.Text, 24),
            Signature = TrimToLength(SignatureTextBox.Text, SignatureMaxLength),
            Gender = TrimToLength(GenderTextBox.Text, 16),
            Age = TrimToLength(AgeTextBox.Text, 8),
            Email = TrimToLength(EmailTextBox.Text, 64),
            AvatarPath = File.Exists(_draftAvatarPath) ? _draftAvatarPath : string.Empty
        };
        var hasChanges = !AreSameProfile(_profile, updated);

        try
        {
            await _userProfileService.SaveAsync(updated);
            _profile = updated;
            RefreshProfileDisplay();
            SetEditMode(false);
            DeleteDiscardedTemporaryAvatars(updated.AvatarPath);
            DeletePreviousManagedAvatar(previousAvatarPath, updated.AvatarPath);
            ShowToast(hasChanges ? "资料已保存。" : "没有检测到修改，资料已保持当前状态。", ProfileToastKind.Success);
        }
        catch
        {
            ShowToast("资料保存失败，请稍后重试。", ProfileToastKind.Error);
        }
    }

    private void RefreshProfileDisplay()
    {
        SummaryNameBlock.Text = _profile.UserName;
        DisplayUserNameBlock.Text = _profile.UserName;
        DisplayAccountBlock.Text = _profile.Account;
        DisplayPhoneBlock.Text = _profile.PhoneNumber;
        DisplaySignatureBlock.Text = _profile.Signature;
        DisplayGenderBlock.Text = _profile.Gender;
        DisplayAgeBlock.Text = _profile.Age;
        DisplayEmailBlock.Text = _profile.Email;
        _draftAvatarPath = _profile.AvatarPath;
        RefreshAvatarDisplay();
    }

    private void RefreshAvatarDisplay()
    {
        var activeAvatarPath = _isEditMode ? _draftAvatarPath : _profile.AvatarPath;
        var avatarImage = LoadBitmapImage(activeAvatarPath);
        var hasAvatar = avatarImage is not null;

        AvatarImageBrush.ImageSource = avatarImage;
        AvatarImageEllipse.Visibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed;
        AvatarInitialBlock.Visibility = hasAvatar || (_isEditMode && string.IsNullOrWhiteSpace(activeAvatarPath))
            ? Visibility.Collapsed
            : Visibility.Visible;
        AvatarInitialBlock.Text = BuildAvatarInitial(_profile.UserName);

        AvatarBackgroundEllipse.Fill = (Brush)FindResource(
            _isEditMode && !hasAvatar ? "BrushSurfaceAlt" : "BrushAccent");

        AvatarRemoveButton.Visibility = _isEditMode && hasAvatar ? Visibility.Visible : Visibility.Collapsed;
        AvatarAddButton.Visibility = _isEditMode && !hasAvatar ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RemoveAvatarButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        _draftAvatarPath = string.Empty;
        RefreshAvatarDisplay();
    }

    private void ChooseAvatarButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "选择头像图片",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有文件 (*.*)|*.*",
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var avatarPath = CreateCircularAvatar(dialog.FileName);
            _temporaryAvatarPaths.Add(avatarPath);
            _draftAvatarPath = avatarPath;
            RefreshAvatarDisplay();
        }
        catch
        {
            ShowToast("头像文件无法读取，请选择 PNG、JPG、BMP、GIF 或 TIFF 图片。", ProfileToastKind.Warning);
        }
    }

    private static string CreateCircularAvatar(string sourceFilePath)
    {
        var source = new BitmapImage();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        source.UriSource = new Uri(sourceFilePath, UriKind.Absolute);
        source.EndInit();
        source.Freeze();

        var side = Math.Min(source.PixelWidth, source.PixelHeight);
        if (side <= 0)
        {
            throw new InvalidOperationException("Invalid avatar image size.");
        }

        var crop = new CroppedBitmap(
            source,
            new Int32Rect(
                Math.Max(0, (source.PixelWidth - side) / 2),
                Math.Max(0, (source.PixelHeight - side) / 2),
                side,
                side));
        crop.Freeze();

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var brush = new ImageBrush(crop)
            {
                Stretch = Stretch.UniformToFill
            };
            context.DrawEllipse(
                brush,
                null,
                new Point(AvatarRenderSize / 2d, AvatarRenderSize / 2d),
                AvatarRenderSize / 2d,
                AvatarRenderSize / 2d);
        }

        var target = new RenderTargetBitmap(
            AvatarRenderSize,
            AvatarRenderSize,
            96,
            96,
            PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();

        var avatarDirectory = GetAvatarDirectory();
        Directory.CreateDirectory(avatarDirectory);
        var avatarPath = Path.Combine(avatarDirectory, $"avatar-{DateTime.UtcNow:yyyyMMddHHmmssfff}.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = new FileStream(avatarPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
        return avatarPath;
    }

    private void SignatureTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = WouldExceedSignatureLimit(e.Text);
    }

    private void SignatureTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (WouldExceedSignatureLimit(text))
        {
            e.CancelCommand();
            ShowToast("个性签名最多显示两行，超出内容不会写入。", ProfileToastKind.Warning);
        }
    }

    private bool WouldExceedSignatureLimit(string input)
    {
        var currentLength = SignatureTextBox.Text.Length - SignatureTextBox.SelectionLength;
        return currentLength + input.Length > SignatureMaxLength || input.Contains('\r') || input.Contains('\n');
    }

    private void ShowToast(string message, ProfileToastKind kind)
    {
        var version = ++_toastVersion;
        ProfileToastTextBlock.Text = message;
        switch (kind)
        {
            case ProfileToastKind.Warning:
                ProfileToastBorder.Background = (Brush)FindResource("ProfileToastWarningBackgroundBrush");
                ProfileToastBorder.BorderBrush = (Brush)FindResource("BrushWarningBorder");
                ProfileToastTextBlock.Foreground = (Brush)FindResource("BrushWarningForeground");
                break;
            case ProfileToastKind.Error:
                ProfileToastBorder.Background = (Brush)FindResource("ProfileToastErrorBackgroundBrush");
                ProfileToastBorder.BorderBrush = (Brush)FindResource("BrushErrorBorder");
                ProfileToastTextBlock.Foreground = (Brush)FindResource("BrushErrorForeground");
                break;
            default:
                ProfileToastBorder.Background = (Brush)FindResource("ProfileToastSuccessBackgroundBrush");
                ProfileToastBorder.BorderBrush = (Brush)FindResource("BrushSuccessBorder");
                ProfileToastTextBlock.Foreground = (Brush)FindResource("BrushSuccessForeground");
                break;
        }

        ProfileToastBorder.Visibility = Visibility.Visible;
        _ = HideToastAfterDelayAsync(version);
    }

    private async Task HideToastAfterDelayAsync(int version)
    {
        await Task.Delay(ToastDurationMilliseconds);
        if (version == _toastVersion)
        {
            ProfileToastBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void DeleteDiscardedTemporaryAvatars(string savedAvatarPath)
    {
        foreach (var path in _temporaryAvatarPaths.ToArray())
        {
            if (!PathsEqual(path, savedAvatarPath))
            {
                TryDeleteManagedAvatar(path);
            }

            _temporaryAvatarPaths.Remove(path);
        }
    }

    private static void DeletePreviousManagedAvatar(string previousAvatarPath, string nextAvatarPath)
    {
        if (!PathsEqual(previousAvatarPath, nextAvatarPath))
        {
            TryDeleteManagedAvatar(previousAvatarPath);
        }
    }

    private static void TryDeleteManagedAvatar(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsManagedAvatarPath(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Avatar cleanup is best-effort and should not block profile editing.
        }
    }

    private static bool IsManagedAvatarPath(string path)
    {
        var avatarDirectory = Path.GetFullPath(GetAvatarDirectory());
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(avatarDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAvatarDirectory()
    {
        return Path.Combine(AppPaths.GetAppDataDirectory(), "UserProfile", "Avatars");
    }

    private static BitmapImage? LoadBitmapImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildAvatarInitial(string userName)
    {
        return string.IsNullOrWhiteSpace(userName)
            ? "X"
            : userName.Trim()[..1].ToUpperInvariant();
    }

    private static string TrimToLength(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static bool AreSameProfile(UserProfileModel left, UserProfileModel right)
    {
        return string.Equals(left.UserName, right.UserName, StringComparison.Ordinal)
               && string.Equals(left.Account, right.Account, StringComparison.Ordinal)
               && string.Equals(left.PhoneNumber, right.PhoneNumber, StringComparison.Ordinal)
               && string.Equals(left.Email, right.Email, StringComparison.Ordinal)
               && string.Equals(left.Gender, right.Gender, StringComparison.Ordinal)
               && string.Equals(left.Age, right.Age, StringComparison.Ordinal)
               && string.Equals(left.Signature, right.Signature, StringComparison.Ordinal)
               && PathsEqual(left.AvatarPath, right.AvatarPath);
    }

    private static bool PathsEqual(string? left, string? right)
    {
        return string.Equals(
            string.IsNullOrWhiteSpace(left) ? string.Empty : Path.GetFullPath(left),
            string.IsNullOrWhiteSpace(right) ? string.Empty : Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static UserProfileModel CreateDefaultProfile()
    {
        return new UserProfileModel
        {
            UserName = "James",
            Account = "local_user"
        };
    }

    private enum ProfileToastKind
    {
        Success,
        Warning,
        Error
    }
}
