using System.Text.RegularExpressions;
using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Infrastructure.AI;

public class PromptGuard : IPromptGuard
{
    private readonly List<(Regex Pattern, string Reason)> _rules;
    private const int MaxLength = 500;

    public PromptGuard(ISchemaIntrospectionOptions options)
    {
        _rules = new List<(Regex, string)>
        {
            // Detects attempts to make the LLM ignore its original/system instructions
(new Regex(@"ignore (all |previous |above |prior )?instructions", RegexOptions.IgnoreCase), "instruction override attempt"),

// Same as above but catches "disregard" phrasing, incl. targeting rules/prompt not just "instructions"
(new Regex(@"disregard (all |previous |above |prior )?(instructions|rules|prompt)", RegexOptions.IgnoreCase), "instruction override attempt"),

// Catches common persona-hijack / jailbreak phrasing (act as, pretend, DAN, developer mode, etc.)
(new Regex(@"system prompt|you are now|act as|pretend (you|to be)|roleplay as|developer mode|jailbreak|\bDAN\b", RegexOptions.IgnoreCase), "role override / jailbreak attempt"),

// Flags triple-backtick code fences, which can be used to smuggle fake instructions/structure into the prompt
(new Regex(@"```", RegexOptions.None), "markdown code fence in prompt"),

// Flags SQL write/DDL keywords — the LLM should only ever generate read-only (SELECT) queries
(new Regex(@"\bDROP\b|\bDELETE\b|\bUPDATE\b|\bINSERT\b|\bALTER\b|\bTRUNCATE\b|\bMERGE\b", RegexOptions.IgnoreCase), "write/DDL keyword in prompt"),

// Flags SQL execution keywords and xp_cmdshell (SQL Server RCE vector via stored procs)
(new Regex(@"\bEXEC(UTE)?\b|xp_cmdshell", RegexOptions.IgnoreCase), "command execution keyword in prompt"),

// Detects UNION SELECT — classic technique to append a second query and exfiltrate extra data
(new Regex(@"UNION(\s+ALL)?\s+SELECT", RegexOptions.IgnoreCase), "UNION-based injection attempt"),

// Detects tautology patterns like OR 1=1 / OR '1'='1' used to bypass WHERE clause filters
(new Regex(@"\bOR\b\s*'?""?\d+'?""?\s*=\s*'?""?\d+'?""?", RegexOptions.IgnoreCase), "tautology injection attempt (OR 1=1)"),

// Detects stacked queries — a semicolon followed by another SQL statement (e.g. "; DROP TABLE ...")
(new Regex(@";\s*(SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|EXEC|EXECUTE|MERGE|WITH)\b", RegexOptions.IgnoreCase), "SQL statement chaining attempt"),

// Detects SQL comment syntax (-- or /*) used to truncate/neutralize the rest of a legitimate query
(new Regex(@"--|/\*", RegexOptions.None), "SQL comment injection"),
        };

        if (options.SensitiveColumns.Length > 0)
        {
            var pattern = string.Join("|", options.SensitiveColumns.Select(Regex.Escape));
            _rules.Add((new Regex(pattern, RegexOptions.IgnoreCase), "sensitive column requested directly"));
        }

        if (!string.IsNullOrEmpty(options.TenantColumn))
        {
            var tenantPattern = $@"{Regex.Escape(options.TenantColumn)}\s*(!=|<>|not)";
            _rules.Add((new Regex(tenantPattern, RegexOptions.IgnoreCase), "tenant isolation bypass attempt"));
        }
    }

    public string? Check(string promptText)
    {
        if (promptText.Length == 0) return "prompt_empty";
        if (string.IsNullOrWhiteSpace(promptText)) return "prompt_whitespace";
        if (promptText.Length > MaxLength) return "prompt_too_long";

        foreach (var (pattern, reason) in _rules)
            if (pattern.IsMatch(promptText))
                return reason;

        return null;
    }
}