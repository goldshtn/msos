using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine
{
    internal class OptionsClassProcessor
    {
        public static Dictionary<string, OptionAttribute> GetOptions(Type type)
        {
            var result = new Dictionary<string, OptionAttribute>();
            foreach (var prop in type.GetProperties())
            {
                foreach (var option in prop.GetCustomAttributes(typeof(OptionAttribute), true))
                {
                    OptionAttribute optionAttr = (OptionAttribute)option;
                    if (String.IsNullOrEmpty(optionAttr.Option))
                    {
                        throw new ParserException("An option's name cannot be null");
                    }
                    if (result.ContainsKey(optionAttr.Option))
                    {
                        throw new ParserException("There is more than one option named '" + optionAttr.Option + "'");
                    }
                    optionAttr.TargetProperty = prop;
                    VerifyAttribute(optionAttr);
                    result.Add(optionAttr.Option, optionAttr);
                }
            }
            return result;
        }

        public static Dictionary<int, ValueAttribute> GetValues(Type type)
        {
            var result = new Dictionary<int, ValueAttribute>();
            foreach (var prop in type.GetProperties())
            {
                foreach (var value in prop.GetCustomAttributes(typeof(ValueAttribute), true))
                {
                    ValueAttribute valueAttr = (ValueAttribute)value;
                    if (result.ContainsKey(valueAttr.Index))
                    {
                        throw new ParserException("There is more than one [Value] with the index " + valueAttr.Index);
                    }
                    if (valueAttr.Index < 0)
                    {
                        throw new ParserException("A [Value]'s index cannot be negative");
                    }
                    valueAttr.TargetProperty = prop;
                    VerifyAttribute(valueAttr);
                    result.Add(valueAttr.Index, valueAttr);
                }
            }
            if (!result.Keys.SequenceEqual(Enumerable.Range(0, result.Count)))
            {
                throw new ParserException("[Value] indices must be consecutive from 0 to N");
            }
            return result;
        }

        private static void VerifyAttribute(AttributeBase attr)
        {
            if (attr.Hexadecimal && !attr.TargetProperty.PropertyType.IsNumeric())
                throw new ParserException("Hexadecimal inputs are allowed only for numeric types");

            if (attr.Default != null && attr.Required)
                throw new ParserException("A required value cannot have a default");

            if (attr.Default != null && attr.Default.GetType() != attr.TargetProperty.PropertyType)
                throw new ParserException("The default value must be of type '" + attr.TargetProperty.PropertyType.Name + "'");

            if ((attr.Min != null || attr.Max != null) && !attr.TargetProperty.PropertyType.IsNumeric())
                throw new ParserException("Minimum and maximum values are allowed only for numeric types");

            if (attr.Min != null && attr.Min.GetType() != attr.TargetProperty.PropertyType)
                throw new ParserException("The minimum value must be of type '" + attr.TargetProperty.PropertyType.Name + "'");

            if (attr.Max != null && attr.Max.GetType() != attr.TargetProperty.PropertyType)
                throw new ParserException("The maximum value must be of type '" + attr.TargetProperty.PropertyType.Name + "'");

            // The cast always succeeds because we already checked that the type is one of the closed list
            // of numeric types that we support, and they all implement IComparable.
            if (attr.Min != null && attr.Max != null && ((IComparable)attr.Min).CompareTo(attr.Max) >= 0)
                throw new ParserException("The minimum value must be smaller than the maximum value");
        }

        public static Tuple<PropertyInfo, RestOfInputAttribute> GetRestOfInputProperty(Type type)
        {
            var info = (from prop in type.GetProperties()
                        let attr = prop.GetCustomAttribute<RestOfInputAttribute>()
                        where attr != null
                        select new { prop, attr }).FirstOrDefault();
            if (info != null && info.prop.PropertyType != typeof(string))
            {
                throw new ParserException("The property decorated with [RestOfInput] must be of type string");
            }
            if (info == null)
                return null;
            info.attr.TargetProperty = info.prop;
            return new Tuple<PropertyInfo, RestOfInputAttribute>(info.prop, info.attr);
        }

        public static Dictionary<string, Type> GetVerbs(IEnumerable<Type> verbTypes)
        {
            var result = new Dictionary<string, Type>();
            foreach (var type in verbTypes)
            {
                var verbs = type.GetCustomAttributes<VerbAttribute>();
                if (!verbs.Any())
                {
                    throw new ParserException("The type '" + type.FullName + "' does not have a [Verb] attribute");
                }
                foreach (var verb in verbs)
                {
                    if (result.ContainsKey(verb.Verb))
                    {
                        throw new ParserException("There is more than one [Verb] attribute with the verb '" + verb.Verb + "'");
                    }
                    result.Add(verb.Verb, type);
                }
            }
            return result;
        }
    }
}
