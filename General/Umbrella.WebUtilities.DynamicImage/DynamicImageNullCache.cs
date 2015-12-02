﻿using Umbrella.WebUtilities.DynamicImage.Enumerations;
using Umbrella.WebUtilities.DynamicImage.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Caching;

namespace Umbrella.WebUtilities.DynamicImage
{
    public class DynamicImageNullCache : IDynamicImageCache
    {
        public void Add(DynamicImage dynamicImage, Func<CacheItemPolicy> policyFunc)
        {

        }

        public DynamicImage Get(string key, string originalFilePhysicalPath, string fileExtension)
        {
            return null;
        }

        public void Remove(string key, string fileExtension)
        {

        }

        public string GenerateCacheKey(DynamicImageOptions options)
        {
            return string.Empty;
        }
    }
}