using System;

namespace Inceptum.Cqrs
{
    abstract class OptionalParameter
    {
        public object Value { get; protected set; }
        public Type Type { get; protected set; }
        public string Name { get; protected set; }
    }

    internal class FactoryParameter<T> : OptionalParameter
    {
        public FactoryParameter(Func<T> func)
        {
            Value = func;
            Type = typeof(T); 
        }
    }

    internal class OptionalParameter<T> : OptionalParameter
    {
        public OptionalParameter(string name,T value)
        {
            Name = name;
            Value = value;
            Type = typeof (T);
        }
        
        public OptionalParameter(T value)
        {
            Value = value;
            Type = typeof (T);
        }
    }
}