﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbrella.AspNetCore.WebUtilities.Hosting;
using Xunit;

namespace Umbrella.AspNetCore.WebUtilities.Test.Hosting
{
    public class UmbrellaWebHostingEnvironmentTest
    {
        [Fact]
        public void MapPath_InvalidPath()
        {
            var env = Mocks.CreateUmbrellaWebHostingEnvironment();

            Assert.Throws<ArgumentNullException>(() => env.MapPath(null));
            Assert.Throws<ArgumentException>(() => env.MapPath(""));
            Assert.Throws<ArgumentException>(() => env.MapPath("      "));
        }

        [Fact]
        public void MapPath_VirtualPath_ContentRoot()
        {
            string path = "~/images/test.jpg";

            var env = Mocks.CreateUmbrellaWebHostingEnvironment();

            string mappedPath = env.MapPath(path);

            Assert.Equal(@"C:\MockedWebApp\src\images\test.jpg", mappedPath);
        }

        [Fact]
        public void MapPath_VirtualPath_WebRoot()
        {
            string path = "~/images/test.jpg";

            var env = Mocks.CreateUmbrellaWebHostingEnvironment();

            string mappedPath = env.MapPath(path, false);

            Assert.Equal(@"C:\MockedWebApp\src\wwwroot\images\test.jpg", mappedPath);
        }

        [Fact]
        public void MapPath_RelativePath_ContentRoot()
        {
            string path = "/images/test.jpg";

            var env = Mocks.CreateUmbrellaWebHostingEnvironment();

            string mappedPath = env.MapPath(path);

            Assert.Equal(@"C:\MockedWebApp\src\images\test.jpg", mappedPath);
        }

        [Fact]
        public void MapPath_RelativePath_WebRoot()
        {
            string path = "/images/test.jpg";

            var env = Mocks.CreateUmbrellaWebHostingEnvironment();

            string mappedPath = env.MapPath(path, false);

            Assert.Equal(@"C:\MockedWebApp\src\wwwroot\images\test.jpg", mappedPath);
        }

        [Fact]
        public void MapWebPath_VirtualPath()
        {
            string path = "~/images/test.jpg";

            var env = Mocks.CreateUmbrellaWebHostingEnvironment();

            string mappedPath = env.MapWebPath(path, true);

            Assert.Equal("http://www.test.com/images/test.jpg", mappedPath);
        }

        [Fact]
        public void MapWebPath_RelativePath()
        {
            string path = "/images/test.jpg";

            var env = Mocks.CreateUmbrellaWebHostingEnvironment();

            string mappedPath = env.MapWebPath(path, true);

            Assert.Equal("http://www.test.com/images/test.jpg", mappedPath);
        }
    }
}