using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Threading;
using System.Threading.Tasks;
using Uchat.Services;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        private bool isWindowFocused = false;
        private readonly AuthApiService _authService;

        public MainWindow()
        {
            InitializeComponent();
            _authService = new AuthApiService();
        }

        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back)
            {
                if (!string.IsNullOrEmpty(chatTextBox.Text))
                {
                    chatTextBox.Text = chatTextBox.Text.Substring(0, chatTextBox.Text.Length - 1);
                }
            }
            else if (e.Key == Key.Enter)
            {
                //SendMessage();
            }
            else
            {
                chatTextBox.Focus();
            }
        }

        // Login Form Navigation Methods
        private void GoToSignUpButton_Click(object? sender, RoutedEventArgs e)
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

        private void GoToLogInButton_Click(object? sender, RoutedEventArgs e)
        {
            loginForm.IsVisible = true;
            signUpForm.IsVisible = false;
            GoToSignUpButton.IsVisible = true;
            GoToLogInButton.IsVisible = false;
        }

        private void ForgotPasswordButton_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            loginForm.IsVisible = false;
            EmailVerification.IsVisible = true;
            GoToLogInButtonForgottenForm.IsVisible = true;
        }

        private void GoToLogInButtonForgottenForm_Click(object? sender, RoutedEventArgs e)
        {
            loginForm.IsVisible = true;
            EmailVerification.IsVisible = false;
            CodeVerification.IsVisible = false;
            ResetPasswordForm.IsVisible = false;
            GoToLogInButtonForgottenForm.IsVisible = false;
        }

        private void BackToEmailForm_Click(object? sender, RoutedEventArgs e)
        {
            CodeVerification.IsVisible = false;
            EmailVerification.IsVisible = true;
        }

        private void GoToResetPasswordForm_Click(object? sender, RoutedEventArgs e)
        {
            string code = codeVerificationTextBox.Text ?? string.Empty;
            invalidDataInCodeVerification.IsVisible = false;
            CodeVerification.IsVisible = false;
            ResetPasswordForm.IsVisible = true;
        }

        private void ChangePasswordButton_Click(object? sender, RoutedEventArgs e)
        {
            string newPassword = newPasswordTextBox.Text ?? string.Empty;
            string confirmPassword = confirmPasswordTextBox.Text ?? string.Empty;

            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                invalidDataInResetPassword.IsVisible = true;
                invalidDataInResetPassword.Text = "Both fields cannot be empty";
                return;
            }

            if (newPassword != confirmPassword)
            {
                invalidDataInResetPassword.IsVisible = true;
                invalidDataInResetPassword.Text = "Passwords do not match";
                return;
            }

            invalidDataInResetPassword.IsVisible = false;
            loginForm.IsVisible = true;
            ResetPasswordForm.IsVisible = false;
            GoToLogInButtonForgottenForm.IsVisible = false;
        }

        private void GoToCodeVerification_Click(object? sender, RoutedEventArgs e)
        {
            string emailAddress = emailInRecoveryEmailTextBox.Text ?? string.Empty;

            if (string.IsNullOrEmpty(emailAddress))
            {
                invalidDataInRecoveryEmail.IsVisible = true;
                invalidDataInRecoveryEmail.Text = "Email field cannot be empty";
                return;
            }

            if (!emailAddress.EndsWith("@gmail.com"))
            {
                invalidDataInRecoveryEmail.IsVisible = true;
                invalidDataInRecoveryEmail.Text = "                 Invalid email address \nOnly [@gmail.com] is allowed";
                return;
            }

            if (emailAddress.Length < 16 || emailAddress.Length > 40)
            {
                invalidDataInRecoveryEmail.IsVisible = true;
                invalidDataInRecoveryEmail.Text = "Email size must be [more 16 and less 40]";
                return;
            }

            invalidDataInRecoveryEmail.IsVisible = false;
            CodeVerification.IsVisible = true;
            EmailVerification.IsVisible = false;
        }

        private void CloseLoginFormButton_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Authentication Methods
        private async void LogInButton_Click(object? sender, RoutedEventArgs e)
        {
            string username = usernameTextBox.Text ?? string.Empty;
            string password = passwordTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                invalidDataInHelloAgain.IsVisible = true;
                invalidDataInHelloAgain.Text = "Username and password are required";
                return;
            }

            try
            {
                invalidDataInHelloAgain.IsVisible = false;
                await HandleLoginAsync(username, password);
            }
            catch (Exception ex)
            {
                invalidDataInHelloAgain.IsVisible = true;
                invalidDataInHelloAgain.Text = ex.Message;
            }
        }

        private async Task HandleLoginAsync(string username, string password)
        {
            try
            {
                var response = await _authService.LoginAsync(username, password);

                if (response != null)
                {
                    UserSession.Instance.SetSession(response);
                    invalidDataInHelloAgain.IsVisible = false;

                    // Switch to chat view
                    SwitchToChatView();
                }
            }
            catch (Exception ex)
            {
                invalidDataInHelloAgain.IsVisible = true;
                invalidDataInHelloAgain.Text = ex.Message;
            }
        }

        private async void CreateAccountButton_Click(object? sender, RoutedEventArgs e)
        {
            string username = createUsernameTextBox.Text ?? string.Empty;
            string password = createPasswordTextBox.Text ?? string.Empty;
            string email = createEmailTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "All fields are required";
                return;
            }

            if (username.Length < 3 || username.Length > 14)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "Username must be 3-14 characters";
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "Username can only contain letters, numbers and underscore";
                return;
            }

            if (password.Length < 6)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "Password must be at least 6 characters";
                return;
            }

            if (!email.Contains("@") || email.Length < 5)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "Invalid email address";
                return;
            }

            try
            {
                await HandleRegisterAsync(username, email, password);
            }
            catch (Exception ex)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = ex.Message;
            }
        }

        private async Task HandleRegisterAsync(string username, string email, string password)
        {
            try
            {
                var response = await _authService.RegisterAsync(username, email, password);

                if (response != null)
                {
                    UserSession.Instance.SetSession(response);
                    invalidDataInCreateAccount.IsVisible = false;

                    // Switch to chat view
                    SwitchToChatView();
                }
            }
            catch (Exception ex)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = ex.Message;
            }
        }

        private void SwitchToChatView()
        {
            // Hide login program, show main chat program
            LoginProgram.IsVisible = false;
            MainProgram.IsVisible = true;

            // Initialize chat components with current session
            InitializeChatComponents();
        }
    }
}