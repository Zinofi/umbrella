﻿using log4net;
using log4net.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using System.IO;

namespace Umbrella.Extensions.Logging.Log4Net
{
    /// <summary>
    /// A set of extension methods to configure log4net to be used by your application
    /// </summary>
    public static class Log4NetExtensions
    {
        /// <summary>
        /// This method will configure log4net using the file path provided.
        /// </summary>
        /// <param name="contentRootPath">The root path of the application.</param>
        /// <param name="configFileRelativePath">The file path to the config file relative to <paramref name="contentRootPath"/>
        /// </param>
        public static ILoggerFactory AddLog4Net(this ILoggerFactory loggerFactory, string contentRootPath, string configFileRelativePath)
        {
            GlobalContext.Properties["appRoot"] = contentRootPath;
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(contentRootPath, configFileRelativePath)));

            loggerFactory.AddProvider(new Log4NetProvider());

            return loggerFactory;
        }
    }
}