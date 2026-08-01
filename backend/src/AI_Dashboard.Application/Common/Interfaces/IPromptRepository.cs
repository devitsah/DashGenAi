using AI_Dashboard.Domain.Entities;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface IPromptRepository
{
    Task<Prompt> AddAsync(Prompt prompt, CancellationToken ct = default);
    Task<Prompt?> GetByIdAsync(short tenantId, int id, CancellationToken ct = default);
    Task<List<Prompt>> GetChainAsync(short tenantId, int promptId, CancellationToken ct = default);
    Task UpdateAsync(Prompt prompt, CancellationToken ct = default);
}