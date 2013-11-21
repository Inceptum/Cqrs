using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Cqrs.Configuration
{
    public static class Saga
    {
        public static SagaListeningDescriptor Instance(object saga)
        {
            return new SagaListeningDescriptor(saga);
        }
        public static SagaListeningDescriptor OfType(Type saga)
        {
            return new SagaListeningDescriptor(saga);
        }
    }
   public static class Saga<T>
    {
        public static SagaRegistration Listening(params string[] boundContexts)
        {
            return new SagaRegistration(engine => engine.DependencyResolver.GetService(typeof(T)), boundContexts,typeof(T));
        }
    }

    public class SagaRegistration : IRegistration
    {
        private object m_Saga;
        private readonly Func<CqrsEngine, object> m_SagaResolver;
        private readonly string[] m_BoundContexts;

        public SagaRegistration(Func<CqrsEngine, object> sagaResolver, string[] boundContexts,params Type[] dependencies)
        {
            m_BoundContexts = boundContexts;
            m_SagaResolver = sagaResolver;
            Dependencies = dependencies;
        }

        public void Create(CqrsEngine cqrsEngine)
        {
            m_Saga = m_SagaResolver(cqrsEngine);
        }

        public void Process(CqrsEngine cqrsEngine)
        {
            foreach (var boundContext in m_BoundContexts)
            {
                var context = cqrsEngine.BoundedContexts.FirstOrDefault(bc => bc.Name == boundContext);
                context.EventDispatcher.Wire(m_Saga,new OptionalParameter<ICommandSender>(cqrsEngine));
            }
        }

        public IEnumerable<Type> Dependencies { get; private set; }
    }

    public class SagaListeningDescriptor
    {
        private readonly Func<CqrsEngine,object> m_SagaResolver;
        private readonly Type[] m_SagaType=new Type[0];

        public SagaListeningDescriptor(object saga)
        {
            m_SagaResolver = engine => saga;
        }

        public SagaListeningDescriptor(Type saga)
        {
            m_SagaType = new [] {saga};
            m_SagaResolver = engine => engine.DependencyResolver.GetService(saga);
        }

        public SagaRegistration Listening(params string[] boundContexts)
        {
            return new SagaRegistration(m_SagaResolver, boundContexts, m_SagaType);
        }
    }

}