﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoPainter.Core.Util
{
    public static class BoolExt
    {
        public static bool SetFalse(ref this bool a)
        {
            bool c = a;
            a = false;
            return c;
        }
    }
}
