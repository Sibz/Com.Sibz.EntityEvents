
# Entity Events

**Event Component System and associated helpers for creating single frame event entities.**   
**All Event entities are destroyed and created at the beginning of the initialisation system, they live for exactly one frame.**

Available via Package Manager [https://pm.entityzero.com/](https://pm.entityzero.com/-/web/detail/com.sibz.entity-events)  
*Find package manager setup instructions [here](https://github.com/Sibz/Sibz.UnityPackages)*.  
Source on [GitHub](https://github.com/Sibz/Com.Sibz.EntityEvents).
##EventComponentSystem
### System Creation

The system `EventComponentSystem` does not automatically inject itself into world. You can either create your 
own implementation or set up manually.
```c#
public class MyEventComponentSystem : EventComponentSystem
{
}
```
```c#
var updateGroup = World.GetExistingSystem<SimulationSystemGroup>();
updateGroup.AddSystemToUpdateList(World.CreateSystem<EventComponentSystem>());
updateGroup.SortSystemUpdateList();
```
*Note: Can be in any system group providing it gets updated every frame.*

### Event Component
Extend your struct from `IEventComponentData`
```c#
public struct MyEvent : IEventComponentData 
{
    public int Index;
}
```

### EnqueueEvent
Queue event to be created first thing next frame:
```c#
World.EnqueueEvent<MyEvent>();
// Or with data
World.EnqueueEvent(new MyEvent { Index = 42 });
```

As simple as that, the event is queued for creation next frame, gets created and is removed the following frame.

### Concurrency
Sometimes you may want to queue events from a job, this can be done and below are examples.   
*Don't forget to `World.EventSystemAddJobDependency(jobHandle)`.*

#### Create a job part to execute inside the job  
```c#
EnqueueEventJobPart<MyEvent> jobPart = World.GetEnqueueEventJobPart<MyEvent>();
```
*If data is available outside the job you can also include as follows:*
```c#
EnqueueEventJobPart<MyEvent> jobPart = World.GetEnqueueEventJobPart(new MyEvent {Index = 42}); 
```  
#### Execute from within a job
```c#
inputDeps = Entities.ForEach((ref MyComponent component)=>{
    // can add/change data if required
    jobPart.EventData.Index = -1;
    // then call Execute()
    jobPart.Execute();
}).Schedule(inputDeps);

// Don't forget this!
World.EventSystemAddJobDependency(inputdeps);
```
*From IJob or similar*
```c#
public struct MyJob : IJob
{
    public EnqueueEventJobPart<MyEvent> JobPart;
    
    public void Execute()
    {
        JobPart.Execute();
    }
}

// Schedule in system:
public class MySystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        inputDeps = new ConcurrentRaiseEventTest<MyJob>
        {
            JobPart = World.GetEnqueueEventJobPart<MyJob>()
        }.Schedule(inputDeps);

        // Don't forget this!
        World.EventSystemAddJobDependency(inputDeps);

        return inputDeps;
    }
}
```
## HookSystem
This provides a system that connects events to managed code execution.
### Create System
```c#
public class MyHookSystem : HookSystem
{
}
```
### Register events in your manged code
```c#
public class MyManagedClass
{
    public void OnEvent(IEventComponentData eventData)
    {
        // Unbox event if data is needed.
        TestEventWithData data = (TestEventWithData)eventData;
    }    
    public MyManagedClass(World world)
    {
        world.GetExistingSystem<HookSystem>().RegisterHook<TestEventWithData>(OnEvent);
    }
}
```