using System;

namespace Inceptum.Cqrs.Configuration.Saga
{
    public class SagaRegistration : ContextRegistrationBase<ISagaRegistration>, ISagaRegistration
    {
        public SagaRegistration(string name, Type type) : base(name)
        {
            AddDescriptor(new SagaDescriptor(type));
        }

        protected override Context CreateContext(CqrsEngine cqrsEngine)
        {
            return new Context(cqrsEngine, Name, 0);
        }
    }
}