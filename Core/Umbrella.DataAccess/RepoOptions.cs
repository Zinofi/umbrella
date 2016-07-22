﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbrella.DataAccess.Interfaces;

namespace Umbrella.DataAccess
{
    /// <summary>
    /// A default RepoOptions type that contains 2 virtual properties for SanitizeEntity and ValidateEntity with default values of true.
    /// </summary>
    public class RepoOptions
    {
        public virtual bool SanitizeEntity { get; set; } = true;
        public virtual bool ValidateEntity { get; set; } = true;
    }
}