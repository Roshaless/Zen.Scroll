// Author: Roshaless | Email: Roshaless@outlook.com | Created by 2026.9.19
//
// === MouseHorizontalWheel ===
//
// Adds standard routed event accessors for horizontal mouse wheel input
// (WM_MOUSEHWHEEL), matching the pattern of the existing vertical wheel
// events.
//
// Drop this file into your project and include it in the compile.
// No registration, no initialization, no configuration. The feature
// enables itself via [ModuleInitializer] and attaches to every Window
// as it loads.
//
// Requirements:
//
//     <LangVersion>preview</LangVersion>
//
// Supported frameworks: .NET Framework 4.x through the latest .NET.
//
// Usage:
//
//     element.MouseWheel += OnMouseWheel;
//
//     private void OnMouseWheel(object sender, MouseWheelEventArgs e)
//     {
//         if (e.IsHorizontalMouseWheel || Keyboard.Modifiers is ModifierKeys.Shift)
//         {
//             // HorizontalWheel
//         }
//         else
//         {
//             // VerticalWheel
//         }
//     }
//

#pragma warning disable IDE0079
#pragma warning disable SYSLIB1054
#pragma warning disable CA2255

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace System.Windows
{
    file static class AutoInitializer
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                EventManager.RegisterClassHandler(
                    typeof(Window),
                    FrameworkElement.LoadedEvent,
                    new RoutedEventHandler(OnWindowLoaded));

                MouseHorizontalWheelHook.Initialize();
                OnWindowLoaded(null!, EventArgs.Empty);
            });
        }

        private static void OnWindowLoaded(object sender, EventArgs e)
        {
            if (Application.Current is null) return;

            foreach (Window window in Application.Current.Windows)
            {
                if (window.IsLoaded)
                {
                    MouseHorizontalWheelHook.InitializeHook(window);
                }
            }
        }
    }

    // WPF内部输入事件报告处理 & 推送到用户路由事件
    file partial class MouseHorizontalWheelHook
    {
        public static void Initialize()
        {
            InputManager.Current.PreNotifyInput += OnPreNotifyInput;
            InputManager.Current.PostProcessInput += OnPostProcessInput;
        }

        private static void OnPreNotifyInput(object sender, NotifyInputEventArgs e)
        {
            if (e.StagingItem.Input is { Handled: false, Device: MouseDevice mouseDevice, RoutedEvent.Name: "PreviewInputReport" } input)
            {
                var report = InputReportEventArgsStatic.GetReport(input);
                var actions = RawMouseInputReportStatic.GetActions(report);

                var inputSource = MouseDeviceStatic.GetInputSource(mouseDevice);
                if (inputSource != null && InputReportStatic.GetInputSource(report) == inputSource)
                {
                    if ((Convert.ToInt32(actions) & 0x20000) == 0x20000)
                    {
                        InputManangerStatic.SetMostRecentInputDevice(e.InputManager, input.Device);
                    }
                }
            }
        }

        private static void OnPostProcessInput(object sender, ProcessInputEventArgs e)
        {
            // PreviewMouseHorizontalWheel --> MouseHorizontalWheel
            if (e.StagingItem.Input.RoutedEvent == Mouse.PreviewMouseWheelEvent)
            {
                if (e.StagingItem.Input is { Handled: false, Device: MouseDevice mouseDevice } input)
                {
                    MouseWheelEventArgs previewWheel = (MouseWheelEventArgs)e.StagingItem.Input;
                    MouseWheelEventArgs wheel;

                    if (previewWheel is MouseHorizontalWheelEventArgs)
                    {
                        wheel = new MouseHorizontalWheelEventArgs(mouseDevice, previewWheel.Timestamp, previewWheel.Delta)
                        {
                            RoutedEvent = Mouse.MouseWheelEvent
                        };
                    }
                    else
                    {
                        wheel = new(mouseDevice, previewWheel.Timestamp, previewWheel.Delta)
                        {
                            RoutedEvent = Mouse.MouseWheelEvent
                        };
                    }

                    input.Handled = true;
                    e.PushInput(wheel, e.StagingItem);
                }
            }

            if (e.StagingItem.Input.RoutedEvent.Name == "InputReport")
            {
                if (e.StagingItem.Input is { Handled: false, Device: MouseDevice mouseDevice } input)
                {
                    var report = InputReportEventArgsStatic.GetReport(input);
                    var actions = RawMouseInputReportStatic.GetActions(report);

                    var inputSource = MouseDeviceStatic.GetInputSource(mouseDevice);
                    if (inputSource != null && InputReportStatic.GetInputSource(report) == inputSource)
                    {
                        // Raw --> PreviewMouseWheel (Horizontal)
                        if ((Convert.ToInt32(actions) & 0x20000) == 0x20000)
                        {
                            MouseHorizontalWheelEventArgs previewWheel = new(mouseDevice, InputReportStatic.GetTimestamp(report), RawMouseInputReportStatic.GetWheel(report))
                            {
                                RoutedEvent = Mouse.PreviewMouseWheelEvent
                            };

                            input.Handled = true;
                            e.PushInput(previewWheel, e.StagingItem);
                        }
                    }
                }
            }
        }
    }

    // 全局 Hook 注册 & 卸载管理
    file partial class MouseHorizontalWheelHook
    {
        private static readonly ConcurrentDictionary<PresentationSource, MouseHorizontalWheelHook> Hooks = [];

        public static void InitializeHook(Visual visual)
        {
            var source = (HwndSource)PresentationSource.FromVisual(visual);
            if (source is null || Hooks.ContainsKey(source)) return;

            Hooks.TryAdd(source, new MouseHorizontalWheelHook(source));
        }

        public static void UninitializeHook(HwndSource source)
        {
            if (source is not null && Hooks.TryRemove(source, out var hook))
            {
                hook.Dispose();
            }
        }
    }

    // WM 消息接收 & 水平滚动报告
    file partial class MouseHorizontalWheelHook : IDisposable
    {
        private readonly HwndSource Source;

        private MouseHorizontalWheelHook(HwndSource hwndSource)
        {
            Source = hwndSource;
            Source.AddHook(WndProc);
            Source.Disposed += OnDisposed;
        }

        public void Dispose()
        {
            Source.RemoveHook(WndProc);
            Source.Disposed -= OnDisposed;
        }

        private void OnDisposed(object? sender, EventArgs e)
        {
            UninitializeHook(Source);
        }

        // HorizontalWheelRotate = 0x20000
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x020E /*WM_MOUSEHWHEEL*/)
            {
                var wheel = SignedHIWORD(wParam);
                int x = SignedLOWORD(lParam);
                int y = SignedHIWORD(lParam);

                var provider = HwndSourceStatic.GetMouse(Source);
                if (provider is not null)
                {
                    var pt = new Drawing.Point() { X = x, Y = y };
                    if (ScreenToClient(hwnd, ref pt))
                    {
                        handled = HwndMouseInputProviderStatic.ReportInput(provider,
                                                                           hwnd,
                                                                           InputMode.Foreground,
                                                                           0x20000,
                                                                           pt.X,
                                                                           pt.Y,
                                                                           wheel);
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static short SignedHIWORD(IntPtr ptr)
        {
            unchecked
            {
                if (Environment.Is64BitOperatingSystem)
                {
                    return (short)((ptr.ToInt64() >> 16) & 0xFFFF);
                }

                return (short)((ptr.ToInt32() >> 16) & 0xFFFF);
            }
        }

        private static short SignedLOWORD(IntPtr ptr)
        {
            unchecked
            {
                if (Environment.Is64BitOperatingSystem)
                {
                    return (short)(ptr.ToInt64() & 0xFFFF);
                }

                return (short)(ptr.ToInt32() & 0xFFFF);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ScreenToClient(IntPtr hWnd, ref Drawing.Point lpPoint);
    }


    namespace Input
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static class HorizontalWheelExtensions
        {

            extension(MouseWheelEventArgs args)
            {
                public bool IsHorizontalMouseWheel
                {
                    [DebuggerNonUserCode]
                    get => args is MouseHorizontalWheelEventArgs;
                }
            }
        }

        internal sealed class MouseHorizontalWheelEventArgs(MouseDevice mouse, int timestamp, int delta) : MouseWheelEventArgs(mouse, timestamp, delta);
    }

    file static class WPFAssemblies
    {
        public static readonly Assembly PresentationCoreAssembly = typeof(HwndSource).Assembly;
    }
    file static class InputManangerStatic
    {
        private static readonly Action<object, object> MostRecentInputDeviceSetter = ExpressionAccessor.BuildSetter(typeof(InputManager), "_mostRecentInputDevice");
        public static void SetMostRecentInputDevice(object instance, object value) => MostRecentInputDeviceSetter(instance, value);
    }
    file static class InputReportEventArgsStatic
    {
        private static readonly Type InputReportEventArgsType = WPFAssemblies.PresentationCoreAssembly.GetType("System.Windows.Input.InputReportEventArgs")!;
        private static readonly Func<object, object> ReportGetter = ExpressionAccessor.BuildGetter(InputReportEventArgsType, "Report");
        public static object GetReport(object instance) => ReportGetter(instance);
    }
    file static class InputReportStatic
    {
        private static readonly Type InputReportType = WPFAssemblies.PresentationCoreAssembly.GetType("System.Windows.Input.InputReport")!;
        private static readonly Func<object, PresentationSource> InputSourceGetter = ExpressionAccessor.BuildGetter<PresentationSource>(InputReportType, "InputSource");
        private static readonly Func<object, int> TimestampGetter = ExpressionAccessor.BuildGetter<int>(InputReportType, "Timestamp");
        public static PresentationSource GetInputSource(object instance) => InputSourceGetter(instance);
        public static int GetTimestamp(object instance) => TimestampGetter(instance);
    }
    file static class RawMouseInputReportStatic
    {
        private static readonly Type RawMouseInputReportType = WPFAssemblies.PresentationCoreAssembly.GetType("System.Windows.Input.RawMouseInputReport")!;
        private static readonly Func<object, object> ActionsGetter = ExpressionAccessor.BuildGetter(RawMouseInputReportType, "Actions");
        private static readonly Func<object, int> WheelGetter = ExpressionAccessor.BuildGetter<int>(RawMouseInputReportType, "Wheel");
        public static object GetActions(object instance) => ActionsGetter(instance);
        public static int GetWheel(object instance) => WheelGetter(instance);
    }
    file static class MouseDeviceStatic
    {
        private static readonly Func<object, object> InputSourceGetter = ExpressionAccessor.BuildGetter(typeof(MouseDevice), "_inputSource");
        public static object? GetInputSource(object instance)
        {
            var result = InputSourceGetter(instance);
            if (result is not null && result.GetType() != typeof(HwndSource))
            {
                return SecurityCriticalDataStatic.GetValue(result, typeof(PresentationSource));
            }

            return result;
        }
    }
    file static class HwndSourceStatic
    {
        private static readonly Type ProviderType = WPFAssemblies.PresentationCoreAssembly.GetType("System.Windows.Interop.HwndMouseInputProvider")!;
        private static readonly Func<object, object> MouseGetter = ExpressionAccessor.BuildGetter(typeof(HwndSource), "_mouse");
        public static object? GetMouse(object instance)
        {
            var result = MouseGetter(instance);
            if (result is not null && result.GetType() != ProviderType)
            {
                return SecurityCriticalDataStatic.GetValue(result, ProviderType);
            }

            return result;
        }
    }
    file static class HwndMouseInputProviderStatic
    {
        private static readonly Type RawMouseActionsType = WPFAssemblies.PresentationCoreAssembly.GetType("System.Windows.Input.RawMouseActions")!;
        private static readonly Type ProviderType = WPFAssemblies.PresentationCoreAssembly.GetType("System.Windows.Interop.HwndMouseInputProvider")!;
        private static readonly Func<object, int> MsgTimeGetter = ExpressionAccessor.BuildGetter<int>(ProviderType, "_msgTime");
        private static readonly Func<object, IntPtr, InputMode, int, object, int, int, int, bool> ReportInputMethod = BuildReportInput();

        public static bool ReportInput(object mouse, IntPtr hwnd, InputMode mode, object actions, int x, int y, int wheel)
        {
            return ReportInputMethod(mouse, hwnd, mode, MsgTimeGetter(mouse), actions, x, y, wheel);
        }

        private static Func<object, IntPtr, InputMode, int, object, int, int, int, bool> BuildReportInput()
        {
            var provider = Linq.Expressions.Expression.Parameter(typeof(object), "provider");
            var hwnd = Linq.Expressions.Expression.Parameter(typeof(IntPtr), "hwnd");
            var mode = Linq.Expressions.Expression.Parameter(typeof(InputMode), "mode");
            var timestamp = Linq.Expressions.Expression.Parameter(typeof(int), "timestamp");
            var actions = Linq.Expressions.Expression.Parameter(typeof(object), "actions");
            var x = Linq.Expressions.Expression.Parameter(typeof(int), "x");
            var y = Linq.Expressions.Expression.Parameter(typeof(int), "y");
            var wheel = Linq.Expressions.Expression.Parameter(typeof(int), "wheel");

            var typedProvider = Linq.Expressions.Expression.Convert(provider, ProviderType);
            var reportInputMethod = ProviderType.GetMethod("ReportInput", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var call = Linq.Expressions.Expression.Call(
                typedProvider,
                reportInputMethod,
                hwnd,
                mode,
                timestamp,
                Linq.Expressions.Expression.Convert(actions, RawMouseActionsType),   // object → RawMouseActions
                x,
                y,
                wheel);

            return Linq.Expressions.Expression.Lambda<
                Func<object, IntPtr, InputMode, int, object, int, int, int, bool>>(
                call, provider, hwnd, mode, timestamp, actions, x, y, wheel).Compile();
        }
    }
    file static class SecurityCriticalDataStatic
    {
#pragma warning disable IDE0001
        private static readonly System.Collections.Generic.Dictionary<Type, Func<object, object>> TypedGetters = [];
        public static object GetValue(object instance, Type valueType)
        {
            if (TypedGetters.TryGetValue(valueType, out var getter) is not true)
            {
                TypedGetters.Add(valueType, getter = ExpressionAccessor.BuildGetter(instance.GetType(), "Value"));
            }

            return getter(instance);
        }
#pragma warning restore IDE0001
    }

    file static class ExpressionAccessor
    {
        public static Func<object, object> BuildGetter(Type declaringType, string member)
        {
            // obj => (object)obj.Property
            var obj = Linq.Expressions.Expression.Parameter(typeof(object), "obj");
            var cast = Linq.Expressions.Expression.Convert(obj, declaringType);
            var access = Linq.Expressions.Expression.PropertyOrField(cast, member);
            var boxed = Linq.Expressions.Expression.Convert(access, typeof(object));
            return Linq.Expressions.Expression.Lambda<Func<object, object>>(boxed, obj).Compile();
        }

        public static Func<object, TValue> BuildGetter<TValue>(Type declaringType, string member)
        {
            // obj => obj.Property
            var obj = Linq.Expressions.Expression.Parameter(typeof(object), "obj");
            var cast = Linq.Expressions.Expression.Convert(obj, declaringType);
            var access = Linq.Expressions.Expression.PropertyOrField(cast, member);
            return Linq.Expressions.Expression.Lambda<Func<object, TValue>>(access, obj).Compile();
        }

        public static Action<object, object> BuildSetter(Type declaringType, string propertyName)
        {
            // () => obj.PropertyOrField = value
            var obj = Linq.Expressions.Expression.Parameter(typeof(object), "obj");
            var value = Linq.Expressions.Expression.Parameter(typeof(object), "value");
            var cast = Linq.Expressions.Expression.Convert(obj, declaringType);
            var access = Linq.Expressions.Expression.PropertyOrField(cast, propertyName);
            var boxed = Linq.Expressions.Expression.Convert(value, access.Member is PropertyInfo p ? p.PropertyType : ((FieldInfo)access.Member).FieldType);
            var assign = Linq.Expressions.Expression.Assign(access, boxed);
            return Linq.Expressions.Expression.Lambda<Action<object, object>>(assign, obj, value).Compile();
        }
    }
}