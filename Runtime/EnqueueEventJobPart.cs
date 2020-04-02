using Sibz.CommandBufferHelpers;
using Unity.Entities;

namespace Sibz.EntityEvents
{
    public struct EnqueueEventJobPart<T>
        where T : struct, IEventComponentData
    {
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public T EventData;
        public int Index;

        public void Execute()
        {
            CommandBuffer.CreateSingleton(Index, EventData);
        }
    }
}