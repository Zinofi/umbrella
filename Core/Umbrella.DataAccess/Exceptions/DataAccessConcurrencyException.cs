﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Umbrella.DataAccess.Exceptions
{
    public class DataAccessConcurrencyException : Exception
    {
        public DataAccessConcurrencyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
