using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;

namespace Sibz.EntityEvents.Tests
{
    public class HookSystemTests
    {
        private World world;

        [SetUp]
        public void SetUp() => world = new World("Test");

        [TearDown]
        public void TearDown() => world.Dispose();

        [Test]
        public void ShouldCallActionWhenEventExists()
        {
            var actionCalled = false;

            void OnAction(IEventComponentData test) => actionCalled = true;

            world.EntityManager.CreateEntity(typeof(TestEvent));
            var system = world.CreateSystem<HookSystem>();
            system.RegisterHook<TestEvent>(OnAction);
            system.Update();
            Assert.IsTrue(actionCalled);
        }

        [Test]
        public void ShouldCallActionWithData()
        {
            var index = 0;

            void OnAction(IEventComponentData test) => index = ((TestEventWithData) test).Index;

            Entity e = world.EntityManager.CreateEntity(typeof(TestEventWithData));
            world.EntityManager.SetComponentData(e, new TestEventWithData { Index = 42 });
            var system = world.CreateSystem<HookSystem>();
            system.RegisterHook<TestEventWithData>(OnAction);
            system.Update();
            Assert.AreEqual(42, index);
        }

        [Test]
        public void ShouldNotCallAfterDeregisterHook()
        {
            var actionCalled = false;

            void OnAction(IEventComponentData test) => actionCalled = true;

            world.EntityManager.CreateEntity(typeof(TestEvent));
            var system = world.CreateSystem<HookSystem>();
            system.RegisterHook<TestEvent>(OnAction);
            system.UnregisterHook<TestEvent>();
            system.Update();
            Assert.IsFalse(actionCalled);
        }

        [Test]
        public void ShouldCallMultipleActions()
        {
            var index = 0;
            var actionCalled = false;
            var system = world.CreateSystem<HookSystem>();

            void OnAction1(IEventComponentData test) => actionCalled = true;

            void OnAction2(IEventComponentData test) => index = ((TestEventWithData) test).Index;

            world.EntityManager.CreateEntity(typeof(TestEvent));


            Entity e = world.EntityManager.CreateEntity(typeof(TestEventWithData));
            world.EntityManager.SetComponentData(e, new TestEventWithData { Index = 42 });

            system.RegisterHooks(new Dictionary<ComponentType, Action<IEventComponentData>>
            {
                { typeof(TestEvent), OnAction1 },
                { typeof(TestEventWithData), OnAction2 }
            });

            system.Update();
            system.Update();

            Assert.IsTrue(actionCalled);
            Assert.AreEqual(42, index);
        }

        public struct TestEvent : IEventComponentData
        {
        }

        public struct TestEventWithData : IEventComponentData
        {
            public int Index;
        }
    }
}