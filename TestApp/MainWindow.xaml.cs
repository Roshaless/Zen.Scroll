using System.Runtime.InteropServices;
using System.Windows;

namespace TestApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = $"{Title} - .NET {RuntimeEnvironment.GetSystemVersion()}";
    }
}
