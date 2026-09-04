using System.Runtime.CompilerServices;
using System.Windows;

namespace Zen.Scroll;

internal static class ExtensionMethods
{
    extension(FrameworkElement element)
    {
        public DependencyObject GetElement(string name) => GetTemplateChild(element, name);

        public T GetElement<T>(string name) where T : DependencyObject => Unsafe.As<T>(GetTemplateChild(element, name));

        [UnsafeAccessor(UnsafeAccessorKind.Method)]
        private static extern DependencyObject GetTemplateChild(FrameworkElement e, string name);
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
