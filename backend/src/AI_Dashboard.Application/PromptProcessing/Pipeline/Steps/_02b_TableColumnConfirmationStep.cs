using AI_Dashboard.Application.PromptProcessing.Models;
 
namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;
 
/// <summary>
/// Confirms the table/column that SchemaResolutionStep picked via vector similarity,
/// before any SQL is generated. Deliberately does NOT call Ollama — the table and
/// column names are already sitting in context.SchemaMap from the embedding search
/// that just ran, so the question is built from a plain string template. This keeps
/// the confirmation instant (no model round-trip) as opposed to the AI-generated
/// option lists used elsewhere in ClarificationStep.
/// </summary>
public class TableColumnConfirmationStep : IPipelineStep
{
    public Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (context.Intent is null)
            return Task.CompletedTask;
 
        // SchemaResolutionStep couldn't match any real table/column to what the user
        // asked for (e.g. "show incidents by priority" when there's no incidents
        // table). Nothing downstream can do anything useful with this, so stop here
        // with a clear, terminal message instead of falling through to SQL generation
        // against an unrelated table. Mirrors the IsBlocked pattern PromptGuardStep
        // uses for rejections — this is also not something to "answer", so it must
        // not become a PendingClarification that gets chained via ParentPromptId.
        if (context.SchemaMap is null)
        {
            context.Clarification = new ClarificationRequest
            {
                Question = "No data regarding that was found in the database. Please try rephrasing your request.",
                Options = null
            };
            context.IsBlocked = true;
            return Task.CompletedTask;
        }
 
        var map = context.SchemaMap;
 
        // The table matched, but the requested dimension didn't resolve against real
        // schema metadata (SchemaResolutionStep already tried: exact column match,
        // foreign-key match, related table's display column). Two different outcomes:
        if (!map.DimensionResolved)
        {
            if (map.DimensionCandidates is { Count: > 1 })
            {
                // Genuinely ambiguous — e.g. "user" could mean created_by or
                // updated_by. Ask which, don't guess. This is a normal (non-blocked)
                // clarification: whichever option the user picks becomes literal text
                // in the combined prompt next turn, which SchemaResolutionStep's
                // exact-match tier will then find directly.
                context.Clarification = new ClarificationRequest
                {
                    Question = $"Which \"{context.Intent.Dimension}\" column did you mean?",
                    Options  = map.DimensionCandidates
                };
                return Task.CompletedTask;
            }
 
            // Nothing plausible matched at all (no exact column, no FK, no related
            // display column) — don't ask a clarifying question the schema can't
            // actually answer. End here, same terminal pattern as "no matching table".
            context.Clarification = new ClarificationRequest
            {
                Question = $"No data regarding \"{context.Intent.Dimension}\" was found in the {map.Table} table. Please try rephrasing your request.",
                Options = null
            };
            context.IsBlocked = true;
            return Task.CompletedTask;
        }
 
        // Nothing to confirm yet — SchemaResolutionStep only resolves once viz type /
        // metric / etc. are already settled, so this naturally runs after those.
        if (context.Intent.TableColumnConfirmed)
            return Task.CompletedTask;
 
        // User said "No" last turn — offer the other candidate tables the vector
        // search already retrieved, instead of asking the same yes/no question again.
        if (context.Intent.TableColumnRejected)
        {
            var alternatives = map.CandidateTables
                .Where(t => !t.Equals(map.Table, StringComparison.OrdinalIgnoreCase))
                .ToList();
 
            context.Clarification = new ClarificationRequest
            {
                Question = "No problem — which table did you mean?",
                Options  = alternatives.Count > 0 ? alternatives : map.CandidateTables
            };
            return Task.CompletedTask;
        }
 
        var fieldPhrase = map.DimensionColumn is not null
            ? $"the {map.DimensionColumn} column in the {map.Table} table"
            : $"the {map.Table} table";
 
        context.Clarification = new ClarificationRequest
        {
            Question = $"I think you want data from {fieldPhrase}. Is that right?",
            Options  = ["Yes", "No"]
        };
        return Task.CompletedTask;
    }
}