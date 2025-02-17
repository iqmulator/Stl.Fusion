namespace Stl.Conversion.Internal
{
    public class CastToDescendantConverter<TSource, TTarget> : Converter<TSource, TTarget>
        where TTarget : TSource
    {
        public static CastToDescendantConverter<TSource, TTarget> Instance { get; } = new();

        public override TTarget Convert(TSource source)
            => (TTarget) source!;
        public override object? ConvertUntyped(object? source)
            => (TTarget) source!;

        public override Option<TTarget> TryConvert(TSource source)
            => (TTarget) source!;
        public override Option<object?> TryConvertUntyped(object? source)
            => (object?) (TTarget?) source;
    }
}
