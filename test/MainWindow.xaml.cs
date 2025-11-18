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

namespace test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoginForm.Visibility = Visibility.Visible;
            SignUpForm.Visibility = Visibility.Collapsed;
        }
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LogInButton_Click(object sender, RoutedEventArgs e)
        {
            string username = loginUsername.Text;
            string password = loginPassword.Password;

            if (username.Length == 0 || password.Length == 0)
            {
                MessageBox.Show("Поля не должны быть пустыми!");
                return;
            }
        }

        private void ButtonToSignUp_Click(object sender, RoutedEventArgs e)
        {
            SignUpForm.Visibility = Visibility.Visible;
            LoginForm.Visibility = Visibility.Collapsed;
        }

        private void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            string username = signUpUsername.Text;
            string password = signUpPassword.Password;
            string confirmPassword = signUpConfirmPassword.Password;

            if (username.Length == 0 || password.Length == 0 || confirmPassword.Length == 0)
            {
                MessageBox.Show("Поля не должны быть пустыми!");
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Пароли должны быть одинаковыми!");
                return;
            }
        }

        private void BackToLoginFormButton_Click(object sender, RoutedEventArgs e)
        {
            LoginForm.Visibility = Visibility.Visible;
            SignUpForm.Visibility = Visibility.Collapsed;
        }
    }
}