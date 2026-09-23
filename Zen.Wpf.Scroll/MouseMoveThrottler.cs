using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace Zen.Scroll;

public static class MouseMoveThrottler
{
    private static long LastPassTick;
    private static int EnabledState;
    public static long IntervalMs { get; set; } = 40;
    public static bool IsEnabled => Volatile.Read(ref EnabledState) > 0;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Enable()
    {
        if (Interlocked.Increment(ref EnabledState) == 1)
        {
            Interlocked.Exchange(ref LastPassTick, Environment.TickCount64);
            InputManager.Current.PreNotifyInput += OnPreNotify;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Disable()
    {
        if (Interlocked.Decrement(ref EnabledState) == 0)
        {
            InputManager.Current.PreNotifyInput -= OnPreNotify;
            Mouse.PrimaryDevice.Synchronize();
        }
    }

    private static void OnPreNotify(object sender, NotifyInputEventArgs e)
    {
        if (e.StagingItem.Input is { Device: MouseDevice, RoutedEvent.Name: "PreviewInputReport" } input)
        {
            var report = InputReportEventArgsStatic.GetReport(input);
            var actions = RawMouseInputReportStatic.GetActions(report);

            // RawMouseInputReport.Actions & ~(AbsoluteMove | QueryCursor)
            if ((Convert.ToInt32(actions) & ~(0x10 | 0x40000)) != 0)
            {
                // RawMouseInputReport.Actions & Deactivate
                if ((Convert.ToInt32(actions) & 0x04) == 0x04)
                    return;

                // RawMouseInputReport.Actions & VerticalWheelRotate
                if ((Convert.ToInt32(actions) & 0x10000) == 0x10000)
                    return;

                // RawMouseInputReport.Actions & HorizontalWheelRotate
                if ((Convert.ToInt32(actions) & 0x20000) == 0x20000)
                    return;

                var ptClient = new Point(RawMouseInputReportStatic.GetX(report), RawMouseInputReportStatic.GetY(report));
                var ptRoot = TryClientToRoot(ptClient, InputReportStatic.GetInputSource(report), false, out bool success);
                if (success)
                {
                    RawMouseInputReportStatic.SetActions(report, Convert.ToInt32(actions) | 0x10 | 0x40000);
                    e.StagingItem.SetData(MouseDeviceStatic.GetTagRootPoint(input.Device), ptRoot);
                    return;
                }
            }

            if (Environment.TickCount64 - LastPassTick < IntervalMs)
            {
                input.Handled = true;
                return;
            }

            LastPassTick = Environment.TickCount64;
        }
    }

    private static readonly Assembly PresentationCoreAssembly = typeof(InputManager).Assembly;

    private static readonly TryClientToRootDelegate TryClientToRoot =
        (TryClientToRootDelegate)Delegate.CreateDelegate(typeof(TryClientToRootDelegate),
            PresentationCoreAssembly.GetType("MS.Internal.PointUtil")!.GetMethod("TryClientToRoot",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null,
                    [typeof(Point), typeof(PresentationSource), typeof(bool), typeof(bool).MakeByRefType()], null)!);

    private delegate Point TryClientToRootDelegate(
        Point point,
        PresentationSource presentationSource,
        bool throwOnError,
        out bool success);

    private abstract class InputReportEventArgsStatic
    {
        private static readonly Type InputReportEventArgsType = PresentationCoreAssembly.GetType("System.Windows.Input.InputReportEventArgs")!;
        private static readonly Func<object, object> ReportGetter = ExpressionAccessor.BuildGetter(InputReportEventArgsType, "Report");
        public static object GetReport(object instance) => ReportGetter(instance);
    }
    private abstract class InputReportStatic
    {
        private static readonly Type InputReportType = PresentationCoreAssembly.GetType("System.Windows.Input.InputReport")!;
        private static readonly Func<object, PresentationSource> InputSourceGetter = ExpressionAccessor.BuildGetter<PresentationSource>(InputReportType, "InputSource");
        public static PresentationSource GetInputSource(object instance) => InputSourceGetter(instance);
    }
    private abstract class RawMouseInputReportStatic : InputReportStatic
    {
        private static readonly Type RawMouseInputReportType = PresentationCoreAssembly.GetType("System.Windows.Input.RawMouseInputReport")!;
        private static readonly Func<object, object> ActionsGetter = ExpressionAccessor.BuildGetter(RawMouseInputReportType, "Actions");
        private static readonly Func<object, int> WheelGetter = ExpressionAccessor.BuildGetter<int>(RawMouseInputReportType, "Wheel");
        private static readonly Func<object, int> XGetter = ExpressionAccessor.BuildGetter<int>(RawMouseInputReportType, "X");
        private static readonly Func<object, int> YGetter = ExpressionAccessor.BuildGetter<int>(RawMouseInputReportType, "Y");
        public static object GetActions(object instance) => ActionsGetter(instance);
        public static int GetWheel(object instance) => WheelGetter(instance);
        public static int GetX(object instance) => XGetter(instance);
        public static int GetY(object instance) => YGetter(instance);

        public static void SetActions(object instance, object value) => ActionsSetter(instance, value);
        private static readonly Action<object, object> ActionsSetter = ExpressionAccessor.BuildSetter(RawMouseInputReportType, "_actions");
    }
    private abstract class MouseDeviceStatic
    {
        private static readonly Func<object, object> TagRootPointGetter = ExpressionAccessor.BuildGetter(typeof(MouseDevice), "_tagRootPoint");
        public static object GetTagRootPoint(object instance) => TagRootPointGetter(instance);
    }
}
