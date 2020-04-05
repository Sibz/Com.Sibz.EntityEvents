using System;
using Unity.Entities;
using Unity.Jobs;

namespace Sibz.EntityEvents
{
    public static class WorldEventExtensions
    {
        public static T GetFirstSystemWithBaseType<T>(this World world)
            where T : ComponentSystemBase
        {
            foreach (ComponentSystemBase worldSystem in world.Systems)
            {
                if (worldSystem.GetType() == typeof(EventComponentSystem) ||
                    worldSystem.GetType().IsSubclassOf(typeof(EventComponentSystem)))
                {
                    return worldSystem as T;
                }
            }

            return null;
        }

        public static void EnqueueEvent<T>(this World world, T eventData = default)
            where T : struct, IEventComponentData
        {
            var system = world.GetFirstSystemWithBaseType<EventComponentSystem>();
            if (system is null)
            {
                throw new NullReferenceException($"{nameof(EventComponentSystem)} is null. Unable to enqueue event");
            }

            // ReSharper disable once HeapView.BoxingAllocation
            system.EnqueueEvent(eventData);
        }

        public static EnqueueEventJobPart<T> GetEnqueueEventJobPart<T>(this World world, T eventData = default)
            where T : struct, IEventComponentData
        {
            var system = world.GetFirstSystemWithBaseType<EventComponentSystem>();
            if (system is null)
            {
                throw new NullReferenceException($"{nameof(EventComponentSystem)} is null. Unable to enqueue event");
            }

            return system.GetJobPart(eventData);
        }

        public static void EventSystemAddJobDependency(this World world, JobHandle job)
        {
            var system = world.GetFirstSystemWithBaseType<EventComponentSystem>();
            if (system is null)
            {
                throw new NullReferenceException($"{nameof(EventComponentSystem)} is null. Unable to enqueue event");
            }

            system.ConcurrentBufferAddJobDependency(job);
        }
    }
}