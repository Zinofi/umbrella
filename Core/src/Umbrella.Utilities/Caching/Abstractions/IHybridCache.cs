﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Umbrella.Utilities.Caching.Abstractions
{
	// TODO: Remove the non-async methods. Why would we ever not want async now especially as we always want a distributed cache to be accessed asynchronously!!!
	// TODO: 

    /// <summary>
    /// A multi cache that allows cache items to be stored in an <see cref="IMemoryCache"/> or a <see cref="IDistributedCache"/> implementation.
    /// The cache includes the option to allow internal errors that occur when adding or retrieving items to be masked so that transient errors with the cache, e.g. a Redis error where
	/// the service is being restarted, does not cause a hard application failure.
    /// </summary>
    public interface IHybridCache
    {
        T GetOrCreate<T>(string cacheKey, Func<T> actionFunction, Func<TimeSpan> expirationTimeSpanBuilder = null, bool useMemoryCache = true, bool slidingExpiration = false, bool throwOnCacheFailure = false, CacheItemPriority priority = CacheItemPriority.Normal, Func<IEnumerable<IChangeToken>> expirationTokensBuilder = null, bool? cacheEnabledOverride = null);
        Task<T> GetOrCreateAsync<T>(string cacheKey, Func<Task<T>> actionFunction, CancellationToken cancellationToken = default, Func<TimeSpan> expirationTimeSpanBuilder = null, bool useMemoryCache = true, bool slidingExpiration = false, bool throwOnCacheFailure = false, CacheItemPriority priority = CacheItemPriority.Normal, Func<IEnumerable<IChangeToken>> expirationTokensBuilder = null, bool? cacheEnabledOverride = null);
        (bool itemFound, T cacheItem) TryGetValue<T>(string cacheKey, bool useMemoryCache = true);
        Task<(bool itemFound, T cacheItem)> TryGetValueAsync<T>(string cacheKey, CancellationToken cancellationToken = default, bool useMemoryCache = true);
        T Set<T>(string cacheKey, T value, TimeSpan expirationTimeSpan, bool useMemoryCache = true, bool slidingExpiration = false, bool throwOnCacheFailure = false, CacheItemPriority priority = CacheItemPriority.Normal, Func<IEnumerable<IChangeToken>> expirationTokensBuilder = null);
        Task<T> SetAsync<T>(string cacheKey, T value, TimeSpan expirationTimeSpan, CancellationToken cancellationToken = default, bool useMemoryCache = true, bool slidingExpiration = false, bool throwOnCacheFailure = false, CacheItemPriority priority = CacheItemPriority.Normal, Func<IEnumerable<IChangeToken>> expirationTokensBuilder = null);
        IReadOnlyCollection<HybridCacheMetaEntry> GetAllMemoryCacheMetaEntries();
		Task RemoveAsync<T>(string cacheKey, CancellationToken cancellationToken = default);
		void ClearMemoryCache();
    }
}