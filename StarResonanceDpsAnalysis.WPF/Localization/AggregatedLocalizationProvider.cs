using System.Collections.ObjectModel;
using System.Globalization;
using System.Resources;
using System.Windows;
using Microsoft.Extensions.Logging;
using WPFLocalizeExtension.Providers;

namespace StarResonanceDpsAnalysis.WPF.Localization;

/// <summary>
/// Aggregates multiple localization providers (ResX and JSON) to provide a unified localization experience.
/// </summary>
public class AggregatedLocalizationProvider : ILocalizationProvider
{
    private readonly JsonLocalizationProvider _jsonProvider;
    private readonly ILogger _logger;
    private readonly ResxLocalizationProvider _resxProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregatedLocalizationProvider"/> class.
    /// </summary>
    /// <param name="resxProvider">The ResX localization provider.</param>
    /// <param name="jsonProvider">The JSON localization provider.</param>
    /// <param name="logger"></param>
    public AggregatedLocalizationProvider(ResxLocalizationProvider resxProvider, JsonLocalizationProvider jsonProvider,
        ILogger logger)
    {
        _logger = logger;

        _jsonProvider = jsonProvider ?? throw new ArgumentNullException(nameof(jsonProvider));
        _resxProvider = resxProvider; // ?? ResxLocalizationProvider.Instance;

        // Subscribe to inner providers to bubble up events
        _jsonProvider.ProviderChanged += (_, e) => OnProviderChanged(e);
        _jsonProvider.ProviderError += (_, e) => OnProviderError(e);
        _jsonProvider.ValueChanged += (_, e) => OnValueChanged(e);

        _resxProvider.ProviderChanged += (_, e) => OnProviderChanged(e);
        _resxProvider.ProviderError += (_, e) => OnProviderError(e);
        _resxProvider.ValueChanged += (_, e) => OnValueChanged(e);

        RefreshAvailableCultures();
    }

    /// <summary>
    /// Gets the fully qualified resource key for the specified key and target.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <param name="target">The dependency object target.</param>
    /// <returns>The fully qualified resource key.</returns>
    public FullyQualifiedResourceKeyBase GetFullyQualifiedResourceKey(string key, DependencyObject target)
    {
        // Prefer resx provider for fully qualified key resolution
        return _resxProvider.GetFullyQualifiedResourceKey(key, target);
    }

    /// <summary>
    /// Retrieves the localized object for the given key, target, and culture.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <param name="target">The dependency object target.</param>
    /// <param name="culture">The culture to use for localization.</param>
    /// <returns>The localized object, or null if not found.</returns>
    public object? GetLocalizedObject(string? key, DependencyObject? target, CultureInfo? culture)
    {
        return ProbeKey(key, target, culture).SuccessStep?.Value;
    }

    /// <summary>
    /// Gets the collection of available cultures from all aggregated providers.
    /// </summary>
    public ObservableCollection<CultureInfo> AvailableCultures { get; } = new();

    /// <summary>
    /// Occurs when the provider changes.
    /// </summary>
    public event ProviderChangedEventHandler? ProviderChanged;

    /// <summary>
    /// Occurs when a provider error occurs.
    /// </summary>
    public event ProviderErrorEventHandler? ProviderError;

    /// <summary>
    /// Occurs when a value changes.
    /// </summary>
    public event ValueChangedEventHandler? ValueChanged;

    /// <summary>
    /// Collects a detailed trace of how the localization key is resolved across providers.
    /// </summary>
    public LocalizationLookupResult ProbeKey(string? key, DependencyObject? target, CultureInfo? culture,
        bool includeInvariantFallback = true)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return LocalizationLookupResult.Empty;
        }

        var steps = new List<LocalizationLookupStep>();
        var current = culture ?? CultureInfo.CurrentUICulture;

        while (!Equals(current, CultureInfo.InvariantCulture))
        {
            var jsonHit = CaptureStep(_jsonProvider, "JSON", key, target, current, steps);
            if (jsonHit != null || key.StartsWith("JsonDictionary"))
            {
                return BuildResult(jsonHit, steps);
            }

            var resxHit = CaptureStep(_resxProvider, "RESX", key, target, current, steps);
            if (resxHit != null)
            {
                return BuildResult(resxHit, steps);
            }

            current = current.Parent;
            if (Equals(current, CultureInfo.InvariantCulture))
            {
                break;
            }
        }

        if (includeInvariantFallback)
        {
            var invariantJson = CaptureStep(_jsonProvider, "JSON", key, target, CultureInfo.InvariantCulture, steps);
            if (invariantJson != null)
            {
                return BuildResult(invariantJson, steps);
            }

            var invariantResx = CaptureStep(_resxProvider, "RESX", key, target, CultureInfo.InvariantCulture, steps);
            if (invariantResx != null)
            {
                return BuildResult(invariantResx, steps);
            }
        }

        return BuildResult(null, steps);
    }

    private void OnProviderChanged(ProviderChangedEventArgs e)
    {
        RefreshAvailableCultures();
        ProviderChanged?.Invoke(this, e);
    }

    private void OnProviderError(ProviderErrorEventArgs e)
    {
        ProviderError?.Invoke(this, e);
    }

    private void OnValueChanged(ValueChangedEventArgs e)
    {
        ValueChanged?.Invoke(this, e);
    }

    private void RefreshAvailableCultures()
    {
        var cultures = Enumerable.Empty<CultureInfo>()
            .Concat(_jsonProvider.AvailableCultures ?? Enumerable.Empty<CultureInfo>())
            .Concat(_resxProvider.AvailableCultures ?? Enumerable.Empty<CultureInfo>())
            .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        AvailableCultures.Clear();
        foreach (var c in cultures)
            AvailableCultures.Add(c);
    }

    private LocalizationLookupStep? CaptureStep(ILocalizationProvider provider, string providerName, string key,
        DependencyObject? target, CultureInfo culture, ICollection<LocalizationLookupStep> steps)
    {
        try
        {
            var value = provider.GetLocalizedObject(key, target, culture);
            var step = new LocalizationLookupStep(providerName, culture, value);
            steps.Add(step);
            return value != null ? step : null;
        }
        catch (MissingManifestResourceException)
        {
            _logger.LogDebug("Missing resource key:{0}", key);
            return null;
        }
    }

    private static LocalizationLookupResult BuildResult(LocalizationLookupStep? successStep,
        ICollection<LocalizationLookupStep> steps)
    {
        return new LocalizationLookupResult(successStep, steps.ToArray());
    }
}

/// <summary>
/// Represents a single lookup attempt against a specific provider/culture pair.
/// </summary>
public sealed record LocalizationLookupStep(string Provider, CultureInfo Culture, object? Value)
{
    public bool IsHit => Value is not null;
}

/// <summary>
/// Aggregates all lookup attempts and indicates which one succeeded.
/// </summary>
public sealed class LocalizationLookupResult
{
    public LocalizationLookupResult(LocalizationLookupStep? successStep, IReadOnlyList<LocalizationLookupStep> steps)
    {
        SuccessStep = successStep;
        Steps = steps;
    }

    public static LocalizationLookupResult Empty { get; } = new(null, Array.Empty<LocalizationLookupStep>());

    public LocalizationLookupStep? SuccessStep { get; }

    public IReadOnlyList<LocalizationLookupStep> Steps { get; }

    public bool HasValue => SuccessStep is not null;
}