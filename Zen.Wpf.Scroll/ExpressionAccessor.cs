using System.Linq.Expressions;
using System.Reflection;

namespace Zen.Scroll;

internal static class ExpressionAccessor
{
    public static Func<TSelf, TValue> BuildGetter<TSelf, TValue>(string member)
    {
        // TSelf => TSelf.Property
        var obj = Expression.Parameter(typeof(TSelf), "obj");
        var access = Expression.PropertyOrField(obj, member);
        return Expression.Lambda<Func<TSelf, TValue>>(access, obj).Compile();
    }

    public static Func<object, TValue> BuildGetter<TValue>(Type declaringType, string member)
    {
        // obj => obj.Property
        var obj = Expression.Parameter(typeof(object), "obj");
        var cast = Expression.Convert(obj, declaringType);
        var access = Expression.PropertyOrField(cast, member);
        return Expression.Lambda<Func<object, TValue>>(access, obj).Compile();
    }

    public static Func<object, object> BuildGetter(Type declaringType, string member)
    {
        // obj => (object)obj.Property
        var obj = Expression.Parameter(typeof(object), "obj");
        var cast = Expression.Convert(obj, declaringType);
        var access = Expression.PropertyOrField(cast, member);
        var boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object>>(boxed, obj).Compile();
    }

    public static Action<TSelf, TValue> BuildSetter<TSelf, TValue>(string member)
    {
        // () => TSelf.PropertyOrField = value
        var obj = Expression.Parameter(typeof(TSelf), "obj");
        var value = Expression.Parameter(typeof(TValue), "value");
        var access = Expression.PropertyOrField(obj, member);
        var assign = Expression.Assign(access, value);
        return Expression.Lambda<Action<TSelf, TValue>>(assign, obj, value).Compile();
    }

    public static Action<object, TValue> BuildSetter<TValue>(Type declaringType, string member)
    {
        // () => obj.PropertyOrField = value
        var obj = Expression.Parameter(typeof(object), "obj");
        var value = Expression.Parameter(typeof(TValue), "value");
        var cast = Expression.Convert(obj, declaringType);
        var access = Expression.PropertyOrField(cast, member);
        var assign = Expression.Assign(access, value);
        return Expression.Lambda<Action<object, TValue>>(assign, obj, value).Compile();
    }

    public static Action<object, object> BuildSetter(Type declaringType, string member)
    {
        // () => obj.PropertyOrField = value
        var obj = Expression.Parameter(typeof(object), "obj");
        var value = Expression.Parameter(typeof(object), "value");
        var cast = Expression.Convert(obj, declaringType);
        var access = Expression.PropertyOrField(cast, member);
        var boxed = Expression.Convert(value, GetMemberType(access.Member));
        var assign = Expression.Assign(access, boxed);
        return Expression.Lambda<Action<object, object>>(assign, obj, value).Compile();
    }

    private static Type GetMemberType(MemberInfo info)
    {
        if (info.MemberType is MemberTypes.Field)
            return ((FieldInfo)info).FieldType;

        return ((PropertyInfo)info).PropertyType;
    }
}
