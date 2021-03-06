﻿using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Legacy.WebUtilities.Middleware
{
	/// <summary>
	/// Owin Middleware to ensure all environment objects that implement IDisposable are
	/// disposed before a request ends.
	/// </summary>
	public class CleanupIDisposableMiddleware : OwinMiddleware
	{
        protected ILogger Log { get; }

		public CleanupIDisposableMiddleware(OwinMiddleware next, ILogger<CleanupIDisposableMiddleware> logger)
			: base(next)
		{
            Log = logger;

			if (Log.IsEnabled(LogLevel.Debug))
				Log.WriteDebug($"{nameof(CleanupIDisposableMiddleware)} registered successfully");
		}

		public override async Task Invoke(IOwinContext context)
		{
			context.Request.CallCancelled.ThrowIfCancellationRequested();
			await Next.Invoke(context);
			context.Request.CallCancelled.ThrowIfCancellationRequested();

			try
			{
				//Ensure any disposable objects are disposed correctly before the end of the request
				context.Environment.AsParallel().Select(x => x.Value).OfType<IDisposable>().ForAll(x => x.Dispose());
			}
			catch(Exception exc) when(Log.WriteError(exc))
			{
                throw;
			}
		}
	}
}