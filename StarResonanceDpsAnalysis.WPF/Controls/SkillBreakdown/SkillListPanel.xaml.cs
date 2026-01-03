using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDpsAnalysis.WPF.Controls.SkillBreakdown;

public partial class SkillListPanel : UserControl
{
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(SkillListPanel),
            new PropertyMetadata(null, OnItemTemplateChanged));

    public SkillListPanel()
    {
        InitializeComponent();
    }

    public DataTemplate ItemTemplate
    {
        get => (DataTemplate)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SkillListPanel panel)
        {
            panel.SkillItemsControl.ItemTemplate = e.NewValue as DataTemplate;
        }
    }
}
