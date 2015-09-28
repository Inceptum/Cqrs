## Incptum.Cqrs ##

Incptum.Cqrs simplefies implementation of cqrs approuach in software developement. It takes care of 

* commands and events routing
* processing prioritization 
     

Incptum.Cqrs depends on the following packages

* Inceptum.Messaging - transport abstraction
* NEventStore - event sourcing
* CommonDomain - CQRS aware domain model


## Basic configuration ##

CqrsEngine relies on Inceptum.Messaging, so preconfigured instance of IMessagingEngine should be provided to it via constructor.

```cs

    var messagingEngine = 
                    new MessagingEngine(
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))

	var engine = new CqrsEngine(messagingEngine, Register.BoundedContext("bc"));
```

## Routing ##


#### Basic Routing ####


Route is a named message processing pipeline. It is used to resolve endpoint to be subscribed for particular message type or to be used to send message of given type. Route also defines processing group (consuming thread) with in which the message would be processed.


```cs

	Register.BoundedContext("bcA")
				.PublishingEvents(typeof (EventA),typeof (EventB)).With("routeA"));
				.ListeningCommands(typeof (CommandA),typeof (CommandB)).On("routeB"));
				.ListeningEvents(typeof(EventC)).From("bcB").On("routeA")
				.PublishingCommands(typeof(CommandC)).To("bcB").With("routeB")
```

the code above will register bounded context named 'bcA' that  

* publishs events of types  EventA and EventB with route 'routeA'
* listens commands of types CommandA and CommandB on route 'routeB'
* listens events of type EventC from bounded context named 'bcB' on route 'routeA'
* sends commands of type CommandC to bounded context 'bcB' with route 'routeB'


#### Loopback routes ####

to receive own events or send commands to itself bounded context should define loopback routes:

```cs

	Register.BoundedContext("bcA")
				.PublishingEvents(typeof (EventA),typeof (EventB)).With("routeA"))
					.WithLoopback("selfEventsRoute")
				.ListeningCommands(typeof (CommandA),typeof (CommandB)).On("routeB"))
					.WithLoopback("selfCommandsRoute");
```

If loopback route name is not provided (it is an optional parameter), the publishing route name would be used

```cs

	Register.BoundedContext("bcA")
				.PublishingEvents(typeof (EventA),typeof (EventB)).With("routeA"))
					.WithLoopback() //same as .WithLoopback("routeA") 
				.ListeningCommands(typeof (CommandA),typeof (CommandB)).On("routeB"))
					.WithLoopback();//same as .WithLoopback("routeB")

```

#### Default routing ####

TBD

#### Endpoint resolution ####

route name is used to resolve endpoint when sending or subscribing for message. By default endpoint with name matching the route name is looked up. This behaviour may be overriden by providing **IEndpointResolver** implementation.

globally:

```cs

    var engine = new CqrsEngine(messagingEngine, 
									Register.BoundedContext("bc"),
									Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("rmq", "json"));
```

for particular route	

```cs

	Register.BoundedContext("bc")
				.ListeningCommands(typeof(CommandA))
					.On("routeA")
					.WithEndpointResolver(new InMemoryEndpointResolver())

```

#### Multithreaded processing and prioritization ####

By default messages within single route are processed on a single thread. It may be configured to put incomming messages in a queue an dprocess it with a number of worker threads:

```cs

	Register.BoundedContext("bc")
		.ListeningCommands(typeof(CommandA)).On("routeA")
		.ListeningCommands(typeof(CommandB)).On("routeA")
		.ProcessingOptions("routeA").MultiThreaded(10).QueueCapacity(1024)

```


Prioritization may be defined only for multithreaded routes:

```cs

	Register.BoundedContext("bc")
		.ListeningCommands(typeof(CommandA)).On("prioritizedCommandsRoute")
			.Prioritized(lowestPriority: 2) 
		.ProcessingOptions("prioritizedCommandsRoute").MultiThreaded(10).QueueCapacity(1024)

```


