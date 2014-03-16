using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class ListeningRouteDescriptor<TDescriptor,TRegistration> : RouteDescriptorBase<TDescriptor,TRegistration>, IListeningRouteDescriptor<TDescriptor> where TDescriptor :  RouteDescriptorBase<TRegistration> where TRegistration : IRegistration
    {
        protected TDescriptor Descriptor { private get; set; }

        protected ListeningRouteDescriptor(TRegistration registration) : base(registration)
        {
        }

        protected internal string Route { get; private set; }

        TDescriptor IListeningRouteDescriptor<TDescriptor>.On(string route)
        {
            Route = route;
            return Descriptor;
        }

        public abstract IEnumerable<Type> GetDependencies();
        public abstract void Create(BoundedContext boundedContext, IDependencyResolver resolver);
        public abstract void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);

    }
}