using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Controls;

/// <summary>
///     Interaction logic for DpsDetailPopup.xaml
/// </summary>
public partial class DpsDetailPopup : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(DpsDetailPopup),
            new PropertyMetadata("技能详情") // 默认值
        );

    public static readonly DependencyProperty SkillListProperty = DependencyProperty.Register(
        nameof(SkillList), typeof(IEnumerable<SkillItemViewModel>), typeof(DpsDetailPopup),
        new PropertyMetadata(default(IEnumerable<SkillItemViewModel>), OnSkillListChanged));

    public DpsDetailPopup()
    {
        InitializeComponent();
        Debug.WriteLine("[DpsDetailPopup] Constructor called");
    }

    /// <summary>
    ///     弹窗标题，比如“技能统计”
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IEnumerable<SkillItemViewModel> SkillList
    {
        get => (IEnumerable<SkillItemViewModel>)GetValue(SkillListProperty);
        set => SetValue(SkillListProperty, value);
    }

    private static void OnSkillListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DpsDetailPopup)d;
        var oldList = e.OldValue as IEnumerable<SkillItemViewModel>;
        var newList = e.NewValue as IEnumerable<SkillItemViewModel>;

        var oldCount = oldList?.Count() ?? 0;
        var newCount = newList?.Count() ?? 0;

        Debug.WriteLine($"[DpsDetailPopup] SkillList changed: {oldCount} -> {newCount} skills");

        if (newList != null && newCount > 0)
        {
            Debug.WriteLine($"[DpsDetailPopup] First skill: {newList.First().SkillName}");
        }
    }
}