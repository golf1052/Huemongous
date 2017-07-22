using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Huemongous
{
    public static class HelperMethods
    {
        public static int GetMired(int temp)
        {
            return 1000000 / temp;
        }
    }
}
