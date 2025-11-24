using Avalonia.Controls;

namespace LoginFormAvalonia
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void GoToSignUpButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            loginForm.IsVisible = false;
            signUpForm.IsVisible = true;
            GoToSignUpButton.IsVisible = false;
            GoToLogInButton.IsVisible = true;
            EmailVerification.IsVisible = false;
            CodeVerification.IsVisible = false;
            ResetPasswordForm.IsVisible = false;
            GoToLogInButtonForgottenForm.IsVisible = false;
        }

        private void GoToLogInButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            loginForm.IsVisible = true;
            signUpForm.IsVisible = false;
            GoToSignUpButton.IsVisible = true;
            GoToLogInButton.IsVisible = false;
        }

        private void ForgotPasswordButton_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            loginForm.IsVisible = false;
            EmailVerification.IsVisible = true;
            GoToLogInButtonForgottenForm.IsVisible = true;
        }

        private void GoToLogInButtonForgottenForm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            loginForm.IsVisible = true;
            EmailVerification.IsVisible = false;
            CodeVerification.IsVisible = false;
            ResetPasswordForm.IsVisible = false;
            GoToLogInButtonForgottenForm.IsVisible = false;
        }

        private void BackToEmailForm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CodeVerification.IsVisible = false;
            EmailVerification.IsVisible = true;
        }

        private void GoToResetPasswordForm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CodeVerification.IsVisible = false;
            ResetPasswordForm.IsVisible = true;
        }

        private void ChangePasswordButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            loginForm.IsVisible = true;
            ResetPasswordForm.IsVisible = false;
            GoToLogInButtonForgottenForm.IsVisible = false;
        }

        private void GoToCodeVerification_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CodeVerification.IsVisible = true;
            EmailVerification.IsVisible = false;
        }
    }
}