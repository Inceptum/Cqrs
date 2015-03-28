using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Messaging.Contract;
using NLog;

namespace Inceptum.Cqrs
{
    internal class EventDispatcher
    {
        readonly Dictionary<EventOrigin, List<Func<object[], CommandHandlingResult[]>>> m_Handlers = new Dictionary<EventOrigin, List<Func<object[], CommandHandlingResult[]>>>();
        private readonly string m_BoundedContext;
        internal static long m_FailedEventRetryDelay = 60000;
        readonly Dictionary<Guid, Replay> m_Replays = new Dictionary<Guid, Replay>();
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
                    !m.GetParameters().First().ParameterType.IsInterface &&
                    !(m.GetParameters().First().ParameterType.IsArray && m.GetParameters().First().ParameterType.GetElementType().IsInterface)
                    )
                .Select(m=>new
                    {
                        method=m,
                        eventType = m.GetParameters().First().ParameterType,
                        returnsResult = m.ReturnType == typeof(CommandHandlingResult),
                        isBatch = m.ReturnType == typeof(CommandHandlingResult[]) && m.GetParameters().First().ParameterType.IsArray,
                        callParameters=m.GetParameters().Skip(1).Select(p=>new
                            {
                                parameter = p,
                                optionalParameter=parameters.FirstOrDefault(par=>par.Name==p.Name||par.Name==null && p.ParameterType==par.Type),
                            })
                    })
                .Where(m=>m.callParameters.All(p=>p.parameter!=null));


