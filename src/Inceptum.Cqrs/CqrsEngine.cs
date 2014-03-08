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
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
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


    public class DefaultEndpointProvider : IEndpointProvider
    {
        public bool Contains(string endpointName)
        {
            return false;
        }

        public Endpoint Get(string endpointName)
        {
            throw new NotImplementedException();
        }
    }

    public class CqrsEngine :ICqrsEngine
    {
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly CompositeDisposable m_Subscription=new CompositeDisposable();
        internal  IEndpointResolver EndpointResolver { get; private set; }
     
        private readonly IEndpointProvider m_EndpointProvider;
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


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, IEndpointProvider endpointProvider, params IRegistration[] registrations)
            : this(new DefaultDependencyResolver(), messagingEngine,endpointResolver, endpointProvider, registrations)
        {
        }


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver,params IRegistration[] registrations)
            : this(new DefaultDependencyResolver(), messagingEngine, endpointResolver, new DefaultEndpointProvider(), registrations)
        {
        }

        public CqrsEngine(IDependencyResolver dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, IEndpointProvider endpointProvider, params IRegistration[] registrations)
            : this(dependencyResolver, messagingEngine, endpointResolver, endpointProvider, false, registrations)
        {
        }

        public CqrsEngine(IDependencyResolver dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, IEndpointProvider endpointProvider, bool createMissingEndpoints, params IRegistration[] registrations)
        {
            m_Logger.Debug("CqrsEngine instanciating. createMissingEndpoints: "+ createMissingEndpoints);
            m_CreateMissingEndpoints = createMissingEndpoints;
            m_DependencyResolver = dependencyResolver;
            m_Registrations = registrations;
            EndpointResolver =endpointResolver?? new DefaultEndpointResolver();
            m_MessagingEngine = messagingEngine;
            m_EndpointProvider = endpointProvider;
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
                foreach (var route in boundedContext.Routes)
                {
                            
                    BoundedContext context = boundedContext;
                    var subscriptions = route.MessageRoutes
                        .Where(r => r.Key.CommunicationType == CommunicationType.Subscribe)
                        .Select(r => new
                        {
                            type = r.Key.MessageType,
                            priority = r.Key.Priority,
                            endpoint =
                                new Endpoint(r.Value.TransportId, "", r.Value.Destination.Subscribe, r.Value.SharedDestination, r.Value.SerializationFormat)
                        })
                        .GroupBy(x => Tuple.Create(x.endpoint, x.priority))
                        .Select(g => new
                        {
                            endpoint = g.Key.Item1,
                            priority = g.Key.Item2,
                            types = g.Select(x => x.type).ToArray()
                        });


                    foreach (var subscription in subscriptions)
                    {
                        var routeName = route.Name;
                        var endpoint = subscription.endpoint;
                        CallbackDelegate<object> callback=null;
                        string messageTypeName=null;
                        switch (route.Type)
                        {
                            case RouteType.Events:
                                callback = (@event, acknowledge) => context.EventDispatcher.Dispacth(@event, acknowledge);
                                messageTypeName = "event";
                                break;
                            case RouteType.Commands:
                                callback =
                                    (command, acknowledge) =>
                                        context.CommandDispatcher.Dispatch(command, CommandPriority.Normal, acknowledge, endpoint, routeName);
                                messageTypeName = "command";
                                break;
                        }
                        
                        m_Subscription.Add(m_MessagingEngine.Subscribe(
                            endpoint,
                            callback,
                            (type, acknowledge) =>
                            {
                                throw new InvalidOperationException(string.Format("Unknown {0} received: {1}",messageTypeName, type));
                                //acknowledge(0, true);
                            },
                            routeName,
                            (int)subscription.priority,
                            subscription.types));
                    }
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
                boundedContext.ResolveRoutes(m_EndpointProvider);
                foreach (var route in boundedContext.Routes)
                {
                    foreach (var messageRoute in route.MessageRoutes)
                    {
                        string error;
                        var routingKey = messageRoute.Key;
                        var endpoint = messageRoute.Value;
                        var messageTypeName = route.Type.ToString().ToLower().TrimEnd('s');
                        bool result = true;

                        if (routingKey.CommunicationType == CommunicationType.Publish && !m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Publish, m_CreateMissingEndpoints, out error))
                        {
                            errorMessage.AppendFormat("Route '{1}' within bounded context '{0}' for {2} type '{3}' has resolved endpoint {4} that is not properly configured for publishing: {5}.", boundedContext.Name, route.Name, messageTypeName, routingKey.MessageType.Name, endpoint, error).AppendLine();
                            log.AppendFormat("Route '{1}' {2} type '{3}' resolved endpoint {4}: endpoint is not properly configured for publishing: {5}.", boundedContext.Name, route.Name, messageTypeName, routingKey.MessageType.Name, endpoint, error).AppendLine();
                            result = false;
                        }

                        if (routingKey.CommunicationType == CommunicationType.Subscribe && !m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Subscribe, m_CreateMissingEndpoints, out error))
                        {
                            errorMessage.AppendFormat("Route '{1}' within bounded context '{0}' for {2} type '{3}' has resolved endpoint {4} that is not properly configured for subscription: {5}.", boundedContext.Name, route.Name, messageTypeName, routingKey.MessageType.Name, endpoint, error).AppendLine();
                            log.AppendFormat("Route '{1}'  {2} type '{3}' resolved endpoint {4}: endpoint is not properly configured for subscription: {5}.", boundedContext.Name, route.Name, messageTypeName, routingKey.MessageType.Name, endpoint, error).AppendLine();
                            result = false;
                        }

                        if (result)
                            log.AppendFormat("Route '{1}' {2} type '{3}' resolved endpoint {4}: OK", boundedContext.Name, route.Name, messageTypeName, routingKey.MessageType.Name, endpoint).AppendLine();

                        allEndpointsAreValid = allEndpointsAreValid && result;
                    }
                    
                }
            }

            if (!allEndpointsAreValid)
                throw new ConfigurationErrorsException(errorMessage.ToString());

            m_Logger.Debug(log);
        }
