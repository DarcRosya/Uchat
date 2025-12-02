using Avalonia.Controls;
using System.Net.Mail;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using LoginFormAvalonia.Services;

namespace LoginFormAvalonia
{
    public partial class MainWindow : Window
    {
        private readonly AuthApiService _authService;

        public MainWindow()
        {
            InitializeComponent();
            _authService = new AuthApiService();
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
            string code = codeVerificationTextBox.Text;

            /* ��������� ������������ ��� � ����. ���� �Ѩ ���� -- �������, ���� ��� -- ������ ���)
            if ( �������� ��� )
            {
                invalidDataInCodeVerification.IsVisible = true;
                invalidDataInCodeVerification.Text = "Invalid code ���-�� ���. ��������� �������� ������"

                return;
            }
             */


            invalidDataInCodeVerification.IsVisible = false;

            CodeVerification.IsVisible = false;
            ResetPasswordForm.IsVisible = true;
        }

        private void ChangePasswordButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string newPassword = newPasswordTextBox.Text;
            string confirmPassword = confirmPasswordTextBox.Text;

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


        private void GoToCodeVerification_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string emailAddress = emailInRecoveryEmailTextBox.Text;

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

        private void CloseLoginFormButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
        }

        private async void LogInButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string username = usernameTextBox.Text;
            string password = passwordTextBox.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                invalidDataInHelloAgain.IsVisible = true;
                invalidDataInHelloAgain.Text = "Both fields cannot be empty";

                return;
            }

            await HandleLoginAsync(username, password);
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

                    // Запускаем чат
                    OpenChatWindow();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                invalidDataInHelloAgain.IsVisible = true;
                invalidDataInHelloAgain.Text = ex.Message.Contains("401")
                    ? "Incorrect username or password"
                    : $"Login failed: {ex.Message}";
            }
        }

        private async void CreateAccountButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string username = createUsernameTextBox.Text;
            string password = createPasswordTextBox.Text;
            string email = createEmailTextBox.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "All fields cannot be empty";

                return;
            }

            if (password.Length < 6)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "Password size must be more 6";

                return;
            }

            if (!email.EndsWith("@gmail.com"))
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "                 Invalid email address \nOnly [@gmail.com] is allowed";

                return;
            }

            if (email.Length < 16 || email.Length > 40)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "Email size must be [more 16 and less 40]";

                return;
            }

            await HandleRegisterAsync(username, email, password);
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

                    // Запускаем чат
                    OpenChatWindow();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = ex.Message.Contains("already exists")
                    ? "This username is already taken"
                    : $"Registration failed: {ex.Message}";
            }
        }

        private void OpenChatWindow()
        {
            try
            {
                var chatExePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..",
                    "Uchat", "bin", "Debug", "net9.0-windows", "Uchat.exe"
                );

                chatExePath = System.IO.Path.GetFullPath(chatExePath);

                if (System.IO.File.Exists(chatExePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = chatExePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Fallback: запускаем через dotnet run
                    var uchatProjectPath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..", "..", "..", "..",
                        "Uchat"
                    );

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "run --project Uchat.csproj",
                        WorkingDirectory = System.IO.Path.GetFullPath(uchatProjectPath),
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open chat window: {ex.Message}");
            }
        }
    }
}