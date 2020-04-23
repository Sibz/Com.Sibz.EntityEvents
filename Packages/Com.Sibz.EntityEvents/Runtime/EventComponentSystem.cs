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
    public class EventComponentSystem : EventComponentSystem<BeginInitCommandBuffer>
    {
        private static readonly string[] ExcludedAssemblies =
        {
            "mscorlib,",
            "Accessibility,",
            "Unity.",
            "UnityEngine,",
            "UnityEngine.",
            "UnityEditor,",
            "UnityEditor.",
            "System,",
            "System.",
            "nunit.framework,",
            "ReportGeneratorMerged,",
            "netstandard",
            "ExCSS.Unity,",
            "JetBrains.",
            "Mono.",
            "Novell.",
            "Microsoft."
        };

        public static readonly ComponentType[] EventTypes = GetEventTypes();

        private static ComponentType[] GetEventTypes()
        {

            List<Assembly> asses = new List<Assembly>();
            List<ComponentType> types = new List<ComponentType>();
            var localAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < localAssemblies.Length; i++)
            {
                bool exclude = false;
                for (int j = 0; j < ExcludedAssemblies.Length; j++)
                {
                    if (!localAssemblies[i].FullName.StartsWith(ExcludedAssemblies[j]))
                    {
                        continue;
                    }

                    exclude = true;
                    break;
                }

                if (!exclude)
                {
                    asses.Add(localAssemblies[i]);
                }

            }

            foreach (Assembly a in asses)
            {
                types.AddRange(a.GetTypes()
                    .Where(x => x.IsValueType && x.GetInterfaces().Contains(typeof(IEventComponentData)))
                    .Select(t => (ComponentType) t));
            }

            return types.ToArray();
        }
    }

    [AlwaysUpdateSystem]
    public abstract class EventComponentSystem<T> : SystemBase
        where T : class, ICommandBuffer, new()
    {

        private EntityQuery allEventComponentsQuery;
        private T commandBufferDestroyer;
        private T commandBufferCreator;
        private T commandBufferConcurrent;
        private int concurrentRequestCount;

        public EnqueueEventJobPart<T> GetJobPart<T>(T eventData)
            where T : struct, IEventComponentData
        {
            EnsureDestroyBufferIsExecutedFirst(commandBufferDestroyer);
            commandBufferConcurrent.ForceNewBuffer();
            return new EnqueueEventJobPart<T>
            {
                CommandBuffer = commandBufferConcurrent.Concurrent,
                EventData = eventData,
                Index = concurrentRequestCount++
            };
        }

        private static void EnsureDestroyBufferIsExecutedFirst(T destroyBuffer)
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
            allEventComponentsQuery = GetEntityQuery(new EntityQueryDesc { Any = EventComponentSystem.EventTypes });
            commandBufferDestroyer = new T { World = World };
            commandBufferConcurrent = new T { World = World };
            commandBufferCreator = new T { World = World };
            commandBufferConcurrent.NewBuffer += () => concurrentRequestCount = 0;
        }

        protected override void OnUpdate()
        {
            commandBufferDestroyer.Buffer.DestroyEntity(allEventComponentsQuery);
        }

        private void CreateSingletonFromObject(object obj)
        {
            MethodInfo method = typeof(EventComponentSystem<T>).GetMethod(nameof(CreateSingleton),
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method is null)
            {
                throw new NullReferenceException($"Unable to get method {nameof(CreateSingleton)}");
            }

            method.MakeGenericMethod(obj.GetType()).Invoke(this, new[] { obj });
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