using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public interface IBoundedContextDescriptor
    {
        IEnumerable<Type> GetDependencies();
        void Create(BoundedContext boundedContext, IDependencyResolver resolver);
        void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);
    }
}