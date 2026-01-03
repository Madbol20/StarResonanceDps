using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for SkillStatsSummaryPanel component
/// </summary>
public partial class SkillStatsViewModel : ObservableObject
{
    [ObservableProperty] private string _totalLabel = string.Empty;
    [ObservableProperty] private string _averageLabel = string.Empty;
    [ObservableProperty] private string _hitsLabel = string.Empty;
    [ObservableProperty] private string _critRateLabel = string.Empty;
    [ObservableProperty] private string _critCountLabel = string.Empty;
    [ObservableProperty] private string _luckyRateLabel = string.Empty;
    [ObservableProperty] private string _luckyCountLabel = string.Empty;
    [ObservableProperty] private string _normalValueLabel = string.Empty;
    [ObservableProperty] private string _critValueLabel = string.Empty;
    [ObservableProperty] private string _luckyValueLabel = string.Empty;

    [ObservableProperty] private long _total;
    [ObservableProperty] private double _average;
    [ObservableProperty] private long _hits;
    [ObservableProperty] private double _critRate;
    [ObservableProperty] private long _critCount;
    [ObservableProperty] private double _luckyRate;
    [ObservableProperty] private long _luckyCount;
    [ObservableProperty] private long _normalValue;
    [ObservableProperty] private long _critValue;
    [ObservableProperty] private long _luckyValue;

    public void UpdateFromDataStatistics(DataStatistics stats)
    {
        Total = stats.Total;
        Average = stats.Average;
        Hits = stats.Hits;
        CritRate = stats.CritRate;
        CritCount = stats.CritCount;
        LuckyRate = stats.LuckyRate;
        LuckyCount = stats.LuckyCount;
        NormalValue = stats.NormalValue;
        CritValue = stats.CritValue;
        LuckyValue = stats.LuckyValue;
    }
}
