using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Cqrs.Configuration.Saga
{
    internal class SagaDescriptor : DescriptorWithDependencies<Context>
    {
        public SagaDescriptor(object saga)
            : base(saga)
        {

        }
        public SagaDescriptor(Type saga)
            : base(saga)
        {
           
        }

        public override void Process(Context context, CqrsEngine cqrsEngine)
        {
            //Wire saga to handle events from all subscribed bounded contexts
            IEnumerable<string> listenedBoundedContexts = context
                .SelectMany(r=>r.RoutingKeys)
                .Where(k=>k.CommunicationType==CommunicationType.Subscribe && k.RouteType==RouteType.Events)
                .Select(k=>k.RemoteBoundedContext)
                .Distinct()
                .ToArray();
            foreach (var saga in ResolvedDependencies)
            {
                foreach (var listenedBoundedContext in listenedBoundedContexts)
                {
                    context.EventDispatcher.Wire(listenedBoundedContext, saga, new OptionalParameter<ICommandSender>(context));
                }
            }

        }
    }
}