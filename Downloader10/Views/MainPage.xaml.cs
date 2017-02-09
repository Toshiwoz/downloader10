using Downloader10.ViewModels;
using UWC;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Downloader10.Views
{
    public sealed partial class MainPage : Page
    {
        CrawlerMain CRLWLR = new CrawlerMain(4, false);

        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            gvDatos.ItemsSource = CRLWLR.PagesCrawled;
        }

        private void btStart_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                tbStartURL.Text = UWC.Classes.CrawlerPage.Sanitize(tbStartURL.Text).AbsoluteUri;
                CRLWLR.Start(tbStartURL.Text, 2);
            }
            catch { }
        }

        private void btStop_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            CRLWLR.Stop();
        }
    }
}
