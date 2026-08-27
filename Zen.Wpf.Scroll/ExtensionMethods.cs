using System.Runtime.CompilerServices;
using System.Windows;

namespace Zen.Scroll;

public static class ExtensionMethods
{
    extension(FrameworkElement element)
    {
        public DependencyObject GetElement(string name) => GetTemplateChild(element, name);

        public T GetElement<T>(string name) where T : DependencyObject => Unsafe.As<T>(GetTemplateChild(element, name));

        [UnsafeAccessor(UnsafeAccessorKind.Method)]
        private static extern DependencyObject GetTemplateChild(FrameworkElement _, string __);
    }

    extension(Vector vector)
    {
        public Vector ConstrainedBetween(Vector min, Vector max)
        {
            return new(
                Math.Max(min.X, Math.Min(max.X, vector.X)),
                Math.Max(min.Y, Math.Min(max.Y, vector.Y)));
        }
    }

    extension(Size)
    {
        public static Size operator +(Size a, Vector b)
        {
            return new(a.Width + b.X, a.Height + b.Y);
        }

        public static Size operator -(Size a, Vector b)
        {
            return new(a.Width + b.X, a.Height + b.Y);
        }

        public static Size operator +(Size a, Size b)
        {
            return new(a.Width + b.Width, a.Height + b.Height);
        }

        public static Size operator -(Size a, Size b)
        {
            return new(a.Width + b.Width, a.Height + b.Height);
        }

        public static bool operator <(Size a, Size b)
        {
            return a.Width < b.Width && a.Height < b.Height;
        }

        public static bool operator >(Size a, Size b)
        {
            return a.Width > b.Width && a.Height > b.Height;
        }
    }
}
