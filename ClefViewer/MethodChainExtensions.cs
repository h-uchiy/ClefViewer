using System;

namespace ClefViewer
{
    public static class MethodChainExtensions
    {
        public static T Do<T>(this T src, bool execute, Func<T, T> process)
        {
            return execute ? process(src) : src;
        }
    }
}