using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Inceptum.Cqrs.Configuration
{
    public interface IBoundedContextDescriptor : IHideObjectMembers
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        IEnumerable<Type> GetDependencies();

        [EditorBrowsable(EditorBrowsableState.Never)]
        void Create(BoundedContext boundedContext, IDependencyResolver resolver);

        [EditorBrowsable(EditorBrowsableState.Never)]
        void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);
    }
}