            foreach (var method in handleMethods)
            {
                if(method.isBatch)
                    registerBatchHandler(fromBoundedContext, method.eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value));
                else
                    registerHandler(fromBoundedContext, method.eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value), method.returnsResult);
            }
        }

        private void registerBatchHandler(string fromBoundedContext, Type eventsType, object o, Dictionary<ParameterInfo, object> optionalParameters)
        {
            var eventType = eventsType.GetElementType();
            LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult[]));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult[0]));

            var events = Expression.Parameter(typeof(object[]));
            var eventsListType = typeof(List<>).MakeGenericType(eventType);
            var list = Expression.Variable(eventsListType, "list");
            var @event = Expression.Variable(typeof(object), "@event");
            var handleParams = new Expression[] { Expression.Call(list, eventsListType.GetMethod("ToArray")) }.Concat(optionalParameters.Select(p => Expression.Constant(p.Value))).ToArray();
            var callHandler = Expression.Call(Expression.Constant(o), "Handle", null, handleParams);

            Expression addConvertedEvent = Expression.Call(list, eventsListType.GetMethod("Add"), Expression.Convert(@event, eventType));

            var create = Expression.Block(
               new[] { list, @event },
               Expression.Assign(list, Expression.New(eventsListType)),
               ForEachExpr(events, @event, addConvertedEvent),
               Expression.Return(returnTarget,callHandler),
               returnLabel
               );

            var lambda = (Expression<Func<object[], CommandHandlingResult[]>>)Expression.Lambda(create, events);

            List<Func<object[], CommandHandlingResult[]>> handlersList;
            var key = new EventOrigin(fromBoundedContext, eventType);
            if (!m_Handlers.TryGetValue(key, out handlersList))
            {
                handlersList = new List<Func<object[], CommandHandlingResult[]>>();
                m_Handlers.Add(key, handlersList);
            }
            handlersList.Add(lambda.Compile());

        }

        private void registerHandler(string fromBoundedContext, Type eventType, object o, Dictionary<ParameterInfo, object> optionalParameters, bool returnsResult)
        {
            LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult[]));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult[0]));

            var events = Expression.Parameter(typeof(object[]));
            var result = Expression.Variable(typeof(List<CommandHandlingResult>), "result");
            var @event = Expression.Variable(typeof(object), "@event");
            var handleParams = new Expression[] { Expression.Convert(@event, eventType) }.Concat(optionalParameters.Select(p => Expression.Constant(p.Value))).ToArray();
            var callHandler = Expression.Call(Expression.Constant(o), "Handle", null, handleParams);
            
            Expression registerResult;
            if (returnsResult)
                registerResult = Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("Add"), callHandler);
            else
            {
                var okResult = Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 });
                registerResult = Expression.Block(callHandler, Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("Add"), okResult));
            }

            var create = Expression.Block(
               new[] { result, @event },
               Expression.Assign(result, Expression.New(typeof(List<CommandHandlingResult>))),
               ForEachExpr(events, @event, registerResult),
               Expression.Return(returnTarget, Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("ToArray"))),
               returnLabel
               );

            var lambda = (Expression<Func<object[], CommandHandlingResult[]>>)Expression.Lambda(create, events);

            List<Func<object[], CommandHandlingResult[]>> handlersList;
            var key = new EventOrigin(fromBoundedContext, eventType);
            if (!m_Handlers.TryGetValue(key, out handlersList))
            {
                handlersList = new List<Func<object[], CommandHandlingResult[]>>();
                m_Handlers.Add(key, handlersList);
            }
            handlersList.Add(lambda.Compile());
        }


        private static BlockExpression ForEachExpr(ParameterExpression enumerable, ParameterExpression item, Expression expression)
        {

            var enumerator = Expression.Variable(typeof(IEnumerator), "enumerator");
            var doMoveNext = Expression.Call(enumerator, typeof(IEnumerator).GetMethod("MoveNext"));
            var assignToEnum = Expression.Assign(enumerator, Expression.Call(enumerable, typeof(IEnumerable).GetMethod("GetEnumerator")));
            var assignCurrent = Expression.Assign(item, Expression.Property(enumerator, "Current"));
            var @break = Expression.Label();

            var @foreach = Expression.Block(
                    new [] { enumerator },
                    assignToEnum,
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.NotEqual(doMoveNext, Expression.Constant(false)),
                            Expression.Block(assignCurrent, expression),
                            Expression.Break(@break)), 
                        @break)
                );

            return @foreach;
        }
        public void Dispatch(string fromBoundedContext, IEnumerable<Tuple<object,AcknowledgeDelegate>> events)
        {
            foreach (var e in events.GroupBy(e=>new EventOrigin(fromBoundedContext,e.Item1.GetType())))
            {
                dispatch(e.Key,e.ToArray());
            }

        }

        //TODO: delete
        public void Dispatch(string fromBoundedContext, object message, AcknowledgeDelegate acknowledge)
        {
            Dispatch(fromBoundedContext, new[] {Tuple.Create(message, acknowledge)});
        }


        private void dispatch(EventOrigin origin, Tuple<object, AcknowledgeDelegate>[] events)
        {
            List<Func<object[], CommandHandlingResult[]>> list;

            if (events == null)
            {
                //TODO: need to handle null deserialized from messaging
                throw new ArgumentNullException("events");
            }

            if (!m_Handlers.TryGetValue(origin, out list))
            {
                foreach (var @event in events)
                {
                    @event.Item2(0, true);
                }
                return;
            }


            foreach (var handler in list)
            {
                CommandHandlingResult[] results;
                try
                {
                    results = handler(@events.Select(e=>e.Item1).ToArray());
                    //TODO: verify number of reults matches nuber of events
                }
                catch (Exception e)
                {
                    m_Logger.WarnException("Failed to handle events batch of type " + origin.EventType.Name, e);
                    results = @events.Select(x=>new CommandHandlingResult {Retry = true, RetryDelay = m_FailedEventRetryDelay}).ToArray();
                }

                for (int i = 0; i < events.Length; i++)
                {
                    var result = results[i];
                    var acknowledge = events[i].Item2;
                    if (result.Retry)
                        acknowledge(result.RetryDelay, !result.Retry);
                    else
                        acknowledge(0, true);
                }
            }
            
        }



        public void ProcessReplayedEvent(object @event, AcknowledgeDelegate acknowledge, string remoteBoundedContext,
          Dictionary<string, string> headers)
        {
            Replay replay=null;

            if (headers.ContainsKey("CommandId"))
            {
                var commandId = Guid.Parse(headers["CommandId"]);
                replay = findReplay(commandId);
            }
            else
            {
                m_Logger.Warn("Bounded context '{0}' uses obsolete Inceptum.Cqrs version. Callback would be never invoked.",remoteBoundedContext);
            }
            IEnumerable<Tuple<object, AcknowledgeDelegate>> eventsToDispatch;
            var replayFinishedEvent = @event as ReplayFinishedEvent;
            if (replayFinishedEvent != null )
            {
                if (replay == null)
                {
                    acknowledge(0, true);
                    return;
                }

                lock (replay)
                {
                    eventsToDispatch = replay.GetEventsToDispatch(replayFinishedEvent, acknowledge);

                    if (replay.ReportReplayFinishedIfRequired(m_Logger))
                    {
                        lock (m_Replays)
                        {
                            m_Replays.Remove(replay.Id);
                        }
                    }
                }

            }
            else if (replay != null)
            {
                lock (replay)
                {
                    eventsToDispatch = replay.GetEventsToDispatch(@event, (delay, doAcknowledge) =>
                    {
                        acknowledge(delay, doAcknowledge);

                        if (doAcknowledge)
                            replay.Increment();

                        if (replay.ReportReplayFinishedIfRequired(m_Logger))
                        {
                            lock (m_Replays)
                            {
                                m_Replays.Remove(replay.Id);
                            }
                        }

                    });
                }
            }
            else
            {
                eventsToDispatch = new[] {Tuple.Create(@event, acknowledge)};
            }

            Dispatch(remoteBoundedContext, eventsToDispatch);
        }

        private Replay findReplay(Guid replayId)
        {
            Replay replay;
            lock (m_Replays)
            {
                if (!m_Replays.TryGetValue(replayId, out replay))
                    throw new InvalidOperationException(string.Format("Replay with id {0} is not found", replayId));
                if (replay == null)
                    throw new InvalidOperationException(string.Format("Replay with id {0} is null", replayId));
            }
            return replay;
        }

        public void RegisterReplay(Guid id, Action<long> callback,int batchSize)
        {
            lock (m_Replays)
            {
                if (m_Replays.ContainsKey(id))
                    throw new InvalidOperationException(string.Format("Replay with id {0} is already in pogress", id));
                var replay = new Replay(id, callback, batchSize);
                m_Replays[id] = replay;
            }

        }

    }
}