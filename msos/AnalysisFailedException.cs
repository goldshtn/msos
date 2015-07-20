using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace msos
{
    [Serializable]
    class AnalysisFailedException : Exception
    {
        public AnalysisFailedException()
            : base()
        {
        }

        public AnalysisFailedException(string message)
            : base(message)
        {
        }

        protected AnalysisFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
