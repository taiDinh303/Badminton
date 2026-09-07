//using System.Text.Encodings.Web;

//namespace Services.Infrastructure
//{
//    public class EmailTemplateRenderer
//    {
//        public string RenderVerificationCode(string code)
//        {
//            return $@"
//            <p>Hello,</p>
//            <p>Your confirmation code is:</p>
//            <h2 style='color:#0d6efd'>{code}</h2>
//            <p>Please enter this code to complete your registration.</p>";
//        }

//        public string RenderChangeEmail(string username, string link, string newEmail)
//        {
//            return $@"
//            <p>Hello {username},</p>
//            <p>You requested to change your email to <strong>{newEmail}</strong>.</p>
//            <p><a href='{link}'>Click here to confirm</a></p>";
//        }

//        public string RenderSetPasswordLink(string username, string link)
//        {
//            return $@"
//            <p>Hello {HtmlEncoder.Default.Encode(username)},</p>
//            <p>Your account was created via Google Sign-In and does not yet have a password.</p>
//            <p>Please click the link below to set a new password:</p>
//            <p><a href='{link}'>Set Password</a></p>";
//        }

//        public string RenderForgotPassword(string username, string link)
//        {
//            return $@"
//            <p>Hello {HtmlEncoder.Default.Encode(username)},</p>
//            <p>We received a request to reset your password.</p>
//            <p>Please click the link below to set a new password:</p>
//            <p><a href='{link}'>Reset Password</a></p>
//            <p>If you did not request this, please ignore this email.</p>";
//        }

//        // ... Và các template khác
//    }

//}
