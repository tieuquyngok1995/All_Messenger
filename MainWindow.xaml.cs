using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace All_Messenger
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Navigate(typeof(Pages.MessengerPage));
            TrySetMicaBackdrop();
        }

        private void TrySetMicaBackdrop()
        {
            this.SystemBackdrop = new MicaBackdrop();
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                string page = item.Tag.ToString();

                switch (page)
                {
                    case "MessengerPage":
                        ContentFrame.Navigate(typeof(Pages.MessengerPage));
                        break;

                    case "ZaloPage":
                        ContentFrame.Navigate(typeof(Pages.ZaloPage));
                        break;

                    case "TeamsPage":
                        ContentFrame.Navigate(typeof(Pages.TeamsPage));
                        break;
                }
            }
        }
    }
}
