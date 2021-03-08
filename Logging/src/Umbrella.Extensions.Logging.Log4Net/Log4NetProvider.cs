﻿using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using log4net.Repository;
using Microsoft.Extensions.Logging;

namespace Umbrella.Extensions.Logging.Log4Net
{
	/// <summary>
	/// A log4net specific implementation of the Microsoft <see cref="ILoggerProvider"/>.
	/// </summary>
	/// <seealso cref="ILoggerProvider" />
	[ProviderAlias("log4net")]
	public class Log4NetProvider : ILoggerProvider
	{
		private readonly ConcurrentDictionary<string, ILogger> _loggerDictionary = new ConcurrentDictionary<string, ILogger>();
		private readonly ILoggerRepository _loggerRepository;

		/// <summary>
		/// Initializes a new instance of the <see cref="Log4NetProvider"/> class.
		/// </summary>
		/// <param name="contentRootPath">The content root path.</param>
		/// <param name="configFileRelativePath">The configuration file relative path.</param>
		public Log4NetProvider(string contentRootPath, string configFileRelativePath)
		{
			_loggerRepository = LogManager.CreateRepository(Assembly.GetEntryAssembly(), typeof(log4net.Repository.Hierarchy.Hierarchy));

			GlobalContext.Properties["appRoot"] = contentRootPath;
			XmlConfigurator.ConfigureAndWatch(_loggerRepository, new FileInfo(Path.Combine(contentRootPath, configFileRelativePath)));
		}

		/// <summary>
		/// Creates the logger.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns>The logger.</returns>
		public ILogger CreateLogger(string name) => _loggerDictionary.GetOrAdd(name, (x) => new Log4NetLogger(_loggerRepository.Name, x));

		#region IDisposable Support
		private bool _disposedValue = false; // To detect redundant calls

		/// <summary>
		/// Releases unmanaged and - optionally - managed resources.
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_loggerDictionary.Clear();
					_loggerRepository.Shutdown();
				}

				_disposedValue = true;
			}
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose() => Dispose(true);
		#endregion


	}
}