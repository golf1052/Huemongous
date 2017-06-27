using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Huemongous.Bindings
{
    public class LightListViewBinding
    {
        public string Name { get; set; }
        public string Number { get; set; }

        public LightListViewBinding()
        {
        }

        public LightListViewBinding(string name, string number)
        {
            this.Name = name;
            this.Number = number;
        }
    }
}
