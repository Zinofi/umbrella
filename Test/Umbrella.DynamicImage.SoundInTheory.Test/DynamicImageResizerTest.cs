﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Logging;
using Umbrella.DynamicImage.Caching;
using Umbrella.DynamicImage.Abstractions;
using Xunit;
using Umbrella.FileSystem.Abstractions;
using System.Threading;

namespace Umbrella.DynamicImage.SoundInTheory.Test
{
    public class DynamicImageResizerTest
    {
        //This is a 3KB test png of the ASP.NET MVC Logo
        private const string TestPNG = "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";

        [Fact]
        public async Task GenerateImageAsync_FromFunc()
        {
            var resizer = CreateDynamicImageResizer();

            var options = new DynamicImageOptions("/dummypath.png", 100, 100, DynamicResizeMode.UniformFill, DynamicImageFormat.Jpeg);
            byte[] bytes = Convert.FromBase64String(TestPNG);

            var fileMock = new Mock<IUmbrellaFileInfo>();
            fileMock.Setup(x => x.ReadAsByteArrayAsync(default(CancellationToken), true)).Returns(Task.FromResult(bytes));
            fileMock.Setup(x => x.LastModified).Returns(DateTimeOffset.UtcNow);
            fileMock.Setup(x => x.ExistsAsync(default(CancellationToken))).Returns(Task.FromResult(true));
            fileMock.Setup(x => x.Length).Returns(bytes.LongLength);
            
            var fileProviderMock = new Mock<IUmbrellaFileProvider>();
            fileProviderMock.Setup(x => x.GetAsync("/dummypath.png", default(CancellationToken))).Returns(Task.FromResult(fileMock.Object));

            DynamicImageItem result = await resizer.GenerateImageAsync(fileProviderMock.Object, options);

            byte[] resizedImageBytes = await result.GetContentAsync();

            Assert.NotEmpty(resizedImageBytes);
        }

        private DynamicImageResizer CreateDynamicImageResizer()
        {
            var logger = new Mock<ILogger<DynamicImageResizer>>();

            return new DynamicImageResizer(logger.Object, new DynamicImageDefaultCache());
        }
    }
}