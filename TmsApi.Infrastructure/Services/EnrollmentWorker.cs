using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Interfaces;
public class EnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnrollmentWorker> _logger;

    public EnrollmentWorker(IServiceScopeFactory scopeFactory, ILogger<EnrollmentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void ProcessBatch()
    {
        _logger.LogInformation("EnrollmentWorker starting batch processing.");

        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();

        // var record = svc.EnrollmentAsync("S-001", 101).GetAwaiter().GetResult();

        // _logger.LogInformation("Processed enrollment batch record {EnrollmentId} for {StudentId}.", record.Id, record.StudentId);
    }
}
