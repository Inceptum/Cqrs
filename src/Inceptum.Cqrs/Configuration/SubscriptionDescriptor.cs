using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Cqrs.Configuration
{
    class SubscriptionDescriptor : IBoundedContextDescriptor
    {
        private List<MessageRoute> m_MessageRoutes;

        public SubscriptionDescriptor(List<MessageRoute> messageRoutes)
        {
            m_MessageRoutes = messageRoutes;
        }
        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }


        public void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            boundedContext.MessageRoutes = m_MessageRoutes;
        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {

        }
    }
}