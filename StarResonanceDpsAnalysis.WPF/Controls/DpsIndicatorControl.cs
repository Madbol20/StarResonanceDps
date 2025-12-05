using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarResonanceDpsAnalysis.WPF.Controls;

public class DpsIndicatorControl : Control
{
    // Percentage value in range 0..Maximum. Use double for proper binding with ProgressBar-like behavior.
    public static readonly DependencyProperty PercentageProperty = DependencyProperty.Register(
        nameof(Percentage), typeof(double), typeof(DpsIndicatorControl),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPercentageChanged));

    // AnimatedPercentage: used by the template to animate visual changes when Percentage updates
    public static readonly DependencyProperty AnimatedPercentageProperty = DependencyProperty.Register(
        nameof(AnimatedPercentage), typeof(double), typeof(DpsIndicatorControl),
        new PropertyMetadata(0d));

    // Maximum value used to scale Percentage (default 100)
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(DpsIndicatorControl),
        new PropertyMetadata(100d));

    // Background brush for the track
    public static readonly DependencyProperty IndicatorBackgroundProperty = DependencyProperty.Register(
        nameof(IndicatorBackground), typeof(Brush), typeof(DpsIndicatorControl),
        new PropertyMetadata(Brushes.LightGray));

    // Foreground / fill brush for the indicator
    public static readonly DependencyProperty IndicatorForegroundProperty = DependencyProperty.Register(
        nameof(IndicatorForeground), typeof(Brush), typeof(DpsIndicatorControl),
        new PropertyMetadata(Brushes.DodgerBlue));

    // CornerRadius for rounded track/indicator
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(DpsIndicatorControl),
        new PropertyMetadata(new CornerRadius(4)));

    // Template used to render overlay content on top of the progress indicator.
    public static readonly DependencyProperty OverlayTemplateProperty = DependencyProperty.Register(
        nameof(OverlayTemplate), typeof(DataTemplate), typeof(DpsIndicatorControl),
        new PropertyMetadata(null));

    // Content to be passed to the overlay template.
    public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
        nameof(OverlayContent), typeof(object), typeof(DpsIndicatorControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty PopupTemplateProperty = DependencyProperty.Register(
        nameof(PopupTemplate), typeof(DataTemplate), typeof(DpsIndicatorControl),
        new PropertyMetadata(default(DataTemplate?), OnPopupTemplateChanged));

    public static readonly DependencyProperty PopupContentProperty = DependencyProperty.Register(
        nameof(PopupContent), typeof(object), typeof(DpsIndicatorControl),
        new PropertyMetadata(default, OnPopupContentChanged));

    public static readonly DependencyProperty TrackOpacityProperty = DependencyProperty.Register(
        nameof(TrackOpacity), typeof(double), typeof(DpsIndicatorControl), new PropertyMetadata(default(double)));

    static DpsIndicatorControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DpsIndicatorControl),
            new FrameworkPropertyMetadata(typeof(DpsIndicatorControl)));
    }

    public DpsIndicatorControl()
    {
        // ? 修改: MouseEnter时触发tooltip数据刷新
        MouseEnter += OnMouseEnterRefreshTooltip;
      MouseLeave += (s, e) => Debug.WriteLine("[DpsIndicatorControl] MouseLeave");
    }

    public double TrackOpacity
    {
        get => (double)GetValue(TrackOpacityProperty);
        set => SetValue(TrackOpacityProperty, value);
    }

    public double Percentage
    {
        get => (double)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    public double AnimatedPercentage
    {
        get => (double)GetValue(AnimatedPercentageProperty);
        set => SetValue(AnimatedPercentageProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Brush? IndicatorBackground
    {
        get => (Brush?)GetValue(IndicatorBackgroundProperty);
        set => SetValue(IndicatorBackgroundProperty, value);
    }

    public Brush? IndicatorForeground
    {
        get => (Brush?)GetValue(IndicatorForegroundProperty);
        set => SetValue(IndicatorForegroundProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public DataTemplate? OverlayTemplate
    {
        get => (DataTemplate?)GetValue(OverlayTemplateProperty);
        set => SetValue(OverlayTemplateProperty, value);
    }

    public object? OverlayContent
    {
        get => GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public DataTemplate? PopupTemplate
    {
        get => (DataTemplate?)GetValue(PopupTemplateProperty);
        set => SetValue(PopupTemplateProperty, value);
    }

    public object? PopupContent
    {
        get => (object?)GetValue(PopupContentProperty);
        set => SetValue(PopupContentProperty, value);
    }

    private static void OnPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DpsIndicatorControl ctl) return;

        var newVal = (double)e.NewValue;

        // Create smooth animation from current AnimatedPercentage to new Percentage
        var animation = new DoubleAnimation
        {
            To = newVal,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // Begin animation on AnimatedPercentageProperty
        ctl.BeginAnimation(AnimatedPercentageProperty, animation);
    }

    private static void OnPopupTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Debug.WriteLine($"[DpsIndicatorControl] PopupTemplate changed: {e.NewValue?.GetType().Name ?? "null"}");
    }

    private static void OnPopupContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var oldPlayerName = (e.OldValue as dynamic)?.Player?.Name ?? "null";
        var newPlayerName = (e.NewValue as dynamic)?.Player?.Name ?? "null";
        Debug.WriteLine($"[DpsIndicatorControl] PopupContent changed: {oldPlayerName} -> {newPlayerName}");
    }

    /// <summary>
    /// ? 新增: 鼠标进入时刷新tooltip中的技能列表
    /// </summary>
    private void OnMouseEnterRefreshTooltip(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Debug.WriteLine($"[DpsIndicatorControl] MouseEnter - PopupContent: {PopupContent?.GetType().Name ?? "null"}");

        // ? 关键: 从PopupContent获取StatisticDataViewModel,刷新其FilteredSkillList
        if (PopupContent is not ViewModels.StatisticDataViewModel slot)
        {
            return;
        }

        // 获取当前的技能显示条数限制
        // 注意: 这里需要从父级DpsStatisticsViewModel获取SkillDisplayLimit
        // 由于DpsIndicatorControl是独立的Control,我们需要通过DataContext链找到父级ViewModel
        var window = Window.GetWindow(this);
        if (window?.DataContext is not ViewModels.DpsStatisticsViewModel parentVm)
        {
            Debug.WriteLine("[DpsIndicatorControl] Unable to find parent DpsStatisticsViewModel");
            return;
        }

        var skillDisplayLimit = parentVm.CurrentStatisticData?.SkillDisplayLimit ?? 8;

        // ? 刷新三类技能的FilteredSkillList
        // RefreshFilteredList会触发PropertyChanged,WPF绑定系统会自动更新
        slot.Damage.RefreshFilteredList(skillDisplayLimit);
        slot.Heal.RefreshFilteredList(skillDisplayLimit);
        slot.TakenDamage.RefreshFilteredList(skillDisplayLimit);
   
        // ? 关键: 递增刷新触发器,强制MultiBinding重新评估
        slot.SkillListRefreshTrigger++;

        Debug.WriteLine($"[DpsIndicatorControl] Refreshed skill lists for player: {slot.Player.Name}, limit: {skillDisplayLimit}, trigger: {slot.SkillListRefreshTrigger}");
    }
}