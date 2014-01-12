using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Routing;

namespace Inceptum.Cqrs.Configuration
{
    public class SendingCommandsDescriptor : PublishingRouteDescriptor<SendingCommandsDescriptor>
    {
        private string m_BoundedContext;
        private readonly Type[] m_CommandsTypes;

        public SendingCommandsDescriptor(BoundedContextRegistration1 registration, Type[] commandsTypes):base(registration)
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

        public IPublishingRouteDescriptor<SendingCommandsDescriptor> To(string boundedContext)
        {
            m_BoundedContext = boundedContext;
            return this;
        }

       
    }
}