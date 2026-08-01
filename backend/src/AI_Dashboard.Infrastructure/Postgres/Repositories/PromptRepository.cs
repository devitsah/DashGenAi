using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AI_Dashboard.Infrastructure.Postgres.Repositories;

public class PromptRepository : IPromptRepository
{
    private readonly AppDbContext _db;
    public PromptRepository(AppDbContext db) => _db = db;

    public async Task<Prompt> AddAsync(Prompt prompt, CancellationToken ct = default)
    {
        _db.Prompts.Add(prompt);
        await _db.SaveChangesAsync(ct);
        return prompt;
    }

    public Task<Prompt?> GetByIdAsync(short tenantId, int id, CancellationToken ct = default) =>
        _db.Prompts.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task<List<Prompt>> GetChainAsync(short tenantId, int promptId, CancellationToken ct = default)
    {
        // Walk parent links iteratively to avoid unbounded recursion.
        // Include promptId itself so the full chain root->...->promptId is returned.
        var chain = new List<Prompt>();
        int? currentId = promptId;
        while (currentId.HasValue)
        {
            var p = await _db.Prompts.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == currentId.Value, ct);
            if (p is null) break;
            chain.Insert(0, p);
            currentId = p.ParentPromptId;
        }
        return chain;
    }

    public async Task UpdateAsync(Prompt prompt, CancellationToken ct = default)
    {
        prompt.UpdatedAt = DateTime.UtcNow;
        _db.Prompts.Update(prompt);
        await _db.SaveChangesAsync(ct);
    }
}