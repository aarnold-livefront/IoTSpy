using FluentValidation;
using IoTSpy.Api.Controllers;
using System.Text.RegularExpressions;

namespace IoTSpy.Api.Validators;

public class StartScanDtoValidator : AbstractValidator<StartScanDto>
{
    private static readonly Regex PortRangeFormat =
        new(@"^\d+(-\d+)?(,\d+(-\d+)?)*$", RegexOptions.Compiled);

    public StartScanDtoValidator()
    {
        RuleFor(x => x.PortRange)
            .Must(r => r is null || PortRangeFormat.IsMatch(r))
            .WithMessage("PortRange must be in format '1-1024', '22,80,443', or '1-1024,8080-9090'.")
            .Must(AllPortsInRange)
            .WithMessage("All port numbers must be between 1 and 65535.")
            .When(x => x.PortRange is not null);
        RuleFor(x => x.MaxConcurrency)
            .InclusiveBetween(1, 1000).When(x => x.MaxConcurrency.HasValue)
            .WithMessage("MaxConcurrency must be between 1 and 1000.");
        RuleFor(x => x.TimeoutMs)
            .InclusiveBetween(100, 60_000).When(x => x.TimeoutMs.HasValue)
            .WithMessage("TimeoutMs must be between 100 and 60000.");
    }

    private static bool AllPortsInRange(string? portRange)
    {
        if (portRange is null) return true;
        try
        {
            foreach (var segment in portRange.Split(','))
            {
                if (segment.Contains('-'))
                {
                    var parts = segment.Split('-');
                    if (!int.TryParse(parts[0], out var lo) || !int.TryParse(parts[1], out var hi))
                        return false;
                    if (lo < 1 || hi > 65535 || lo > hi) return false;
                }
                else
                {
                    if (!int.TryParse(segment, out var port) || port < 1 || port > 65535)
                        return false;
                }
            }
            return true;
        }
        catch { return false; }
    }
}
