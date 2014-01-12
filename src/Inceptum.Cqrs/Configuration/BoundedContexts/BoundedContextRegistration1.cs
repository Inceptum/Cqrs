using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Cqrs.Configuration
{
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

        public SendingCommandsDescriptor SendingCommands(params Type[] commandsTypes)
        {
            return AddDescriptor(new SendingCommandsDescriptor(this, commandsTypes));
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
}