using System.Globalization;

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

        if (scenario.SchemaVersion != ScenarioSchema.CurrentVersion)
        {
            errors.Add(
                $"scenario.schemaVersion '{scenario.SchemaVersion}' is not supported. " +
                $"Supported version is {ScenarioSchema.CurrentVersion}.");
        }

        if (string.IsNullOrWhiteSpace(scenario.Name))
        {
            errors.Add("scenario.name is required.");
        }

        if (scenario.TimeoutMs is <= 0)
        {
            errors.Add("scenario.timeoutMs must be greater than zero when specified.");
        }

        if (!string.IsNullOrWhiteSpace(scenario.BrowserProfile) &&
            !IsValidBrowserProfileName(scenario.BrowserProfile))
        {
            errors.Add(
                "scenario.browserProfile must start with an alphanumeric character and contain only alphanumeric characters, '.', '_' or '-' (maximum 64 characters).");
        }

        if (scenario.Steps.Count == 0)
        {
            errors.Add("scenario.steps must contain at least one step.");
        }
        else
        {
            var stepIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ValidateSteps(scenario.Steps, "scenario.steps", errors, stepIds);
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

    private static bool IsValidBrowserProfileName(string value)
    {
        if (value.Length is < 1 or > 64 ||
            !char.IsLetterOrDigit(value[0]))
        {
            return false;
        }

        return value.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '.' or '_' or '-');
    }

    private static void ValidateSteps(
        IReadOnlyList<ScenarioStep> steps,
        string path,
        ICollection<string> errors,
        IDictionary<string, string> stepIds)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            ValidateStep(steps[index], $"{path}[{index}]", errors, stepIds);
        }
    }

    private static void ValidateStep(
        ScenarioStep step,
        string path,
        ICollection<string> errors,
        IDictionary<string, string> stepIds)
    {
        if (!string.IsNullOrWhiteSpace(step.Id))
        {
            var normalizedId = step.Id.Trim();
            if (stepIds.TryGetValue(normalizedId, out var existingPath))
            {
                errors.Add($"{path}.id '{normalizedId}' duplicates {existingPath}.id.");
            }
            else
            {
                stepIds.Add(normalizedId, path);
            }
        }

        if (string.IsNullOrWhiteSpace(step.Selector) &&
            step.Locator is null &&
            step.Type is StepType.Click or StepType.FillText or StepType.PasteText or StepType.ReadText)
        {
            errors.Add($"{path}.selector or locator is required for {step.Type}.");
        }

        if (!string.IsNullOrWhiteSpace(step.Selector) &&
            step.Locator is not null)
        {
            errors.Add(
                $"{path} must not define both selector and locator.");
        }

        if (step.Locator is not null &&
            string.IsNullOrWhiteSpace(step.Locator.Value))
        {
            errors.Add($"{path}.locator.value is required.");
        }

        if (string.IsNullOrWhiteSpace(step.Url) && step.Type is StepType.OpenUrl or StepType.CallApi)
        {
            errors.Add($"{path}.url is required for {step.Type}.");
        }

        if (string.IsNullOrWhiteSpace(step.Output) && step.Type == StepType.ReadText)
        {
            errors.Add($"{path}.output is required for ReadText.");
        }

        if (step.Type == StepType.CallApi && step.TimeoutMs is <= 0)
        {
            errors.Add($"{path}.timeoutMs must be greater than zero for CallApi.");
        }

        if (step.Type == StepType.Delay)
        {
            if (!step.TimeoutMs.HasValue && string.IsNullOrWhiteSpace(step.Value))
            {
                errors.Add($"{path}.timeoutMs or value is required for Delay.");
            }
            else if (step.TimeoutMs is < 0)
            {
                errors.Add($"{path}.timeoutMs must be zero or greater for Delay.");
            }
            else if (!step.TimeoutMs.HasValue &&
                     !string.IsNullOrWhiteSpace(step.Value) &&
                     !ScenarioVariableInterpolator.IsExactVariableReference(step.Value) &&
                     (!int.TryParse(
                          step.Value,
                          NumberStyles.None,
                          CultureInfo.InvariantCulture,
                          out var delayMs) ||
                      delayMs < 0))
            {
                errors.Add(
                    $"{path}.value must be a non-negative integer number of milliseconds for Delay.");
            }
        }

        if (step.Type == StepType.WaitFor &&
            string.IsNullOrWhiteSpace(step.Selector) &&
            step.Locator is null &&
            string.IsNullOrWhiteSpace(step.Value) &&
            (step.Parameters is null || step.Parameters.Count == 0))
        {
            errors.Add($"{path} requires selector, value or parameters for WaitFor.");
        }

        if (step.Type is StepType.If or StepType.Loop && step.Children.Count == 0)
        {
            errors.Add($"{path}.children must contain at least one step for {step.Type}.");
        }

        if (step.Type == StepType.If &&
            (string.IsNullOrWhiteSpace(step.Value) ||
             (!ScenarioVariableInterpolator.IsExactVariableReference(step.Value) &&
              !bool.TryParse(step.Value, out _))))
        {
            errors.Add($"{path}.value must be 'true', 'false', or a variable reference for If.");
        }

        if (step.Type == StepType.Loop &&
            (string.IsNullOrWhiteSpace(step.Value) ||
             (!ScenarioVariableInterpolator.IsExactVariableReference(step.Value) &&
              (!int.TryParse(step.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var loopCount) ||
               loopCount < 0))))
        {
            errors.Add($"{path}.value must be a non-negative integer or a variable reference for Loop.");
        }

        if (step.RetryCount is < 0)
        {
            errors.Add($"{path}.retryCount must be zero or greater.");
        }
        else if (step.RetryCount == int.MaxValue)
        {
            errors.Add($"{path}.retryCount must be at most {int.MaxValue - 1}.");
        }

        if (step.RetryDelayMs is < 0)
        {
            errors.Add($"{path}.retryDelayMs must be zero or greater.");
        }

        if (step.Children.Count > 0)
        {
            ValidateSteps(step.Children, $"{path}.children", errors, stepIds);
        }
    }
}
