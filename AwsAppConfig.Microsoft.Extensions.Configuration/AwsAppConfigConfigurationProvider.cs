using Amazon;
using Amazon.AppConfigData;
using Amazon.AppConfigData.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

namespace AwsAppConfig.Microsoft.Extensions.Configuration;

public class AwsAppConfigConfigurationProvider : ConfigurationProvider
{
    private readonly IAmazonAppConfigData _appConfigDataClient;
    private readonly string _environmentName;
    private readonly string _applicationName;
    private readonly string _configurationName;
    private readonly TimeSpan _loadTimeout;

    private TimeSpan _reloadPeriod;
    private string? _nextPollConfigurationToken;


    public AwsAppConfigConfigurationProvider(AwsAppConfigConfigurationSource source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        _appConfigDataClient = new AmazonAppConfigDataClient(new AmazonAppConfigDataConfig
        {
            RegionEndpoint = source.RegionEndpoint ?? RegionEndpoint.EUWest1
        });

        _environmentName = source.EnvironmentName;
        _applicationName = source.ApplicationName;
        _configurationName = source.ConfigurationName;
        _reloadPeriod = source.ReloadPeriod;
        _loadTimeout = source.LoadTimeout;

        ChangeToken.OnChange(() =>
        {
            CancellationTokenSource cts = new CancellationTokenSource(_reloadPeriod);
            CancellationChangeToken cancellationChangeToken = new CancellationChangeToken(cts.Token);
            return cancellationChangeToken;
            }, async () => { await LoadAsync(); });
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    private async Task LoadAsync()
    {
        using (CancellationTokenSource cts = new CancellationTokenSource(_loadTimeout))
        {
            Dictionary<string, string>? kvPairs = await GetConfigurationsAsync(cts.Token);

            if (kvPairs != null && kvPairs.Count() > 0)
            {
                Data = kvPairs;
                OnReload();
            };
        }
    }

    private async Task<Dictionary<string, string>?> GetConfigurationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_nextPollConfigurationToken))
                await StartConfigurationSessionRequest(cancellationToken);


            var request = new GetLatestConfigurationRequest
            {
                ConfigurationToken = _nextPollConfigurationToken,
            };

            var response = await _appConfigDataClient.GetLatestConfigurationAsync(request, cancellationToken);

            _nextPollConfigurationToken = response.NextPollConfigurationToken;
            _reloadPeriod = TimeSpan.FromSeconds(response.NextPollIntervalInSeconds);

            if (response.Configuration is null)
                return null;

            Dictionary<string, string> result = await DeserializeDataAsync(response.Configuration);

            return result;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    private async Task StartConfigurationSessionRequest(CancellationToken cancellationToken)
    {
        var request = new StartConfigurationSessionRequest
        {
            ApplicationIdentifier = _applicationName,
            EnvironmentIdentifier = _environmentName,
            ConfigurationProfileIdentifier = _configurationName,
            RequiredMinimumPollIntervalInSeconds = _reloadPeriod.Seconds
        };

        var response = await _appConfigDataClient.StartConfigurationSessionAsync(request,cancellationToken);

        if(response.HttpStatusCode == System.Net.HttpStatusCode.Created)
            _nextPollConfigurationToken = response.InitialConfigurationToken;
    }

    private async Task<Dictionary<string, string>> DeserializeDataAsync(MemoryStream stream)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();

        string json = null;

        using (StreamReader reader = new StreamReader(stream))
        {
            json = await reader.ReadToEndAsync();
        }

        if (!string.IsNullOrEmpty(json))
        {
            result = GetJsonAsConfiguration(json);
        }

        return result;
    }

    private Dictionary<string, string> GetJsonAsConfiguration(string json)
    {
        IEnumerable<(string Path, string P)> GetLeaves(string path, JsonProperty p)
        {
            return p.Value.ValueKind != JsonValueKind.Object
                                ? new[] { (Path: path == null ? p.Name : path + ":" + p.Name, p.Value.ToString()) }
                                : p.Value.EnumerateObject().SelectMany(child => GetLeaves(path == null ? p.Name : path + ":" + p.Name, child));
        }

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            return document.RootElement.EnumerateObject()
                .SelectMany(p => GetLeaves(null, p))
                .ToDictionary(k => k.Path, v => v.P);
        }
    }
}
