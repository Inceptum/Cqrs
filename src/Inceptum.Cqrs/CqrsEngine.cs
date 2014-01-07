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
        private readonly bool m_CreateMissingEndpoints = false;

        protected IMessagingEngine MessagingEngine
        {
            get { return m_MessagingEngine; }
        }


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, params IRegistration[] registrations)
            :this(new DefaultDependencyResolver(), messagingEngine, endpointResolver, registrations)
        {
        }

        public CqrsEngine(IDependencyResolver dependencyResolver, IMessagingEngine messagingEngine,IEndpointResolver endpointResolver, params IRegistration[] registrations)
            :this(dependencyResolver, messagingEngine,endpointResolver, false, registrations)
        {
        }

        public CqrsEngine(IDependencyResolver dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, bool createMissingEndpoints, params IRegistration[] registrations)
        {
            m_Logger.Debug("CqrsEngine instanciating. createMissingEndpoints: "+ createMissingEndpoints);
            m_CreateMissingEndpoints = createMissingEndpoints;
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
                    var route = eventsSubscription.Key;
                    var boundedContextName = boundedContext.Name;
                    var groupedBySubscribeEndpoint=eventsSubscription.Value.GroupBy(type =>
                    {
                        var e = m_EndpointResolver.Resolve(boundedContextName, route, type);
                        return new Endpoint(e.TransportId, "", e.Destination.Subscribe, e.SharedDestination,
                            e.SerializationFormat);
                    });

                    foreach (var group in groupedBySubscribeEndpoint)
                    {
                        BoundedContext context = boundedContext;
                        m_Subscription.Add(m_MessagingEngine.Subscribe(
                            group.Key,
                            (@event, acknowledge) => context.EventDispatcher.Dispacth(@event, acknowledge),
                            (type, acknowledge) =>
                            {
                                throw new InvalidOperationException("Unknown event received: " + type);
                                //acknowledge(0, true);
                            },
                            group.ToArray()));
                    }
                }
            }

            foreach (var boundedContext in BoundedContexts)
            {
                foreach (var commandsSubscription in boundedContext.CommandsSubscriptions)
                {

                    var route = commandsSubscription.Endpoint;
                    var boundedContextName = boundedContext.Name;
                    var groupedBySubscribeEndpoint = commandsSubscription.Types.GroupBy(t =>
                    {
                        var type = t.Key;
                        var commandPriority = t.Value;
                        var e = m_EndpointResolver.Resolve(boundedContextName, route, type);
                        return new
                        {
                            endpoint = new Endpoint(e.TransportId, "", e.Destination.Subscribe, e.SharedDestination,e.SerializationFormat),
                            priority=commandPriority
                        };
                    });
                    BoundedContext context = boundedContext;
                    //TODO: use messaging priority processing
                    foreach (var group in groupedBySubscribeEndpoint)
                    {
                        var g = @group.Key;
                        m_Subscription.Add(m_MessagingEngine.Subscribe(
                            g.endpoint,
                            (command, acknowledge) =>
                                context.CommandDispatcher.Dispatch(command, g.priority, acknowledge, g.endpoint),
                            (type, acknowledge) =>
                            {
                                throw new InvalidOperationException("Unknown command received: " + type);
                                //acknowledge(0, true);
                            },
                            group.Select(gr => gr.Key).ToArray()
                            ));
                    }


/*
                    var endpoint = m_EndpointResolver.Resolve(boundedContext.Name, commandsSubscription.Endpoint);
                    BoundedContext context = boundedContext;
                    CommandSubscription commandSubscription = commandsSubscription;
                    m_Subscription.Add(m_MessagingEngine.Subscribe(
                        endpoint,
                        (command, acknowledge) =>
                            context.CommandDispatcher.Dispatch(command, commandSubscription.Types[command.GetType()],
                                acknowledge, endpoint),
                        (type, acknowledge) =>
                        {
                            throw new InvalidOperationException("Unknown command received: " + type);
                            //acknowledge(0, true);
                        },
                        commandSubscription.Types.Keys.ToArray()));
*/
                }
            }

        }

        private void ensureEndpoints()
        {
            var allEndpointsAreValid = true;
            var errorMessage=new StringBuilder("Some endpoints are not valid:").AppendLine();

            foreach (var boundedContext in BoundedContexts)
            {
                IEnumerable<Tuple<string, Type>> eventSubscribeRoutes = boundedContext.EventsSubscriptions.SelectMany(s=>s.Value.Select(v=>Tuple.Create(s.Key,v))).ToArray();
                IEnumerable<Tuple<string, Type>> eventPublishRoutes = boundedContext.EventRoutes.Select(p=>Tuple.Create(p.Value,p.Key));
                IEnumerable<Tuple<string, Type>> commandSubscribeRoutes = boundedContext.CommandsSubscriptions.SelectMany(s => s.Types.Select(v => Tuple.Create(s.Endpoint, v.Key))).ToArray();
                IEnumerable<Tuple<string, Type>> commandPublishRoutes = boundedContext.CommandRoutes.Select(p => Tuple.Create(p.Value, p.Key.Item1));
                if (boundedContext.IsLocal)
                {
                    eventPublishRoutes = eventPublishRoutes.Union(eventSubscribeRoutes).ToArray();
                    commandPublishRoutes = commandPublishRoutes.Union(commandSubscribeRoutes).ToArray();
                }


                allEndpointsAreValid = 
                    allEndpointsAreValid &&
                    verifyEndpoints(boundedContext, eventPublishRoutes, eventSubscribeRoutes, errorMessage) &&
                    verifyEndpoints(boundedContext, commandPublishRoutes, commandSubscribeRoutes, errorMessage);
            }
            if (!allEndpointsAreValid)
                throw new ConfigurationErrorsException(errorMessage.ToString());
        }

        private bool verifyEndpoints(BoundedContext boundedContext, IEnumerable<Tuple<string, Type>> publishRoutes, IEnumerable<Tuple<string, Type>> subscribeRoutes, StringBuilder errorMessage)
        {
            var publishEndpoints = publishRoutes.Select(t => new { route = t.Item1, type = t.Item2 }).Distinct().ToArray();
            var subscribeEndpoints = subscribeRoutes.Select(t => new { route = t.Item1, type = t.Item2 }).Distinct().ToArray();
            var routeBindings = publishEndpoints.Union(subscribeEndpoints);
            var log = new StringBuilder();
            log.Append("Endpoints verification").AppendLine();

            var res= routeBindings.Aggregate(true, (isValid, routeBinding) =>
            {
                var endpoint = m_EndpointResolver.Resolve(boundedContext.Name, routeBinding.route,routeBinding.type);
                string error;
                bool result = true;
                if (publishEndpoints.Contains(routeBinding) &&
                    !m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Publish, m_CreateMissingEndpoints,
                        out error))
                {
                    errorMessage.AppendFormat("Bounded context '{0}' route '{1}' type '{2}' resolved endpoint {3} is not properly configured for publishing: {4}.",boundedContext.Name, routeBinding.route,routeBinding.type, endpoint, error).AppendLine();
                    log.AppendFormat("Bounded context '{0}' route '{1}' type '{2}' resolved endpoint {3} is not properly configured for publishing: {4}.", boundedContext.Name, routeBinding.route, routeBinding.type, endpoint, error).AppendLine();
                    result = false;
                }

                if (subscribeEndpoints.Contains(routeBinding) &&
                    !m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Subscribe, m_CreateMissingEndpoints, out error))
                {
                    errorMessage.AppendFormat("Bounded context '{0}' route '{1}' type '{2}' resolved endpoint {3} is not properly configured for subscription: {4}.", boundedContext.Name, routeBinding.route, routeBinding.type, endpoint, error).AppendLine();
                    log.AppendFormat("Bounded context '{0}' route '{1}' type '{2}' resolved endpoint {3} is not properly configured for subscription: {4}.", boundedContext.Name, routeBinding.route, routeBinding.type, endpoint).AppendLine();
                    result = false;
                }

                if(result)
                    log.AppendFormat("Bounded context '{0}' route '{1}' type '{2}' resolved endpoint {3} : OK", boundedContext.Name, routeBinding.route, routeBinding.type, endpoint).AppendLine(); 
                
                return result && isValid;
            });

            m_Logger.Debug(log);
            return res;
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
            m_MessagingEngine.Send(command, m_EndpointResolver.Resolve(boundedContext, endpoint,typeof(T)));
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

            var ep = m_EndpointResolver.Resolve(boundedContext, endpoint, typeof(ReplayEventsCommand));

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
            PublishEvent(@event, m_EndpointResolver.Resolve(boundedContext,endpoint,@event.GetType()));
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