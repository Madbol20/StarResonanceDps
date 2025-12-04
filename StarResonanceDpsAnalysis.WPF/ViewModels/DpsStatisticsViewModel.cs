using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Analyze.Exceptions;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;
using System.Linq;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DpsStatisticsOptions : BaseViewModel
{
    [ObservableProperty] private int _minimalDurationInSeconds;

    [RelayCommand]
    private void SetMinimalDuration(int duration)
    {
        MinimalDurationInSeconds = duration;
    }
}

public partial class DpsStatisticsViewModel : BaseViewModel, IDisposable
{
    private readonly IApplicationControlService _appControlService;
    private readonly IConfigManager _configManager;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<DpsStatisticsViewModel> _logger;

    private readonly IDataStorage _storage;

    // ⭐ 新增: 快照服务
    private readonly BattleSnapshotService _snapshotService;

    // Use a single stopwatch for both total and section durations
    private readonly Stopwatch _timer = new();
    private readonly ITopmostService _topmostService;
    private readonly IWindowManagementService _windowManagement;

    // Whether we are waiting for the first datapoint of a new section
    private bool _awaitingSectionStart;

    // Timer for active update mode
    private DispatcherTimer? _dpsUpdateTimer;
    private DispatcherTimer? _durationTimer;

    private bool _isInitialized;

    // Captured elapsed of the last section to freeze UI until new data arrives
    private TimeSpan _lastSectionElapsed = TimeSpan.Zero;

    [ObservableProperty] private ScopeTime _scopeTime = ScopeTime.Current;

    // Snapshot of elapsed time at the moment a new section starts
    private TimeSpan _sectionStartElapsed = TimeSpan.Zero;
    [ObservableProperty] private bool _showContextMenu;
    [ObservableProperty] private SortDirectionEnum _sortDirection = SortDirectionEnum.Descending;
    [ObservableProperty] private string _sortMemberPath = "Value";
    [ObservableProperty] private StatisticType _statisticIndex;
    [ObservableProperty] private AppConfig _appConfig;
    [ObservableProperty] private TimeSpan _battleDuration;
    [ObservableProperty] private bool _isServerConnected;
    [ObservableProperty] private bool _showTeamTotalDamage;
    [ObservableProperty] private ulong _teamTotalDamage;
    [ObservableProperty] private double _teamTotalDps;
    [ObservableProperty] private string _teamTotalLabel = "团队DPS";  // 动态标签
    
    // ===== 调试用: 更新计数器 =====
    [ObservableProperty] private int _debugUpdateCount;
  private DpsDataUpdatedEventHandler? _resumeActiveTimerHandler;

    // ⭐ 新增: 控制是否显示NPC数据
    [ObservableProperty] private bool _isIncludeNpcData = false;
    
    // ⭐ 新增: 快照查看模式相关字段
    [ObservableProperty] private bool _isViewingSnapshot = false;
    [ObservableProperty] private BattleSnapshotData? _currentSnapshot = null;
  
    // 保存实时数据订阅状态,以便恢复
    private bool _wasPassiveMode = false;
  private bool _wasTimerRunning = false;

    // ⭐ 新增: 暴露快照服务给View绑定
    public BattleSnapshotService SnapshotService => _snapshotService;

    public Dictionary<StatisticType, DpsStatisticsSubViewModel> StatisticData { get; }

    public DpsStatisticsSubViewModel CurrentStatisticData => StatisticData[StatisticIndex];

    public DebugFunctions DebugFunctions { get; }

    public DpsStatisticsOptions Options { get; } = new();

  // ⭐ 添加构造函数以初始化StatisticData和DebugFunctions
    public DpsStatisticsViewModel(
        ILogger<DpsStatisticsViewModel> logger,
      IDataStorage storage,
        IConfigManager configManager,
      IWindowManagementService windowManagement,
    ITopmostService topmostService,
  IApplicationControlService appControlService,
 Dispatcher dispatcher,
        DebugFunctions debugFunctions,
   BattleSnapshotService snapshotService)
    {
        _logger = logger;
        _storage = storage;
_configManager = configManager;
        _windowManagement = windowManagement;
    _topmostService = topmostService;
     _appControlService = appControlService;
        _dispatcher = dispatcher;
       DebugFunctions = debugFunctions;
        _snapshotService = snapshotService;

  // 初始化StatisticData字典，为每种统计类型创建对应的SubViewModel
        StatisticData = new Dictionary<StatisticType, DpsStatisticsSubViewModel>
    {
            [StatisticType.Damage] = new DpsStatisticsSubViewModel(logger, dispatcher, StatisticType.Damage, storage, debugFunctions),
            [StatisticType.Healing] = new DpsStatisticsSubViewModel(logger, dispatcher, StatisticType.Healing, storage, debugFunctions),
            [StatisticType.TakenDamage] = new DpsStatisticsSubViewModel(logger, dispatcher, StatisticType.TakenDamage, storage, debugFunctions),
            [StatisticType.NpcTakenDamage] = new DpsStatisticsSubViewModel(logger, dispatcher, StatisticType.NpcTakenDamage, storage, debugFunctions)
        };

        // 订阅配置更新事件
     _configManager.ConfigurationUpdated += ConfigManagerOnConfigurationUpdated;

        // 订阅存储事件
        _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
_storage.NewSectionCreated += StorageOnNewSectionCreated;
 _storage.ServerConnectionStateChanged += StorageOnServerConnectionStateChanged;
 _storage.PlayerInfoUpdated += StorageOnPlayerInfoUpdated;
        _storage.ServerChanged += StorageOnServerChanged;  // ⭐ 订阅服务器切换事件

        // 订阅DebugFunctions事件
   DebugFunctions.SampleDataRequested += OnSampleDataRequested;

        // 初始化AppConfig
   AppConfig = _configManager.CurrentConfig;

 _logger.LogDebug("DpsStatisticsViewModel constructor completed");
    }

  /// <inheritdoc/>
    public void Dispose()
    {
        // Unsubscribe from DebugFunctions events
        DebugFunctions.SampleDataRequested -= OnSampleDataRequested;
        _configManager.ConfigurationUpdated -= ConfigManagerOnConfigurationUpdated;

        if (_durationTimer != null)
        {
            _durationTimer.Stop();
            _durationTimer.Tick -= DurationTimerOnTick;
        }

        // Stop and dispose DPS update timer
        if (_dpsUpdateTimer != null)
        {
            _dpsUpdateTimer.Stop();
            _dpsUpdateTimer.Tick -= DpsUpdateTimerOnTick;
            _dpsUpdateTimer = null;
        }

        _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
        _storage.NewSectionCreated -= StorageOnNewSectionCreated;
        _storage.ServerConnectionStateChanged -= StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated -= StorageOnPlayerInfoUpdated;
        _storage.Dispose();

        foreach (var dpsStatisticsSubViewModel in StatisticData.Values)
        {
            dpsStatisticsSubViewModel.Initialized = false;
        }

        _isInitialized = false;

        // Detach one-shot resume handler if any
        if (_resumeActiveTimerHandler != null)
        {
            _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
            _resumeActiveTimerHandler = null;
        }
    }

    [RelayCommand]
    private void OpenSkillBreakdown(StatisticDataViewModel? slot)
    {
        // allow command binding from item click
        var target = slot ?? CurrentStatisticData.SelectedSlot;
        if (target is null) return;

        var vm = _windowManagement.SkillBreakdownView.DataContext as SkillBreakdownViewModel;
        Debug.Assert(vm != null, "vm!=null");
        vm.InitializeFrom(target);
        _windowManagement.SkillBreakdownView.Show();
        _windowManagement.SkillBreakdownView.Activate();
    }

