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
            var boundedContextsWithWrongLocal = m_BoundedContexts.Where(bc => m_BoundedContexts.All(b => b.Name != bc.LocalBoundedContext || !b.IsLocal)).ToArray();
            if (boundedContextsWithWrongLocal.Any())
                throw new ConfigurationErrorsException("Following bounded contexts are mapped to not existing local bounded context: " + string.Join(", ", boundedContextsWithWrongLocal.Select(s => string.Format("'{0}'", s.Name))));

            ensureEndpoints();
            foreach (var boundedContext in BoundedContexts)
            {
                foreach (var eventsSubscription in boundedContext.EventsSubscriptions)
                {
                    var route = eventsSubscription.Key;
                    var boundedContextName = boundedContext.Name;
                    var localBoundedContextName = boundedContext.LocalBoundedContext;
                    var groupedBySubscribeEndpoint=eventsSubscription.Value.GroupBy(type =>
                    {
                        var e = m_EndpointResolver.Resolve(localBoundedContextName ,boundedContextName, route, type,RouteType.Events);
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
                    var localBoundedContextName = boundedContext.LocalBoundedContext;
                    var groupedBySubscribeEndpoint = commandsSubscription.Types.GroupBy(t =>
                    {
                        var type = t.Key;
                        var commandPriority = t.Value;
                        var e = m_EndpointResolver.Resolve(localBoundedContextName ,boundedContextName, route, type,RouteType.Commands);
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
            var log = new StringBuilder();
            log.Append("Endpoints verification").AppendLine();


            foreach (var boundedContext in BoundedContexts)
            {
                log.AppendFormat("Bounded context '{0}':",boundedContext.Name).AppendLine();
                IEnumerable<Tuple<string, Type, RouteType>> eventSubscribeRoutes = boundedContext.EventsSubscriptions.SelectMany(s => s.Value.Select(v => Tuple.Create(s.Key, v, RouteType.Events))).ToArray();
                IEnumerable<Tuple<string, Type, RouteType>> eventPublishRoutes = boundedContext.EventRoutes.Select(p => Tuple.Create(p.Value, p.Key, RouteType.Events));
                IEnumerable<Tuple<string, Type, RouteType>> commandSubscribeRoutes = boundedContext.CommandsSubscriptions.SelectMany(s => s.Types.Select(v => Tuple.Create(s.Endpoint, v.Key, RouteType.Commands))).ToArray();
                IEnumerable<Tuple<string, Type, RouteType>> commandPublishRoutes = boundedContext.CommandRoutes.Select(p => Tuple.Create(p.Value, p.Key.Item1, RouteType.Commands));
                if (boundedContext.IsLocal)
                {
                    eventPublishRoutes = eventPublishRoutes.Union(eventSubscribeRoutes).ToArray();
                    commandPublishRoutes = commandPublishRoutes.Union(commandSubscribeRoutes).ToArray();
                }


                allEndpointsAreValid = 
                    allEndpointsAreValid &&
                    verifyEndpoints(boundedContext, eventPublishRoutes, eventSubscribeRoutes, errorMessage,log) &&
                    verifyEndpoints(boundedContext, commandPublishRoutes, commandSubscribeRoutes, errorMessage, log);
            }
            if (!allEndpointsAreValid)
                throw new ConfigurationErrorsException(errorMessage.ToString());

            m_Logger.Debug(log);

        }

        private bool verifyEndpoints(BoundedContext boundedContext, IEnumerable<Tuple<string, Type, RouteType>> publishRoutes, IEnumerable<Tuple<string, Type, RouteType>> subscribeRoutes, StringBuilder errorMessage, StringBuilder log)
        {
            var publishEndpoints = publishRoutes.Select(t => new { route = t.Item1, type = t.Item2, routeType = t.Item3 }).Distinct().ToArray();
            var subscribeEndpoints = subscribeRoutes.Select(t => new { route = t.Item1, type = t.Item2, routeType = t.Item3 }).Distinct().ToArray();
            var routeBindings = publishEndpoints.Union(subscribeEndpoints);
           

            var res= routeBindings.Aggregate(true, (isValid, routeBinding) =>
            {
                var messageType = routeBinding.routeType.ToString().ToLower().TrimEnd('s');
                var endpoint = m_EndpointResolver.Resolve(boundedContext.LocalBoundedContext, boundedContext.Name, routeBinding.route, routeBinding.type, routeBinding.routeType);
                string error;
                bool result = true;
                if (publishEndpoints.Contains(routeBinding) &&
                    !m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Publish, m_CreateMissingEndpoints,
                        out error))
                {
                    errorMessage.AppendFormat("Route '{1}' within bounded context '{0}' for {2} type '{3}' has resolved endpoint {4} that is not properly configured for publishing: {5}.", boundedContext.Name, routeBinding.route, messageType, routeBinding.type.Name, endpoint, error).AppendLine();
                    log.AppendFormat("Route '{1}' {2} type '{3}' resolved endpoint {4}: endpoint is not properly configured for publishing: {5}.", boundedContext.Name, routeBinding.route, messageType, routeBinding.type.Name, endpoint, error).AppendLine();
                    result = false;
                }

                if (subscribeEndpoints.Contains(routeBinding) &&
                    !m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Subscribe, m_CreateMissingEndpoints, out error))
                {
                    errorMessage.AppendFormat("Route '{1}' within bounded context '{0}' for {2} type '{3}' has resolved endpoint {4} that is not properly configured for subscription: {5}.", boundedContext.Name, routeBinding.route, messageType, routeBinding.type.Name, endpoint, error).AppendLine();
                    log.AppendFormat("Route '{1}'  {2} type '{3}' resolved endpoint {4}}: endpoint is not properly configured for subscription: {5}.", boundedContext.Name, routeBinding.route, messageType, routeBinding.type.Name, endpoint, error).AppendLine();
                    result = false;
                }

                if(result)
                    log.AppendFormat("Route '{1}' {2} type '{3}' resolved endpoint {4}: OK", boundedContext.Name, routeBinding.route,messageType, routeBinding.type.Name, endpoint).AppendLine(); 
                
                return result && isValid;
            });

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
            m_MessagingEngine.Send(command, m_EndpointResolver.Resolve(context.LocalBoundedContext,boundedContext, endpoint,typeof(T),RouteType.Commands));
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

            var ep = m_EndpointResolver.Resolve(context.LocalBoundedContext, boundedContext, endpoint, typeof(ReplayEventsCommand),RouteType.Commands);

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


            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found", boundedContext), "boundedContext");
            PublishEvent(@event, m_EndpointResolver.Resolve(context.LocalBoundedContext, boundedContext, endpoint, @event.GetType(),RouteType.Events));
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