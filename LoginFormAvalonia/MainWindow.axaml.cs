using Avalonia.Controls;
using System.Net.Mail;

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
            string code = codeVerificationTextBox.Text;

            /* œ–Œ¬≈–»“‹ Œ“œ–¿¬À≈ÕÕ€…  Œƒ — ›“»Ã. ≈—À» ¬—® ÕŒ–Ã -- œ”—“»“‹, ≈—À» Õ≈“ -- «Õ¿◊»“ Õ≈“)
            if ( ÌÂ‚ÂÌ˚È ÍÓ‰ )
            {
                invalidDataInCodeVerification.IsVisible = true;
                invalidDataInCodeVerification.Text = "Invalid code ˜ÚÓ-ÚÓ Ú‡Ï. œ–»ƒ”Ã¿“‹ Õ¿«¬¿Õ»≈ Œÿ»¡ »"

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

        private void LogInButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string username = usernameTextBox.Text;
            string password = passwordTextBox.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                invalidDataInHelloAgain.IsVisible = true;
                invalidDataInHelloAgain.Text = "Both fields cannot be empty";

                return;
            }

            /*
              Õ”∆ÕŒ —ƒ≈À¿“‹ œ–Œ¬≈– ” Õ¿ —ŒŒ“¬≈“—“¬»≈ ƒ¿ÕÕ€’ » «¿œ»’Õ”“‹ “Œ, ◊“Œ Õ¿’Œƒ»“‹—ﬂ œŒƒ IF
            if ( ÂÒÎË ÌÂÚ ÒÓÓÚ‚ÂÚÒÚ‚ËÈ )
            {
                invalidDataInHelloAgain.Text = "Incorrect username or password";

                return;
            }
             */

            invalidDataInHelloAgain.IsVisible = false;
        }

        private void CreateAccountButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

            /*
            if ( ÒÛ˘ÂÒÚ‚ÛÂÚ ÔÓÎ¸ÁÓ‚‡ÚÂÎ¸ Ò Ú‡ÍËÏ ÌËÍÓÏ )
            {
                invalidDataInCreateAccount.IsVisible = true;
                invalidDataInCreateAccount.Text = "This username is already taken";

                return;
            }
             */

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

            invalidDataInCreateAccount.IsVisible = false;
        }
    }
}