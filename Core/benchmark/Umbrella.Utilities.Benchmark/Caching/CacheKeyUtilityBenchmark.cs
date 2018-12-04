﻿using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbrella.Utilities.Caching;

namespace Umbrella.Utilities.Benchmark.Caching
{
    [CoreJob]
    [BenchmarkCategory(nameof(CacheKeyUtility))]
    public class CacheKeyUtilityBenchmark
    {
        private readonly CacheKeyUtility _cacheKeyUtility;

        public CacheKeyUtilityBenchmark()
        {
            _cacheKeyUtility = new CacheKeyUtility();
        }

        [Benchmark]
        public string CreateCacheKey()
        {
            return _cacheKeyUtility.Create<CacheKeyUtilityBenchmark>(new[] { "part1", "part2", "part3", "part4", "part5" });
        }

        [Benchmark]
        public string CreateCacheKeyOld()
        {
            return _cacheKeyUtility.CreateOld<CacheKeyUtilityBenchmark>(new[] { "part1", "part2", "part3", "part4", "part5" });
        }
    }
}
