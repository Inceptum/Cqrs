using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;

namespace Inceptum.Cqrs.Configuration
{
    public interface IHideObjectMembers
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        string ToString();

        [EditorBrowsable(EditorBrowsableState.Never)]
        Type GetType();

        [EditorBrowsable(EditorBrowsableState.Never)]
        int GetHashCode();

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool Equals(object obj);
    }

    public static class LocalBoundedContextRegistrationExtensions
    {
        public static PublishingCommandsDescriptor WithLoopback(this ListeningCommandsDescriptor descriptor, string route)
        {
            return descriptor.PublishingCommands(descriptor.Types).To(descriptor.BoundedContextName).With(route);
        }

        public static ListeningEventsDescriptor WithLoopback(this PublishingEventsDescriptor descriptor, string route)
        {
            return descriptor.ListeningEvents(descriptor.Types).From(descriptor.BoundedContextName).On(route);
        }

    }


    public static class LocalBoundedContext
    {


        public static LocalBoundedContextRegistration Named(string name)
        {
            return new LocalBoundedContextRegistration(name);
        }

        public static IBoundedContextRegistration Named1(string name)
        {
            return new BoundedContextRegistration1(name);
        }
    }


    public interface IBoundedContextRegistration : IRegistration, IHideObjectMembers
    {
        PublishingCommandsDescriptor PublishingCommands(params Type[] commandsTypes);
        ListeningEventsDescriptor ListeningEvents(params Type[] type);
        IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningCommands(params Type[] type);
        IPublishingRouteDescriptor<PublishingEventsDescriptor> PublishingEvents(params Type[] type);
        ProcessingOptionsDescriptor ProcessingOptions(string route);
    }

    public class BoundedContextRegistration1 : IBoundedContextRegistration
    {
        public string BoundedContextName { get; private set; }
        private readonly List<IBoundedContextDescriptor> m_Descriptors = new List<IBoundedContextDescriptor>();
        private Type[] m_Dependencies;

        public BoundedContextRegistration1(string boundedContextName)
        {
            BoundedContextName = boundedContextName;
        }

        protected T AddDescriptor<T>(T descriptor) where T : IBoundedContextDescriptor
        {
            m_Dependencies = m_Dependencies.Concat(descriptor.GetDependencies()).Distinct().ToArray();
            m_Descriptors.Add(descriptor);
            return descriptor;
        }

        public PublishingCommandsDescriptor PublishingCommands(params Type[] commandsTypes)
        {
            return AddDescriptor(new PublishingCommandsDescriptor(this, commandsTypes));
        }

        public ListeningEventsDescriptor ListeningEvents(params Type[] types)
        {
            return AddDescriptor(new ListeningEventsDescriptor(this, types));
        }

        public IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningCommands(params Type[] types)
        {
            return AddDescriptor(new ListeningCommandsDescriptor(this, types));
        }

        public IPublishingRouteDescriptor<PublishingEventsDescriptor> PublishingEvents(params Type[] types)
        {
            return AddDescriptor(new PublishingEventsDescriptor(this, types));
        }

        public ProcessingOptionsDescriptor ProcessingOptions(string route)
        {
            return AddDescriptor(new ProcessingOptionsDescriptor(this, route));
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            var boundedContext = new BoundedContext(cqrsEngine, BoundedContextName);
            foreach (var descriptor in m_Descriptors)
            {
                descriptor.Create(boundedContext, cqrsEngine.DependencyResolver);
            }
            cqrsEngine.BoundedContexts.Add(boundedContext);

        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            var boundedContext = cqrsEngine.BoundedContexts.FirstOrDefault(bc => bc.Name == BoundedContextName);
            foreach (var descriptor in m_Descriptors)
            {
                descriptor.Process(boundedContext, cqrsEngine);
            }
        }

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return m_Dependencies; }
        }
    }

    public class ProcessingOptionsDescriptor : BoundedContextRegistrationWrapper, IBoundedContextDescriptor
    {
        private readonly string m_Route;
        private uint m_ThreadCount;

        public ProcessingOptionsDescriptor(BoundedContextRegistration1 registration, string route) : base(registration)
        {
            m_Route = route;
        }

        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            boundedContext.RouteMap[m_Route].ConcurrencyLevel = m_ThreadCount;
        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
        }

        public ProcessingOptionsDescriptor MultiThreaded(uint threadCount)
        {
            if (threadCount == 0)
                throw new ArgumentOutOfRangeException("threadCount", "threadCount should be greater then 0");
            m_ThreadCount = threadCount;
            return this;
        }
    }

    public class PublishingEventsDescriptor : PublishingRouteDescriptor<PublishingEventsDescriptor>
    {
        public Type[] Types { get; private set; }

        public PublishingEventsDescriptor(BoundedContextRegistration1 registration, Type[] types) : base(registration)
        {
            Types = types;
        }

        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            foreach (var eventType in Types)
            {
                boundedContext.RouteMap[Route].AddRoute(new RoutingKey
                {
                    LocalBoundedContext = boundedContext.Name,
                    RemoteBoundContext = null,
                    MessageType = eventType,
                    RouteType = RouteType.Events,
                    Priority = 0
                }, (IEndpointResolver) resolver.GetService(typeof (IEndpointResolver)));
            }
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {

        }
    }

    public class PublishingCommandsDescriptor : PublishingRouteDescriptor<PublishingCommandsDescriptor>
    {
        private string m_BoundedContext;
        private readonly Type[] m_CommandsTypes;

        public PublishingCommandsDescriptor(BoundedContextRegistration1 registration, Type[] commandsTypes):base(registration)
        {
            m_CommandsTypes = commandsTypes;
            Descriptor = this;
        }

        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            foreach (var type in m_CommandsTypes)
            {
                boundedContext.RouteMap[Route].AddRoute(new RoutingKey
                {
                    LocalBoundedContext = boundedContext.Name,
                    RemoteBoundContext = m_BoundedContext,
                    MessageType = type,
                    RouteType = RouteType.Commands,
                    Priority = 0
                },(IEndpointResolver) resolver.GetService(typeof(IEndpointResolver)));
            }
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
        }

        public IPublishingRouteDescriptor<PublishingCommandsDescriptor> To(string boundedContext)
        {
            m_BoundedContext = boundedContext;
            return this;
        }

       
    }

    public class ListeningCommandsDescriptor : ListeningRouteDescriptor<ListeningCommandsDescriptor>
    {
        public Type[] Types { get; private set; }

        public ListeningCommandsDescriptor(BoundedContextRegistration1 registration, Type[] types) : base(registration)
        {
            Types = types;
            Descriptor = this;
        }


        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            foreach (var type in Types)
            {
                for (uint priority = 0; priority <= LowestPriority; priority++)
                {
                    var routingKey = new RoutingKey
                    {
                        LocalBoundedContext = boundedContext.Name,
                        RemoteBoundContext = null,
                        MessageType = type,
                        RouteType = RouteType.Commands,
                        Priority = priority
                    };
                    var endpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors, cqrsEngine.EndpointResolver);
                    boundedContext.RouteMap[Route].AddRoute(routingKey, endpointResolver);
                }
            }
        }
 

    }

    public class ListeningEventsDescriptor : ListeningRouteDescriptor<ListeningEventsDescriptor>
    {
        private string m_BoundedContext;
        private readonly Type[] m_Types;

        public ListeningEventsDescriptor(BoundedContextRegistration1 registration, Type[] types) : base(registration)
        {
            m_Types = types;
            Descriptor = this;
        }

        public IListeningRouteDescriptor<ListeningEventsDescriptor> From(string boundedContext)
        {
            m_BoundedContext = boundedContext;
            return this;
        }

        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            foreach (var type in m_Types)
            {
                boundedContext.RouteMap[Route].AddRoute(new RoutingKey
                {
                    LocalBoundedContext = boundedContext.Name,
                    RemoteBoundContext = m_BoundedContext,
                    MessageType = type,
                    RouteType = RouteType.Events,
                    Priority = 0
                }, (IEndpointResolver) resolver.GetService(typeof (IEndpointResolver)));
            }
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
        }
    }


    public class ExplicitEndpointDescriptor<T> where T : RouteDescriptorBase
    {
        private readonly string m_Endpoint;
        private readonly T m_Descriptor;

        public ExplicitEndpointDescriptor(string endpoint, T descriptor)
        {
            m_Descriptor = descriptor;
            m_Endpoint = endpoint;
        }

        public T For(Func<RoutingKey, bool> criteria)
        {
            m_Descriptor.AddExplicitEndpoint(criteria, m_Endpoint);
            return m_Descriptor;
        }
    }


    public interface IListeningRouteDescriptor<out T> : IBoundedContextDescriptor
    {
        T On(string route);
    }

    public abstract class ListeningRouteDescriptor<T> : RouteDescriptorBase<T>, IListeningRouteDescriptor<T> where T :  RouteDescriptorBase
    {
        protected T Descriptor { private get; set; }

        protected ListeningRouteDescriptor(BoundedContextRegistration1 registration) : base(registration)
        {
        }

        protected string Route { get; private set; }

        T IListeningRouteDescriptor<T>.On(string route)
        {
            Route = route;
            return Descriptor;
        }

        public abstract IEnumerable<Type> GetDependencies();
        public abstract void Create(BoundedContext boundedContext, IDependencyResolver resolver);
        public abstract void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);

    }

    public interface IPublishingRouteDescriptor<out T> : IBoundedContextDescriptor 
    {
        T  With(string route);
    }

    public abstract class PublishingRouteDescriptor<T> : RouteDescriptorBase<T>, IPublishingRouteDescriptor<T> where T : RouteDescriptorBase
    {
        protected T Descriptor { private get; set; }
        protected string Route { get; private set; }

        protected PublishingRouteDescriptor(BoundedContextRegistration1 registration):base(registration)
        {
        }

        T IPublishingRouteDescriptor<T>.With(string route)
        {
            Route = route;
            return Descriptor;
        }

        public abstract IEnumerable<Type> GetDependencies();
        public abstract void Create(BoundedContext boundedContext, IDependencyResolver resolver);
        public abstract void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);

    }

    public abstract class RouteDescriptorBase<T> : RouteDescriptorBase where T :  RouteDescriptorBase
    {

        protected RouteDescriptorBase(BoundedContextRegistration1 registration) : base(registration)
        {
        }

        public T Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;
            return this as T;
        }


        public ExplicitEndpointDescriptor<T> WithEndpoint(string endpoint)
        {
            return new ExplicitEndpointDescriptor<T>(endpoint, this as T);
        }
    }


    public abstract class RouteDescriptorBase : BoundedContextRegistrationWrapper
    {
        private readonly Dictionary<Func<RoutingKey, bool>, string> m_ExplicitEndpointSelectors = new Dictionary<Func<RoutingKey, bool>, string>();

        protected Dictionary<Func<RoutingKey, bool>, string> ExplicitEndpointSelectors
        {
            get { return m_ExplicitEndpointSelectors; }
        }

        protected uint LowestPriority { get; set; }

        protected RouteDescriptorBase(BoundedContextRegistration1 registration) : base(registration)
        {
        }

        internal void AddExplicitEndpoint(Func<RoutingKey, bool> criteria, string endpoint)
        {
            ExplicitEndpointSelectors.Add(criteria, endpoint);
        }
    }

    public abstract class BoundedContextRegistrationWrapper : IBoundedContextRegistration
    {
        private readonly BoundedContextRegistration1 m_Registration;

        protected BoundedContextRegistrationWrapper(BoundedContextRegistration1 registration)
        {
            m_Registration = registration;
        }
        public string BoundedContextName
        {
            get { return m_Registration.BoundedContextName; }
        }

        public PublishingCommandsDescriptor PublishingCommands(params Type[] commandsTypes)
        {
            return m_Registration.PublishingCommands(commandsTypes);
        }

        public ListeningEventsDescriptor ListeningEvents(params Type[] types)
        {
            return m_Registration.ListeningEvents(types);
        }

        public IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningCommands(params Type[] types)
        {
            return m_Registration.ListeningCommands(types);
        }

        public IPublishingRouteDescriptor<PublishingEventsDescriptor> PublishingEvents(params Type[] types)
        {
            return m_Registration.PublishingEvents(types);
        }

        public ProcessingOptionsDescriptor ProcessingOptions(string route)
        {
            return m_Registration.ProcessingOptions(route);
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            (m_Registration as IRegistration).Create(cqrsEngine);
        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            (m_Registration as IRegistration).Process(cqrsEngine);
        }

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return (m_Registration as IRegistration).Dependencies; }
        }
    }
}