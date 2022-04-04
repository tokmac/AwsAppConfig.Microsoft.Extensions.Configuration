using Amazon;
using Microsoft.Extensions.Configuration;

namespace AwsAppConfig.Microsoft.Extensions.Configuration;

public static class AwsAppConfigConfiguratioBuilderExtensions
{
    public static IConfigurationBuilder AddAwsAppConfig(this IConfigurationBuilder builder)
    {
        var tempConfig = builder.Build();
        var configSection = tempConfig.GetSection("AwsAppConfig");

        if (configSection.GetChildren().Count() == 0)
            return builder;

        string applicationName = configSection.GetSection("ApplicationName").Value;
        string environmentName = configSection.GetSection("EnvironmentName").Value;
        string configurationName = configSection.GetSection("ConfigurationName").Value;
        string? reloadPeriodAsString = configSection.GetSection("ReloadPeriodInSeconds").Value;
        TimeSpan? reloadPeriod = string.IsNullOrWhiteSpace(reloadPeriodAsString) ? null : TimeSpan.FromSeconds(int.Parse(reloadPeriodAsString));
        RegionEndpoint regionEndpoint = RegionEndpoint.GetBySystemName(configSection.GetSection("Region").Value);

        return builder.Add(new AwsAppConfigConfigurationSource(applicationName,configurationName, environmentName, regionEndpoint, reloadPeriod));
    }
}
