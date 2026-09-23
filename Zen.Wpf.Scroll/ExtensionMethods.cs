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
                vector.X.LessThanOrClose(min.X) ? min.X : vector.X.GreaterThanOrClose(max.X) ? max.X : vector.X,
                vector.Y.LessThanOrClose(min.Y) ? min.Y : vector.Y.GreaterThanOrClose(max.Y) ? max.Y : vector.Y);
        }

        public Vector ValidOr(Vector fallback)
        {
            return new(
                vector.X.IsValidSize() ? vector.X : fallback.X,
                vector.Y.IsValidSize() ? vector.Y : fallback.Y);
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

    extension(Point point)
    {
        public Vector ToVector() => new(point.X, point.Y);
    }

    extension(double value1)
    {
        public bool AreClose(double value2)
        {
            const double Epsilon = 0.00000153;
            return value1.AreClose(value2, Epsilon);
    }

        public bool AreClose(double value2, double epsilon)
        {
            if (value1 == value2)
            {
                return true;
            }

            double delta = value1 - value2;
            return (delta < epsilon) && (delta > -epsilon);
        }

        public bool LessThan(double value2)
        {
            return (value1 < value2) && !value1.AreClose(value2);
        }

        public bool GreaterThan(double value2)
        {
            return (value1 > value2) && !value1.AreClose(value2);
        }

        public bool LessThanOrClose(double value2)
        {
            return (value1 < value2) || value1.AreClose(value2);
        }

        public bool GreaterThanOrClose(double value2)
        {
            return (value1 > value2) || value1.AreClose(value2);
        }

        public bool IsFinite()
        {
            return !double.IsNaN(value1) && !double.IsInfinity(value1);
        }

        public bool IsValidSize()
    {
            return value1.IsFinite() && value1.GreaterThanOrClose(0);
        }
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
            if (min.GreaterThan(max))
            {
                throw new ArgumentException($"'{min}' cannot be greater than {max}.");
            }

            if (value.LessThanOrClose(min))
            {
                return min;
            }
            else if (value.GreaterThanOrClose(max))
            {
                return max;
            }

            return value;
        }
    }
#endif
}
