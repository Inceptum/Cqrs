using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;
using CommonDomain.Persistence;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.Configuration.BoundedContext;
using IRegistration = Inceptum.Cqrs.Configuration.IRegistration;

namespace Inceptum.Cqrs.Castle
{
    public interface ICqrsEngineBootstrapper
    {
        void Start();
    }

    internal class CastleDependencyResolver : IDependencyResolver
    {
        private readonly IKernel m_Kernel;

        public CastleDependencyResolver(IKernel kernel)
        {
            if (kernel == null) throw new ArgumentNullException("kernel");
            m_Kernel = kernel;
        }

        public object GetService(Type type)
        {
            return m_Kernel.Resolve(type);
        }

        public bool HasService(Type type)
        {
            return m_Kernel.HasComponent(type);
        }
    }

    public class CqrsFacility : AbstractFacility, ICqrsEngineBootstrapper, ISubDependencyResolver
    {
        private readonly string m_EngineComponetName = Guid.NewGuid().ToString();
        private readonly Dictionary<IHandler, Action<IHandler>> m_WaitList = new Dictionary<IHandler, Action<IHandler>>();
        private IRegistration[] m_BoundedContexts = new IRegistration[0];
        private bool m_InMemory=false;
        private static bool m_CreateMissingEndpoints = false;
        private ICqrsEngine m_CqrsEngine;
        public bool HasEventStore { get; set; }

        public CqrsFacility RunInMemory()
        {
            m_InMemory = true;
            return this;
        }

        public CqrsFacility Contexts(params IRegistration[] boundedContexts)
        {
            m_BoundedContexts = boundedContexts;
            return this;
        }

        protected override void Init()
        {
            Kernel.Register(Component.For<ICqrsEngineBootstrapper>().Instance(this));
            Kernel.ComponentRegistered += onComponentRegistered;
            Kernel.HandlersChanged += (ref bool changed) => processWaitList();
        }

        public CqrsFacility CreateMissingEndpoints(bool createMissingEndpoints = true)
        {
            m_CreateMissingEndpoints = createMissingEndpoints;
            return this;
        }


        private void processWaitList()
        {
            foreach (var pair in m_WaitList.ToArray().Where(pair => pair.Key.CurrentState == HandlerState.Valid && pair.Key.TryResolve(CreationContext.CreateEmpty()) != null))
            {
                pair.Value(pair.Key);
                m_WaitList.Remove(pair.Key);
            }
        }
      


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void onComponentRegistered(string key, IHandler handler)
        {
            if (handler.ComponentModel.Name == m_EngineComponetName)
            {
                var dependencyModels = m_BoundedContexts.SelectMany(bc => bc.Dependencies).Select(d => new DependencyModel(d.Name, d, false));
                foreach (var dependencyModel in dependencyModels)
                {
                    handler.ComponentModel.Dependencies.Add(dependencyModel);
                }
                return;
            }

            var isCommandsHandler = (bool)(handler.ComponentModel.ExtendedProperties["IsCommandsHandler"] ?? false);
            var isProjection = (bool)(handler.ComponentModel.ExtendedProperties["IsProjection"] ?? false);
            var isProcess = (bool)(handler.ComponentModel.ExtendedProperties["isProcess"] ?? false);
            var dependsOnBoundedContextRepository = handler.ComponentModel.ExtendedProperties["dependsOnBoundedContextRepository"];
            if (dependsOnBoundedContextRepository != null)
            {
                handler.ComponentModel.Dependencies.Add(new DependencyModel(m_EngineComponetName, typeof(ICqrsEngine), false));
                return;
            }

            if (isCommandsHandler && isProjection)
                throw new InvalidOperationException("Component can not be projection and commands handler simultaneousely");

            if (isProjection)
            {
                var projectedBoundContext = (string)(handler.ComponentModel.ExtendedProperties["ProjectedBoundContext"]);
                var boundContext = (string)(handler.ComponentModel.ExtendedProperties["BoundContext"]);

                var registration = findBoundedContext(boundContext);
                if (registration == null)
                    throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered", boundContext));
               
                //TODO: decide which service to use
                registration.WithProjection(handler.ComponentModel.Services.First(), projectedBoundContext);
 
            }


           if (isCommandsHandler)
           {
               var commandsHandlerFor = (string)(handler.ComponentModel.ExtendedProperties["CommandsHandlerFor"]);
               var registration = findBoundedContext(commandsHandlerFor);
               if (registration == null)
                   throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered",commandsHandlerFor)); 
              
               registration.WithCommandsHandler(handler.ComponentModel.Services.First());
 
           } 

            if (isProcess)
            {
                var processFor = (string)(handler.ComponentModel.ExtendedProperties["ProcessFor"]);
                var registration = findBoundedContext(processFor);
                
                if (registration == null)
                    throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered", processFor));
               
                registration.WithProcess(handler.ComponentModel.Services.First());
            }
        }

