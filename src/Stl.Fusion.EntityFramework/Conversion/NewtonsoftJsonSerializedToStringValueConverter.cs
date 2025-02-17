using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stl.Serialization;

namespace Stl.Fusion.EntityFramework.Conversion
{
    public class NewtonsoftJsonSerializedToStringValueConverter<T>
        : ValueConverter<NewtonsoftJsonSerialized<T>, string>
    {
        public NewtonsoftJsonSerializedToStringValueConverter(ConverterMappingHints? mappingHints = null)
            : base(
                v => v.Data,
                v => NewtonsoftJsonSerialized.New<T>(v),
                mappingHints)
        { }
    }
}
