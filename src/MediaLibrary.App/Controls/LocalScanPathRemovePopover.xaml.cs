using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MediaLibrary.App.Controls;

public partial class LocalScanPathRemovePopover : UserControl
{
    private ICommand? _attachedCommand;

    public LocalScanPathRemovePopover()
    {
        InitializeComponent();
        Loaded += (_, _) => AttachCommand(ConfirmCommand);
        Unloaded += (_, _) => DetachCommand(_attachedCommand);
    }

    public static readonly DependencyProperty ConfirmCommandProperty =
        DependencyProperty.Register(
            nameof(ConfirmCommand),
            typeof(ICommand),
            typeof(LocalScanPathRemovePopover),
            new PropertyMetadata(null, OnConfirmCommandChanged));

    public ICommand? ConfirmCommand
    {
        get => (ICommand?)GetValue(ConfirmCommandProperty);
        set => SetValue(ConfirmCommandProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter),
            typeof(object),
            typeof(LocalScanPathRemovePopover),
            new PropertyMetadata(null, OnCommandParameterChanged));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register(
            nameof(ButtonText),
            typeof(string),
            typeof(LocalScanPathRemovePopover),
            new PropertyMetadata("移除"));

    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(LocalScanPathRemovePopover),
            new PropertyMetadata("移除此扫描路径？"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(LocalScanPathRemovePopover),
            new PropertyMetadata("将删除该扫描路径配置，并可能影响对应软件记录的可见性。不会删除真实本地文件。"));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly DependencyProperty ConfirmButtonTextProperty =
        DependencyProperty.Register(
            nameof(ConfirmButtonText),
            typeof(string),
            typeof(LocalScanPathRemovePopover),
            new PropertyMetadata("移除"));

    public string ConfirmButtonText
    {
        get => (string)GetValue(ConfirmButtonTextProperty);
        set => SetValue(ConfirmButtonTextProperty, value);
    }

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(LocalScanPathRemovePopover),
            new PropertyMetadata(false));

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public static readonly DependencyProperty IsActionEnabledProperty =
        DependencyProperty.Register(
            nameof(IsActionEnabled),
            typeof(bool),
            typeof(LocalScanPathRemovePopover),
            new PropertyMetadata(true));

    public bool IsActionEnabled
    {
        get => (bool)GetValue(IsActionEnabledProperty);
        private set => SetValue(IsActionEnabledProperty, value);
    }

    private static void OnConfirmCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not LocalScanPathRemovePopover popover)
        {
            return;
        }

        popover.DetachCommand(args.OldValue as ICommand);
        if (popover.IsLoaded)
        {
            popover.AttachCommand(args.NewValue as ICommand);
        }

        popover.UpdateActionEnabled();
    }

    private static void OnCommandParameterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is LocalScanPathRemovePopover popover)
        {
            popover.UpdateActionEnabled();
        }
    }

    private void AttachCommand(ICommand? command)
    {
        if (command is null || ReferenceEquals(_attachedCommand, command))
        {
            return;
        }

        DetachCommand(_attachedCommand);
        command.CanExecuteChanged += ConfirmCommand_CanExecuteChanged;
        _attachedCommand = command;
    }

    private void DetachCommand(ICommand? command)
    {
        if (command is null || !ReferenceEquals(_attachedCommand, command))
        {
            return;
        }

        command.CanExecuteChanged -= ConfirmCommand_CanExecuteChanged;
        _attachedCommand = null;
    }

    private void ConfirmCommand_CanExecuteChanged(object? sender, EventArgs e)
    {
        UpdateActionEnabled();
    }

    private void UpdateActionEnabled()
    {
        IsActionEnabled = ConfirmCommand?.CanExecute(CommandParameter) ?? false;
        if (!IsActionEnabled)
        {
            IsOpen = false;
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateActionEnabled();
        if (IsActionEnabled)
        {
            IsOpen = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        IsOpen = false;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmCommand?.CanExecute(CommandParameter) != true)
        {
            UpdateActionEnabled();
            return;
        }

        IsOpen = false;
        ConfirmCommand.Execute(CommandParameter);
    }

    private void ConfirmPopup_Opened(object sender, EventArgs e)
    {
        CancelButton.Focus();
    }

    private void Popover_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsOpen = false;
            e.Handled = true;
        }
    }
}
