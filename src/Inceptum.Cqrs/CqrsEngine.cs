using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using Castle.Components.DictionaryAdapter.Xml;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Messaging.Contract;
using NLog;

namespace Inceptum.Cqrs
{
    public interface IDependencyResolver
    {
        object GetService(Type type);
        bool HasService(Type type);
    }

    internal class DefaultDependencyResolver : IDependencyResolver
    {
        public object GetService(Type type)
        {
            return Activator.CreateInstance(type);
        }

        public bool HasService(Type type)
        {
            return !type.IsInterface;
        }
    }

    public class CqrsEngine :ICqrsEngine
    {
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly CompositeDisposable m_Subscription=new CompositeDisposable();
        private readonly IEndpointResolver m_EndpointResolver;
        private readonly List<BoundedContext> m_BoundedContexts;
        internal List<BoundedContext> BoundedContexts {
            get { return m_BoundedContexts; }
        }
        private readonly IRegistration[] m_Registrations;
        private readonly IDependencyResolver m_DependencyResolver;
        private bool m_CreateMissingEndpoints = false;

        protected IMessagingEngine MessagingEngine
        {
            get { return m_MessagingEngine; }
        }


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, params IRegistration[] registrations)
            :this(new DefaultDependencyResolver(), messagingEngine, endpointResolver, registrations)
        {
        }

        public CqrsEngine(IDependencyResolver dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver,
            params IRegistration[] registrations)
        {
            m_DependencyResolver = dependencyResolver;
            m_Registrations = registrations;
            m_EndpointResolver = endpointResolver;
            m_MessagingEngine = messagingEngine;
            m_BoundedContexts=new List<BoundedContext>();
            init();
            
        }

        private void init()
        {
            foreach (var registration in m_Registrations)
            {
                registration.Create(this);
            }
            foreach (var registration in m_Registrations)
            {
                registration.Process(this);
            }

            foreach (var boundedContext in BoundedContexts)
            {
                boundedContext.Processes.ForEach(p => p.Start(this, boundedContext.EventsPublisher));
            }

            ensureEndpoints();
            foreach (var boundedContext in BoundedContexts)
            {
                foreach (var eventsSubscription in boundedContext.EventsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(boundedContext.Name,eventsSubscription.Key);
                    BoundedContext context = boundedContext;
                    m_Subscription.Add(m_MessagingEngine.Subscribe(
                        endpoint,
                        (@event, acknowledge) => context.EventDispatcher.Dispacth(@event, acknowledge),
                        (type, acknowledge) =>
                         {
                             throw new InvalidOperationException("Unknown event received: " + type);
                             //acknowledge(0, true);
                         },
                        eventsSubscription.Value.ToArray()));

                }
            }

            foreach (var boundedContext in BoundedContexts)
            {
                foreach (var commandsSubscription in boundedContext.CommandsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(boundedContext.Name, commandsSubscription.Endpoint);
                    BoundedContext context = boundedContext;
                    CommandSubscription commandSubscription = commandsSubscription;
                    m_Subscription.Add(m_MessagingEngine.Subscribe(
                        endpoint,
                        (command, acknowledge) =>context.CommandDispatcher.Dispatch(command, commandSubscription.Types[command.GetType()], acknowledge, endpoint),
                    (type, acknowledge) =>
                                 {
                                     throw new InvalidOperationException("Unknown command received: " + type); 
                                     //acknowledge(0, true);
                                 }, 
                        commandSubscription.Types.Keys.ToArray()));
                }
            }

        }

        private void ensureEndpoints()
        {
            var allEndpointsAreValid = true;
            var errorMessage=new StringBuilder("Some endpoints are not valid:").AppendLine();

            foreach (var boundedContext in BoundedContexts)
            {
                allEndpointsAreValid = 
                    allEndpointsAreValid &&
                    verifyEndpoints(boundedContext, boundedContext.EventRoutes.Values, boundedContext.EventsSubscriptions.Keys, errorMessage) &&
                    verifyEndpoints(boundedContext, boundedContext.CommandRoutes.Values, boundedContext.CommandsSubscriptions.Select(subscription => subscription.Endpoint), errorMessage);
            }
            if (!allEndpointsAreValid)
                throw new ConfigurationErrorsException(errorMessage.ToString());
        }

