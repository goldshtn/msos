using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false)]
    class SupportedTargetsAttribute : Attribute
    {
        public IEnumerable<TargetType> SupportedTargets { get; private set; }

        public SupportedTargetsAttribute(params TargetType[] targets)
        {
            SupportedTargets = targets;
        }
    }
}
