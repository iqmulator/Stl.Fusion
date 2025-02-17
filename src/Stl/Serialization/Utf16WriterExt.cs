namespace Stl.Serialization
{
    public static class Utf16WriterExt
    {
        public static string Write<T>(this IUtf16Writer writer, T value)
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            => writer.Write(value, typeof(T));
    }
}
