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
using System.Timers;
using Uchat.Services;
using Uchat.Shared;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private readonly AuthApiService _authService;
        private string[] systemArgs;

        public MainWindow(string[] args)
        {
            InitializeComponent();
            string[] dev = {"-local", "6000"};
            systemArgs = dev;
            _authService = new AuthApiService(systemArgs);

            // Initialize UserSession with system arguments BEFORE Loaded event
            UserSession.Instance.Initialize(systemArgs);

            Loaded += MainWindow_Loaded;


            chatLayout.LayoutUpdated += ChatLayout_LayoutUpdated;
        }

        private void ChatTicks()
        {
           LoadPendingFriendRequestsAsync();
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

        private void GoToResetPasswordForm_Click(object? sender, RoutedEventArgs e)
        {
            string code = codeVerificationTextBox.Text ?? string.Empty;

            /*
             if (code != //КОД ОТ EMAIL)
            {
                invalidDataInCodeVerification.Text = "Verification failed";
                invalidDataInCodeVerification.IsVisible = true;
                return;
            }
             */

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
                invalidDataInResetPassword.Text = "Passwords fields are required";
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
                invalidDataInRecoveryEmail.Text = "Email is required";
                return;
            }

            if (!emailAddress.EndsWith("@gmail.com"))
            {
                invalidDataInRecoveryEmail.IsVisible = true;
                invalidDataInRecoveryEmail.Text = "Invalid email address. Only [@gmail.com] is allowed";
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

        private void CreateAccountButton_Click(object? sender, RoutedEventArgs e)
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

            // ЕСЛИ ВСЕ ДАННЫЕ В НОРМЕ И МЫ ХОТИМ ПЕРЕЙТИ ПОДТВЕРЖНЕИЮ GMAIL!
            signUpForm.IsVisible = false;
            CodeVerificationSignUpForm.IsVisible = true;
            string emailAdress = email.Replace("@gmail.com", "");
            CodeVerificationSignUpFormTextBox.Watermark = $"Check email [{emailAdress}]";
        }

        private void BackToSignUp_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            signUpForm.IsVisible = true;
            CodeVerificationSignUpForm.IsVisible = false;
        }

        private async void GoToMainProgram_Click(object? sender, RoutedEventArgs e)
        {
            string username = createUsernameTextBox.Text ?? string.Empty;
            string password = createPasswordTextBox.Text ?? string.Empty;
            string email = createEmailTextBox.Text ?? string.Empty;
            string code = CodeVerificationSignUpFormTextBox.Text ?? string.Empty;

            /*
                         if (code != //КОД ОТ EMAIL)
            {
                invalidDataInCodeVerificationSignUpForm.Text = "Verification failed";
                invalidDataInCodeVerificationSignUpForm.IsVisible = true;
                return;
            }
             */

            try
            {
                await HandleRegisterAsync(username, email, password);
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
                var response = await _authService.RegisterAsync(username, email, password);

                if (response != null)
                {
                    UserSession.Instance.SetSession(response);

                    invalidDataInCodeVerificationSignUpForm.IsVisible = false;
                    CodeVerificationSignUpForm.IsVisible = false;

                    // Switch to chat view
                    SwitchToChatView(username);
                }
            }
            catch (Exception ex)
            {
                invalidDataInCodeVerificationSignUpForm.IsVisible = true;
                invalidDataInCodeVerificationSignUpForm.Text = ex.Message;
            }
        }

        private void SwitchToChatView(string username)
        {
            // Hide login program, show main chat program
            LoginProgram.IsVisible = false;
            MainProgram.IsVisible = true;
            userNameTextBlock.Text = username;
            Chat.ClientName = username;

            // Start tick-based functions
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (_, _) => ChatTicks();
            _timer.Start();
            // Initialize chat components with current session
            InitializeChatComponents();
        }

        private void ExitInfoAboutGroup_Click(object? sender, RoutedEventArgs e)
        {
            groupInfoBox.IsVisible = false;
            backgroundForGroupInfo.IsVisible = false;
        }

        private void groupTopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            groupInfoBox.IsVisible = true;
            groupInfoName.Text = groupTopBarName.Text;
            backgroundForGroupInfo.IsVisible = true;
            groupInfoNumberOfMembers.Text = groupTopBarNumberOfMembers.Text;

            e.Handled = true;
        }

        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!groupInfoBox.IsVisible || LeaveGroupAndConfirm.IsVisible || AddPersonToGroup.IsVisible)
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

                    PanelForGroupNameEdit.IsVisible = false;
                    PanelForGroupName.IsVisible = true;
                }
            }
        }
    }
}
