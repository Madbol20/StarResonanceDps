using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for ChartPanel component
/// </summary>
public partial class ChartViewModel : ObservableObject
{
    [ObservableProperty] private string _timeSeriesTitle = string.Empty;
    [ObservableProperty] private string _pieChartTitle = string.Empty;
    [ObservableProperty] private string _hitTypeChartTitle = string.Empty;

    [ObservableProperty] private PlotModel? _seriesPlotModel;
    [ObservableProperty] private PlotModel? _piePlotModel;
    [ObservableProperty] private PlotModel? _hitTypeBarPlotModel;

    [ObservableProperty] private IRelayCommand? _resetZoomCommand;
    [ObservableProperty] private IRelayCommand? _zoomInCommand;
    [ObservableProperty] private IRelayCommand? _zoomOutCommand;

    [ObservableProperty] private double _normalPercentage;
    [ObservableProperty] private double _critPercentage;
    [ObservableProperty] private double _luckyPercentage;
    [ObservableProperty] private long _normalCount;
    [ObservableProperty] private long _critCount;
    [ObservableProperty] private long _luckyCount;

    public void UpdateFromPlotViewModel(PlotViewModel plotViewModel)
    {
        SeriesPlotModel = plotViewModel.SeriesPlotModel;
        PiePlotModel = plotViewModel.PiePlotModel;
        HitTypeBarPlotModel = plotViewModel.HitTypeBarPlotModel;
    }
}
