namespace DesktopAutomationBot.Core;

public static class ScenarioDefinitionNormalizer
{
    public static ScenarioDefinition Normalize(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var reservedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectExplicitIds(scenario.Steps, reservedIds);

        return scenario with
        {
            Steps = NormalizeSteps(scenario.Steps, parentStructuralId: null, reservedIds),
        };
    }

    private static List<ScenarioStep> NormalizeSteps(
        IReadOnlyList<ScenarioStep> steps,
        string? parentStructuralId,
        HashSet<string> reservedIds)
    {
        var normalized = new List<ScenarioStep>(steps.Count);

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var structuralId = parentStructuralId is null
                ? $"step-{index + 1:D3}"
                : $"{parentStructuralId}-{index + 1:D3}";

            var id = string.IsNullOrWhiteSpace(step.Id)
                ? ReserveGeneratedId(structuralId, reservedIds)
                : step.Id.Trim();

            normalized.Add(step with
            {
                Id = id,
                Enabled = step.Enabled == true
                    ? null
                    : step.Enabled,
                Children = NormalizeSteps(step.Children, structuralId, reservedIds),
            });
        }

        return normalized;
    }

    private static void CollectExplicitIds(IReadOnlyList<ScenarioStep> steps, HashSet<string> reservedIds)
    {
        foreach (var step in steps)
        {
            if (!string.IsNullOrWhiteSpace(step.Id))
            {
                reservedIds.Add(step.Id.Trim());
            }

            if (step.Children.Count > 0)
            {
                CollectExplicitIds(step.Children, reservedIds);
            }
        }
    }

    private static string ReserveGeneratedId(string structuralId, HashSet<string> reservedIds)
    {
        if (reservedIds.Add(structuralId))
        {
            return structuralId;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{structuralId}-{suffix}";
            if (reservedIds.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
