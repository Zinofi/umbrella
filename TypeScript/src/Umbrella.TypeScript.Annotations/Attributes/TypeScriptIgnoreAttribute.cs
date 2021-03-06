﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Umbrella.TypeScript
{
    /// <summary>
    /// Used to mark model properties that should be ignored by the TypeScript generator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class TypeScriptIgnoreAttribute : Attribute
    {
    }
}
