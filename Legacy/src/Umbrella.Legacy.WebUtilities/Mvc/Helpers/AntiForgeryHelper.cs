﻿using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace Umbrella.Legacy.WebUtilities.Mvc.Helpers
{
	public static class AntiForgeryHelper
	{
		public static HtmlString RequestVerificationToken(this HtmlHelper helper)
		{
			IOwinContext context = HttpContext.Current.GetOwinContext();

            return new HtmlString(string.Format("data-request-verification-token=\"{0}\"", GetAntiForgeryTokenValue(context)));
        }

		public static string GetAntiForgeryTokenValue(this IOwinContext context)
		{
			string value = context.Request.Cookies["_RequestVerificationToken"];

            AntiForgery.GetTokens(value, out string cookieToken, out string formToken);

            return cookieToken + ":" + formToken;
		}
	}
}