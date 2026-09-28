using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public static class ScenarioHandlerValidator
{
    public static ScenarioValidationResult Validate(
        ScenarioDefinition scenario,
        IEnumerable<StepType> supportedStepTypes)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(supportedStepTypes);

        var supported = supportedStepTypes.ToHashSet();
        var errors = new List<string>();

        ValidateSteps(
            scenario.Steps,
            "scenario.steps",
            supported,
            errors);

        return new ScenarioValidationResult(errors);
    }

    public static void ValidateOrThrow(
        ScenarioDefinition scenario,
        IEnumerable<StepType> supportedStepTypes)
    {
        var result = Validate(scenario, supportedStepTypes);

        if (!result.IsValid)
        {
            throw new ScenarioValidationException(result.Errors);
        }
    }

    private static void ValidateSteps(
        IReadOnlyList<ScenarioStep> steps,
        string path,
        IReadOnlySet<StepType> supported,
        ICollection<string> errors)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var stepPath = $"{path}[{index}]";

            if (!supported.Contains(step.Type))
            {
                errors.Add(
                    $"{stepPath}.type '{step.Type}' has no registered handler.");
            }

            if (step.Children.Count > 0)
            {
                ValidateSteps(
                    step.Children,
                    $"{stepPath}.children",
                    supported,
                    errors);
            }
        }
    }
}
