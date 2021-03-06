﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbrella.WebUtilities.Security;

namespace Umbrella.Legacy.WebUtilities.Extensions
{
    public static class HttpContextBaseExtensions
    {
        public static string GetCurrentRequestNonce(this HttpContextBase httpContext) => httpContext.Items[SecurityConstants.DefaultNonceKey] as string;
	}
}