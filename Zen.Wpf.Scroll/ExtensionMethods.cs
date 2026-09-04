using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

namespace Zen.Scroll;

internal static class ExtensionMethods
{
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
        //private const long TicksPerMillisecond = 10000;
        //private const long TicksPerSecond = TicksPerMillisecond * 1000;

        //// performance-counter frequency, in counts per ticks.
        //// This can speed up conversion from high frequency performance-counter
        //// to ticks.
        //private static readonly double s_tickFrequency = (double)TicksPerSecond / Frequency;

        public static TimeSpan GetElapsedTime(long startingTimestamp) =>
            GetElapsedTime(startingTimestamp, Stopwatch.GetTimestamp());

        public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
            new((endingTimestamp - startingTimestamp) * (10000 * 1000) / Stopwatch.Frequency /*s_tickFrequency*/);
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

    extension(FrameworkElement element)
    {
        public DependencyObject GetElement(string name) => GetTemplateChild(element, name);

        public T GetElement<T>(string name) where T : DependencyObject => (T)GetTemplateChild(element, name);

#if NET8_0_OR_GREATER
        [UnsafeAccessor(UnsafeAccessorKind.Method)]
        private static extern DependencyObject GetTemplateChild(FrameworkElement e, string name);
#else
        private static DependencyObject GetTemplateChild(FrameworkElement e, string name)
        {
            var internalFlag = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            return (DependencyObject)typeof(FrameworkElement).GetMethod("GetTemplateChild", internalFlag).Invoke(e, [name]);
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

        public Vector WithX(double x)
        {
            return new(x, vector.Y);
        }

        public Vector WithX(Vector vector1)
        {
            return new(vector1.X, vector.Y);
        }

        public Vector WithY(double y)
        {
            return new(vector.X, y);
        }

        public Vector WithY(Vector vector1)
        {
            return new(vector.X, vector1.Y);
        }

        public static Vector operator /(Vector vector1, Vector vector2)
        {
            return new(vector1.X / vector2.X, vector1.Y / vector2.Y);
        }
    }
}
