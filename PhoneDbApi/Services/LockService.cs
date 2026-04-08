using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace PhoneDb.Api.Services
{
    public interface ILockService
    {
        Task<IDisposable> AcquireLockAsync(string key);
    }

    public class LockService : ILockService
    {
        private readonly IMemoryCache _cache;

        public LockService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<IDisposable> AcquireLockAsync(string key)
        {
            var semaphore = _cache.GetOrCreate(key, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(2);
                return new SemaphoreSlim(1, 1);
            });

            await semaphore.WaitAsync();
            return new LockReleaser(semaphore);
        }

        private class LockReleaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            public LockReleaser(SemaphoreSlim semaphore) => _semaphore = semaphore;
            public void Dispose() => _semaphore.Release();
        }
    }
}
