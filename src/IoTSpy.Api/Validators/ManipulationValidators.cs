using FluentValidation;
using IoTSpy.Api.Controllers;
using System.Text.RegularExpressions;

namespace IoTSpy.Api.Validators;

public class CreateRuleDtoValidator : AbstractValidator<CreateRuleDto>
{
    public CreateRuleDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HostPattern)
            .Must(RegexHelper.IsValidOrNull).When(x => x.HostPattern is not null)
            .WithMessage("HostPattern must be a valid regular expression.");
        RuleFor(x => x.PathPattern)
            .Must(RegexHelper.IsValidOrNull).When(x => x.PathPattern is not null)
            .WithMessage("PathPattern must be a valid regular expression.");
        RuleFor(x => x.BodyReplace)
            .Must(RegexHelper.IsValidOrNull).When(x => x.BodyReplace is not null)
            .WithMessage("BodyReplace must be a valid regular expression.");
        RuleFor(x => x.OverrideStatusCode)
            .InclusiveBetween(100, 599).When(x => x.OverrideStatusCode.HasValue)
            .WithMessage("OverrideStatusCode must be between 100 and 599.");
        RuleFor(x => x.DelayMs)
            .GreaterThanOrEqualTo(0).When(x => x.DelayMs.HasValue)
            .WithMessage("DelayMs must be non-negative.");
    }
}

public class UpdateRuleDtoValidator : AbstractValidator<UpdateRuleDto>
{
    public UpdateRuleDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.HostPattern)
            .Must(RegexHelper.IsValidOrNull).When(x => x.HostPattern is not null)
            .WithMessage("HostPattern must be a valid regular expression.");
        RuleFor(x => x.PathPattern)
            .Must(RegexHelper.IsValidOrNull).When(x => x.PathPattern is not null)
            .WithMessage("PathPattern must be a valid regular expression.");
        RuleFor(x => x.BodyReplace)
            .Must(RegexHelper.IsValidOrNull).When(x => x.BodyReplace is not null)
            .WithMessage("BodyReplace must be a valid regular expression.");
        RuleFor(x => x.OverrideStatusCode)
            .InclusiveBetween(100, 599).When(x => x.OverrideStatusCode.HasValue)
            .WithMessage("OverrideStatusCode must be between 100 and 599.");
        RuleFor(x => x.DelayMs)
            .GreaterThanOrEqualTo(0).When(x => x.DelayMs.HasValue)
            .WithMessage("DelayMs must be non-negative.");
    }
}

public class CreateBreakpointDtoValidator : AbstractValidator<CreateBreakpointDto>
{
    public CreateBreakpointDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScriptCode).NotEmpty();
        RuleFor(x => x.HostPattern)
            .Must(RegexHelper.IsValidOrNull).When(x => x.HostPattern is not null)
            .WithMessage("HostPattern must be a valid regular expression.");
        RuleFor(x => x.PathPattern)
            .Must(RegexHelper.IsValidOrNull).When(x => x.PathPattern is not null)
            .WithMessage("PathPattern must be a valid regular expression.");
    }
}

internal static class RegexHelper
{
    internal static bool IsValidOrNull(string? pattern)
    {
        if (pattern is null) return true;
        try { _ = new Regex(pattern); return true; }
        catch (ArgumentException) { return false; }
    }
}
