using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration.Routing
{
    public class ProcessingOptionsDescriptor<TRegistrtaion> : RegistrationWrapper<TRegistrtaion>, IDescriptor<IRouteMap> where TRegistrtaion : IRegistration
    {
        private readonly string m_Route;
        private uint m_ThreadCount;
        private uint? m_QueueCapacity;

        public ProcessingOptionsDescriptor(TRegistrtaion registration, string route) : base(registration)
        {
            m_Route = route;
        }

        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public void Create(IRouteMap routeMap, IDependencyResolver resolver)
        {
            routeMap[m_Route].ProcessingGroup.ConcurrencyLevel = m_ThreadCount;
            if(m_QueueCapacity!=null)
                routeMap[m_Route].ProcessingGroup.QueueCapacity = m_QueueCapacity.Value;
        }

        public void Process(IRouteMap routeMap, CqrsEngine cqrsEngine)
        {
        }

        public ProcessingOptionsDescriptor<TRegistrtaion> MultiThreaded(uint threadCount)
        {
            if (threadCount == 0)
                throw new ArgumentOutOfRangeException("threadCount", "threadCount should be greater then 0");
            m_ThreadCount = threadCount;
            return this;
        }
        public ProcessingOptionsDescriptor<TRegistrtaion> QueueCapacity(uint queueCapacity)
        {
            m_QueueCapacity = queueCapacity;
            return this;
        }
    }
}