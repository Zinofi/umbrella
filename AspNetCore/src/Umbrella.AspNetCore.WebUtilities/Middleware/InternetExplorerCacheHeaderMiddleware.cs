﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbrella.AspNetCore.WebUtilities.Middleware.Options;
using Umbrella.Utilities;
using Umbrella.Utilities.Compilation;
using Umbrella.Utilities.Extensions;

namespace Umbrella.AspNetCore.WebUtilities.Middleware
{
    public class InternetExplorerCacheHeaderMiddleware
    {
        #region Private Members
        private readonly RequestDelegate m_Next;
        private readonly ILogger m_Logger;
        private readonly InternetExplorerCacheHeaderOptions m_Options = new InternetExplorerCacheHeaderOptions();
        #endregion

        #region Constructors
        public InternetExplorerCacheHeaderMiddleware(RequestDelegate next,
            ILogger<InternetExplorerCacheHeaderMiddleware> logger,
            Action<InternetExplorerCacheHeaderOptions> optionsBuilder)
        {
            m_Next = next;
            m_Logger = logger;

            optionsBuilder?.Invoke(m_Options);

            Guard.ArgumentNotNull(m_Options.ContentTypes, $"{nameof(InternetExplorerCacheHeaderOptions)}.{nameof(m_Options.ContentTypes)}");
            Guard.ArgumentNotNull(m_Options.Methods, $"{nameof(InternetExplorerCacheHeaderOptions)}.{nameof(m_Options.Methods)}");
            Guard.ArgumentNotNull(m_Options.UserAgentKeywords, $"{nameof(InternetExplorerCacheHeaderOptions)}.{nameof(m_Options.UserAgentKeywords)}");
        }
        #endregion

        #region Public Methods
        public async Task Invoke(HttpContext context)
        {
            try
            {
                string method = context.Request.Method;
                string userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();

                bool addHeaders = (m_Options.UserAgentKeywords.Count == 0 || m_Options.UserAgentKeywords.Any(x => userAgent.Contains(x)))
                    && (m_Options.Methods.Count == 0 || m_Options.Methods.Any(x => method.Contains(x)));

                if (addHeaders)
                {
                    context.Response.OnStarting(state =>
                    {
                        HttpResponse response = (HttpResponse)state;

                        string contentType = response.ContentType;

                        if (m_Options.ContentTypes.Count == 0 || m_Options.ContentTypes.Any(x => contentType.Contains(x)))
                        {
                            //Set standard HTTP/1.0 no-cache header (no-store, no-cache, must-revalidate)
                            //Set IE extended HTTP/1.1 no-cache headers (post-check, pre-check)
                            response.Headers.Add("Cache-Control", "no-store, no-cache, must-revalidate, post-check=0, pre-check=0");

                            //Set standard HTTP/1.0 no-cache header.
                            response.Headers.Add("Pragma", "no-cache");
                        }

                        return Task.CompletedTask;

                    }, context.Response);
                }

                await m_Next.Invoke(context);
            }
            catch (Exception exc) when (m_Logger.WriteError(exc))
            {
                throw;
            }
        }
        #endregion
    }
}