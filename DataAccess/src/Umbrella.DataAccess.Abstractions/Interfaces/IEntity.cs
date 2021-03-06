﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Umbrella.DataAccess.Abstractions.Interfaces
{
    public interface IEntity : IEntity<int>
    {
    }

    public interface IEntity<T>
    {
        T Id { get; set; }
    }
}