        private IBoundedContextRegistration findBoundedContext(string name)
        {

            return m_BoundedContexts.Where(r => r is IBoundedContextRegistration).Cast<IBoundedContextRegistration>()
                .Concat(m_BoundedContexts.Where(r => r is IRegistrationWrapper<IBoundedContextRegistration>).Select(r => (r as IRegistrationWrapper<IBoundedContextRegistration>).Registration))
                .FirstOrDefault(bc => bc.Name == name);
        }

        public void Start()
        {
            var engineReg = m_InMemory
                ? Component.For<ICqrsEngine>().ImplementedBy<InMemoryCqrsEngine>()
                : Component.For<ICqrsEngine>().ImplementedBy<CqrsEngine>().DependsOn(new { createMissingEndpoints = m_CreateMissingEndpoints });
            Kernel.Register(Component.For<IDependencyResolver>().ImplementedBy<CastleDependencyResolver>());
            Kernel.Resolver.AddSubResolver(this);
            Kernel.Register(engineReg.Named(m_EngineComponetName).DependsOn(new
                {
                    registrations = m_BoundedContexts.ToArray()
                }));
            Kernel.Register(
                  Component.For<ICommandSender>().ImplementedBy<CommandSender>().DependsOn(new { kernel = Kernel }));

            m_CqrsEngine = Kernel.Resolve<ICqrsEngine>();
        }
        public bool CanResolve(CreationContext context, ISubDependencyResolver contextHandlerResolver, ComponentModel model, DependencyModel dependency)
        {
            var dependsOnBoundedContextRepository = model.ExtendedProperties["dependsOnBoundedContextRepository"] as string;
            return dependency.TargetType == typeof(IRepository) && dependsOnBoundedContextRepository != null  && m_BoundedContexts.Select(c=>c as BoundedContextRegistration).Where(c=>c!=null).Any(c => c.Name == dependsOnBoundedContextRepository && c.HasEventStore);
        }

        public object Resolve(CreationContext context, ISubDependencyResolver contextHandlerResolver, ComponentModel model, DependencyModel dependency)
        {
            var dependsOnBoundedContextRepository = model.ExtendedProperties["dependsOnBoundedContextRepository"] as string;
            return m_CqrsEngine.GetRepository(dependsOnBoundedContextRepository);
        }
    }


    

    //TODO: should be injected by cqrs with preset local BC
    class CommandSender:ICommandSender
    {
        private ICqrsEngine m_Engine;
        private IKernel m_Kernel;
        private object m_SyncRoot=new object();

        public CommandSender(IKernel kernel)
        {
            m_Kernel = kernel;
        }

        private ICqrsEngine CqrsEngine
        {
           get
            {
                if (m_Engine == null)
                {
                    lock (m_SyncRoot)
                    {
                        if (m_Engine == null)
                        {
                            m_Engine = m_Kernel.Resolve<ICqrsEngine>();
                        }
                    }
                }
                return m_Engine;
            }
        }

        public void Dispose()
        {
        }

        
        
        public void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0)
        {
            CqrsEngine.SendCommand(command, boundedContext,remoteBoundedContext, priority);
        }

      

        public void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, params Type[] types)
        {
            CqrsEngine.ReplayEvents(boundedContext,remoteBoundedContext, @from, types);
        }

        public void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, Action<long> callback, params Type[] types)
        {
            CqrsEngine.ReplayEvents(boundedContext,remoteBoundedContext, @from,callback, types);
        }

        public void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, Action<long> callback,int batchSize, params Type[] types)
        {
            CqrsEngine.ReplayEvents(boundedContext,remoteBoundedContext, @from,callback,batchSize, types);
        }

        public void SendCommand<T>(T command, string remoteBoundedContext, uint priority = 0)
        {
            CqrsEngine.SendCommand(command,null,remoteBoundedContext,priority);
        }

        public void ReplayEvents(string remoteBoundedContext, DateTime @from, int batchSize, params Type[] types)
        {
            throw new NotImplementedException();
        }

        public void ReplayEvents(string remoteBoundedContext, DateTime @from, Action<long> callback, int batchSize, params Type[] types)
        {
            throw new NotImplementedException();
        }

        public void ReplayEvents(string remoteBoundedContext, DateTime @from, params Type[] types)
        {
            throw new NotImplementedException();
        }

        public void ReplayEvents(string remoteBoundedContext, DateTime @from, Action<long> callback, params Type[] types)
        {
            throw new NotImplementedException();
        }


    }

}