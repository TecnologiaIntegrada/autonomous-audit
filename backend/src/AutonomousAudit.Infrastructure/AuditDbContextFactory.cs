using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutonomousAudit.Infrastructure;

public class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=postgres;Port=5432;Database=appdb;Username=app;Password=SfoxdV6ctjQYEVHhvWyQQE4GgGnY0bT";

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AuditDbContext(options);
    }
}
