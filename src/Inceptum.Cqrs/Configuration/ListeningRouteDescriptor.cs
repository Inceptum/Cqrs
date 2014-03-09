using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class ListeningRouteDescriptor<T> : RouteDescriptorBase<T>, IListeningRouteDescriptor<T> where T :  RouteDescriptorBase
    {
        protected T Descriptor { private get; set; }

        protected ListeningRouteDescriptor(BoundedContextRegistration registration) : base(registration)
        {
        }

        protected internal string Route { get; private set; }

        T IListeningRouteDescriptor<T>.On(string route)
        {
            Route = route;
            return Descriptor;
        }

        public abstract IEnumerable<Type> GetDependencies();
        public abstract void Create(BoundedContext boundedContext, IDependencyResolver resolver);
        public abstract void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);

    }
}