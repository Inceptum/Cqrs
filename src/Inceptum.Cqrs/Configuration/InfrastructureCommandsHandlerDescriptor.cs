using Inceptum.Cqrs.InfrastructureCommands;

namespace Inceptum.Cqrs.Configuration
{
    class InfrastructureCommandsHandlerDescriptor : DescriptorWithDependencies
    {
        public InfrastructureCommandsHandlerDescriptor()
            : base(typeof(ReplayEventsCommand))
        {

        }
        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            boundedContext.CommandDispatcher.Wire(new InfrastructureCommandsHandler(cqrsEngine, boundedContext));
        }
    }
}