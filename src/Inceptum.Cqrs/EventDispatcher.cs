using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Messaging.Contract;
using NLog;
using ThreadState = System.Threading.ThreadState;

namespace Inceptum.Cqrs
{

    class BatchManager
    {
        readonly List<Action> m_Events=new List<Action>();
        private long m_Counter = 0;
        private readonly int m_BatchSize;
        private readonly long m_ApplyTimeout;
        private readonly long m_FailedEventRetryDelay;
        private readonly Logger m_Logger;
        private readonly Stopwatch m_SinceFirstEvent=new Stopwatch();
        private Action m_BeforeBatchApply;
        private Action m_AfterBatchApply;

        public BatchManager( long failedEventRetryDelay,Logger logger,int batchSize=0, long applyTimeout=0,Action beforeBatchApply=null,Action afterBatchApply=null )
        {
            m_AfterBatchApply = afterBatchApply??(()=>{});
            m_BeforeBatchApply = beforeBatchApply ?? (() => { });
            m_Logger = logger;
            m_FailedEventRetryDelay = failedEventRetryDelay;
            m_ApplyTimeout = applyTimeout;
            m_BatchSize = batchSize;
        }

        public void Handle(Func<object[], CommandHandlingResult[]> handler, Tuple<object, AcknowledgeDelegate>[] events, EventOrigin origin)
        {
            if(!events.Any())
                return;

            if (m_BatchSize == 0 && m_ApplyTimeout == 0)
            {
                doHandle(handler, events, origin);
                return;
            }

            lock (m_Events)
            {
                m_Events.Add(() => doHandle(handler, events, origin));
                if(m_Counter==0)
                    m_SinceFirstEvent.Start();
                m_Counter += events.Length;
                ApplyBatchIfRequired();
            }
        }

        internal void ApplyBatchIfRequired(bool force = false)
        {
            Action[] handles = new Action[0];

            lock (m_Events)
            {
                if (m_Counter == 0)
                    return;

                if (m_Counter >= m_BatchSize || m_SinceFirstEvent.ElapsedMilliseconds > m_ApplyTimeout || force)
                {
                    handles = m_Events.ToArray();
                    m_Events.Clear();
                    m_Counter = 0;
                    m_SinceFirstEvent.Reset();
                }
            }

            m_BeforeBatchApply();
            foreach (var handle in handles)
            {
                handle();
            }
            m_AfterBatchApply();
        }

