﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oed_authz;
public static class Utils
{
    public static bool IsValidSsn(string estateSsnOnly)
    {
        if (estateSsnOnly.Length != 11) return false;

        foreach (var t in estateSsnOnly)
        {
            if (t is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}