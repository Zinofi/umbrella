﻿using System;
using System.ComponentModel.DataAnnotations;

namespace Umbrella.DataAnnotations
{
	/// <summary>
	/// Extends the <see cref="RangeAttribute" /> using either <see cref="double.MinValue" /> or <see cref="int.MinValue" /> as the default for convenience.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class MaxRangeAttribute : RangeAttribute
	{
		/// <summary>
		/// Create a new instance.
		/// </summary>
		/// <param name="maximum">The maximum.</param>
		public MaxRangeAttribute(double maximum)
			: base(double.MinValue, maximum)
		{
		}

		/// <summary>
		/// Create a new instance.
		/// </summary>
		/// <param name="maximum">The maximum.</param>
		public MaxRangeAttribute(int maximum)
			: base(int.MinValue, maximum)
		{
		}
	}
}