using Avalonia.Controls;
using PropertyChanged;
using System.Diagnostics;
using System.Reflection;

namespace InfiniteTool
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    [DoNotNotify]
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            this.DataContext = this;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.CanResize = false;
        }

        public string AppInfo
        {
            get
            {
                return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ToString();
            }
        }
    }
}
