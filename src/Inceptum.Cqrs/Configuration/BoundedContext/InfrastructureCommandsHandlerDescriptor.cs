using Inceptum.Cqrs.InfrastructureCommands;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    class InfrastructureCommandsHandlerDescriptor : DescriptorWithDependencies<Context>
    {
        public InfrastructureCommandsHandlerDescriptor()
            : base(typeof(ReplayEventsCommand))
        {

        }
        public override void Process(Context context, CqrsEngine cqrsEngine)
        {
            context.CommandDispatcher.Wire(new InfrastructureCommandsHandler(cqrsEngine, context));
        }
    }
}