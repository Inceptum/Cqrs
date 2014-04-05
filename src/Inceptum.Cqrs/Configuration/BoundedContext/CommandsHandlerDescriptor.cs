using System;
using CommonDomain.Persistence;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    class CommandsHandlerDescriptor : DescriptorWithDependencies<Context>
    {
        public CommandsHandlerDescriptor(params object[] handlers):base(handlers)
        {
        }

        public CommandsHandlerDescriptor(params Type[] handlers):base(handlers)
        {

        }
        
        public override void Process(Context context, CqrsEngine cqrsEngine)
        {
            foreach (var handler in ResolvedDependencies)
            {
                var repository = context.EventStore==null?null:context.EventStore.Repository;
                context.CommandDispatcher.Wire(handler,
                                                      new OptionalParameter<IEventPublisher>(context.EventsPublisher),
                                                      new FactoryParameter<IRepository>(repository) 
                    );
            }
        }

    } 
    
    
    
}