    private void ConfigManagerOnConfigurationUpdated(object? sender, AppConfig newConfig)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(ConfigManagerOnConfigurationUpdated, sender, newConfig);
            return;
        }

        var oldMode = AppConfig.DpsUpdateMode;
        var oldInterval = AppConfig.DpsUpdateInterval;

        AppConfig = newConfig;

        // If update mode or interval changed, reconfigure update mechanism
        if (oldMode != newConfig.DpsUpdateMode || oldInterval != newConfig.DpsUpdateInterval)
        {
            ConfigureDpsUpdateMode();
        }
    }

    private void OnSampleDataRequested(object? sender, EventArgs e)
    {
        // Handle the event from DebugFunctions
        AddRandomData();
    }

    private void StorageOnServerConnectionStateChanged(bool serverConnectionState)
    {
        if (_dispatcher.CheckAccess())
        {
            IsServerConnected = serverConnectionState;
        }
        else
        {
            _dispatcher.Invoke(() => IsServerConnected = serverConnectionState);
        }
    }

    /// <summary>
    /// 切换窗口置顶状态（命令）。
    /// 通过绑定 Window.Topmost 到 AppConfig.TopmostEnabled 实现。
    /// </summary>
    [RelayCommand]
    private async Task ToggleTopmost()
    {
        AppConfig.TopmostEnabled = !AppConfig.TopmostEnabled;
        try
        {
            await _configManager.SaveAsync(AppConfig);
        }
        catch (InvalidOperationException ex)
        {
            // Ignore
            _logger.LogError(ex, "Failed to save AppConfig");
        }
    }

    [RelayCommand]
    private void OpenPersonalDpsView()
    {
        _windowManagement.PersonalDpsView.Show();
        _windowManagement.DpsStatisticsView.Hide();
    }

    [RelayCommand]
    public void ResetAll()
    {
        _logger.LogInformation("=== ResetAll START === ScopeTime={ScopeTime}", ScopeTime);
        
        // ⭐ 新增: 保存快照(在清空数据之前)
       if (_storage.ReadOnlyFullDpsDataList.Count > 0 || _storage.ReadOnlySectionedDpsDataList.Count > 0)
        {
      try
          {
 if (ScopeTime == ScopeTime.Current)
          {
      // 当前模式:保存当前战斗快照
          // ⭐ 传递用户设置的最小时长(0表示记录所有,但仍受10秒硬性限制)
   _snapshotService.SaveCurrentSnapshot(_storage, BattleDuration, Options.MinimalDurationInSeconds);
    _logger.LogInformation("保存当前战斗快照成功");
}
      else
        {
  // 全程模式:保存全程快照
 // ⭐ 传递用户设置的最小时长
     _snapshotService.SaveTotalSnapshot(_storage, BattleDuration, Options.MinimalDurationInSeconds);
   _logger.LogInformation("保存全程快照成功");
     }
     }
    catch (Exception ex)
  {
 _logger.LogError(ex, "保存快照失败");
    }
        }
        
        // ⭐ 新逻辑: 根据当前视图模式决定清空策略
      if (ScopeTime == ScopeTime.Current)
  {
        // 当前模式: 只清空当前(sectioned)数据,保留全程数据
        _logger.LogInformation("Current mode: Clearing only sectioned data, preserving total data");
     _storage.ClearDpsData(); // 只清空当前数据
    
     // 重置section开始时间为当前时刻
            _sectionStartElapsed = _timer.Elapsed;
            _lastSectionElapsed = TimeSpan.Zero;
            _awaitingSectionStart = false;
        }
        else
{
            // 全程模式: 清空所有数据(全程+当前)
 _logger.LogInformation("Total mode: Clearing ALL data (total + sectioned)");
            _storage.ClearAllDpsData(); // 清空全程+当前数据
    
            // 完全重置计时器
    _timer.Reset();
     _sectionStartElapsed = TimeSpan.Zero;
      _lastSectionElapsed = TimeSpan.Zero;
   _awaitingSectionStart = false;
        }

        // 清空UI数据
        foreach (var subVm in StatisticData.Values)
     {
      subVm.Reset();
     }

        // 重置团队统计
      TeamTotalDamage = 0;
        TeamTotalDps = 0;
        BattleDuration = TimeSpan.Zero;

        // 强制重新初始化更新机制
        if (!_isInitialized)
        {
            _logger.LogWarning("ResetAll called but ViewModel not initialized!");
      return;
        }

        try
        {
            _logger.LogInformation("ResetAll: Mode={Mode}, Interval={Interval}ms", 
                AppConfig.DpsUpdateMode, AppConfig.DpsUpdateInterval);

            // 清理残留
  if (_resumeActiveTimerHandler != null)
      {
         _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
      _resumeActiveTimerHandler = null;
            }

            // ===== 关键修复: 完全重建更新机制 =====
      
    // 步骤1: 完全停止现有机制
            if (_dpsUpdateTimer != null)
     {
        _dpsUpdateTimer.Stop();
           _dpsUpdateTimer.Tick -= DpsUpdateTimerOnTick;
    _dpsUpdateTimer = null;
            }
_storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
            _storage.NewSectionCreated -= StorageOnNewSectionCreated;
       
    _logger.LogInformation("ResetAll: Stopped all existing update mechanisms");

    // 步骤2: 根据模式重新创建
if (AppConfig.DpsUpdateMode == DpsUpdateMode.Active)
   {
 // Active模式: 创建新定时器
        _dpsUpdateTimer = new DispatcherTimer
             {
   Interval = TimeSpan.FromMilliseconds(Math.Clamp(AppConfig.DpsUpdateInterval, 100, 5000))
  };
    _dpsUpdateTimer.Tick += DpsUpdateTimerOnTick;
      _dpsUpdateTimer.Start();
            
    _logger.LogInformation("ResetAll: Created and started NEW timer. Enabled={Enabled}, Interval={Interval}ms",
        _dpsUpdateTimer.IsEnabled, _dpsUpdateTimer.Interval.TotalMilliseconds);
         }
         else
     {
       // Passive模式: 订阅事件
      _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
_logger.LogInformation("ResetAll: Subscribed to DpsDataUpdated event");
            }

          // 始终订阅NewSectionCreated
   _storage.NewSectionCreated += StorageOnNewSectionCreated;

            // 步骤3: 立即触发一次更新显示空状态
      var dpsList = ScopeTime == ScopeTime.Total
      ? _storage.ReadOnlyFullDpsDataList
       : _storage.ReadOnlySectionedDpsDataList;
      
       // ⭐ 关键:即使数据为空也要更新UI,确保UI显示空状态
     UpdateData(dpsList);
            UpdateBattleDuration();

            _logger.LogInformation("=== ResetAll COMPLETE === ScopeTime={ScopeTime}, Mode={Mode}, Timer={Timer}, Event subscribed={Event}",
     ScopeTime,
       AppConfig.DpsUpdateMode,
       _dpsUpdateTimer?.IsEnabled ?? false,
         AppConfig.DpsUpdateMode == DpsUpdateMode.Passive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ResetAll");
      }
    }

    [RelayCommand]
    public void ResetSection()
    {
        _storage.ClearDpsData();
        // Move section start to current elapsed so section duration becomes zero
        _sectionStartElapsed = _timer.Elapsed;
    }

    /// <summary>
    /// 读取用户缓存
    /// </summary>
    private void LoadPlayerCache()
    {
        try
        {
            _storage.LoadPlayerInfoFromFile();
        }
        catch (FileNotFoundException)
        {
            // 没有缓存
        }
        catch (DataTamperedException)
        {
            _storage.ClearAllPlayerInfos();
            _storage.SavePlayerInfoToFile();
        }
    }

    [RelayCommand]
    private void OnLoaded()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        foreach (var vm in StatisticData.Values)
        {
            vm.Initialized = true;
        }

        _logger.LogDebug(WpfLogEvents.VmLoaded, "DpsStatisticsViewModel loaded");
        LoadPlayerCache();

        EnsureDurationTimerStarted();
        UpdateBattleDuration();

        // Configure update mode based on settings
        ConfigureDpsUpdateMode();
    }

    [RelayCommand]
    private void OnUnloaded()
    {
   // ===== 重要: 不要在Unloaded时停止定时器,因为ResetAll可能会触发这个 =====
        // 只记录日志,不实际停止任何东西
  _logger.LogDebug("DpsStatisticsViewModel OnUnloaded called - NOT stopping timers");

        // 注释掉原来的停止逻辑
        // StopDpsUpdateTimer();
        // if (_durationTimer != null)
        // {
        //     _durationTimer.Stop();
   // }
    }

    [RelayCommand]
    private void OnResize()
    {
        _logger.LogDebug("Window Resized");
    }

    /// <summary>
    /// Configure DPS update mode based on AppConfig settings
    /// </summary>
    private void ConfigureDpsUpdateMode()
    {
      if (!_isInitialized)
 {
         _logger.LogWarning("ConfigureDpsUpdateMode called but not initialized");
   return;
}

  _logger.LogInformation("Configuring DPS update mode: {Mode}, Interval: {Interval}ms",
     AppConfig.DpsUpdateMode, AppConfig.DpsUpdateInterval);

   // Ensure any one-shot handler from previous mode is removed
   if (_resumeActiveTimerHandler != null)
   {
            _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
        _resumeActiveTimerHandler = null;
    _logger.LogDebug("Removed resume active timer handler");
        }

        switch (AppConfig.DpsUpdateMode)
        {
    case DpsUpdateMode.Passive:
       // Passive mode: subscribe to event
     StopDpsUpdateTimer();
      _dpsUpdateTimer = null; // ensure timer is released
  _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated; // Unsubscribe first to avoid duplicate
           _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
          _storage.NewSectionCreated -= StorageOnNewSectionCreated;
                _storage.NewSectionCreated += StorageOnNewSectionCreated;
  _logger.LogDebug("Passive mode enabled: DpsDataUpdated event subscribed");
     break;

            case DpsUpdateMode.Active:
       // Active mode: use timer, but still listen for section events to pause/resume
       _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
         _storage.NewSectionCreated -= StorageOnNewSectionCreated;
    _storage.NewSectionCreated += StorageOnNewSectionCreated;
      StartDpsUpdateTimer(AppConfig.DpsUpdateInterval);
 _logger.LogDebug("Active mode enabled: timer started with interval {Interval}ms", AppConfig.DpsUpdateInterval);
      break;

            default:
      _logger.LogWarning("Unknown DPS update mode: {Mode}", AppConfig.DpsUpdateMode);
      break;
        }
  
    _logger.LogInformation("Update mode configuration complete. Mode: {Mode}, Timer enabled: {TimerEnabled}", 
    AppConfig.DpsUpdateMode,
      _dpsUpdateTimer?.IsEnabled ?? false);
    }

    /// <summary>
    /// Start or restart DPS update timer with specified interval
    /// </summary>
    private void StartDpsUpdateTimer(int intervalMs)
    {
        // Validate interval
     var clampedInterval = Math.Clamp(intervalMs, 100, 5000);
        if (clampedInterval != intervalMs)
     {
       _logger.LogWarning("DPS update interval {Original}ms clamped to {Clamped}ms",
     intervalMs, clampedInterval);
        }

        if (_dpsUpdateTimer == null)
 {
      _dpsUpdateTimer = new DispatcherTimer
       {
     Interval = TimeSpan.FromMilliseconds(clampedInterval)
       };
    _dpsUpdateTimer.Tick += DpsUpdateTimerOnTick;
  _logger.LogInformation("Created NEW DPS update timer with interval {Interval}ms", clampedInterval);
        }
else
        {
    _dpsUpdateTimer.Stop();
    _dpsUpdateTimer.Interval = TimeSpan.FromMilliseconds(clampedInterval);
    _logger.LogInformation("Updated EXISTING DPS update timer interval to {Interval}ms", clampedInterval);
        }

    _dpsUpdateTimer.Start();
        _logger.LogInformation("DPS update timer STARTED. Enabled={Enabled}, Interval={Interval}ms", 
    _dpsUpdateTimer.IsEnabled, 
       _dpsUpdateTimer.Interval.TotalMilliseconds);
    }

    /// <summary>
    /// Stop DPS update timer
    /// </summary>
    private void StopDpsUpdateTimer()
    {
        if (_dpsUpdateTimer != null)
        {
            _dpsUpdateTimer.Stop();
            _dpsUpdateTimer.Tick -= DpsUpdateTimerOnTick;
            _logger.LogDebug("DPS update timer stopped");
        }
    }

    /// <summary>
    /// Timer tick handler for active update mode
    /// </summary>
    private void DpsUpdateTimerOnTick(object? sender, EventArgs e)
    {
   // 每10次tick输出一次日志,避免日志爆炸
        if (_dpsUpdateTimer != null && _dpsUpdateTimer.IsEnabled)
        {
          var currentSecond = DateTime.Now.Second;
   if (currentSecond % 10 == 0) // 每10秒输出一次
       {
    _logger.LogInformation("Timer tick triggered - calling DataStorage_DpsDataUpdated");
  }
        }
        
     // Call the same update logic as event-based mode
   DataStorage_DpsDataUpdated();
    }

    private void DataStorage_DpsDataUpdated()
    {
        _logger.LogTrace("DataStorage_DpsDataUpdated called");  // 添加跟踪日志
      
        if (!_dispatcher.CheckAccess())
        {
             _logger.LogTrace("Dispatching to UI thread");
        _dispatcher.BeginInvoke(DataStorage_DpsDataUpdated);
            return;
        }

        _logger.LogDebug("Processing DPS data update on UI thread");

  var dpsList = ScopeTime == ScopeTime.Total
       ? _storage.ReadOnlyFullDpsDataList
            : _storage.ReadOnlySectionedDpsDataList;

        _logger.LogDebug("Data count: {Count}, Timer running: {IsRunning}", dpsList.Count, _timer.IsRunning);

        // ⭐ 关键修复:当有数据且计时器未运行时,启动计时器
        if (!_timer.IsRunning && HasDamageData(dpsList))
   {
      _logger.LogDebug("Starting timer - has damage data");
     _timer.Start();
            // ⭐ 新增:重置section开始时间为当前时刻
    _sectionStartElapsed = _timer.Elapsed;
  }

      // If we're waiting for first datapoint of new section and data arrives, reset UI
        if (_awaitingSectionStart)
  {
        var hasSectionDamage = HasDamageData(_storage.ReadOnlySectionedDpsDataList);
    _logger.LogDebug("Awaiting section start - has section damage: {HasSectionDamage}", hasSectionDamage);
      if (hasSectionDamage)
{
    foreach (var subVm in StatisticData.Values)
     {
   subVm.Reset();
        }

      _sectionStartElapsed = _timer.Elapsed;
             _lastSectionElapsed = TimeSpan.Zero;
  _awaitingSectionStart = false;
       _logger.LogDebug("Section start processed");
            }
   }

        // Always update data (even if empty) to ensure UI reflects current state
        UpdateData(dpsList);
  UpdateBattleDuration();
    }

    private static bool HasDamageData(IReadOnlyList<DpsData> data)
    {
        return data.Any(t => t.TotalAttackDamage > 0);
    }

    private void UpdateData(IReadOnlyList<DpsData> data)
    {
        _logger.LogTrace(WpfLogEvents.VmUpdateData, "Update data requested: {Count} entries", data.Count);

        var currentPlayerUid = _storage.CurrentPlayerInfo.UID;

        // Pre-process data once for all statistic types
        var processedDataByType = PreProcessDataForAllTypes(data);

        // Update each subViewModel with its pre-processed data
        foreach (var (statisticType, processedData) in processedDataByType)
        {
            if (!StatisticData.TryGetValue(statisticType, out var subViewModel)) continue;
            subViewModel.ScopeTime = ScopeTime;
            subViewModel.UpdateDataOptimized(processedData, currentPlayerUid);
        }

        // Append per-player DPS samples after sub VMs updated
        AppendDpsSamples(data);

        // Update team total damage and DPS
        UpdateTeamTotalStats(data);
    }

    /// <summary>
    /// Updates team total damage and DPS statistics
    /// </summary>
  private void UpdateTeamTotalStats(IReadOnlyList<DpsData> data)
 {
        if (!ShowTeamTotalDamage) return;

        ulong totalValue = 0;
        double totalElapsedSeconds = 0;
        int playerCount = 0;  // ⭐ 新增: 统计玩家数量
        int npcCount = 0;     // ⭐ 新增: 统计NPC数量
        int skippedPlayers = 0; // ⭐ 新增: 统计跳过的玩家
      int skippedNpcs = 0;    // ⭐ 新增: 统计跳过的NPC

        // 根据当前统计类型计算不同的数值
        foreach (var dpsData in data)
        {
        // 跳过NPC数据 (除非是NPC承伤统计)
            if (dpsData.IsNpcData && StatisticIndex != StatisticType.NpcTakenDamage) 
            {
 skippedNpcs++;
    continue;
            }
            
 // 跳过玩家数据 (如果是NPC承伤统计)
        if (!dpsData.IsNpcData && StatisticIndex == StatisticType.NpcTakenDamage) 
   {
      skippedPlayers++;
      continue;
       }

            // 统计计入的数据类型
      if (dpsData.IsNpcData) npcCount++;
            else playerCount++;

       // 根据统计类型累加不同的数值
        ulong value = StatisticIndex switch
          {
         StatisticType.Damage => dpsData.TotalAttackDamage.ConvertToUnsigned(),
        StatisticType.Healing => dpsData.TotalHeal.ConvertToUnsigned(),
      StatisticType.TakenDamage => dpsData.TotalTakenDamage.ConvertToUnsigned(),
StatisticType.NpcTakenDamage => dpsData.TotalTakenDamage.ConvertToUnsigned(),
    _ => 0
       };
          
     totalValue += value;

         // 计算经过时间
            var elapsedTicks = dpsData.LastLoggedTick - (dpsData.StartLoggedTick ?? 0);
     if (elapsedTicks > 0)
          {
      var elapsedSeconds = TimeSpan.FromTicks(elapsedTicks).TotalSeconds;
   // 使用最大经过时间作为团队持续时间
            if (elapsedSeconds > totalElapsedSeconds)
      {
              totalElapsedSeconds = elapsedSeconds;
     }
            }
        }

        // 更新标签
 TeamTotalLabel = StatisticIndex switch
  {
     StatisticType.Damage => "团队DPS",
 StatisticType.Healing => "团队治疗",
            StatisticType.TakenDamage => "团队承伤",
            StatisticType.NpcTakenDamage => "NPC承伤",
        _ => "团队DPS"
   };

   // ⭐ 关键修复: 只有在有新数据时更新,否则保持上次的值
if (totalValue > 0 || data.Count > 0)
        {
    TeamTotalDamage = totalValue;
            TeamTotalDps = totalElapsedSeconds > 0 ? totalValue / totalElapsedSeconds : 0;
        }

   // ⭐ 新增: 详细日志输出(只在有数据时输出,避免日志爆炸)
        if (data.Count > 0)
        {
         _logger.LogDebug(
     "TeamTotal [{Type}]: Total={Total:N0}, DPS={Dps:N0}, " +
     "Players={Players}(skipped={SkipP}), NPCs={NPCs}(skipped={SkipN}), " +
    "Duration={Duration:F1}s, Label={Label}",
        StatisticIndex, totalValue, TeamTotalDps,
          playerCount, skippedPlayers, npcCount, skippedNpcs,
    totalElapsedSeconds, TeamTotalLabel);
        }
    }
    /// <summary>
    /// Appends per-player DPS/HPS/DTPS samples (section and total) into each player's StatisticDataViewModel series.
    /// Keeps only the latest N points to avoid unbounded growth.
    /// Adds duration since the last section start to each sample.
    /// </summary>
    private void AppendDpsSamples(IReadOnlyList<DpsData> data)
    {
        // Cap size to roughly what the chart needs
        const int maxSamples = 300;

        // Calculate section duration once per tick (independent of scope)
        var sectionDuration = ComputeSectionDuration();

        foreach (var dpsData in data)
        {
            // Skip empty players
            var totalElapsedTicks = dpsData.LastLoggedTick - (dpsData.StartLoggedTick ?? 0);
            if (totalElapsedTicks <= 0) continue;
            var totalElapsedSeconds = TimeSpan.FromTicks(totalElapsedTicks).TotalSeconds;
            if (totalElapsedSeconds <= 0.01) continue;

            // Compute total values
            var totalDps = Math.Max(0, dpsData.TotalAttackDamage) / totalElapsedSeconds;
            var totalHps = Math.Max(0, dpsData.TotalHeal) / totalElapsedSeconds;
            var totalDtps = Math.Max(0, dpsData.TotalTakenDamage) / totalElapsedSeconds;

            // Section values use current scope
            double sectionDps = totalDps, sectionHps = totalHps, sectionDtps = totalDtps;
            if (_storage.ReadOnlySectionedDpsDatas.TryGetValue(dpsData.UID, out var section))
            {
                var sectionElapsedTicks = section.LastLoggedTick - (section.StartLoggedTick ?? 0);
                var sectionElapsedSeconds = sectionElapsedTicks > 0 ? TimeSpan.FromTicks(sectionElapsedTicks).TotalSeconds : 0.0;
                if (sectionElapsedSeconds > 0.01)
                {
                    sectionDps = Math.Max(0, section.TotalAttackDamage) / sectionElapsedSeconds;
                    sectionHps = Math.Max(0, section.TotalHeal) / sectionElapsedSeconds;
                    sectionDtps = Math.Max(0, section.TotalTakenDamage) / sectionElapsedSeconds;
                }
            }

            // Locate the corresponding slot in the damage view (primary)
            var damageVm = StatisticData.TryGetValue(StatisticType.Damage, out var sub) ? sub : null;
            if (damageVm == null) continue;

            if (!damageVm.DataDictionary.TryGetValue(dpsData.UID, out var slot)) continue;

            // Push samples with duration
            var dpsSeries = slot.Damage.Dps;
      dpsSeries.Add((sectionDuration, sectionDps, totalDps));
       if (dpsSeries.Count > maxSamples)
   {
      while (dpsSeries.Count > maxSamples) dpsSeries.RemoveAt(0);
     }

            var hpsSeries = slot.Heal.Dps;
   hpsSeries.Add((sectionDuration, sectionHps, totalHps));
            if (hpsSeries.Count > maxSamples)
            {
                while (hpsSeries.Count > maxSamples) hpsSeries.RemoveAt(0);
            }

            var dtpsSeries = slot.TakenDamage.Dps;
            dtpsSeries.Add((sectionDuration, sectionDtps, totalDtps));
            if (dtpsSeries.Count > maxSamples)
            {
                while (dtpsSeries.Count > maxSamples) dtpsSeries.RemoveAt(0);
            }
        }
    }

    private TimeSpan ComputeSectionDuration()
    {
        // Duration after the new section is created
        var elapsed = _timer.Elapsed - _sectionStartElapsed;
        if (_awaitingSectionStart || elapsed < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }
        return elapsed;
    }

    /// <summary>
    /// Pre-processes data once for all statistic types to avoid redundant iterations
    /// </summary>
    private Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> PreProcessDataForAllTypes(
        IReadOnlyList<DpsData> data)
    {
  var result = new Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>>
        {
     [StatisticType.Damage] = new(),
            [StatisticType.Healing] = new(),
            [StatisticType.TakenDamage] = new(),
 [StatisticType.NpcTakenDamage] = new()
   };

        var currentPlayerUid = _storage.CurrentPlayerUUID;

        foreach (var dpsData in data)
  {
     // Get player info for this UID
       _storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.UID, out var playerInfo);

  var playerName = playerInfo?.Name ?? $"UID: {dpsData.UID}";
 var playerClass = playerInfo?.Class ?? Classes.Unknown;
            var playerSpec = playerInfo?.Spec ?? ClassSpec.Unknown;
      var powerLevel = playerInfo?.CombatPower ?? 0;

         var duration = (dpsData.LastLoggedTick - (dpsData.StartLoggedTick ?? 0)).ConvertToUnsigned();

   // Build skill lists once for reuse
 var (filteredDmg, totalDmg, filteredHeal, totalHeal, filteredTaken, totalTaken) =
     BuildSkillListSnapshot(dpsData);

            // ⭐ 修复: Process Damage - 根据IsIncludeNpcData控制NPC数据显示
          var damageValue = dpsData.TotalAttackDamage.ConvertToUnsigned();
       if (damageValue > 0)
         {
    // ✅ 新逻辑: 
         // 1. 玩家数据始终显示
     // 2. NPC数据只在勾选"统计BOSS"时显示
    bool shouldShowInDamageList = !dpsData.IsNpcData || IsIncludeNpcData;
             
                if (shouldShowInDamageList)
           {
                 result[StatisticType.Damage][dpsData.UID] = new DpsDataProcessed(
        dpsData, damageValue, duration,
          filteredDmg, totalDmg, filteredHeal, totalHeal, filteredTaken, totalTaken,
 playerName, playerClass, playerSpec, powerLevel);
                }
            }

            // Process Healing - 只统计玩家治疗
      var healingValue = dpsData.TotalHeal.ConvertToUnsigned();
       if (healingValue > 0 && !dpsData.IsNpcData)
            {
    result[StatisticType.Healing][dpsData.UID] = new DpsDataProcessed(
            dpsData, healingValue, duration,
          filteredDmg, totalDmg, filteredHeal, totalHeal, filteredTaken, totalTaken,
    playerName, playerClass, playerSpec, powerLevel);
    }

       // Process TakenDamage
var takenDamageValue = dpsData.TotalTakenDamage.ConvertToUnsigned();
  if (takenDamageValue > 0)
  {
         // ⭐ NPC承伤统计:只显示NPC
    if (dpsData.IsNpcData)
          {
           _logger.LogDebug($"NPC TakenDamage: UID={dpsData.UID}, Value={takenDamageValue}, IsNpcData={dpsData.IsNpcData}");
                    result[StatisticType.NpcTakenDamage][dpsData.UID] = new DpsDataProcessed(
      dpsData, takenDamageValue, duration,
             filteredDmg, totalDmg, filteredHeal, totalHeal, filteredTaken, totalTaken,
       playerName, playerClass, playerSpec, powerLevel);
   }
     else // Player TakenDamage - 只显示玩家
    {
   result[StatisticType.TakenDamage][dpsData.UID] = new DpsDataProcessed(
  dpsData, takenDamageValue, duration,
    filteredDmg, totalDmg, filteredHeal, totalHeal, filteredTaken, totalTaken,
   playerName, playerClass, playerSpec, powerLevel);
   }
    }
        }

        _logger.LogDebug($"PreProcess complete: Damage count = {result[StatisticType.Damage].Count}, NpcTakenDamage count = {result[StatisticType.NpcTakenDamage].Count}, IsIncludeNpcData = {IsIncludeNpcData}");
        return result;
    }

    /// <summary>
    /// Builds skill list snapshot
    /// </summary>
    private (List<SkillItemViewModel> filtered, List<SkillItemViewModel> total,
        List<SkillItemViewModel> filteredHeal, List<SkillItemViewModel> totalHeal,
        List<SkillItemViewModel> filteredTakenDamage, List<SkillItemViewModel> totalTakenDamage) BuildSkillListSnapshot(DpsData dpsData)
    {
        var skills = dpsData.ReadOnlySkillDataList;
        if (skills.Count == 0)
        {
            return ([], [], [], [], [], []);
        }

        var skillDisplayLimit = CurrentStatisticData?.SkillDisplayLimit ?? 8;

        // Process damage skills (existing logic)
        var orderedSkills = skills.OrderByDescending(static s => s.TotalValue);
        var damageSkills = ProjectSkills(orderedSkills, skillDisplayLimit);

        // Process battle logs to separate healing and taken damage skills
        var battleLogs = dpsData.ReadOnlyBattleLogs;
      var healingSkillDict = new Dictionary<long, (long totalValue, int useTimes, int critTimes, int luckyTimes)>();
        var takenDamageSkillDict = new Dictionary<long, (long totalValue, int useTimes, int critTimes, int luckyTimes)>();

      // Aggregate skills from battle logs
        foreach (var log in battleLogs)
        {
            if (log.IsHeal)
            {
                // Healing skill
                if (!healingSkillDict.TryGetValue(log.SkillID, out var healData))
                {
                    healData = (0, 0, 0, 0);
                }
                healingSkillDict[log.SkillID] = (
                    healData.totalValue + log.Value,
                    healData.useTimes + 1,
                    healData.critTimes + (log.IsCritical ? 1 : 0),
                    healData.luckyTimes + (log.IsLucky ? 1 : 0)
                );
            }
            else if (log.IsTargetPlayer && log.TargetUuid == dpsData.UID)
            {
                // Taken damage skill (when this player is the target)
                if (!takenDamageSkillDict.TryGetValue(log.SkillID, out var takenData))
                {
                    takenData = (0, 0, 0, 0);
                }
                takenDamageSkillDict[log.SkillID] = (
                    takenData.totalValue + log.Value,
                    takenData.useTimes + 1,
                    takenData.critTimes + (log.IsCritical ? 1 : 0),
                    takenData.luckyTimes + (log.IsLucky ? 1 : 0)
                );
            }
        }

        // Convert healing skills to ViewModels
        var healingSkillsOrdered = healingSkillDict
            .OrderByDescending(static kvp => kvp.Value.totalValue)
            .Select(kvp =>
            {
                var (totalValue, useTimes, critTimes, luckyTimes) = kvp.Value;
                var average = useTimes > 0 ? Math.Round(totalValue / (double)useTimes) : 0d;
                var avgHeal = average > int.MaxValue ? int.MaxValue : (int)average;
                var skillIdText = kvp.Key.ToString();
                var skillName = EmbeddedSkillConfig.TryGet(skillIdText, out var definition)
                    ? definition.Name
                    : skillIdText;
                var critRate = useTimes > 0 ? (double)critTimes / useTimes : 0d;

                return new SkillItemViewModel
                {
                    SkillName = skillName,
                    TotalDamage = totalValue,
                    HitCount = useTimes,
                    CritCount = critTimes,
                    AvgDamage = avgHeal,
                    CritRate = critRate,
                };
            });

        var healingSkillsList = healingSkillsOrdered.ToList();
        var filteredHealingSkills = skillDisplayLimit > 0
            ? healingSkillsList.Take(skillDisplayLimit).ToList()
            : healingSkillsList;

        // Convert taken damage skills to ViewModels
        var takenDamageSkillsOrdered = takenDamageSkillDict
            .OrderByDescending(static kvp => kvp.Value.totalValue)
            .Select(kvp =>
            {
                var (totalValue, useTimes, critTimes, luckyTimes) = kvp.Value;
                var average = useTimes > 0 ? Math.Round(totalValue / (double)useTimes) : 0d;
                var avgDamage = average > int.MaxValue ? int.MaxValue : (int)average;
                var skillIdText = kvp.Key.ToString();
                var skillName = EmbeddedSkillConfig.TryGet(skillIdText, out var definition)
                    ? definition.Name
                    : skillIdText;
                var critRate = useTimes > 0 ? (double)critTimes / useTimes : 0d;

                return new SkillItemViewModel
                {
                    SkillName = skillName,
                    TotalDamage = totalValue,
                    HitCount = useTimes,
                    CritCount = critTimes,
                    AvgDamage = avgDamage,
                    CritRate = critRate,
                };
            });

        var takenDamageSkillsList = takenDamageSkillsOrdered.ToList();
        var filteredTakenDamageSkills = skillDisplayLimit > 0
            ? takenDamageSkillsList.Take(skillDisplayLimit).ToList()
            : takenDamageSkillsList;

        return (damageSkills.filtered, damageSkills.total,
            filteredHealingSkills, healingSkillsList,
            filteredTakenDamageSkills, takenDamageSkillsList);
    }

    /// <summary>
    /// Helper method to project skills into ViewModels
    /// </summary>
    private (List<SkillItemViewModel> filtered, List<SkillItemViewModel> total) ProjectSkills(
        IOrderedEnumerable<SkillData> orderedSkills, int skillDisplayLimit)
    {
        var projected = orderedSkills.Select(skill =>
        {
            var average = skill.UseTimes > 0
                ? Math.Round(skill.TotalValue / (double)skill.UseTimes)
                : 0d;

            var avgDamage = average > int.MaxValue
                ? int.MaxValue
                : (int)average;

            var skillIdText = skill.SkillId.ToString();
            var skillName = EmbeddedSkillConfig.TryGet(skillIdText, out var definition)
                ? definition.Name
                : skillIdText;

            var critRate = skill.UseTimes > 0
                ? (double)skill.CritTimes / skill.UseTimes
                : 0d;

            return new SkillItemViewModel
            {
                SkillName = skillName,
                TotalDamage = skill.TotalValue,
                HitCount = skill.UseTimes,
                CritCount = skill.CritTimes,
                AvgDamage = avgDamage,
                CritRate = critRate,
            };
        });

        var list = projected.ToList();
        var filtered = skillDisplayLimit > 0
            ? list.Take(skillDisplayLimit).ToList()
            : list;

        return (filtered, list);
    }

    [RelayCommand]
    public void AddRandomData()
    {
        UpdateData();
    }

    [RelayCommand]
    private void SetSkillDisplayLimit(int limit)
    {
        var clampedLimit = Math.Max(0, limit);
        foreach (var vm in StatisticData.Values)
        {
            vm.SkillDisplayLimit = clampedLimit;
        }

        UpdateData();

        // Notify that current data's SkillDisplayLimit changed
        OnPropertyChanged(nameof(CurrentStatisticData));
    }

    protected void AddTestItem()
    {
        CurrentStatisticData.AddTestItem();
    }

    [RelayCommand]
    private void MinimizeWindow()
    {
        _windowManagement.DpsStatisticsView.WindowState = WindowState.Minimized;
    }

    [RelayCommand]
    private void NextMetricType()
    {
        StatisticIndex = StatisticIndex.Next();
    }

    [RelayCommand]
    private void PreviousMetricType()
    {
        StatisticIndex = StatisticIndex.Previous();
    }

    [RelayCommand]
    private void ToggleScopeTime()
    {
        ScopeTime = ScopeTime.Next();
    }

    protected void UpdateData()
    {
        DataStorage_DpsDataUpdated();
    }

    [RelayCommand]
    private void Refresh()
    {
        _logger.LogDebug(WpfLogEvents.VmRefresh, "Manual refresh requested");

        // Reload cached player details so that recent changes in the on-disk
        // cache are reflected in the UI.
        LoadPlayerCache();

        try
        {
            DataStorage_DpsDataUpdated();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh DPS statistics");
        }
    }


    [RelayCommand]
    private void OpenContextMenu()
    {
        ShowContextMenu = true;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManagement.SettingsView.Show();
    }

    [RelayCommand]
    private void Shutdown()
    {
        _appControlService.Shutdown();
    }

    private void EnsureDurationTimerStarted()
    {
        if (_durationTimer != null) return;

        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += DurationTimerOnTick;
        _durationTimer.Start();
    }

    private void DurationTimerOnTick(object? sender, EventArgs e)
    {
        UpdateBattleDuration();
    }

    private void UpdateBattleDuration()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(UpdateBattleDuration);
            return;
        }

        if (_timer.IsRunning)
        {
            if (ScopeTime == ScopeTime.Current && _awaitingSectionStart)
            {
                // Freeze to last section elapsed until new data arrives
                BattleDuration = _lastSectionElapsed;
                return;
            }

            var elapsed = ScopeTime == ScopeTime.Total
                ? _timer.Elapsed
                : _timer.Elapsed - _sectionStartElapsed;

            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            BattleDuration = elapsed;
        }
    }

    private void StorageOnNewSectionCreated()
    {
        _dispatcher.BeginInvoke(() =>
        {
    _logger.LogInformation("=== NewSectionCreated triggered (脱战自动清空) ===");
    
     // ⭐ 新增: 在自动清空之前,先尝试保存当前战斗的快照
            // 只有当前模式下才需要保存(全程模式不受脱战影响)
            if (ScopeTime == ScopeTime.Current && _storage.ReadOnlySectionedDpsDataList.Count > 0)
 {
        try
     {
   // 计算当前section的时长
  var sectionDuration = _timer.IsRunning 
       ? _timer.Elapsed - _sectionStartElapsed 
           : _lastSectionElapsed;
         
 // ⭐ 传递用户设置的最小时长
             _snapshotService.SaveCurrentSnapshot(_storage, sectionDuration, Options.MinimalDurationInSeconds);
        _logger.LogInformation("脱战自动清空时保存当前快照成功,时长: {Duration:F1}秒", sectionDuration.TotalSeconds);
            }
catch (Exception ex)
             {
         _logger.LogError(ex, "脱战自动清空时保存快照失败");
  }
       }

            // Freeze current section duration and await first datapoint of the new section
  _lastSectionElapsed = _timer.IsRunning ? _timer.Elapsed - _sectionStartElapsed : TimeSpan.Zero;
   _awaitingSectionStart = true;
       UpdateBattleDuration();

      _logger.LogInformation("NewSection: awaiting={AwaitingStart}, lastElapsed={LastElapsed}",
  _awaitingSectionStart, _lastSectionElapsed);

    // ⭐ 关键修复: 不要在Active模式下停止定时器!
       // Active模式应该继续运行定时器,让它检查新数据到来时自动处理
          // 只需要设置 _awaitingSectionStart 标记,DataStorage_DpsDataUpdated会处理剩余逻辑

       if (AppConfig.DpsUpdateMode == DpsUpdateMode.Active)
     {
          // ❌ 移除: 不再停止定时器
     // StopDpsUpdateTimer();
     
   // ❌ 移除: 不再需要一次性恢复handler
   // 因为定时器会持续运行,当有新数据时DataStorage_DpsDataUpdated会自动处理
        
 // 清理可能残留的handler
   if (_resumeActiveTimerHandler != null)
        {
 _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
    _resumeActiveTimerHandler = null;
       }

_logger.LogInformation("NewSection (Active mode): Timer continues running, awaiting first datapoint");
   }
     else
      {
 // Passive模式不需要特殊处理,事件驱动会自动处理
        _logger.LogInformation("NewSection (Passive mode): Event-driven, no special handling needed");
     }

       // Do NOT clear current UI data here; wait until data for the new section arrives
        });
    }

    private void StorageOnPlayerInfoUpdated(PlayerInfo? info)
    {
        if (info == null)
        {
            return;
        }

        foreach (var subViewModel in StatisticData.Values)
        {
            if (!subViewModel.DataDictionary.TryGetValue(info.UID, out var slot))
            {
                continue;
            }

            if (_dispatcher.CheckAccess())
            {
                ApplyUpdate();
            }
            else
            {
                _dispatcher.BeginInvoke((Action)ApplyUpdate);
            }

            continue;

            void ApplyUpdate()
            {
                slot.Player.Name = info.Name ?? slot.Player.Name;
                slot.Player.Class = info.ProfessionID.GetClassNameById();
                slot.Player.Spec = info.Spec;
                slot.Player.Uid = info.UID;

                if (_storage.CurrentPlayerInfo.UID == info.UID)
                {
                    subViewModel.CurrentPlayerSlot = slot;
                }
            }
        }
    }

    partial void OnScopeTimeChanged(ScopeTime value)
    {
        foreach (var subViewModel in StatisticData.Values)
        {
            subViewModel.ScopeTime = value;
        }

        UpdateBattleDuration();
        UpdateData();

        // Notify that CurrentPlayerSlot might have changed
        OnPropertyChanged(nameof(CurrentStatisticData));
    }

    partial void OnStatisticIndexChanged(StatisticType value)
    {
        OnPropertyChanged(nameof(CurrentStatisticData));
        UpdateData();
    }

    // ⭐ 新增: 当IsIncludeNpcData改变时刷新数据
    partial void OnIsIncludeNpcDataChanged(bool value)
    {
      _logger.LogDebug($"IsIncludeNpcData changed to: {value}");
        
        // ⭐ 新增: 如果取消勾选,清除所有StatisticData中的NPC数据
      if (!value)
        {
            _logger.LogInformation("Removing NPC data from UI (IsIncludeNpcData=false)");
            
       // 遍历所有统计类型的SubViewModel
    foreach (var subViewModel in StatisticData.Values)
            {
           // 找出所有NPC的slot
      var npcSlots = subViewModel.Data
           .Where(slot => slot.Player.IsNpc)
.ToList(); // ToList()避免遍历时修改集合

     // 从UI中移除NPC数据
      foreach (var npcSlot in npcSlots)
          {
         _dispatcher.Invoke(() =>
    {
          subViewModel.Data.Remove(npcSlot);
     _logger.LogDebug($"Removed NPC slot: UID={npcSlot.Player.Uid}, Name={npcSlot.Player.Name}");
       });
          }

      _logger.LogInformation($"Removed {npcSlots.Count} NPC slots from {subViewModel.GetType().Name}");
          }
   }
      
 // 刷新当前显示的数据
        UpdateData();
    }

    // ⭐ 新增: 服务器切换事件处理
    private void StorageOnServerChanged(string currentServer, string prevServer)
    {
 _dispatcher.BeginInvoke(() =>
   {
 _logger.LogInformation("服务器切换: {Prev} -> {Current}", prevServer, currentServer);
         
     // 在全程模式下,服务器切换时保存全程快照
       if (ScopeTime == ScopeTime.Total && _storage.ReadOnlyFullDpsDataList.Count > 0)
  {
    try
    {
      // ⭐ 传递用户设置的最小时长
     _snapshotService.SaveTotalSnapshot(_storage, BattleDuration, Options.MinimalDurationInSeconds);
         _logger.LogInformation("服务器切换时保存全程快照成功");
  }
     catch (Exception ex)
   {
        _logger.LogError(ex, "服务器切换时保存快照失败");
     }
       }
        });
    }

    // ⭐ 新增: 加载快照命令
    [RelayCommand]
    private void LoadSnapshot(BattleSnapshotData snapshot)
    {
        if (snapshot == null)
 {
 _logger.LogWarning("尝试加载空快照");
    return;
        }

    try
{
    _logger.LogInformation("加载快照: {Label}", snapshot.DisplayLabel);
    
   // ⭐ 进入快照查看模式
            EnterSnapshotViewMode(snapshot);
        }
   catch (Exception ex)
   {
         _logger.LogError(ex, "加载快照失败: {Label}", snapshot.DisplayLabel);
  }
    }
    
    /// <summary>
    /// ⭐ 新增: 进入快照查看模式
    /// </summary>
  private void EnterSnapshotViewMode(BattleSnapshotData snapshot)
  {
    _dispatcher.Invoke(() =>
   {
    _logger.LogInformation("=== 进入快照查看模式 ===");

  // 1. 保存当前状态
       _wasPassiveMode = AppConfig.DpsUpdateMode == DpsUpdateMode.Passive;
       _wasTimerRunning = _dpsUpdateTimer?.IsEnabled ?? false;
       
      // 2. 停止DPS更新定时器
  if (_dpsUpdateTimer != null)
 {
  _dpsUpdateTimer.Stop();
  _logger.LogDebug("已停止DPS更新定时器");
  }
  
          // ⭐ 新增: 停止战斗时长定时器
if (_durationTimer != null)
        {
         _durationTimer.Stop();
    _logger.LogDebug("已停止战斗时长定时器");
          }
          
         // ⭐ 新增: 停止主计时器(Stopwatch)
          if (_timer.IsRunning)
          {
       _timer.Stop();
 _logger.LogDebug("已停止主计时器");
          }
    
   // 3. 取消订阅实时数据事件
     _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
   _storage.NewSectionCreated -= StorageOnNewSectionCreated;
        _logger.LogDebug("已取消订阅实时数据事件");
       
      // 4. 设置快照模式标记
        IsViewingSnapshot = true;
  CurrentSnapshot = snapshot;
            
   // 5. 加载快照数据到UI
LoadSnapshotDataToUI(snapshot);
    
      _logger.LogInformation("快照查看模式已启动: {Label}, 战斗时长: {Duration}", 
    snapshot.DisplayLabel, snapshot.Duration);
     });
 }

    /// <summary>
    /// ⭐ 新增: 退出快照查看模式,恢复实时统计
    /// </summary>
 [RelayCommand]
 private void ExitSnapshotViewMode()
 {
        _dispatcher.Invoke(() =>
     {
        _logger.LogInformation("=== 退出快照查看模式 ===");
 
   // 1. 清除快照状态
   IsViewingSnapshot = false;
   CurrentSnapshot = null;
          
     // 2. 清空UI数据
 foreach (var subVm in StatisticData.Values)
       {
              subVm.Reset();
         }
     
   // 3. 恢复DPS更新机制
if (_wasPassiveMode)
   {
       _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
     _logger.LogDebug("已恢复订阅DpsDataUpdated事件");
    }
   else if (_wasTimerRunning && _dpsUpdateTimer != null)
          {
    _dpsUpdateTimer.Start();
   _logger.LogDebug("已恢复DPS更新定时器");
  }
  
       // ⭐ 新增: 恢复战斗时长定时器
     if (_durationTimer != null && !_durationTimer.IsEnabled)
     {
  _durationTimer.Start();
  _logger.LogDebug("已恢复战斗时长定时器");
    }
        
   // ⭐ 新增: 恢复主计时器(如果之前在运行)
        // 注意:只有当有实时数据时才需要恢复
  var hasData = _storage.ReadOnlyFullDpsDataList.Count > 0 || 
     _storage.ReadOnlySectionedDpsDataList.Count > 0;
      if (hasData && !_timer.IsRunning)
   {
      _timer.Start();
 _logger.LogDebug("已恢复主计时器");
    }
 
    _storage.NewSectionCreated += StorageOnNewSectionCreated;
         
  // 4. 刷新实时数据
var dpsList = ScopeTime == ScopeTime.Total
      ? _storage.ReadOnlyFullDpsDataList
     : _storage.ReadOnlySectionedDpsDataList;
  
     UpdateData(dpsList);
          UpdateBattleDuration();
  
     _logger.LogInformation("已恢复实时DPS统计模式");
        });
    }

    /// <summary>
    /// ⭐ 新增: 将快照数据加载到UI显示
    /// </summary>
    private void LoadSnapshotDataToUI(BattleSnapshotData snapshot)
    {
        _logger.LogDebug("开始加载快照数据到UI...");
  
  // 1. 设置战斗时长
     BattleDuration = snapshot.Duration;
 
        // 2. 设置团队总伤/治疗
      TeamTotalDamage = snapshot.TeamTotalDamage;
 TeamTotalDps = snapshot.Duration.TotalSeconds > 0
  ? snapshot.TeamTotalDamage / snapshot.Duration.TotalSeconds
   : 0;

     // 3. 构建每个统计类型的数据
        var damageData = new Dictionary<long, DpsDataProcessed>();
   var healingData = new Dictionary<long, DpsDataProcessed>();
   var takenData = new Dictionary<long, DpsDataProcessed>();
    var npcTakenData = new Dictionary<long, DpsDataProcessed>();

     foreach (var (uid, playerData) in snapshot.Players)
        {
    // ⭐ 关键修复: 添加空值检查
   if (playerData == null)
        {
 _logger.LogWarning("Player data is null for UID: {Uid}", uid);
       continue;
}
        
      // 构建技能列表
    var damageSkills = ConvertSnapshotSkillsToViewModel(playerData.DamageSkills);
  var healingSkills = ConvertSnapshotSkillsToViewModel(playerData.HealingSkills);
  var takenSkills = ConvertSnapshotSkillsToViewModel(playerData.TakenSkills);
          
      var skillDisplayLimit = CurrentStatisticData?.SkillDisplayLimit ?? 8;
  var filteredDamage = skillDisplayLimit > 0 ? damageSkills.Take(skillDisplayLimit).ToList() : damageSkills;
  var filteredHealing = skillDisplayLimit > 0 ? healingSkills.Take(skillDisplayLimit).ToList() : healingSkills;
var filteredTaken = skillDisplayLimit > 0 ? takenSkills.Take(skillDisplayLimit).ToList() : takenSkills;

    // 解析职业
            Enum.TryParse<Classes>(playerData.Profession, out var playerClass);
    
      // 伤害统计
    if (playerData.TotalDamage > 0)
  {
    // 根据IsIncludeNpcData控制NPC显示
      bool shouldShow = !playerData.IsNpc || IsIncludeNpcData;
        if (shouldShow)
       {
    // ⭐ 使用默认的DpsData而不是null
         var dummyDpsData = new DpsData { UID = uid };
     damageData[uid] = new DpsDataProcessed(
  dummyDpsData,
     playerData.TotalDamage,
    0, // duration
         filteredDamage, damageSkills,
        filteredHealing, healingSkills,
  filteredTaken, takenSkills,
   playerData.Nickname,
      playerClass,
  ClassSpec.Unknown,
        playerData.CombatPower
      );
     }
       }

   // 治疗统计(不含NPC)
 if (playerData.TotalHealing > 0 && !playerData.IsNpc)
    {
         var dummyDpsData = new DpsData { UID = uid };
         healingData[uid] = new DpsDataProcessed(
dummyDpsData,
  playerData.TotalHealing,
          0,
     filteredDamage, damageSkills,
   filteredHealing, healingSkills,
  filteredTaken, takenSkills,
     playerData.Nickname,
 playerClass,
  ClassSpec.Unknown,
  playerData.CombatPower
        );
     }

   // 承伤统计
            if (playerData.TakenDamage > 0)
  {
     if (playerData.IsNpc)
      {
       // NPC承伤
        var dummyDpsData = new DpsData { UID = uid };
   npcTakenData[uid] = new DpsDataProcessed(
       dummyDpsData,
  playerData.TakenDamage,
   0,
    filteredDamage, damageSkills,
        filteredHealing, healingSkills,
  filteredTaken, takenSkills,
 playerData.Nickname,
 playerClass,
          ClassSpec.Unknown,
          playerData.CombatPower
           );
        }
    else
 {
    // 玩家承伤
   var dummyDpsData = new DpsData { UID = uid };
  takenData[uid] = new DpsDataProcessed(
  dummyDpsData,
          playerData.TakenDamage,
      0,
          filteredDamage, damageSkills,
        filteredHealing, healingSkills,
         filteredTaken, takenSkills,
     playerData.Nickname,
      playerClass,
         ClassSpec.Unknown,
     playerData.CombatPower
           );
 }
         }
     }

        // 4. 更新各个统计类型的UI
    StatisticData[StatisticType.Damage].UpdateDataOptimized(damageData, 0);
    StatisticData[StatisticType.Healing].UpdateDataOptimized(healingData, 0);
  StatisticData[StatisticType.TakenDamage].UpdateDataOptimized(takenData, 0);
        StatisticData[StatisticType.NpcTakenDamage].UpdateDataOptimized(npcTakenData, 0);

        _logger.LogDebug("快照数据加载完成: {Count}个玩家", snapshot.Players.Count);
    }

    /// <summary>
    /// ⭐ 新增: 将快照技能数据转换为ViewModel
    /// </summary>
    private List<SkillItemViewModel> ConvertSnapshotSkillsToViewModel(List<SnapshotSkillData> snapshotSkills)
    {
      if (snapshotSkills == null || snapshotSkills.Count == 0)
         return new List<SkillItemViewModel>();

        return snapshotSkills.Select(s => new SkillItemViewModel
  {
          SkillName = s.SkillName,
     TotalDamage = (long)s.TotalValue,
            HitCount = s.UseTimes,
    CritCount = s.CritTimes,
   AvgDamage = s.UseTimes > 0 ? (int)(s.TotalValue / (ulong)s.UseTimes) : 0,
  CritRate = s.UseTimes > 0 ? (double)s.CritTimes / s.UseTimes : 0
  }).ToList();
    }
}
