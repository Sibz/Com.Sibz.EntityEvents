using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sibz.CommandBufferHelpers;
using Unity.Entities;
using Unity.Jobs;

[assembly: DisableAutoCreation]

namespace Sibz.EntityEvents
{
    [AlwaysUpdateSystem]
    public class EventComponentSystem : JobComponentSystem
    {
        private EntityQuery allEventComponentsQuery;
        private BeginInitCommandBuffer commandBufferDestroyer;
        private BeginInitCommandBuffer commandBufferCreator;
        private BeginInitCommandBuffer commandBufferConcurrent;
        //private readonly Queue<object> eventQueue = new Queue<object>();
        private int concurrentRequestCount;

        // ReSharper disable once MemberCanBePrivate.Global
        public static readonly ComponentType[] EventTypes = GetEventTypes();

        public EnqueueEventJobPart<T> GetJobPart<T>(T eventData)
            where T : struct, IEventComponentData
        {

            EnsureDestroyBufferIsExecutedFirst(commandBufferDestroyer);

            return new EnqueueEventJobPart<T>
            {
                //TODO CommandBuffer should provide ability to get new Concurrent Buffer
                CommandBuffer = new BeginInitCommandBuffer(World).Concurrent,
                EventData = eventData,
                Index = concurrentRequestCount++
            };
        }

        private static void EnsureDestroyBufferIsExecutedFirst(BeginInitCommandBuffer destroyBuffer)
        {
            // This ensures the non concurrent buffer is created/executed first
            // Required as the destroy entities on OnUpdate needs to occur first
            // The actual exception should never be thrown
            if (!destroyBuffer.Buffer.IsCreated)
            {
                throw new InvalidOperationException();
            }
        }

        public void ConcurrentBufferAddJobDependency(JobHandle job)
        {
            commandBufferConcurrent.AddJobDependency(job);
        }

        public void EnqueueEvent(object eventData)
        {
            CreateSingletonFromObject(eventData);
        }

        protected override void OnCreate()
        {
            allEventComponentsQuery = GetEntityQuery(new EntityQueryDesc {Any = EventTypes});
            commandBufferDestroyer = new BeginInitCommandBuffer(World);
            commandBufferConcurrent = new BeginInitCommandBuffer(World);
            commandBufferCreator = new BeginInitCommandBuffer(World);
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // TODO This should reset only when commandBufferConcurrent gets a new
            // command buffer internally. Need a hook there to reset this.
            concurrentRequestCount = 0;

            commandBufferDestroyer.Buffer.DestroyEntity(allEventComponentsQuery);

            /*while (eventQueue.Count > 0)
            {
                CreateSingletonFromObject(eventQueue.Dequeue());
            }*/

            return inputDeps;
        }

        private static ComponentType[] GetEventTypes()
        {
            var types = new List<ComponentType>();
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                types.AddRange(a.GetTypes()
                    .Where(x => x.IsValueType && x.GetInterfaces().Contains(typeof(IEventComponentData)))
                    .Select(t => (ComponentType) t));
            }

            return types.ToArray();
        }

        private void CreateSingletonFromObject(object obj)
        {
            MethodInfo method = typeof(EventComponentSystem).GetMethod(nameof(CreateSingleton),
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method is null)
            {
                throw new NullReferenceException($"Unable to get method {nameof(CreateSingleton)}");
            }

            method.MakeGenericMethod(obj.GetType()).Invoke(this, new[] {obj});
        }

        // ReSharper disable once UnusedMember.Local
        private void CreateSingleton<T>(T obj)
            where T : struct, IComponentData
        {
            EnsureDestroyBufferIsExecutedFirst(commandBufferDestroyer);

            commandBufferCreator.Buffer.CreateSingleton(obj);
        }
    }
}