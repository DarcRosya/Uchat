using Avalonia.Controls;
using System.Net.Mail;
using System;
using System.IO;
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
            string code = codeVerificationTextBox.Text ?? string.Empty;
            invalidDataInCodeVerification.IsVisible = false;
            CodeVerification.IsVisible = false;
            ResetPasswordForm.IsVisible = true;
        }

        private void ChangePasswordButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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


        private void GoToCodeVerification_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

        private void CloseLoginFormButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
        }

        private async void LogInButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

                    OpenChatWindow();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                invalidDataInHelloAgain.IsVisible = true;
                invalidDataInHelloAgain.Text = ex.Message;
            }
        }

        private async void CreateAccountButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

                    OpenChatWindow();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = ex.Message;
            }
        }

        private void OpenChatWindow()
        {
            try
            {
                var session = UserSession.Instance;
                var args = $"--token \"{session.AccessToken}\" --username \"{session.Username}\" --userId {session.UserId}";

                // Try to find Uchat.exe
                var uchatExe = FindUchatExecutable();

                if (!string.IsNullOrEmpty(uchatExe))
                {
                    System.Diagnostics.Debug.WriteLine($"Launching Uchat.exe from: {uchatExe}");
                    System.Diagnostics.Debug.WriteLine($"Arguments: {args}");
                    
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = uchatExe,
                        Arguments = args,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(uchatExe)
                    });
                    
                    System.Diagnostics.Debug.WriteLine($"Process started: {process != null}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Uchat.exe not found, trying dotnet run");
                    
                    var uchatProjectDir = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..", "..", "..", "..", "Uchat"
                    );
                    
                    var fullProjectDir = Path.GetFullPath(uchatProjectDir);
                    System.Diagnostics.Debug.WriteLine($"Project directory: {fullProjectDir}");
                    
                    if (Directory.Exists(fullProjectDir))
                    {
                        var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"run -- {args}",
                            UseShellExecute = true,
                            WorkingDirectory = fullProjectDir
                        });
                        
                        System.Diagnostics.Debug.WriteLine($"Process started: {process != null}");
                    }
                    else
                    {
                        throw new DirectoryNotFoundException($"Uchat project directory not found: {fullProjectDir}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error launching chat: {ex}");
                invalidDataInHelloAgain.IsVisible = true;
                invalidDataInHelloAgain.Text = $"Failed to launch chat: {ex.Message}";
            }
        }

        private string FindUchatExecutable()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            System.Diagnostics.Debug.WriteLine($"Base directory: {baseDir}");
            
            var possiblePaths = new[]
            {
                // Same bin folder (when both projects built)
                Path.Combine(baseDir, "Uchat.exe"),
                // Relative to LoginFormAvalonia bin
                Path.Combine(baseDir, "..", "..", "..", "Uchat", "bin", "Debug", "net9.0", "Uchat.exe"),
                // From project root
                Path.Combine(baseDir, "..", "..", "..", "..", "Uchat", "bin", "Debug", "net9.0", "Uchat.exe"),
                // Release build
                Path.Combine(baseDir, "..", "..", "..", "..", "Uchat", "bin", "Release", "net9.0", "Uchat.exe")
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    System.Diagnostics.Debug.WriteLine($"Checking: {fullPath}");
                    
                    if (File.Exists(fullPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Found: {fullPath}");
                        return fullPath;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking path {path}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine("Uchat.exe not found in any expected location");
            return string.Empty;
        }
    }
}