        private bool verifyEndpoints(BoundedContext boundedContext, IEnumerable<string> publishEndpoints, IEnumerable<string> subscribeEndpoints, StringBuilder errorMessage)
        {
            var endpoints = publishEndpoints.Union(subscribeEndpoints);
            return endpoints.Aggregate(true, (isValid, endpointName) =>
            {
                var endpoint = m_EndpointResolver.Resolve(boundedContext.Name, endpointName);
                string error;
                bool result = isValid;

                if (publishEndpoints.Contains(endpointName) &&
                    !m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Publish, m_CreateMissingEndpoints, out error))
                {
                    errorMessage.AppendFormat("Bounded context '{0}' endpoint '{1}'({2}) is not properly configured for publishing: {3}.",boundedContext.Name, endpointName, endpoint, error).AppendLine();
                    result = false;
                }

                if (subscribeEndpoints.Contains(endpointName) &&
                    !m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Subscribe, m_CreateMissingEndpoints, out error))
                {
                    errorMessage.AppendFormat("Bounded context '{0}' endpoint '{1}'({2}) is not properly configured for subscription: {3}.", boundedContext.Name, endpointName, endpoint, error).AppendLine();
                    result = false;
                }
                return result;
            });
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var boundedContext in BoundedContexts.Where(b => b != null))
                {

                    if (boundedContext.Processes != null)
                    {
                        foreach (var process in boundedContext.Processes)
                        {
                            process.Dispose();
                        }
                    }

                    boundedContext.Dispose();

                }

                if (m_Subscription != null)
                    m_Subscription.Dispose();
            }
        }

        public void SendCommand<T>(T command,string boundedContext,CommandPriority priority=CommandPriority.Normal )
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");
            string endpoint;
            if (!context.CommandRoutes.TryGetValue(Tuple.Create(typeof (T),priority), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}",boundedContext,typeof(T),priority));
            }
            m_MessagingEngine.Send(command, m_EndpointResolver.Resolve(boundedContext, endpoint));
        }

        public void ReplayEvents(string boundedContext, params Type[] types)
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
                if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");
            string endpoint;
            if (!context.CommandRoutes.TryGetValue(Tuple.Create(typeof (ReplayEventsCommand),CommandPriority.Normal), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", boundedContext, typeof(ReplayEventsCommand), CommandPriority.Normal));
            }

            var ep = m_EndpointResolver.Resolve(boundedContext, endpoint);

            Destination tmpDestination;
            if (context.GetTempDestination(ep.TransportId, () => m_MessagingEngine.CreateTemporaryDestination(ep.TransportId,"EventReplay"), out tmpDestination))
            {
                var replayEndpoint = new Endpoint { Destination = tmpDestination, SerializationFormat = ep.SerializationFormat, SharedDestination = true, TransportId = ep.TransportId };
                var knownEventTypes = context.EventsSubscriptions.SelectMany(e => e.Value).ToArray();
                m_Subscription.Add(m_MessagingEngine.Subscribe(
                    replayEndpoint,
                    (@event, acknowledge) => context.EventDispatcher.Dispacth(@event, acknowledge),
                    (typeName, acknowledge) => { }, 
                    "EventReplay",
                    0,
                    knownEventTypes));
            }
            SendCommand(new ReplayEventsCommand { Destination = tmpDestination.Publish, From = DateTime.MinValue, SerializationFormat = ep.SerializationFormat, Types = types }, boundedContext);
        }

        internal void PublishEvent(object @event,string boundedContext,string endpoint)
        {
            PublishEvent(@event, m_EndpointResolver.Resolve(boundedContext,endpoint));
        }

        internal void PublishEvent(object @event,Endpoint endpoint)
        {
            m_MessagingEngine.Send(@event, endpoint);
        }

        internal IDependencyResolver DependencyResolver {
            get { return m_DependencyResolver; }
        }
    }

    internal interface ICqrsEngine : ICommandSender
    {

    }
}