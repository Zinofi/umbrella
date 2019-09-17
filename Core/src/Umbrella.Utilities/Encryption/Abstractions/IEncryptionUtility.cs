﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Umbrella.Utilities.Encryption.Abstractions
{
	public interface IEncryptionUtility
	{
        string DecryptString(string value);
		string EncryptString(string value);
	}
}