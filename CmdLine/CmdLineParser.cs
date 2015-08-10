using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine
{
    public class CmdLineParser
    {
        private TextWriter _helpWriter;

        public CmdLineParser(TextWriter helpWriter = null)
        {
            _helpWriter = helpWriter ?? Console.Out;
        }

        const int LineWidth = 80;
        const int PrepadSpaces = 21;

        public string Usage<T>() where T : class
        {
            return Usage(typeof(T));
        }

        private string Usage(Type type)
        {
            VerbAttribute verbAttr = type.GetCustomAttributes<VerbAttribute>().FirstOrDefault();
            var options = OptionsClassProcessor.GetOptions(type);
            var values = OptionsClassProcessor.GetValues(type);
            var restOfInput = OptionsClassProcessor.GetRestOfInputProperty(type);

            StringBuilder result = new StringBuilder();
            if (verbAttr != null)
            {
                result.AppendLine(verbAttr.HelpText.SplitToLines(80, 0) + Environment.NewLine);
            }
            foreach (var option in options.Values)
            {
                result.AppendFormat("{0,-20} {1}" + Environment.NewLine,
                    (option.IsShortOption ? "-" : "--") + option.Option,
                    option.Description.SplitToLines(LineWidth, PrepadSpaces));
            }
            foreach (var value in values.Values.OrderBy(v => v.Index))
            {
                result.AppendFormat("#{0,-19} {1}" + Environment.NewLine,
                    value.Index,
                    value.Description.SplitToLines(LineWidth, PrepadSpaces));
            }
            if (restOfInput != null)
            {
                result.AppendFormat("{0,-20} {1}" + Environment.NewLine,
                    "rest of input",
                    restOfInput.Item2.Description.SplitToLines(LineWidth, PrepadSpaces));
            }
            return result.ToString();
        }

        public string Usage(IEnumerable<Type> verbTypes)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var verbType in verbTypes)
            {
                var verbs = verbType.GetCustomAttributes<VerbAttribute>();
                foreach (var verb in verbs)
                {
                    builder.AppendFormat("{0,-20} {1}" + Environment.NewLine,
                        verb.Verb, verb.HelpText.SplitToLines(LineWidth, PrepadSpaces));
                }
            }
            return builder.ToString();
        }

        public ParseResult<T> Parse<T>(string input) where T : class
        {
            var result = Parse(typeof(T), input);
            if (result.Success)
            {
                return ParseResult<T>.WithValue((T)result.Value);
            }
            else
            {
                _helpWriter.Write(Usage(typeof(T)));
                return ParseResult<T>.WithError(result.Error);
            }
        }

        private ParseResult<object> Parse(Type type, string input)
        {
            // TODO Refactor this whole thing to use exception handling and store common objects in a class

            object instance = Activator.CreateInstance(type);

            Dictionary<string, OptionAttribute> options = OptionsClassProcessor.GetOptions(type);
            Dictionary<int, ValueAttribute> values = OptionsClassProcessor.GetValues(type);
            int unsatisfiedCount = options.Count + values.Count;
            int nextValueToFill = 0;
            OptionAttribute optionExpectingValue = null;
            Tokenizer tokenizer = new Tokenizer(input);
            while (!tokenizer.AtEnd && unsatisfiedCount > 0)
            {
                Token token = tokenizer.NextToken;
                switch (token.Kind)
                {
                    case TokenKind.LongOption:
                    case TokenKind.ShortOption:
                        if (optionExpectingValue != null)
                        {
                            return ParseResult<object>.WithError("No value provided for option '" + optionExpectingValue.Option + "'");
                        }
                        OptionAttribute optionAttr;
                        if (!options.TryGetValue(token.Value, out optionAttr))
                        {
                            return ParseResult<object>.WithError("Unexpected option '" + token.Value + "'");
                        }
                        if (optionAttr.Satisfied)
                        {
                            return ParseResult<object>.WithError("Multiple values for option '" + token.Value + "' are not allowed");
                        }

                        var alreadySatisfiedExclusiveOption = FirstAlreadySatisfiedMutuallyExclusiveOption(options, optionAttr);
                        if (alreadySatisfiedExclusiveOption != null)
                        {
                            return ParseResult<object>.WithError("The option '" + token.Value + "' is mutually exclusive with the option '" + alreadySatisfiedExclusiveOption.Option  + "' that was already satisfied");
                        }

                        if (optionAttr.TargetProperty.PropertyType == typeof(bool))
                        {
                            optionAttr.TargetProperty.SetValue(instance, true);
                            optionAttr.Satisfied = true;
                            --unsatisfiedCount;
                        }
                        else
                        {
                            optionExpectingValue = optionAttr;
                        }
                        break;
                    case TokenKind.Value:
                        if (optionExpectingValue != null)
                        {
                            string error = SetPropertyValue(optionExpectingValue, instance, token.Value);
                            if (error != null)
                            {
                                return ParseResult<object>.WithError("Error setting option '" + optionExpectingValue.Option + "': " + error);
                            }
                            optionExpectingValue.Satisfied = true;
                            optionExpectingValue = null;
                            --unsatisfiedCount;
                        }
                        else
                        {
                            if (nextValueToFill >= values.Count)
                            {
                                // There are no more values to fill, just options
                                return ParseResult<object>.WithError("Unexpected plain value '" + token.Value + "'");
                            }

                            ValueAttribute valueAttr = values[nextValueToFill];
                            string error = SetPropertyValue(valueAttr, instance, token.Value);
                            if (error != null)
                            {
                                return ParseResult<object>.WithError("Error setting value #" + nextValueToFill + ": " + error);
                            }
                            valueAttr.Satisfied = true;
                            --unsatisfiedCount;
                            ++nextValueToFill;
                        }
                        break;
                    case TokenKind.Error:
                        return ParseResult<object>.WithError("Error parsing options: " + token.Value);
                }
            }

            SetDefaultValues(instance, options, values);

            var errorResult = FindUnsatisfiedOptionsAndValues(options, values);
            if (errorResult != null)
                return errorResult;

            errorResult = SetRestOfInputPropertyIfAvailable(type, instance, tokenizer);
            if (errorResult != null)
                return errorResult;

            return ParseResult<object>.WithValue(instance);
        }

        private void SetDefaultValues(object instance, Dictionary<string, OptionAttribute> options, Dictionary<int, ValueAttribute> values)
        {
            foreach (var option in from o in options.Values where !o.Satisfied && o.Default != null select o)
            {
                option.TargetProperty.SetValue(instance, option.Default);
            }
            foreach (var value in from v in values.Values where !v.Satisfied && v.Default != null select v)
            {
                value.TargetProperty.SetValue(instance, value.Default);
            }
        }

        private static OptionAttribute FirstAlreadySatisfiedMutuallyExclusiveOption(Dictionary<string, OptionAttribute> options, OptionAttribute optionAttr)
        {
            return (from option in options.Values
                    where !String.IsNullOrEmpty(option.MutuallyExclusiveSet)
                    where String.Equals(option.MutuallyExclusiveSet, optionAttr.MutuallyExclusiveSet)
                    where option.Satisfied
                    select option).FirstOrDefault();
        }

        private static ParseResult<object> SetRestOfInputPropertyIfAvailable(Type type, object instance, Tokenizer tokenizer)
        {
            var restOfInputProp = OptionsClassProcessor.GetRestOfInputProperty(type);
            if (restOfInputProp != null)
            {
                if (restOfInputProp.Item2.Required && tokenizer.AtEnd)
                {
                    return ParseResult<object>.WithError("A value for the property '" + restOfInputProp.Item1.Name + "' is missing");
                }
                restOfInputProp.Item1.SetValue(instance, tokenizer.RestOfInput);
            }
            return null; // No error
        }

        private static ParseResult<object> FindUnsatisfiedOptionsAndValues(Dictionary<string, OptionAttribute> options, Dictionary<int, ValueAttribute> values)
        {
            var unsatisfiedRequiredOptions = from option in options.Values
                                             where option.Required && !option.Satisfied
                                             select option;
            var unsatisfiedRequiredValues = from value in values.Values
                                            where value.Required && !value.Satisfied
                                            select value;
            if (unsatisfiedRequiredOptions.Any())
            {
                return ParseResult<object>.WithError("Missing value for required option(s): " +
                    String.Join(", ", (from option in unsatisfiedRequiredOptions select option.Option).ToArray()));
            }
            if (unsatisfiedRequiredValues.Any())
            {
                return ParseResult<object>.WithError(String.Format(
                    "Missing value for {0} required value(s)", unsatisfiedRequiredValues.Count()));
            }
            return null; // No error
        }

        private static string SetPropertyValue(AttributeBase attr, object instance, string valueString)
        {
            object value = null;
            try
            {
                if (attr.TargetProperty.PropertyType.IsEnum)
                {
                    value = Enum.Parse(attr.TargetProperty.PropertyType, valueString, ignoreCase: true);
                }
                else
                {
                    value = Convert.ChangeType(valueString, attr.TargetProperty.PropertyType);
                }
            }
            catch (Exception)
            {
            }

            if (value == null && attr.Hexadecimal)
            {
                ulong result;
                if (ulong.TryParse(valueString, NumberStyles.HexNumber, null, out result))
                {
                    try
                    {
                        value = Convert.ChangeType(result, attr.TargetProperty.PropertyType);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            if (value == null)
            {
                return "Unable to convert the value '" + valueString + "' to the desired type '" +
                    attr.TargetProperty.PropertyType.Name + "'";
            }

            if (attr.Min != null && ((IComparable)attr.Min).CompareTo(value) > 0)
            {
                return "The provided value '" + valueString + "' is smaller than the minimum value " + attr.Min;
            }
            if (attr.Max != null && ((IComparable)attr.Max).CompareTo(value) < 0)
            {
                return "The provided value '" + valueString + "' is greater than the maximum value " + attr.Max;
            }

            attr.TargetProperty.SetValue(instance, value);
            return null; // No error
        }

        public ParseResult<object> Parse(IEnumerable<Type> verbTypes, string input)
        {
            Dictionary<string, Type> verbs = OptionsClassProcessor.GetVerbs(verbTypes);

            var tokenizer = new Tokenizer(input);
            var token = tokenizer.NextToken;
            if (token.Kind != TokenKind.Value)
            {
                DisplayHelp(verbs, null);
                return ParseResult<object>.WithError("Could not find an expected verb");
            }

            if (token.Value == "help")
            {
                DisplayHelp(verbs, tokenizer);
                // The user intentionally asked for help, and this is not an error. The caller has to 
                // be aware of the option that they are getting a null value in that case.
                return ParseResult<object>.WithValue(null);
            }

            Type type;
            if (!verbs.TryGetValue(token.Value, out type))
            {
                DisplayHelp(verbs, null);
                return ParseResult<object>.WithError("Could not find a match for verb '" + token.Value  + "'");
            }

            var result = Parse(type, tokenizer.RestOfInput);
            if (!result.Success)
            {
                _helpWriter.Write(Usage(type));
            }
            return result;
        }

        private void DisplayHelp(Dictionary<string, Type> verbs, Tokenizer tokenizer)
        {
            if (tokenizer == null || tokenizer.AtEnd)
            {
                _helpWriter.Write(Usage(verbs.Values));
            }
            else
            {
                string specificVerb = tokenizer.NextToken.Value;
                Type specificVerbType;
                if (verbs.TryGetValue(specificVerb, out specificVerbType))
                {
                    _helpWriter.Write(Usage(specificVerbType));
                }
                else
                {
                    _helpWriter.Write(Usage(verbs.Values));
                }
            }
        }

        public static IEnumerable<Type> FindVerbTypesInAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes<VerbAttribute>().Any())
                {
                    yield return type;
                }
            }
        }
    }
}
