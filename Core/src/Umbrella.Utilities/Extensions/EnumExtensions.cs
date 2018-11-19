﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Umbrella.Utilities.Extensions
{
    /// <summary>
    /// A set of extension methods for <see cref="Enum"/> types.
    /// </summary>
    public static class EnumExtensions
    {
        public static T ToEnum<T>(this string value) where T : struct, Enum => value.ToEnum(default(T));

        public static T ToEnum<T>(this string value, T defaultValue) where T : struct, Enum
        {
            Guard.ArgumentNotNullOrWhiteSpace(value, nameof(value));

            if (Enum.TryParse(value, true, out T result))
                return result;

            return defaultValue;
        }

        public static T ToEnum<T>(this int value) => value.ToEnum<T>(default);

        public static T ToEnum<T>(this int value, T defaultValue)
        {
            Type eType = typeof(T);

            if (Enum.IsDefined(eType, value))
            {
                object obj = Enum.ToObject(eType, value);
                return (T)obj;
            }

            return defaultValue;
        }

        // TODO: Add some internal caching to avoid repeatedly doing the same work.
        public static string ToFlagsString(this Enum options, Func<string, string> valueTransformer = null, string separator = ",")
        {
            Guard.ArgumentNotNull(separator, nameof(separator));

            List<string> lstOption = new List<string>();

            foreach (Enum item in Enum.GetValues(options.GetType()))
            {
                if (options.HasFlag(item))
                    lstOption.Add(valueTransformer != null ? valueTransformer(item.ToString()) : item.ToString());
            }

            return string.Join(separator, lstOption);
        }
    }
}