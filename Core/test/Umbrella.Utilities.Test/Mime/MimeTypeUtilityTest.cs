﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Utilities.Mime;
using Umbrella.Utilities.Mime.Abstractions;
using Xunit;

namespace Umbrella.Utilities.Test.Mime
{
	public class MimeTypeUtilityTest
    {
        [Fact]
        public void GetMimeType_Empty()
        {
            Assert.Throws<ArgumentException>(() => CreateMimeTypeUtility().GetMimeType(""));
        }

        [Fact]
        public void GetMimeType_Whitespace()
        {
            Assert.Throws<ArgumentException>(() => CreateMimeTypeUtility().GetMimeType("    "));
        }

		[Fact]
		public void GetMimeType_Dot()
		{
			string mimeType = CreateMimeTypeUtility().GetMimeType(".");
			Assert.Equal("application/octet-stream", mimeType);
		}

		[Fact]
		public void GetMimeType_NamePlusDot()
		{
			string mimeType = CreateMimeTypeUtility().GetMimeType("name.");
			Assert.Equal("application/octet-stream", mimeType);
		}

		[Fact]
        public void GetMimeType_Extension()
        {
            string mimeType = CreateMimeTypeUtility().GetMimeType(".png");
            Assert.Equal("image/png", mimeType);
        }

        [Fact]
        public void GetMimeType_Extension_NoLeadingPeriod()
        {
            string mimeType = CreateMimeTypeUtility().GetMimeType("png");
            Assert.Equal("image/png", mimeType);
        }

        [Fact]
        public void GetMimeType_Filename()
        {
            string mimeType = CreateMimeTypeUtility().GetMimeType("test.png");
            Assert.Equal("image/png", mimeType);
        }

        [Fact]
        public void GetMimeType_Extension_Uppercase()
        {
            string mimeType = CreateMimeTypeUtility().GetMimeType("PNG");
            Assert.Equal("image/png", mimeType);
        }

        [Fact]
        public void GetMimeType_Filepath()
        {
            string mimeType = CreateMimeTypeUtility().GetMimeType("/path/test.png");
            Assert.Equal("image/png", mimeType);
        }

        [Fact]
        public void GetMimeType_FilenameWithPeriods()
        {
            string mimeType = CreateMimeTypeUtility().GetMimeType("test.image.with.periods.png");
            Assert.Equal("image/png", mimeType);
        }

        private IMimeTypeUtility CreateMimeTypeUtility()
            => new MimeTypeUtility(new Mock<ILogger<MimeTypeUtility>>().Object);
    }
}