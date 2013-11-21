using System;
using System.Collections.Generic;
using Inceptum.Cqrs.InfrastructureCommands;

namespace Inceptum.Cqrs.Configuration
{
    public class RoutingDescriptor : IBoundedContextDescriptor
    {
        private readonly Dictionary<Type, string> m_EventRoutes;
        private readonly Dictionary<Tuple<Type, CommandPriority>, string> m_CommandRoutes;

        public RoutingDescriptor(Dictionary<Type, string> eventRoutes, Dictionary<Tuple<Type, CommandPriority>, string> commandRoutes)
        {
            m_CommandRoutes = commandRoutes;
            m_EventRoutes = eventRoutes;
        }

        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            boundedContext.CommandRoutes = m_CommandRoutes;
            boundedContext.EventRoutes = m_EventRoutes;


        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
        }
    }
}