Sender may define priority from 1 to lowestPriority (less value for higher priority) and worker threads would take messages with higher priority. It is recommended to combine prioritization with custom **IEndpointResolver**  implementation resolving different endpoints (queues) for different priority values or define endpoint for each priority explicitly:

```cs

	Register.BoundedContext("bc")
		.ListeningCommands(typeof(CommandA)).On("prioritizedCommandsRoute")
			.Prioritized(lowestPriority: 3) 
				.WithEndpoint("high").For(key=>key.Priority==1)
				.WithEndpoint("medium").For(key=>key.Priority==2)
				.WithEndpoint("low").For(key=>key.Priority==3)
		.ProcessingOptions("prioritizedCommandsRoute").MultiThreaded(10).QueueCapacity(1024)

```


## Event sourcing ##

CqrsEngine wraps [NEventStore](http://neventstore.org/ "NEventStore") as write model:

```

	Register.BoundedContext("local").WithNEventStore(dispatchCommits => Wireup.Init()
			.UsingInMemoryPersistence()
			.InitializeStorageEngine()
			.UsingJsonSerialization()
			.UsingSynchronousDispatchScheduler()
			.DispatchTo(dispatchCommits))
```

Apply to NEventSTore documentation for more details on NEventStore behaviour.

Bounded context with NEventStore configured injects [CommonDomain](https://github.com/NEventStore/CommonDomain "CommonDomain") IRepository to it's hosted components (see command handlers and processes sections below ).  
	
```cs

	namespace CommonDomain.Persistence
	{
	  public interface IRepository
	  {
	    TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IAggregate;
	
	    TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IAggregate;
	
	    void Save(IAggregate aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders);
	  }
	}

```

## Bounded context hosted components ##

### Command handlers ###

Command handler is a component responsible for processing of commands received by bounded context. 
Class implementing command handler should define method named 'Handle' for each command type it handles.
First parameter should be of type of the command it handles, recieved command would be passed as value of this parameter. 

Optionally there may be parameters of types *IEventPublisher* (event publisher of hosting bounded context) and *IRepository* (access to write model of hosting bounded context). Cqrs engine will inject implementations of these interfaces.

Return type may be void (exception thrown from handler would cause redelivery of the command within 60 seconds delay) or *CommandHandlingResult* (it defines whether the command should be redelivered and with what delay)

```cs

	class CommandHandler{
	    public void Handle(CommandA command, IEventPublisher eventPublisher, IRepository repository)
	    {
	    	Console.WriteLine("Command A recived: " + command);
	        eventPublisher.PublishEvent(new EventA());
	    }

	    public CommandHandlingResult Handle(CommandB command, IEventPublisher eventPublisher, IRepository repository)
	    {
	    	Console.WriteLine("Command B recived: " + command);
	        return new CommandHandlingResult(){Retry = true,RetryDelay = 100};
	    }
	}

```

registration:

   
```cs

	Register.BoundedContext("bc")
		.ListeningCommands(typeof(CommandA),typeof(CommandB)).On("routeA")
		.PublishingEvents(typeof(EventA),typeof(EventB)).With("routeB")
		.WithCommandsHandler(new CommandHandler()))

```

### Projections ###

Projection is a component responsible for processing of incoming events and building read model. 

Class implementing projection should define method named 'Handle' for each event type it handles.
First parameter should be of type of the event it handles, recieved event would be passed as value of this parameter. 

Optionally there may be parameter of type string named boundedContext. If it is defined, event origination bounded context name would be passed

Return type may be void (exception thrown from handler would cause redelivery of the event within 60 seconds delay) or *CommandHandlingResult* (it defines whether the event should be redelivered and with what delay)

Also it is possible to define handler able to process event batches. It should accept array of events and return *CommandHandlingResult[]* . It is usefull for replaying events. When ReplayEvent  is called with batchSize parameter it will collect events in batches and pass to handler (In some cases batch processing improves performance). Usual events (not replayed) will be passed to handler as array with single element. Batch handler should return array of *CommandHandlingResult*  one per each of accepted events, in the same order as events (first *CommandHandlingResult* is for the first event, second *CommandHandlingResult* is for the second event etc)

```cs

	class Projection
    {
        public void Handle(EventA  e,string boundedContext)
        {
            Console.WriteLine("Event A from boundedContext '{0}':{1}",boundedContext,  e);
        }
		
		public CommandHandlingResult Handle(EventB  e,string boundedContext)
        {
            Console.WriteLine("Event B from boundedContext '{0}':{1}",boundedContext,  e);
			return new CommandHandlingResult(){Retry = true,RetryDelay = 100};
        }

		public CommandHandlingResult[] Handle(EventC[]  events,string boundedContext)
        {
			foreach(e in events)
                Console.WriteLine("Event C from boundedContext '{0}':{1}",boundedContext,  e);
				
			return events.Select(e=>new CommandHandlingResult(){Retry = true,RetryDelay = 100}).ToArray();
        }

	}

```

registration:

```cs
   
	Register.BoundedContext("bc")
		.ListeningEvents(typeof(EventA)).From("bcA").On("routeA")
		.ListeningEvents(typeof(EventB)).From("bcB").On("routeB")
		.ListeningEvents(typeof(EventC)).From("bcC").On("routeC")
		.WithProjection(new Projection()))

```

### Processes ###

Process is a component responsible for background processes within bounded context. E.g. it may analize data and send events once some condition is met, or issue commands on schedule base.

Class implementing process should implement *IProcess* interfase. CqrsEngine would call Start method passing hosting bounded context event publisher and command seneder as parameters. On CqrsEngine dispose is disposes all peocesses. 

Sample process sending CommandA to hosting domain with 1000ms interval:

```cs

    public class TestProcess:IProcess
    {
        private readonly ManualResetEvent m_Disposed=new ManualResetEvent(false);
        readonly Thread m_WorkerThread;
        private ICommandSender m_CommandSender;

        public TestProcess()
        {
            m_WorkerThread = new Thread(sendCommands);
        }

        private void sendCommands(object obj)
        {
            while (!m_Disposed.WaitOne(1000))
            {
                m_CommandSender.SendCommand(new CommandA(), "bc");
            }
        }

        public void Start(ICommandSender commandSender, IEventPublisher eventPublisher)
        {
            m_CommandSender = commandSender;
            m_WorkerThread.Start();
        }

        public void Dispose()
        {
            m_Disposed.Set();
            m_WorkerThread.Join();
        }
    }

```


registration

```cs

	Register.BoundedContext("bc")
		.ListeningCommands(typeof(CommandA))).On("routeA").WithLoopback()
		.WithProcess(new Projection()))

```


## Sagas ##

Sagas routing is configured similar to bounded context. But saga can not listen for commands or publish events (only send commands and listen events).

When registering a saga a type implementing it should be provided. 

```cs


	Register.Saga<TheSaga>("saga")
		.PublishingCommands(typeof(CommandA),typeof(CommandB)).To("bcA").With("routeA")
		.PublishingCommands(typeof(CommandA),typeof(CommandB)).To("bcB").With("routeB")
		.ListeningEvents(typeof(EventA),typeof(EventB),).From("bc").On("routeC"),

```


Class implementing saga should define method named 'Handle' for each event type it handles.
First parameter should be of type of the event it handles, recieved event would be passed as value of this parameter. 

Optionally there may be parameter of type string named boundedContext. If it is defined, event origination bounded context name would be passed

Optionally there may be parameter of type *ICommandSender*. If it is defined, command sender would be injected.

Return type may be void (exception thrown from handler would cause redelivery of the event within 60 seconds delay) or *CommandHandlingResult* (it defines whether the event should be redelivered and with what delay)

```cs


    public class TheSaga
    {
        private void Handle(EventA @event, ICommandSender commandSender, string boundedContext)
        {
			commandSender.Send("bcA",new CommandB());
			commandSender.Send("bcB",new CommandB());
        }

        private CommandHandlingResult Handle(EventB @event, ICommandSender commandSender, string boundedContext)
        {
			try{
				doSomething();
				commandSender.Send("bcB",new CommandA());
			}catch
			{
				return new CommandHandlingResult(){Retry = true,RetryDelay = 100};
			}
        }
	}

```
