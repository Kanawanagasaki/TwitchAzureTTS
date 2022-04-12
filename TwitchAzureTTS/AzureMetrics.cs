namespace TwitchAzureTTS;

using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

internal static class AzureMetrics
{
    internal static string TenantId
    {
        get => Settings.Get("AzureMetricsTenantId");
        set => Settings.Set("AzureMetricsTenantId", value);
    }

    internal static string ResourceId
    {
        get => Settings.Get("AzureMetricsResourceId");
        set => Settings.Set("AzureMetricsResourceId", value);
    }

    internal static bool IsAuthorized { get; private set; } = false;

    internal static bool IsRunning { get; private set; }

    internal static MetricsQueryResult? Metrics { get; private set; }

    private static DefaultAzureCredential? _credential;
    private static ArmClient? _armClient;
    private static MetricsQueryClient? _metricsClient;

    private static DateTime _lastUpdate = DateTime.MinValue;

    static AzureMetrics()
    {
        IsRunning = true;
        Task.Run(async () =>
        {
            while (IsRunning)
            {
                if (!string.IsNullOrWhiteSpace(ResourceId) && !string.IsNullOrWhiteSpace(TenantId) && IsAuthorized)
                {
                    if (DateTime.Now - _lastUpdate > TimeSpan.FromMinutes(10))
                        UpdateMetrics();
                }

                await Task.Delay(15000);
            }
        });
    }

    internal static void LogIn()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
        {
            Logger.Warning("<AzureMetrics> TenantId is empty, enter it to access statistics");
            return;
        }

        try
        {
            _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true,
                VisualStudioTenantId = TenantId,
                SharedTokenCacheTenantId = TenantId,
                VisualStudioCodeTenantId = TenantId,
                InteractiveBrowserTenantId = TenantId
            });
            _armClient = new ArmClient(_credential);
            _armClient.GetSubscriptions();
            _metricsClient = new MetricsQueryClient(_credential);
            Logger.Info("<AzureMetrics> Authorization successful");
            IsAuthorized = true;
        }
        catch (Exception e)
        {
            Logger.Warning("<AzureMetrics> " + e.Message);
            _armClient = null;
            IsAuthorized = false;
        }
    }

    internal static GenericResourceData[]? GetResources()
    {
        if (!IsAuthorized)
            LogIn();
        if (!IsAuthorized)
            return null;
        if (_armClient is null)
            return null;

        try
        {
            var subs = _armClient.GetSubscriptions();
            var resources = subs.SelectMany(s => s.GetGenericResources(filter: "resourceType eq 'Microsoft.CognitiveServices/accounts'"));

            return resources?.Where(r => r.HasData)?.Select(r => r.Data)?.ToArray();
        }
        catch (Exception e)
        {
            Logger.Warning("<AzureMetrics> " + e.Message);
            _armClient = null;
            IsAuthorized = false;
            return null;
        }
    }

    internal static MetricsQueryResult? GetMetrics()
    {
        if (_metricsClient is null)
            return null;

        try
        {
            var query = _metricsClient.QueryResource
                (
                    ResourceId,
                    new[] { "SynthesizedCharacters" },
                    new() { Granularity = TimeSpan.FromDays(1), TimeRange = new(TimeSpan.FromDays(30)) }
                );
            return query.Value;
        }
        catch (Exception e)
        {
            Logger.Error("<AzureMetrics> In Get Metrics: " + e.Message);
            return null;
        }
    }

    internal static void UpdateMetrics()
    {
        var result = GetMetrics();
        if (result is not null)
        {
            Metrics = result;
            _lastUpdate = DateTime.Now;
            Renderer.Render();
        }
    }
}
