﻿using System.Collections.Generic;
using Xunit;

namespace Umbrella.DataAnnotations.Test
{
	public class RequiredNonEmptyCollectionAttributeTest
	{
		private class Model : ValidationModelBase<RequiredNonEmptyCollectionAttribute>
		{
			[RequiredNonEmptyCollection]
			public List<string> Value1 { get; set; }
		}

		[Fact]
		public void IsValidTest()
		{
			var model = new Model() { Value1 = new List<string> { "hello" } };
			Assert.True(model.IsValid("Value1"));
		}

		[Fact]
		public void IsNotValidTest()
		{
			var model = new Model() { Value1 = new List<string>() };
			Assert.False(model.IsValid("Value1"));
		}

		[Fact]
		public void IsNotValidNullTest()
		{
			var model = new Model() { Value1 = null };
			Assert.False(model.IsValid("Value1"));
		}
	}
}