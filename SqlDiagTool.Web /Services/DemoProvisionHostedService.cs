using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SqlDiagTool.Configuration;
using SqlDiagTool.Demo;

namespace SqlDiagTool.Web.Services;

// Runs the demo DB provisioner once at startup when AutoCreateDemoDatabases is true and server connection string is set.
public sealed class DemoProvisionHostedService : IHostedService
{
    private readonly SqlDiagOptions _options;
    private readonly ILogger<DemoProvisionHostedService> _logger;

    public DemoProvisionHostedService(IOptions<SqlDiagOptions> options, ILogger<DemoProvisionHostedService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateDemoDatabases || string.IsNullOrWhiteSpace(_options.DemoServerConnectionString))
            return;

        var provisioner = new DemoDatabaseProvisioner();
        var result = await provisioner.EnsureDatabasesAsync(_options.DemoServerConnectionString);

        if (result.Success)
            _logger.LogInformation("Demo databases provisioned.");
        else
            _logger.LogWarning("Demo databases unavailable: {Message}", result.ErrorMessage);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
