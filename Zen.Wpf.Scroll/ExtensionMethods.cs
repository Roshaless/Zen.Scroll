using System.Runtime.CompilerServices;
using System.Windows;

#if !NET8_0_OR_GREATER
using System.Diagnostics;
using System.Runtime.InteropServices;
#endif

namespace Zen.Scroll;

internal static class ExtensionMethods
{
    extension(FrameworkElement element)
    {
        public T? GetElement<T>(string name) where T : DependencyObject => (T?)GetTemplateChild(element, name);

#if NET8_0_OR_GREATER
        [UnsafeAccessor(UnsafeAccessorKind.Method)]
        private static extern DependencyObject? GetTemplateChild(FrameworkElement e, string name);
#else
        private static DependencyObject? GetTemplateChild(FrameworkElement e, string name)
        {
            var internalFlag = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            return (DependencyObject?)typeof(FrameworkElement).GetMethod("GetTemplateChild", internalFlag).Invoke(e, [name]);
        }
#endif
    }

    extension(Vector vector)
    {
        public Vector ConstrainedBetween(Vector min, Vector max)
        {
            return new(
                Math.Max(min.X, Math.Min(max.X, vector.X)),
                Math.Max(min.Y, Math.Min(max.Y, vector.Y)));
        }

        // Keeps NaN / Infinity out of dependency properties.
        public Vector ValidOr(Vector fallback)
        {
            static bool IsValidValue(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
            return new(IsValidValue(vector.X) ? vector.X : fallback.X, IsValidValue(vector.Y) ? vector.Y : fallback.Y);
        }

        public Vector WithX(double x)
        {
            return new(x, vector.Y);
        }

        public Vector WithY(double y)
        {
            return new(vector.X, y);
        }
    }

    extension(Point self)
    {
        public Vector ToVector() => new(self.X, self.Y);
    }

#if !NET8_0_OR_GREATER
    extension(Environment)
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll")]
        internal static extern ulong GetTickCount64();

        public static long TickCount64 => (long)GetTickCount64();
    }

    extension(Stopwatch)
    {
        public static TimeSpan GetElapsedTime(long startingTimestamp) =>
            GetElapsedTime(startingTimestamp, Stopwatch.GetTimestamp());

        public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
            new((endingTimestamp - startingTimestamp) * (10000 * 1000) / Stopwatch.Frequency);
    }

    extension(Math)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double value, double min, double max)
        {
            if (min > max)
            {
                throw new ArgumentException($"'{min}' cannot be greater than {max}.");
            }

            if (value < min)
            {
                return min;
            }
            else if (value > max)
            {
                return max;
            }

            return value;
        }
    }
#endif
}
