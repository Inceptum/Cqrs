using System;

namespace Inceptum.Cqrs.Configuration
{
    internal class LocalProcessDescriptor : DescriptorWithDependencies
    {
        public LocalProcessDescriptor(object process)
            : base(process)
        {
            if (!(process is IProcess))
            {
                throw new Exception("Process must implement IProcess interface");
            }
        }

        public LocalProcessDescriptor(Type process)
            : base(process)
        {
            if (!typeof(IProcess).IsAssignableFrom(process))
            {
                throw new Exception("Process must implement IProcess interface");
            }
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            foreach (var process in ResolvedDependencies)
            {
                boundedContext.Processes.Add(((IProcess)process));
            }
        }
    }
}