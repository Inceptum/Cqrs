using System;
using System.Linq.Expressions;
using Castle.MicroKernel.Registration;

namespace Inceptum.Cqrs
{
    //TODO: arrange thid crap


    abstract class OptionalParameter
    {
        public object Value { get; protected set; }
        public Type Type { get; protected set; }
        public string Name { get; protected set; }
        public virtual Expression ValueExpression
        {
            get { return Expression.Constant(Value); }
        }
    }

    internal class FactoryParameter<T> : OptionalParameter
    {
        public FactoryParameter(Func<T> func)
        {
            Value = func;
            Type = typeof(T); 
        }
    }

    internal class ExpressionParameter : OptionalParameter
    {
        public ExpressionParameter(string name, Type type)
        {
            Type = type;
            Name = name;
            Parameter = Expression.Parameter(typeof(object));
        }

        public ParameterExpression Parameter { get; set; }

        public override Expression ValueExpression
        {
            get { return Expression.Convert(Parameter, Type); }
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