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
using Avalonia.Threading;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Uchat.Services;
using Uchat.Shared;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private readonly AuthApiService _authService;
        private string? _pendingEmail = null;
        private string? _pendingResetEmail = null;
        private string? _pendingUsername = null;
        private string? _pendingPassword = null;
        private string[] systemArgs;

        public MainWindow(string[] args)
        {
            InitializeComponent();
            //string[] dev = { "-local", "6000" };
            //systemArgs = dev;
            systemArgs = args;

            _authService = new AuthApiService(systemArgs);

            // Initialize UserSession with system arguments BEFORE Loaded event
            UserSession.Instance.Initialize(systemArgs);

            Loaded += MainWindow_Loaded;


            chatLayout.LayoutUpdated += ChatLayout_LayoutUpdated;
        }

        private void ChatTicks()
        {
            LoadPendingFriendRequestsAsync();
            foreach (var contact in Chat.ChatsList)
            {
                contact.ShowUnreadMessages();
            }
        }

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            Logger.Log("=== MainWindow loaded, auto-login disabled for testing ===");

            // AUTO-LOGIN DISABLED FOR TESTING - UNCOMMENT TO RE-ENABLE:
            /*
            var restored = await UserSession.Instance.TryRestoreSessionAsync();
            
            if (restored)
            {
                Logger.Log("Session restored, switching to chat view");
                SwitchToChatView();
            }
            else
            {
                Logger.Log("No valid session, showing login form");
            }
            */
        }

        private void ChatLayout_LayoutUpdated(object? sender, EventArgs e)
        {
            NotificationBox.Margin = new Thickness(chatSplitter.Bounds.X - 180, 0, 0, 55);
        }

        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            // if (e.Key == Key.Enter)
            // {
            //     //SendMessage();
            // }
            // else if (chatTextBoxForReply.IsVisible)
            // {
            //     chatTextBoxForReply.Focus();
            //     chatTextBoxForReply.SelectionStart = chatTextBoxForReply.Text.Length;
            //     chatTextBoxForReply.SelectionEnd = chatTextBoxForReply.Text.Length;
            // }
            // else if (chatTextBoxForEdit.IsVisible)
            // {
            //     chatTextBoxForEdit.Focus();
            //     chatTextBoxForEdit.SelectionStart = chatTextBoxForEdit.Text.Length;
            //     chatTextBoxForEdit.SelectionEnd = chatTextBoxForEdit.Text.Length;
            // }
            // else chatTextBox.Focus();
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
            CodeVerificationSignUpForm.IsVisible = false;
        }

        private void GoToLogInButton_Click(object? sender, RoutedEventArgs e)
        {
            loginForm.IsVisible = true;
            signUpForm.IsVisible = false;
            GoToSignUpButton.IsVisible = true;
            GoToLogInButton.IsVisible = false;
            CodeVerificationSignUpForm.IsVisible = false;
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
            CodeVerificationSignUpForm.IsVisible = false;
        }

        private void BackToEmailForm_Click(object? sender, RoutedEventArgs e)
        {
            CodeVerification.IsVisible = false;
            EmailVerification.IsVisible = true;
        }

        private async void GoToResetPasswordForm_Click(object? sender, RoutedEventArgs e)
        {
            string code = codeVerificationTextBox.Text ?? string.Empty;

            if (string.IsNullOrEmpty(code))
            {
                invalidDataInCodeVerification.Text = "Code is required";
                invalidDataInCodeVerification.IsVisible = true;
                return;
            }
            
            // Проверяем, не потерялась ли почта
            if (string.IsNullOrEmpty(_pendingResetEmail))
            {
                invalidDataInCodeVerification.Text = "Session error. Please restart.";
                invalidDataInCodeVerification.IsVisible = true;
                return;
            }

            try
            {
                // Проверяем валидность кода на сервере
                await _authService.VerifyResetCodeAsync(_pendingResetEmail, code);

                // Если все ок - идем к смене пароля
                invalidDataInCodeVerification.IsVisible = false;
                CodeVerification.IsVisible = false;
                ResetPasswordForm.IsVisible = true;
                
                // Очищаем поля паролей
                newPasswordTextBox.Text = string.Empty;
                confirmPasswordTextBox.Text = string.Empty;
                invalidDataInResetPassword.IsVisible = false;
            }
            catch (Exception ex)
            {
                invalidDataInCodeVerification.Text = ex.Message;
                invalidDataInCodeVerification.IsVisible = true;
            }
        }

        private async void ChangePasswordButton_Click(object? sender, RoutedEventArgs e)
        {
            string newPass = newPasswordTextBox.Text ?? string.Empty;
            string confirmPass = confirmPasswordTextBox.Text ?? string.Empty;
            
            string code = codeVerificationTextBox.Text ?? string.Empty; 

            
            if (string.IsNullOrWhiteSpace(newPass))
            {
                invalidDataInResetPassword.Text = "Password cannot be empty";
                invalidDataInResetPassword.IsVisible = true;
                return;
            }

            if (newPass.Length < 6)
            {
                invalidDataInResetPassword.Text = "Password must be at least 6 characters";
                invalidDataInResetPassword.IsVisible = true;
                return;
            }

            if (newPass != confirmPass)
            {
                invalidDataInResetPassword.Text = "Passwords do not match"; 
                invalidDataInResetPassword.IsVisible = true;
                return;
            }

            try
            {
                await _authService.ResetPasswordAsync(_pendingResetEmail!, code, newPass);

                invalidDataInResetPassword.IsVisible = false;
                ResetPasswordForm.IsVisible = false;
                
                loginForm.IsVisible = true;

                _pendingResetEmail = null;
                
                // Maybe display the message “Password changed! Please log in.” ???
            }
            catch (Exception ex)
            {
                invalidDataInResetPassword.Text = ex.Message;
                invalidDataInResetPassword.IsVisible = true;
            }
        }

        private async void GoToCodeVerification_Click(object? sender, RoutedEventArgs e)
        {
            string email = emailInRecoveryEmailTextBox.Text ?? string.Empty;

            if (string.IsNullOrEmpty(email) || !email.EndsWith("@gmail.com"))
            {
                invalidDataInRecoveryEmail.IsVisible = true;
                invalidDataInRecoveryEmail.Text = "Invalid email format";
                return;
            }

            try
            {
                await _authService.ForgotPasswordAsync(email);

                _pendingResetEmail = email;
                
                invalidDataInRecoveryEmail.IsVisible = false;
                EmailVerification.IsVisible = false;
                
                CodeVerification.IsVisible = true;
                string emailDisplay = email.Replace("@gmail.com", "");
                codeVerificationTextBox.Watermark = $"Check email [{emailDisplay}]";

                codeVerificationTextBox.Text = string.Empty;
                invalidDataInCodeVerification.IsVisible = false;
            }
            catch (Exception ex)
            {
                invalidDataInRecoveryEmail.IsVisible = true;
                invalidDataInRecoveryEmail.Text = ex.Message;
            }
        }

        private void CloseLoginFormButton_Click(object? sender, RoutedEventArgs e)
        {
            _pendingEmail = null;
            _pendingUsername = null;
            _pendingPassword = null;
            this.Close();
        }

        private void RevealePasswordInLogIn_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            RevealePasswordInLogIn.IsVisible = false;
            HidePasswordInLogIn.IsVisible = true;
            passwordTextBox.PasswordChar = '\0';
        }

        private void HidePasswordInLogIn_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            RevealePasswordInLogIn.IsVisible = true;
            HidePasswordInLogIn.IsVisible = false;
            passwordTextBox.RevealPassword = false;
            passwordTextBox.PasswordChar = '*';
        }


        private void RevealePasswordInSingUp_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            RevealePasswordInSingUp.IsVisible = false;
            HidePasswordInSingUp.IsVisible = true;
            createPasswordTextBox.PasswordChar = '\0';
        }

        private void HidePasswordInSingUp_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            RevealePasswordInSingUp.IsVisible = true;
            HidePasswordInSingUp.IsVisible = false;
            createPasswordTextBox.PasswordChar = '*';
        }

        private void RevealePasswordInNewPassword_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            RevealePasswordInNewPassword.IsVisible = false;
            HidePasswordInNewPassword.IsVisible = true;
            newPasswordTextBox.PasswordChar = '\0';
        }

        private void HidePasswordInNewPassword_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            RevealePasswordInNewPassword.IsVisible = true;
            HidePasswordInNewPassword.IsVisible = false;
            newPasswordTextBox.PasswordChar = '*';
        }

        private void RevealePasswordInConfirmPassword_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            RevealePasswordInConfirmPassword.IsVisible = false;
            HidePasswordInConfirmPassword.IsVisible = true;
            confirmPasswordTextBox.PasswordChar = '\0';
        }

        private void HidePasswordInConfirmPassword_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            RevealePasswordInConfirmPassword.IsVisible = true;
            HidePasswordInConfirmPassword.IsVisible = false;
            confirmPasswordTextBox.PasswordChar = '*';
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
                    SwitchToChatView(username);
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
                invalidDataInCreateAccount.HorizontalAlignment = HorizontalAlignment.Right;
                invalidDataInCreateAccount.Text = "Username can only contain letters, numbers and underscore";
                return;
            }

            if (password.Length < 6)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "Password must be at least 6 characters";
                return;
            }

            if (!email.EndsWith("@gmail.com") || email.Length < 5)
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "Invalid email address. Only [@gmail.com] is allowed";
                return;
            }

            if (_pendingEmail == email && 
                _pendingUsername == username && 
                _pendingPassword == password) 
            {
                invalidDataInCreateAccount.IsVisible = false;
                SwitchToVerificationUI(email);
                return;
            }

            try
            {
                var regResult = await _authService.RegisterAsync(username, email, password);
                
                if (regResult != null && regResult.RequiresConfirmation)
                {
                    _pendingEmail = email;
                    _pendingUsername = username;
                    _pendingPassword = password; 
                    
                    invalidDataInCreateAccount.IsVisible = false;
                    SwitchToVerificationUI(email);
                }
                else
                {
                    SwitchToChatView(username);
                }
            }
            catch (Exception ex)
            {
                ShowCreateAccountError(ex.Message);
            }
        }

        private void BackToSignUp_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            signUpForm.IsVisible = true;
            CodeVerificationSignUpForm.IsVisible = false;
        }

        private async void GoToMainProgram_Click(object? sender, RoutedEventArgs e)
        {
            string username = createUsernameTextBox.Text ?? string.Empty;
            string code = CodeVerificationSignUpFormTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(code))
            {
                invalidDataInCodeVerificationSignUpForm.IsVisible = true;
                invalidDataInCodeVerificationSignUpForm.Text = "Code is required";
                return;
            }

            if (string.IsNullOrEmpty(_pendingEmail))
            {
                invalidDataInCodeVerificationSignUpForm.IsVisible = true;
                invalidDataInCodeVerificationSignUpForm.Text = "Session expired. Please register again.";
                return;
            }

            try
            {
                var auth = await _authService.ConfirmEmailAsync(_pendingEmail, code);
                
                if (auth != null)
                {
                    invalidDataInCodeVerificationSignUpForm.IsVisible = false;
                    CodeVerificationSignUpForm.IsVisible = false;
                    
                    _pendingEmail = null;
                    SwitchToChatView(username);
                }
            }
            catch (Exception ex)
            {
                invalidDataInCodeVerificationSignUpForm.IsVisible = true;
                invalidDataInCodeVerificationSignUpForm.Text = ex.Message;
            }
        }

        private async Task HandleRegisterAsync(string username, string email, string password)
        {
            try
            {
                var result = await _authService.RegisterAsync(username, email, password);

                if (result != null)
                {
                    if (!result.RequiresConfirmation)
                    {
                        // No confirmation required — proceed
                        invalidDataInCodeVerificationSignUpForm.IsVisible = false;
                        CodeVerificationSignUpForm.IsVisible = false;
                        SwitchToChatView(username);
                    }
                    else
                    {
                        // Show verification UI
                        signUpForm.IsVisible = false;
                        CodeVerificationSignUpForm.IsVisible = true;
                        CodeVerificationSignUpFormTextBox.Watermark = $"Check email [{email.Replace("@gmail.com", "")} ]";
                    }
                }
            }
            catch (Exception ex)
            {
                invalidDataInCodeVerificationSignUpForm.IsVisible = true;
                invalidDataInCodeVerificationSignUpForm.Text = ex.Message;
            }
        }

        private void SwitchToVerificationUI(string email)
        {
            signUpForm.IsVisible = false;
            CodeVerificationSignUpForm.IsVisible = true;
            
            string emailDisplay = email.Replace("@gmail.com", "");
            CodeVerificationSignUpFormTextBox.Watermark = $"Check email [{emailDisplay}]";
        }

        private void ShowCreateAccountError(string message)
        {
            invalidDataInCreateAccount.IsVisible = true;
            invalidDataInCreateAccount.Text = message;
        }

        private void SwitchToChatView(string username)
        {
            // Hide login program, show main chat program
            LoginProgram.IsVisible = false;
            MainProgram.IsVisible = true;
            userNameTextBlock.Text = username;
            Chat.ClientName = username;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (_, _) => ChatTicks();
            _timer.Start();

            // Initialize chat components with current session
            InitializeChatComponents();
        }
        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!groupInfoBox.IsVisible)
            {
                return;
            }

            var clickPoint = e.GetPosition(this);
            var offset = groupInfoBox.TranslatePoint(new Point(0, 0), this);

            if (offset.HasValue)
            {
                var groupRect = new Rect(offset.Value, groupInfoBox.Bounds.Size);

                if (!groupRect.Contains(clickPoint))
                {
                    groupInfoBox.IsVisible = false;
                    backgroundForGroupInfo.IsVisible = false;
                }
            }
        }
    }
}
