namespace AI_Dashboard.Application.Common.Interfaces;

public interface IPromptGuard
{
    string? Check(string promptText);
}
