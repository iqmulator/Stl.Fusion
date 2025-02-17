using System;
using System.Runtime.Serialization;

namespace Stl.Serialization
{
    public static class MessagePackSerialized
    {
        public static MessagePackSerialized<TValue> New<TValue>() => new();
        public static MessagePackSerialized<TValue> New<TValue>(TValue value) => new() { Value = value };
        public static MessagePackSerialized<TValue> New<TValue>(byte[] data) => new(data);
    }

    [DataContract]
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
    public class MessagePackSerialized<T> : ByteSerialized<T>
    {
        [ThreadStatic] private static IByteSerializer<T>? _serializer;

        public MessagePackSerialized() { }
        public MessagePackSerialized(byte[] data) : base(data) { }

        protected override IByteSerializer<T> GetSerializer()
            => _serializer ??= MessagePackByteSerializer.Default.ToTyped<T>();
    }
}
