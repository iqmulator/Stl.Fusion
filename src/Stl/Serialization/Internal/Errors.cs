using System;
using System.Runtime.Serialization;

namespace Stl.Serialization.Internal
{
    public static class Errors
    {
        public static Exception UnsupportedSerializedType(Type type)
            => new SerializationException($"Unsupported type: '{type}'.");

        public static Exception SerializedTypeMismatch(Type supportedType, Type requestedType)
            => new NotSupportedException(
                $"The serializer implements '{supportedType}' serialization, but '{requestedType}' was requested to (de)serialize.");
    }
}
