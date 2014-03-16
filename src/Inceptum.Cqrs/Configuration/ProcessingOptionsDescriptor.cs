using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class ProcessingOptionsDescriptor<TRegistrtaion> : RegistrationWrapper<TRegistrtaion>, IBoundedContextDescriptor where TRegistrtaion : IRegistration
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

        public void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            boundedContext.Routes[m_Route].ProcessingGroup.ConcurrencyLevel = m_ThreadCount;
            if(m_QueueCapacity!=null)
                boundedContext.Routes[m_Route].ProcessingGroup.QueueCapacity = m_QueueCapacity.Value;
        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
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