using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace Inceptum.Cqrs.Tests
{
    [TestFixture]
    public class InvestigationTests
    {
        [Test]
        public void SubjectTest()
        {

            //var list = Expression.New(typeof (List<object>));
            var list = Expression.Variable(typeof(List<object>),"list");
            var array = Expression.Variable(typeof(object[]), "array");

            LabelTarget returnTarget = Expression.Label(typeof(object[]));
            LabelExpression returnLabel = Expression.Label(returnTarget,  Expression.Constant(new object[0]));
            var create = Expression.Block(
                new[] { list, array },
                Expression.Assign(list,Expression.New(typeof (List<object>))),
                Expression.Call(list, typeof (List<object>).GetMethod("Add"), Expression.Convert(Expression.Constant(1), typeof (object))),
                Expression.Call(list, typeof (List<object>).GetMethod("Add"), Expression.Convert(Expression.Constant(2), typeof (object))),
                Expression.Assign(array, Expression.Call(list, typeof(List<object>).GetMethod("ToArray"))),
                Expression.Return(returnTarget,array),
                returnLabel
                );
            var lambda = (Expression<Func<object[]>>)Expression.Lambda(create);
            Console.WriteLine(create);
            var objects = lambda.Compile()();
            foreach (var v in objects)
            {
                Console.WriteLine(v);
                
            }


        }[Test]
        public void Subject1Test()
        {
            
            var cw = typeof(Console).GetMethod("WriteLine",BindingFlags.Static|BindingFlags.Public,null,new []{typeof(object)},null);
            ParameterExpression input = Expression.Parameter(typeof(IEnumerable),"input");
            var list = Expression.Variable(typeof(List<object>), "list");
            var item = Expression.Variable(typeof(object), "item");

            LabelTarget returnTarget = Expression.Label(typeof(object[]));
            LabelExpression returnLabel = Expression.Label(returnTarget,  Expression.Constant(new object[0]));
            var create = Expression.Block(
                new[] {   list,item },
                Expression.Assign(list, Expression.New(typeof(List<object>))),
                Expression.Call(cw,input),
                ForEachExpr(input,item, Expression.Call(list, typeof(List<object>).GetMethod("Add"), Expression.Call(item,"GetType",null))),
                Expression.Return(returnTarget, Expression.Call(list, typeof(List<object>).GetMethod("ToArray"))),
                returnLabel
                );
            var lambda = (Expression<Func<IEnumerable,object[]>>)Expression.Lambda(create, input);
            Console.WriteLine(create);
            var func = lambda.Compile();
            var objects = func(new []{"1w","2w"});
            foreach (var v in objects)
            {
                Console.WriteLine(v);
                
            }


        }


        public static BlockExpression ForEachExpr(ParameterExpression enumerable, ParameterExpression item,Expression expression)
        {

            var enumerator = Expression.Variable(typeof(IEnumerator), "enumerator");
            var doMoveNext = Expression.Call(enumerator, typeof(IEnumerator).GetMethod("MoveNext"));
            var assignToEnum = Expression.Assign(enumerator, Expression.Call(enumerable, typeof(IEnumerable).GetMethod("GetEnumerator")));
            var assignCurrent = Expression.Assign(item, Expression.Property(enumerator, "Current"));
            var @break = Expression.Label();

            var @foreach = Expression.Block(
                    new ParameterExpression[] {  enumerator},
                    assignToEnum , 
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.NotEqual(doMoveNext, Expression.Constant(false)),
                            Expression.Block(assignCurrent, expression),
                            Expression.Break(@break))
                    , @break)  
                );

            return @foreach;
        }
         
    }
}