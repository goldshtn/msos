using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine
{
    public class ParseResult<T> where T : class
    {
        public T Value { get; private set; }
        public string Error { get; private set; }
        public bool Success { get; private set; }

        internal static ParseResult<T> WithError(string error)
        {
            return new ParseResult<T>
            {
                Error = error,
                Success = false
            };
        }

        internal static ParseResult<T> WithValue(T value)
        {
            return new ParseResult<T>
            {
                Value = value,
                Success = true
            };
        }
    }
}
