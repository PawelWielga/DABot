namespace DesktopAutomationBot.Core;

public sealed record ScenarioValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class ScenarioValidationException : Exception
{
    public ScenarioValidationException(IReadOnlyList<string> errors)
        : base("Scenario validation failed.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed class ScenarioDefinitionValidator
{
    public ScenarioValidationResult Validate(ScenarioDefinition? scenario)
    {
        var errors = new List<string>();

        if (scenario is null)
        {
            errors.Add("Scenario definition is required.");
            return new ScenarioValidationResult(errors);
        }

        if (string.IsNullOrWhiteSpace(scenario.Name))
        {
            errors.Add("scenario.name is required.");
        }

        if (scenario.Steps.Count == 0)
        {
            errors.Add("scenario.steps must contain at least one step.");
        }
        else
        {
            ValidateSteps(scenario.Steps, "scenario.steps", errors);
        }

        return new ScenarioValidationResult(errors);
    }

    public void ValidateOrThrow(ScenarioDefinition? scenario)
    {
        var result = Validate(scenario);
        if (!result.IsValid)
        {
            throw new ScenarioValidationException(result.Errors);
        }
    }

    private static void ValidateSteps(IReadOnlyList<ScenarioStep> steps, string path, ICollection<string> errors)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            ValidateStep(steps[index], $"{path}[{index}]", errors);
        }
    }

    private static void ValidateStep(ScenarioStep step, string path, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(step.Selector) &&
            step.Type is StepType.Click or StepType.FillText or StepType.PasteText or StepType.ReadText)
        {
            errors.Add($"{path}.selector is required for {step.Type}.");
        }

        if (string.IsNullOrWhiteSpace(step.Url) && step.Type == StepType.OpenUrl)
        {
            errors.Add($"{path}.url is required for OpenUrl.");
        }

        if (step.Type is StepType.If or StepType.Loop && step.Children.Count == 0)
        {
            errors.Add($"{path}.children must contain at least one step for {step.Type}.");
        }

        if (step.Children.Count > 0)
        {
            ValidateSteps(step.Children, $"{path}.children", errors);
        }
    }
}
