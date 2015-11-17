﻿using System;
using Xunit;
using CachingSample.Abstractions;
using Microsoft.Framework.Caching;
using Microsoft.Framework.Caching.Memory;
using System.Threading;
using Microsoft.Framework.Primitives;

namespace CachingSample.Tests
{
    public class MemoryCacheTests
    {
        private readonly MemoryCache _memoryCache;
        private readonly string _cacheItem = "value";
        private readonly string _cacheKey = "key";
        private string _result;

        public MemoryCacheTests()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [Fact]
        public void RemovalFiresCallback()
        {
            _memoryCache.Set(_cacheKey, _cacheItem,
                new MemoryCacheEntryOptions()
                .RegisterPostEvictionCallback(
                    (key, value, reason, substate) =>
                    {
                        _result = $"'{key}':'{value}' was evicted because: {reason}";
                    }
                ));

            _memoryCache.Remove(_cacheKey);

            // wait for it
            Thread.Sleep(100);

            Assert.Equal("'key':'value' was evicted because: Removed", _result);
        }

        [Fact]
        public void CancellationTokenFiresCallback()
        {
            var cts = new CancellationTokenSource();
            _memoryCache.Set(_cacheKey, _cacheItem,
                new MemoryCacheEntryOptions()
                .AddExpirationToken(new CancellationChangeToken(cts.Token))
                .RegisterPostEvictionCallback(
                    (key, value, reason, substate) =>
                    {
                        _result = $"'{key}':'{value}' was evicted because: {reason}";
                    }
                ));

            // trigger the token
            cts.Cancel();

            // wait for it
            Thread.Sleep(100);

            Assert.Equal("'key':'value' was evicted because: TokenExpired", _result);
        }

        [Fact]
        public void CacheEntryDependencies()
        {
            var cts = new CancellationTokenSource();
            using (var cacheLink = _memoryCache.CreateLinkingScope())
            {
                _memoryCache.Set("master key", "some value",
                    new MemoryCacheEntryOptions()
                    .AddExpirationToken(new CancellationChangeToken(cts.Token)));

                _memoryCache.Set(_cacheKey, _cacheItem,
                    new MemoryCacheEntryOptions()
                    .AddEntryLink(cacheLink)
                    .RegisterPostEvictionCallback(
                        (key, value, reason, substate) =>
                        {
                            _result = $"'{key}':'{value}' was evicted because: {reason}";
                        }
                    ));
            }

            // trigger the token to expire the master item
            cts.Cancel();

            // wait for it
            Thread.Sleep(100);

            Assert.Equal("'key':'value' was evicted because: TokenExpired", _result);
        }
    }
}