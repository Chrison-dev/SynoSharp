using System.Text;
using SynoSharp.Ssh;

namespace SynoSharp.Provisioning;

public enum ActionKind
{
    Create,
    Delete,
    Skip,
}

/// <summary>
/// One reconciled action. <see cref="Command"/> is the on-box command to run for a
/// mutation (null for <see cref="ActionKind.Skip"/>), and <see cref="Reason"/>
/// explains the decision (for dry-run output).
/// </summary>
public sealed record PlannedAction
{
    public required ActionKind Kind { get; init; }

    /// <summary><c>group</c> / <c>user</c> / <c>share</c>.</summary>
    public required string ResourceType { get; init; }

    public required string Name { get; init; }
    public required string Reason { get; init; }
    public SynologyCommand? Command { get; init; }

    public static PlannedAction Create(string type, string name, SynologyCommand command, string reason)
        => new() { Kind = ActionKind.Create, ResourceType = type, Name = name, Command = command, Reason = reason };

    public static PlannedAction Delete(string type, string name, SynologyCommand command, string reason)
        => new() { Kind = ActionKind.Delete, ResourceType = type, Name = name, Command = command, Reason = reason };

    public static PlannedAction Skip(string type, string name, string reason)
        => new() { Kind = ActionKind.Skip, ResourceType = type, Name = name, Reason = reason };
}

/// <summary>The reconciled plan — what would change. Render it for a dry-run.</summary>
public sealed record SynologyPlan
{
    public required IReadOnlyList<PlannedAction> Actions { get; init; }

    /// <summary>Just the actions that would mutate the box.</summary>
    public IEnumerable<PlannedAction> Mutations => Actions.Where(a => a.Kind != ActionKind.Skip);

    public bool HasChanges => Mutations.Any();

    public string Render()
    {
        var sb = new StringBuilder();
        foreach (var a in Actions)
        {
            var marker = a.Kind switch
            {
                ActionKind.Create => "+ create",
                ActionKind.Delete => "- delete",
                _ => "= skip  ",
            };
            sb.Append(marker).Append(' ').Append(a.ResourceType).Append(' ').Append(a.Name)
              .Append("  (").Append(a.Reason).Append(')');
            if (a.Command is not null)
            {
                sb.Append("\n      $ ").Append(a.Command.Render());
            }
            sb.Append('\n');
        }
        var changes = Mutations.Count();
        sb.Append(changes == 0 ? "No changes." : $"{changes} change(s) to apply.");
        return sb.ToString();
    }
}

/// <summary>Outcome of executing (or dry-running) a single planned action.</summary>
public sealed record ActionOutcome(PlannedAction Action, bool Applied, string Message);

public sealed record SynologyApplyResult
{
    public required IReadOnlyList<ActionOutcome> Outcomes { get; init; }
    public bool AllSucceeded => Outcomes.All(o => o.Applied || o.Action.Kind == ActionKind.Skip);
}
