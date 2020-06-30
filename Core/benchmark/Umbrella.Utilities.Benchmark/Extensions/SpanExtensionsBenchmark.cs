﻿using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Benchmark.Extensions
{
	[MemoryDiagnoser]
	[SimpleJob(RuntimeMoniker.Net461), SimpleJob(RuntimeMoniker.NetCoreApp31)]
	public class SpanExtensionsBenchmark
	{
		[Benchmark]
		public int Char_AppendStringBenchmark()
		{
			Span<char> span = stackalloc char[10];

			int currentIndex = span.Append(0, "12345");
			currentIndex = span.Append(currentIndex, "67890");

			return span.Length;
		}

		[Benchmark]
		public int Char_AppendReadOnlySpanBenchmark()
		{
			Span<char> span = stackalloc char[10];

			int currentIndex = span.Append(0, "12345".AsSpan());
			currentIndex = span.Append(currentIndex, "67890".AsSpan());

			return span.Length;
		}

		[Benchmark]
		public int Char_ToLowerBenchmark()
		{
			Span<char> span = stackalloc char[10];
			span.Fill('A');

			span.ToLower();

			return span.Length;
		}

		[Benchmark]
		public int Char_ToLowerInvariantBenchmark()
		{
			Span<char> span = stackalloc char[10];
			span.Fill('A');

			span.ToLowerInvariant();

			return span.Length;
		}

		[Benchmark]
		public int Char_ToUpperBenchmark()
		{
			Span<char> span = stackalloc char[10];
			span.Fill('A');

			span.ToUpper();

			return span.Length;
		}

		[Benchmark]
		public int Char_ToUpperInvariantBenchmark()
		{
			Span<char> span = stackalloc char[10];
			span.Fill('A');

			span.ToUpperInvariant();

			return span.Length;
		}
	}
}