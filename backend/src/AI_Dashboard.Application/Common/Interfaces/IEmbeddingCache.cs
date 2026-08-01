namespace AI_Dashboard.Application.Common.Interfaces;

public interface IEmbeddingCache
{
    Task<float[]> GetOrAddAsync(string key, Func<Task<float[]>> factory);
}
