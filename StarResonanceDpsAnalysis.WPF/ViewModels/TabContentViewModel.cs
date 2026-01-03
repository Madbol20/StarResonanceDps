using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for TabContentPanel component
/// </summary>
public partial class TabContentViewModel : ObservableObject
{
    [ObservableProperty] private ChartViewModel _chartViewModel = new();
    [ObservableProperty] private SkillStatsViewModel _statsViewModel = new();
    [ObservableProperty] private SkillListViewModel _skillListViewModel = new();
}
