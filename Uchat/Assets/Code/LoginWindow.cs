using Avalonia.Controls;

namespace Uchat
{
    public partial class MainWindow : Window
    {
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

            /* ПРОВЕРИТЬ ОТПРАВЛЕННЫЙ КОД С ЭТИМ. ЕСЛИ ВСЁ НОРМ -- ПУСТИТЬ, ЕСЛИ НЕТ -- ЗНАЧИТ НЕТ)
            if ( неверный код )
            {
                invalidDataInCodeVerification.IsVisible = true;
                invalidDataInCodeVerification.Text = "Invalid code что-то там. ПРИДУМАТЬ НАЗВАНИЕ ОШИБКИ"

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
                invalidDataInResetPassword.Text = "Fields cannot be empty";

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
                invalidDataInRecoveryEmail.Text = "Email cannot be empty";

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
                invalidDataInRecoveryEmail.Text = "Email size must be from 6 to 30";

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
                invalidDataInHelloAgain.Text = "Fields cannot be empty";

                return;
            }

            /*
              НУЖНО СДЕЛАТЬ ПРОВЕРКУ НА СООТВЕТСТВИЕ ДАННЫХ И ЗАПИХНУТЬ ТО, ЧТО НАХОДИТЬСЯ ПОД IF
            if ( если нет соответствий )
            {
                invalidDataInHelloAgain.Text = "Incorrect username or password";

                return;
            }
             */

            invalidDataInHelloAgain.IsVisible = false;
            LoginProgram.IsVisible = false;
            MainProgram.IsVisible = true;
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
            if ( существует пользователь с таким ником )
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
                invalidDataInCreateAccount.Text = "Email size must be from 6 to 30";

                return;
            }

            invalidDataInCreateAccount.IsVisible = false;
            LoginProgram.IsVisible = false;
            MainProgram.IsVisible = true;
        }
    }
}