/*

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
                        var e = EndpointResolver.Resolve(localBoundedContextName ,boundedContextName, route, type,CommunicationType.Events);
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
                            route,
                            0,
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
                        var e = EndpointResolver.Resolve(localBoundedContextName ,boundedContextName, route, type,CommunicationType.Commands);
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
                                context.CommandDispatcher.Dispatch(command, g.priority, acknowledge, g.endpoint, route),
                            (type, acknowledge) =>
                            {
                                throw new InvalidOperationException("Unknown command received: " + type);
                                //acknowledge(0, true);
                            },
                            route,
                            0,
                            group.Select(gr => gr.Key).ToArray()
                            ));
                    }
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
                IEnumerable<Tuple<string, Type, CommunicationType>> eventSubscribeRoutes = boundedContext.EventsSubscriptions.SelectMany(s => s.Value.Select(v => Tuple.Create(s.Key, v, CommunicationType.Events))).ToArray();
                IEnumerable<Tuple<string, Type, CommunicationType>> eventPublishRoutes = boundedContext.EventRoutes.Select(p => Tuple.Create(p.Value, p.Key, CommunicationType.Events));
                IEnumerable<Tuple<string, Type, CommunicationType>> commandSubscribeRoutes = boundedContext.CommandsSubscriptions.SelectMany(s => s.Types.Select(v => Tuple.Create(s.Endpoint, v.Key, CommunicationType.Commands))).ToArray();
                IEnumerable<Tuple<string, Type, CommunicationType>> commandPublishRoutes = boundedContext.CommandRoutes.Select(p => Tuple.Create(p.Value, p.Key.Item1, CommunicationType.Commands));
                
                eventPublishRoutes = eventPublishRoutes.Union(eventSubscribeRoutes).ToArray();
                commandPublishRoutes = commandPublishRoutes.Union(commandSubscribeRoutes).ToArray();
                


                allEndpointsAreValid = 
                    allEndpointsAreValid &&
                    verifyEndpoints(boundedContext, eventPublishRoutes, eventSubscribeRoutes, errorMessage,log) &&
                    verifyEndpoints(boundedContext, commandPublishRoutes, commandSubscribeRoutes, errorMessage, log);
            }
            if (!allEndpointsAreValid)
                throw new ConfigurationErrorsException(errorMessage.ToString());

            m_Logger.Debug(log);

        }

        private bool verifyEndpoints(BoundedContext boundedContext, IEnumerable<Tuple<string, Type, CommunicationType>> publishRoutes, IEnumerable<Tuple<string, Type, CommunicationType>> subscribeRoutes, StringBuilder errorMessage, StringBuilder log)
        {
            var publishEndpoints = publishRoutes.Select(t => new { route = t.Item1, type = t.Item2, routeType = t.Item3 }).Distinct().ToArray();
            var subscribeEndpoints = subscribeRoutes.Select(t => new { route = t.Item1, type = t.Item2, routeType = t.Item3 }).Distinct().ToArray();
            var routeBindings = publishEndpoints.Union(subscribeEndpoints);
           

            var res= routeBindings.Aggregate(true, (isValid, routeBinding) =>
            {
                var messageType = routeBinding.routeType.ToString().ToLower().TrimEnd('s');
                var endpoint = EndpointResolver.Resolve(boundedContext.LocalBoundedContext, boundedContext.Name, routeBinding.route, routeBinding.type, routeBinding.routeType);
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
                    log.AppendFormat("Route '{1}'  {2} type '{3}' resolved endpoint {4}: endpoint is not properly configured for subscription: {5}.", boundedContext.Name, routeBinding.route, messageType, routeBinding.type.Name, endpoint, error).AppendLine();
                    result = false;
                }

                if(result)
                    log.AppendFormat("Route '{1}' {2} type '{3}' resolved endpoint {4}: OK", boundedContext.Name, routeBinding.route,messageType, routeBinding.type.Name, endpoint).AppendLine(); 
                
                return result && isValid;
            });

            return res;
        }
*/

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

        public void SendCommand<T>(T command,string boundedContext,uint  priority=0)
        {
            if(!sendMessage(typeof(T),command,RouteType.Commands,boundedContext,priority))
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", boundedContext, typeof(T), priority));
        }

        private bool sendMessage(Type type,object message,RouteType routeType,string boundedContext, uint priority)
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found", boundedContext), "boundedContext");
            var publishDirections = (from route in context.Routes
                                     from messageRoute in route.MessageRoutes
                                     where messageRoute.Key.MessageType == type && messageRoute.Key.RouteType == routeType && messageRoute.Key.Priority == priority
                                     select new
                                     {
                                         routeName = route.Name,
                                         endpoint = messageRoute.Value

                                     }).ToArray();
            if (!publishDirections.Any())
                return false;
            foreach (var direction in publishDirections)
            {
                m_MessagingEngine.Send(message, direction.endpoint, direction.routeName);
            }
            return true;
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
            var publishDirections = (from route in context.Routes
                                     from messageRoute in route.MessageRoutes
                                     where messageRoute.Key.MessageType == typeof(ReplayEventsCommand) && messageRoute.Key.RouteType == RouteType.Commands && messageRoute.Key.Priority == 0
                                     select new
                                     {
                                         routeName = route.Name,
                                         endpoint = messageRoute.Value

                                     }).ToArray();
            if(!publishDirections.Any())
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", boundedContext, typeof(ReplayEventsCommand), 0));

            var direction = publishDirections.First();
            var ep = direction.endpoint;
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
            SendCommand(new ReplayEventsCommand { Destination = tmpDestination.Publish, From = DateTime.MinValue, SerializationFormat = ep.SerializationFormat, Types = types }, boundedContext,0);
        }


        internal void PublishEvent(object @event, string boundedContext)
        {
            if (@event == null) throw new ArgumentNullException("event");
            if (!sendMessage(@event.GetType(), @event, RouteType.Events, boundedContext, 0))
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support event '{1}'", boundedContext, @event.GetType()));
        }

        internal void PublishEvent(object @event, Endpoint endpoint, string processingGroup)
        {
            m_MessagingEngine.Send(@event, endpoint, processingGroup);
        }

        internal IDependencyResolver DependencyResolver {
            get { return m_DependencyResolver; }
        }

    }

    internal interface ICqrsEngine : ICommandSender
    {

    }
}