using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Sibz.EntityEvents
{
    public class HookSystem : SystemBase
    {
        protected readonly Dictionary<ComponentType, Action<IEventComponentData>> ActionMap =
            new Dictionary<ComponentType, Action<IEventComponentData>>();

        private EntityQuery allEventComponentsQuery;
        private MethodInfo getComponentMethodInfo;

        public void RegisterHook<T>(Action<IEventComponentData> action)
            where T : struct, IComponentData =>
            RegisterHook(typeof(T), action);

        public void RegisterHook(ComponentType componentType, Action<IEventComponentData> action)
        {
            if (ActionMap.ContainsKey(componentType))
            {
                ActionMap[componentType] += action;
                return;
            }

            ActionMap.Add(componentType, action);
        }

        public void RegisterHooks(Dictionary<ComponentType, Action<IEventComponentData>> types)
        {
            foreach (KeyValuePair<ComponentType, Action<IEventComponentData>> type in types)
            {
                RegisterHook(type.Key, type.Value);
            }
        }

        public void UnregisterHook<T>()
            where T : struct, IEventComponentData => UnregisterHook(typeof(T));

        public void UnregisterHook(ComponentType componentType)
        {
            if (ActionMap.ContainsKey(componentType))
            {
                ActionMap.Remove(componentType);
            }
        }

        private void ExecuteHook<TEvent>(IEventComponentData eventData)
            where TEvent : struct, IEventComponentData
        {
            if (ActionMap.ContainsKey(typeof(TEvent)))
            {
                ActionMap[typeof(TEvent)].Invoke(eventData);
            }
        }

        protected override void OnCreate()
        {
            allEventComponentsQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    Any = EventComponentSystem.EventTypes
                });
            RequireForUpdate(allEventComponentsQuery);

            getComponentMethodInfo = EntityManager
                .GetType()
                .GetMethod(nameof(EntityManager.GetComponentData));
        }

        protected override void OnUpdate()
        {
            using (NativeArray<Entity> entities =
                allEventComponentsQuery.ToEntityArrayAsync(Allocator.TempJob, out JobHandle jh))
            {
                jh.Complete();
                IterateEntitiesAndInvokeActions(entities);
            }
        }

        private void IterateEntitiesAndInvokeActions(NativeArray<Entity> entities)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                Entity eventEntity = entities[i];
                InvokeActionsIfRequired(eventEntity);
            }
        }

        private void InvokeActionsIfRequired(Entity eventEntity)
        {
            using (NativeArray<ComponentType> components = EntityManager.GetComponentTypes(eventEntity, Allocator.TempJob))
            {
                if (components.Length == 0 || !ActionMap.ContainsKey(components[0]))
                {
                    return;
                }

                if (components[0].IsZeroSized)
                {
                    ActionMap[components[0]].Invoke(default);
                    return;
                }

                Type type = components[0].GetManagedType();
                MethodInfo getComponent = getComponentMethodInfo.MakeGenericMethod(type);
                ActionMap[components[0]]
                    .Invoke(
                        getComponent.Invoke(EntityManager,
                            new object[] { eventEntity }) as IEventComponentData);
            }
        }
    }
}