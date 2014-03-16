using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class PublishingRouteDescriptor<TDescriptor, TRegistration> : RouteDescriptorBase<TDescriptor, TRegistration>, IPublishingRouteDescriptor<TDescriptor>
        where TDescriptor : RouteDescriptorBase <TRegistration>
        where TRegistration : IRegistration
    {
        protected TDescriptor Descriptor { private get; set; }
        protected internal  string Route { get; private set; }

        protected PublishingRouteDescriptor(TRegistration registration)
            : base(registration)
        {
        }

        TDescriptor IPublishingRouteDescriptor<TDescriptor>.With(string route)
        {
            Route = route;
            return Descriptor;
        }

        public abstract IEnumerable<Type> GetDependencies();
        public abstract void Create(BoundedContext boundedContext, IDependencyResolver resolver);
        public abstract void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);

    }
}