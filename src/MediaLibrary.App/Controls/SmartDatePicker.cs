using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MediaLibrary.App.Controls;

public sealed class SmartDatePicker : Control
{
    private readonly RelayActionCommand _previousMonthCommand;
    private readonly RelayActionCommand _nextMonthCommand;
    private readonly RelayActionCommand _selectDateCommand;
    private Popup? _popup;
    private ToggleButton? _switchButton;
    private Window? _outsideClickWindow;

    static SmartDatePicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SmartDatePicker),
            new FrameworkPropertyMetadata(typeof(SmartDatePicker)));
    }

    public SmartDatePicker()
    {
        _previousMonthCommand = new RelayActionCommand(_ => MoveMonth(-1));
        _nextMonthCommand = new RelayActionCommand(_ => MoveMonth(1));
        _selectDateCommand = new RelayActionCommand(SelectDate);
        CalendarDays = [];
        CurrentMonth = DateTime.Today;
        RefreshCalendarDays();
        UpdateDisplayText();
        UpdateCurrentMonthText();
        Unloaded += (_, _) => DetachOutsideClickHandler();
    }

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime?),
            typeof(SmartDatePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedDateChanged));

    public static readonly DependencyProperty CurrentMonthProperty =
        DependencyProperty.Register(
            nameof(CurrentMonth),
            typeof(DateTime),
            typeof(SmartDatePicker),
            new FrameworkPropertyMetadata(
                DateTime.Today,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnCurrentMonthChanged));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(
            nameof(IsDropDownOpen),
            typeof(bool),
            typeof(SmartDatePicker),
            new FrameworkPropertyMetadata(false, OnIsDropDownOpenChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(
            nameof(DisplayText),
            typeof(string),
            typeof(SmartDatePicker),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CurrentMonthTextProperty =
        DependencyProperty.Register(
            nameof(CurrentMonthText),
            typeof(string),
            typeof(SmartDatePicker),
            new PropertyMetadata(string.Empty));

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public DateTime CurrentMonth
    {
        get => (DateTime)GetValue(CurrentMonthProperty);
        set => SetValue(CurrentMonthProperty, value.Date);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        private set => SetValue(DisplayTextProperty, value);
    }

    public string CurrentMonthText
    {
        get => (string)GetValue(CurrentMonthTextProperty);
        private set => SetValue(CurrentMonthTextProperty, value);
    }

    public ObservableCollection<SmartDatePickerDay> CalendarDays { get; }

    public ICommand PreviousMonthCommand => _previousMonthCommand;

    public ICommand NextMonthCommand => _nextMonthCommand;

    public ICommand SelectDateCommand => _selectDateCommand;

    public override void OnApplyTemplate()
    {
        if (_switchButton is not null)
        {
            _switchButton.RemoveHandler(
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(SwitchButton_PreviewMouseLeftButtonDown));
        }

        base.OnApplyTemplate();

        _switchButton = GetTemplateChild("PART_Switch") as ToggleButton;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_switchButton is not null)
        {
            _switchButton.AddHandler(
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(SwitchButton_PreviewMouseLeftButtonDown),
                true);
        }
    }

    private static void OnSelectedDateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var picker = (SmartDatePicker)dependencyObject;
        if (e.NewValue is DateTime date)
        {
            picker.SetCurrentValue(CurrentMonthProperty, new DateTime(date.Year, date.Month, 1));
        }

        picker.UpdateDisplayText();
        picker.RefreshCalendarDays();
    }

    private static void OnCurrentMonthChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var picker = (SmartDatePicker)dependencyObject;
        picker.CoerceCurrentMonth();
        picker.RefreshCalendarDays();
        picker.UpdateCurrentMonthText();
    }

    private static void OnIsDropDownOpenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var picker = (SmartDatePicker)dependencyObject;
        var isOpen = e.NewValue is bool newValue && newValue;
        if (isOpen)
        {
            picker.AttachOutsideClickHandler();
        }
        else
        {
            picker.DetachOutsideClickHandler();
        }

        if (isOpen && picker.SelectedDate.HasValue)
        {
            var selected = picker.SelectedDate.Value;
            picker.SetCurrentValue(CurrentMonthProperty, new DateTime(selected.Year, selected.Month, 1));
        }
    }

    private void MoveMonth(int delta)
    {
        SetCurrentValue(CurrentMonthProperty, new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1).AddMonths(delta));
    }

    private void SelectDate(object? parameter)
    {
        if (parameter is not DateTime date)
        {
            return;
        }

        SetCurrentValue(SelectedDateProperty, date.Date);
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private void SwitchButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);
        e.Handled = true;
    }

    private void AttachOutsideClickHandler()
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(_outsideClickWindow, window))
        {
            return;
        }

        DetachOutsideClickHandler();
        _outsideClickWindow = window;
        if (_outsideClickWindow is not null)
        {
            _outsideClickWindow.PreviewMouseDown += OutsideClickWindow_PreviewMouseDown;
        }
    }

    private void DetachOutsideClickHandler()
    {
        if (_outsideClickWindow is not null)
        {
            _outsideClickWindow.PreviewMouseDown -= OutsideClickWindow_PreviewMouseDown;
            _outsideClickWindow = null;
        }
    }

    private void OutsideClickWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsDropDownOpen || IsSourceWithinPicker(e.OriginalSource as DependencyObject))
        {
            return;
        }

        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private bool IsSourceWithinPicker(DependencyObject? source)
    {
        var popupChild = _popup?.Child;
        for (var current = source; current is not null; current = GetDependencyParent(current))
        {
            if (ReferenceEquals(current, this)
                || ReferenceEquals(current, _switchButton)
                || ReferenceEquals(current, popupChild))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetDependencyParent(DependencyObject current)
    {
        if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(current);
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private void RefreshCalendarDays()
    {
        var month = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
        var firstDayOfMonth = month;
        var lastDayOfMonth = month.AddMonths(1).AddDays(-1);
        var startOffset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;
        var endOffset = 6 - (((int)lastDayOfMonth.DayOfWeek + 6) % 7);
        var firstVisibleDay = firstDayOfMonth.AddDays(-startOffset);
        var lastVisibleDay = lastDayOfMonth.AddDays(endOffset);
        var selectedDate = SelectedDate?.Date;
        var today = DateTime.Today;

        CalendarDays.Clear();
        for (var day = firstVisibleDay; day <= lastVisibleDay; day = day.AddDays(1))
        {
            CalendarDays.Add(new SmartDatePickerDay(
                day,
                day.Day.ToString(CultureInfo.CurrentCulture),
                day.Month == month.Month,
                selectedDate == day.Date,
                today == day.Date));
        }
    }

    private void CoerceCurrentMonth()
    {
        var value = CurrentMonth;
        if (value == default)
        {
            SetCurrentValue(CurrentMonthProperty, DateTime.Today);
            return;
        }

        var normalized = new DateTime(value.Year, value.Month, 1);
        if (normalized != value)
        {
            SetCurrentValue(CurrentMonthProperty, normalized);
        }
    }

    private void UpdateDisplayText()
    {
        DisplayText = SelectedDate.HasValue
            ? SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture)
            : "选择日期";
    }

    private void UpdateCurrentMonthText()
    {
        CurrentMonthText = CurrentMonth.ToString("yyyy年 M月", CultureInfo.CurrentCulture);
    }

    private sealed class RelayActionCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public RelayActionCommand(Action<object?> execute)
        {
            _execute = execute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed record SmartDatePickerDay(
    DateTime Date,
    string DayText,
    bool IsCurrentMonth,
    bool IsSelected,
    bool IsToday);
