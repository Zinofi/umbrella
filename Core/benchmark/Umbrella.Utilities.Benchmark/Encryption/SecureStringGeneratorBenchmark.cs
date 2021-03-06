﻿using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbrella.Utilities.Encryption;

namespace Umbrella.Utilities.Benchmark.Encryption
{
    [CoreJob, ClrJob]
    [MemoryDiagnoser]
    public class SecureStringGeneratorBenchmark
    {
        private readonly SecureStringGenerator _secureStringGenerator;

        public SecureStringGeneratorBenchmark()
        {
            var logger = new Mock<ILogger<SecureStringGenerator>>();
            _secureStringGenerator = new SecureStringGenerator(logger.Object);
        }

        [Benchmark]
        public string Generate()
        {
            return _secureStringGenerator.Generate(20, 5, 5);
        }

#if !AzureDevOps
        [Benchmark(Baseline = true)]
        public string GenerateOld()
        {
#pragma warning disable CS0612 // Type or member is obsolete
            return _secureStringGenerator.GenerateOld(20, 5, 5);
#pragma warning restore CS0612 // Type or member is obsolete
        }
#endif
    }
}