        private void doHandle(Func<object[], CommandHandlingResult[]> handler, Tuple<object, AcknowledgeDelegate>[] events, EventOrigin origin)
        {
            //TODO: Wat if connect is broken and engine failes to aknowledge?..
            CommandHandlingResult[] results;
            try
            {
                results = handler(@events.Select(e => e.Item1).ToArray());
                //TODO: verify number of reults matches nuber of events
            }
            catch (Exception e)
            {
                m_Logger.WarnException("Failed to handle events batch of type " + origin.EventType.Name, e);
                results = @events.Select(x => new CommandHandlingResult {Retry = true, RetryDelay = m_FailedEventRetryDelay}).ToArray();
            }

            for (var i = 0; i < events.Length; i++)
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

    internal class EventDispatcher:IDisposable
    {
        readonly Dictionary<EventOrigin, List<Tuple<Func<object[], CommandHandlingResult[]>,BatchManager>>> m_Handlers = new Dictionary<EventOrigin, List<Tuple<Func<object[], CommandHandlingResult[]>, BatchManager>>>();
        private readonly string m_BoundedContext;
        internal static long m_FailedEventRetryDelay = 60000;
        readonly Dictionary<Guid, Replay> m_Replays = new Dictionary<Guid, Replay>();
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        readonly ManualResetEvent m_Stop=new ManualResetEvent(false);
        private readonly Thread m_ApplyBatchesThread;
        private readonly BatchManager m_DefaultBatchManager;

        public EventDispatcher(string boundedContext)
        {
            m_DefaultBatchManager = new BatchManager(m_FailedEventRetryDelay, m_Logger);
            m_BoundedContext = boundedContext;
            m_ApplyBatchesThread = new Thread(() =>
            {
                while (!m_Stop.WaitOne(1000))
                {
                    applyBatches();
                }
            });
            m_ApplyBatchesThread.Name = string.Format("'{0}' bounded context batch event processing thread",boundedContext);
        }

        private void applyBatches(bool force=false)
        {
            foreach (var batchManager in m_Handlers.SelectMany(h=>h.Value.Select(_=>_.Item2)))
            {
                batchManager.ApplyBatchIfRequired(force);
            }
        }

        public void Wire(string fromBoundedContext,object o, params OptionalParameter[] parameters)
        { 
            
            wire(fromBoundedContext, o,null, parameters);
        }

        public void Wire(string fromBoundedContext, object o, int batchSize, int applyTimeoutInSeconds, Action<object> beforeBatchApply, Action<object> afterBatchApply, params OptionalParameter[] parameters)
        {
            var batchManager = batchSize==0 && applyTimeoutInSeconds==0
                ?null
                : new BatchManager(m_FailedEventRetryDelay, m_Logger, batchSize, applyTimeoutInSeconds, () => beforeBatchApply(o), () => afterBatchApply(o));
            wire(fromBoundedContext, o, batchManager,parameters);
        }

        private void wire(string fromBoundedContext, object o,BatchManager batchManager, params OptionalParameter[] parameters)
        {
            if(batchManager!=null && m_ApplyBatchesThread.ThreadState==ThreadState.Unstarted)
                m_ApplyBatchesThread.Start();

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
                var eventType = method.isBatch ? method.eventType.GetElementType() : method.eventType;
                var key = new EventOrigin(fromBoundedContext, eventType);
                List<Tuple<Func<object[], CommandHandlingResult[]>, BatchManager>> handlersList;
                if (!m_Handlers.TryGetValue(key, out handlersList))
                {
                    handlersList = new List<Tuple<Func<object[], CommandHandlingResult[]>, BatchManager>>();
                    m_Handlers.Add(key, handlersList);
                }
                var handler=method.isBatch
                    ?createBatchHandler(eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value))
                    :createHandler(eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value), method.returnsResult);
                
                handlersList.Add(Tuple.Create(handler,batchManager??m_DefaultBatchManager));
            }
        }

        private Func<object[], CommandHandlingResult[]> createBatchHandler(Type eventType, object o, Dictionary<ParameterInfo, object> optionalParameters)
        {
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

           
            return lambda.Compile();
        }

        private Func<object[], CommandHandlingResult[]> createHandler(Type eventType, object o, Dictionary<ParameterInfo, object> optionalParameters, bool returnsResult)
        {
            LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult[]));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult[0]));

            var events = Expression.Parameter(typeof(object[]));
            var result = Expression.Variable(typeof(List<CommandHandlingResult>), "result");
            var @event = Expression.Variable(typeof(object), "@event");
            var handleParams = new Expression[] { Expression.Convert(@event, eventType) }.Concat(optionalParameters.Select(p => Expression.Constant(p.Value))).ToArray();
            var callHandler = Expression.Call(Expression.Constant(o), "Handle", null, handleParams);


            var okResult = Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 });
            var failResult = Expression.Constant(new CommandHandlingResult { Retry = true, RetryDelay = m_FailedEventRetryDelay });
            
            Expression registerResult  = Expression.TryCatch(
                Expression.Block(
                    typeof(void),
                    returnsResult
                        ?(Expression)Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("Add"), callHandler)
                        :(Expression)Expression.Block(callHandler, Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("Add"), okResult))
                    ),
                Expression.Catch(
                    typeof(Exception),
                     Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("Add"), failResult)
                    )
                );
    

            var create = Expression.Block(
               new[] { result, @event },
               Expression.Assign(result, Expression.New(typeof(List<CommandHandlingResult>))),
               ForEachExpr(events, @event, registerResult),
               Expression.Return(returnTarget, Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("ToArray"))),
               returnLabel
               );

            var lambda = (Expression<Func<object[], CommandHandlingResult[]>>)Expression.Lambda(create, events);

           
            return lambda.Compile();
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
            List<Tuple<Func<object[], CommandHandlingResult[]>, BatchManager>> list;

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
                var batchManager = handler.Item2;
                batchManager.Handle(handler.Item1, events,origin);

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

        public void Dispose()
        {
            if (m_ApplyBatchesThread.ThreadState == ThreadState.Unstarted) 
                return;
            m_Stop.Set();
            m_ApplyBatchesThread.Join();
            applyBatches(true);
        }
    }
}