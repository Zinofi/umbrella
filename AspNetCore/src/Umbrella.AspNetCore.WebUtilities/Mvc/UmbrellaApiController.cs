﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Umbrella.AspNetCore.WebUtilities.Mvc
{
    /// <summary>
    /// Serves as the base class for API controllers and encapsulates API specific functionality.
    /// </summary>
    [ServiceFilter(typeof(ValidateModelStateAttribute))]
    public abstract class UmbrellaApiController : UmbrellaController
    {
        #region Constructors
        public UmbrellaApiController(ILogger logger)
            : base(logger)
        {
        }
        #endregion

        #region Public Methods
        [NonAction]
        public virtual IActionResult Forbidden(string message = null) => HttpObjectOrStatusResult(message, 403);

        [NonAction]
        public virtual IActionResult Conflict(string message = null) => HttpObjectOrStatusResult(message, 409);

        [NonAction]
        public virtual IActionResult InternalServerError(string message = null) => HttpObjectOrStatusResult(message, 500, true);

        [NonAction]
        public virtual IActionResult HttpObjectOrStatusResult(string message, int statusCode, bool wrapMessage = false)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                object value = message;

                if (wrapMessage)
                    value = new { message };

                return StatusCode(statusCode, value);
            }

            return StatusCode(statusCode);
        }
        #endregion
    }
}