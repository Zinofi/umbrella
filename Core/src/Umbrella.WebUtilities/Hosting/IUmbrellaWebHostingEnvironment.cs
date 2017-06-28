﻿using System;
using System.Collections.Generic;
using System.Text;
using Umbrella.Utilities.Hosting;

namespace Umbrella.WebUtilities.Hosting
{
    public interface IUmbrellaWebHostingEnvironment : IUmbrellaHostingEnvironment
    {
        string MapWebPath(string virtualPath, bool toAbsoluteUrl = false, string scheme = "http", bool appendVersion = false, string versionParameterName = "v", bool mapFromContentRoot = true);
    }
}