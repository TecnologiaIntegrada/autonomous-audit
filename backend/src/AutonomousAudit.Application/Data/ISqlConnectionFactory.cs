using System.Data;

namespace AutonomousAudit.Application.Data;

public interface ISqlConnectionFactory
{
    IDbConnection Create();
}
