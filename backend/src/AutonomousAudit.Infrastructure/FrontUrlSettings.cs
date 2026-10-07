using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class FrontUrlSettings : IFrontUrl
{
    public FrontUrlSettings(IOptions<FrontUrlOptions> options)
    {
        BaseUrl = string.IsNullOrWhiteSpace(options.Value.BaseUrl)
            ? "https://autonomousaudit.canada-software.com.br"
            : options.Value.BaseUrl.TrimEnd('/');
    }

    public string BaseUrl { get; }
}
