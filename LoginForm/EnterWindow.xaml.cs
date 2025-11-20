using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LoginForm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class EnterWindow : Window
    {
        public EnterWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void GoToLogInButton_Click(object sender, RoutedEventArgs e)
        {
            loginForm.Visibility = Visibility.Visible;
            SignUpForm.Visibility = Visibility.Collapsed;
            GoToLogInButton.Visibility = Visibility.Collapsed;
            GoToSignUpButton.Visibility = Visibility.Visible;
        }

        private void GoToSignUpButton_Click(object sender, RoutedEventArgs e)
        {
            SignUpForm.Visibility = Visibility.Visible;
            loginForm.Visibility = Visibility.Collapsed;
            GoToLogInButton.Visibility = Visibility.Visible;
            GoToSignUpButton.Visibility = Visibility.Collapsed;
        }

        private void ForgotPasswordButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("AHAHAHAHAHAHAHHAHAHAHAHH");
        }

        private void CloseFormButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}