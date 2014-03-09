using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class ProcessingOptionsDescriptor : BoundedContextRegistrationWrapper, IBoundedContextDescriptor
    {
        private readonly string m_Route;
        private uint m_ThreadCount;

        public ProcessingOptionsDescriptor(BoundedContextRegistration registration, string route) : base(registration)
        {
            m_Route = route;
        }

        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            boundedContext.Routes[m_Route].ConcurrencyLevel = m_ThreadCount;
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
}