using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Cqrs.Utils;
using Inceptum.Messaging.Contract;
using NLog;

namespace Inceptum.Cqrs.Configuration
{
    //TODO: rename to EventHandlingResult
    public class CommandHandlingResult
    {
        public long  RetryDelay { get; set; } 
        public bool  Retry { get; set; } 
    }


    internal class CommandDispatcher:IDisposable
    {
        readonly Dictionary<Type, Func<object, Endpoint,string,CommandHandlingResult>> m_Handlers = new Dictionary<Type, Func<object, Endpoint,string, CommandHandlingResult>>();
        private readonly string m_BoundedContext;
        private readonly QueuedTaskScheduler m_QueuedTaskScheduler;
        private readonly Dictionary<CommandPriority,TaskFactory> m_TaskFactories=new Dictionary<CommandPriority, TaskFactory>();
        private static long m_FailedCommandRetryDelay = 60000;
        readonly Logger m_Logger= LogManager.GetCurrentClassLogger();
        public CommandDispatcher(string boundedContext, int threadCount=1,long failedCommandRetryDelay = 60000)
        {
            m_FailedCommandRetryDelay = failedCommandRetryDelay;
            m_QueuedTaskScheduler = new QueuedTaskScheduler(threadCount);
            foreach (var value in Enum.GetValues(typeof(CommandPriority)))
            {
                m_TaskFactories[(CommandPriority) value] = new TaskFactory(
                    ((CommandPriority) value) == CommandPriority.Normal
                        ? new CurrentThreadTaskScheduler()
                        : m_QueuedTaskScheduler.ActivateNewQueue((int) value));
            }
            m_BoundedContext = boundedContext;
        }

        public void Wire(object o, params OptionalParameter[] parameters)
        {
            if (o == null) throw new ArgumentNullException("o");
            parameters = parameters.Concat(new OptionalParameter[] { new OptionalParameter<string>("boundedContext", m_BoundedContext) }).ToArray();

        
            var handleMethods = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" &&
                    !m.IsGenericMethod &&
                    m.GetParameters().Length > 0 &&
                    !m.GetParameters().First().ParameterType.IsInterface)
                .Select(m => new
                {
                    method = m,
                    returnsResult=m.ReturnType==typeof(CommandHandlingResult),
                    commandType = m.GetParameters().First().ParameterType,
                    callParameters = m.GetParameters().Skip(1).Select(p => new
                    {
                        parameter = p,
                        optionalParameter = parameters.FirstOrDefault(par => par.Name == p.Name || par.Name == null && p.ParameterType == par.Type),
                    })
                })
                .Where(m => m.callParameters.All(p => p.parameter != null));


            foreach (var method in handleMethods)
            {
                registerHandler(method.commandType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value),method.returnsResult);
            }

        }

        private void registerHandler(Type commandType, object o, Dictionary<ParameterInfo, object> optionalParameters, bool returnsResult)
        {
            bool isRoutedCommandHandler = commandType.IsGenericType && commandType.GetGenericTypeDefinition() == typeof (RoutedCommand<>);
            Type handledType;
            var command = Expression.Parameter(typeof(object), "command");
            var endpoint = Expression.Parameter(typeof(Endpoint), "endpoint");
            var route = Expression.Parameter(typeof(string), "route");


            Expression commandParameter;
            
            if (!isRoutedCommandHandler)
            {
                commandParameter = Expression.Convert(command, commandType);
                handledType = commandType;
            }
            else
            {
                handledType = commandType.GetGenericArguments()[0];
                var ctor = commandType.GetConstructor(new[] { handledType, typeof(Endpoint) ,typeof(string)});
                commandParameter = Expression.New(ctor, Expression.Convert(command, handledType), endpoint, route);
            }

                           
            Expression[] parameters =new [] {commandParameter}.Concat(
                        optionalParameters.Select(p => Expression.Constant(p.Value, p.Key.ParameterType))).ToArray();
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);


            Expression<Func<object, Endpoint, string, CommandHandlingResult>> lambda;
            if (returnsResult)
                lambda = (Expression<Func<object, Endpoint, string, CommandHandlingResult>>)Expression.Lambda(call, command, endpoint, route);
            else
            {
                LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult));
                var returnLabel = Expression.Label(returnTarget,Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 })); 
                var block = Expression.Block(
                    call,
                    returnLabel);
                lambda = (Expression<Func<object, Endpoint, string, CommandHandlingResult>>)Expression.Lambda(block, command, endpoint,route);
            }


            Func<object, Endpoint, string, CommandHandlingResult> handler;
            if (m_Handlers.TryGetValue(handledType, out handler))
            {
                throw new InvalidOperationException(string.Format(
                    "Only one handler per command is allowed. Command {0} handler is already registered in bound context {1}. Can not register {2} as handler for it", commandType, m_BoundedContext, o));
            }
            m_Handlers.Add(handledType, lambda.Compile());
        }

        public void Dispatch(object command, CommandPriority priority, AcknowledgeDelegate acknowledge, Endpoint commandOriginEndpoint,string route)
        {
            Func<object, Endpoint, string, CommandHandlingResult> handler;
            if (!m_Handlers.TryGetValue(command.GetType(), out handler))
            {

                m_Logger.Warn("Failed to handle command {0} in bound context {1}, no handler was registered for it", command, m_BoundedContext);
                acknowledge(m_FailedCommandRetryDelay, false);
                return;
            }

            m_TaskFactories[priority].StartNew(() => handle(command, acknowledge, handler,commandOriginEndpoint,route));
        }

        private void handle(object command, AcknowledgeDelegate acknowledge, Func<object, Endpoint, string, CommandHandlingResult> handler, Endpoint commandOriginEndpoint,string route)
        {
            try
            {
                var result = handler(command,commandOriginEndpoint,route);
                acknowledge(result.RetryDelay, !result.Retry);
            }
            catch (Exception e)
            {
                m_Logger.WarnException("Failed to handle command of type " + (command==null?"null":command.GetType().Name), e);
                acknowledge(m_FailedCommandRetryDelay, false);
            }
        }

        public void Dispose()
        {
            m_QueuedTaskScheduler.Dispose();
        }
    }
}