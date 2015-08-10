using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine
{
    internal enum TokenKind
    {
        ShortOption, // -a, -b
        LongOption,  // --alpha, --beta
        Value,
        Error
    }

    internal class Token
    {
        public Token(TokenKind kind, string value, int endPosition)
        {
            Kind = kind;
            Value = value;
            EndPosition = endPosition;
        }

        public TokenKind Kind { get; private set; }
        public string Value { get; private set; }
        public int EndPosition { get; private set; }
    }

    internal class Tokenizer
    {
        private string _input;
        private int _position;
        private Token _currentToken;

        public Tokenizer(string input)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            
            _input = input;
        }

        public Token NextToken
        {
            get
            {
                Advance();
                return _currentToken;
            }
        }

        public string RestOfInput
        {
            get { return _input.Substring(_position).Trim(); }
        }

        public bool AtEnd
        {
            get { return _position >= _input.Length; }
        }

        private char CurrentChar
        {
            get { return _input[_position]; }
        }

        private bool CanPeek
        {
            get { return _position < _input.Length - 1; }
        }

        private char PeekNextChar
        {
            get { return _input[_position + 1]; }
        }

        private void Advance()
        {
            EatWhitespace();

            if (AtEnd)
            {
                _currentToken = null;
                return;
            }

            if (CurrentChar == '-')
            {
                if (CanPeek && Char.IsDigit(PeekNextChar))
                {
                    // It's actually a negative number, not an option
                    EatRawValue();
                }
                else
                {
                    EatOption();
                }
            }
            else
            {
                EatRawValue();
            }

            EatWhitespace();
        }

        private void EatOption()
        {
            ++_position;
            string option = EatNonWhitespace();
            if (option.Length == 0)
            {
                _currentToken = new Token(TokenKind.Error, "No option name specified", _position);
            }
            else if (option[0] == '-')
            {
                if (option.Length == 1)
                {
                    _currentToken = new Token(TokenKind.Error, "No option name specified", _position);
                }
                else
                {
                    _currentToken = new Token(TokenKind.LongOption, option.Substring(1), _position);
                }
            }
            else if (option.Length == 1)
            {
                _currentToken = new Token(TokenKind.ShortOption, option, _position);
            }
            else
            {
                _currentToken = new Token(TokenKind.Error, "Short option name must be a single character", _position);
            }
        }

        private void EatWhitespace()
        {
            while (!AtEnd && Char.IsWhiteSpace(CurrentChar))
                ++_position;
        }

        private string EatNonWhitespace()
        {
            string rawValue = "";
            while (!AtEnd && !Char.IsWhiteSpace(CurrentChar))
            {
                rawValue += CurrentChar;
                ++_position;
            }
            return rawValue;
        }

        private void EatRawValue()
        {
            string rawValue = EatNonWhitespace();
            _currentToken = new Token(TokenKind.Value, rawValue, _position);
        }
    }
}
