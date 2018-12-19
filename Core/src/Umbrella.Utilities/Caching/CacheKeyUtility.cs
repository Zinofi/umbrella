﻿using Microsoft.Extensions.Caching.Memory;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Caching
{
    public class CacheKeyUtility : ICacheKeyUtility
    {
        public string Create<T>(in ReadOnlySpan<string> keyParts, [CallerMemberName]string callerMemberName = "")
        {
            if (keyParts.Length == 0)
                throw new ArgumentException("The length cannot be null.", nameof(keyParts));

            Guard.ArgumentNotNullOrWhiteSpace(callerMemberName, nameof(callerMemberName));

            int partsCount = keyParts.Length;

            // It seems the typeof call is expensive on CLR vs .NET Core
            string typeName = typeof(T).FullName;
            int partsLengthTotal = -1;

            for (int i = 0; i < partsCount; i++)
            {
                string part = keyParts[i];

                if (part != null)
                    partsLengthTotal += part.Length + 1;
            }

            int length = typeName.Length + callerMemberName.Length + partsLengthTotal + 2;

            Span<char> span = stackalloc char[length];

            int currentIndex = span.Append(0, typeName);
            span[currentIndex++] = ':';

            currentIndex = span.Append(currentIndex, callerMemberName);
            span[currentIndex++] = ':';

            for (int i = 0; i < partsCount; i++)
            {
                string part = keyParts[i];

                if (part != null)
                {
                    currentIndex = span.Append(currentIndex, part);

                    if (i < partsCount - 1)
                        span[currentIndex++] = ':';
                }
            }

            span.ToUpperInvariant();

            // This is the only part that allocates
            return span.ToString();
        }

#if !AzureDevOps
        [Obsolete]
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        internal string CreateOld<T>(IEnumerable<string> keyParts, [CallerMemberName]string callerMemberName = "")
        {
            Guard.ArgumentNotNullOrEmpty(keyParts, nameof(keyParts));
            Guard.ArgumentNotNullOrEmpty(callerMemberName, nameof(callerMemberName));

            return $"{typeof(T).FullName}:{callerMemberName}:{string.Join(":", keyParts)}".ToUpperInvariant();
        }
#endif
    }
}