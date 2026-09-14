using System.Windows;

namespace TestApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
#if NET9_0_OR_GREATER
            var uriString = "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml";
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uriString, UriKind.Absolute) });
#endif
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/TemplateStyles.xaml", UriKind.Relative) });
        }
    }

}
