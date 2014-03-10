﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;
using NLog;

namespace Inceptum.Cqrs
{
    internal class EventDispatcher
    {
        readonly Dictionary<Tuple<string, Type>, List<Func<object, CommandHandlingResult>>> m_Handlers = new Dictionary<Tuple<string, Type>, List<Func<object, CommandHandlingResult>>>();
        private readonly string m_BoundedContext;
        internal static long m_FailedEventRetryDelay = 60000;
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        public EventDispatcher(string boundedContext)
        {
            m_BoundedContext = boundedContext;
        }
        public void Wire(string fromBoundedContext,object o, params OptionalParameter[] parameters)
        {
            parameters = parameters.Concat(new OptionalParameter[] { new OptionalParameter<string>("boundedContext", fromBoundedContext) }).ToArray();

            var handleMethods = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" && 
                    !m.IsGenericMethod && 
                    m.GetParameters().Length>0 && 
                    !m.GetParameters().First().ParameterType.IsInterface)
                .Select(m=>new
                    {
                        method=m,
                        eventType = m.GetParameters().First().ParameterType,
                        returnsResult = m.ReturnType == typeof(CommandHandlingResult),
                        callParameters=m.GetParameters().Skip(1).Select(p=>new
                            {
                                parameter = p,
                                optionalParameter=parameters.FirstOrDefault(par=>par.Name==p.Name||par.Name==null && p.ParameterType==par.Type),
                            })
                    })
                .Where(m=>m.callParameters.All(p=>p.parameter!=null));


            foreach (var method in handleMethods)
            {
                registerHandler(fromBoundedContext,method.eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value), method.returnsResult);
            }
        }

        private void registerHandler(string fromBoundedContext, Type eventType, object o, Dictionary<ParameterInfo, object> optionalParameters, bool returnsResult)
        {
            var @event = Expression.Parameter(typeof(object), "event");
         
            Expression[] parameters =
                new Expression[] {Expression.Convert(@event, eventType)}.Concat(optionalParameters.Select(p => Expression.Constant(p.Value))).ToArray();
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);
            Expression<Func<object, CommandHandlingResult>> lambda;

            if (returnsResult)
                lambda = (Expression<Func<object, CommandHandlingResult>>)Expression.Lambda(call, @event);
            else
            {
                LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult));
                var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 }));
                var block = Expression.Block(
                    call,
                    returnLabel);
                lambda = (Expression<Func<object, CommandHandlingResult>>)Expression.Lambda(block, @event);
            }


            List<Func<object, CommandHandlingResult>> list;
            var key = Tuple.Create(fromBoundedContext,eventType);
            if (!m_Handlers.TryGetValue(key, out list))
            {
                list = new List<Func<object, CommandHandlingResult>>();
                m_Handlers.Add(key, list);
            }
            list.Add(lambda.Compile());

        }

        public void Dispacth(string fromBoundedContext,object @event, AcknowledgeDelegate acknowledge)
        {
            List<Func<object, CommandHandlingResult>> list;

            if (@event == null)
            {
                //TODO: need to handle null deserialized from messaging
                throw new ArgumentNullException("event");
            }
            var key = Tuple.Create(fromBoundedContext, @event.GetType());
            if (!m_Handlers.TryGetValue(key, out list))
            {
                acknowledge(0, true);
                return;
            }


            foreach (var handler in list)
            {
                CommandHandlingResult result;
                try
                {
                    result = handler(@event);
                }
                catch (Exception e)
                {
                    m_Logger.WarnException("Failed to handle event of type " + @event.GetType().Name, e);
                    result = new CommandHandlingResult {Retry = true, RetryDelay = m_FailedEventRetryDelay};
                }

                if (result.Retry)
                {
                    acknowledge(result.RetryDelay, !result.Retry);
                    return;
                }
            }
            acknowledge(0, true);
        }
    }
}