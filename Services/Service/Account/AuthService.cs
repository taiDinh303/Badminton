//using Contract.Repositories.AuthEntity;
//using Contract.Repositories.IUnitOfWork;
//using Contract.Services.Interface;
//using Core.Base;
//using Google.Apis.Auth;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Configuration;
//using Microsoft.IdentityModel.Tokens;
//using ModelViews.AuthModelView;
//using ModelViews.UserInfoModelView;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;
//using System.Text;
//using static Core.Base.BaseException;

//namespace Services.Service.Account
//{
//    public class AuthService : IAuthService
//    {
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly UserManager<ApplicationUser> _userManager;
//        private readonly SignInManager<ApplicationUser> _signInManager;
//        private readonly IConfiguration _configuration;
//        private readonly IUserInfoService _userInfoService;
//        private readonly IEmailService _emailService;
//        private readonly IOtpService _otpService;

//        public AuthService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration, IUserInfoService userInfoService, IEmailService emailService, IOtpService otpService)
//        {
//            _unitOfWork = unitOfWork;
//            _userManager = userManager;
//            _signInManager = signInManager;
//            _configuration = configuration;
//            _userInfoService = userInfoService;
//            _emailService = emailService;
//            _otpService = otpService;
//        }

//        #region Implementation Interface
//        //Login
//        public async Task<AuthResponseModelView> LoginAsync(LoginModelView loginModelView)
//        {
//            ApplicationUser? user = await _userManager.Users
//                .Include(x => x.UserInfo)
//                .FirstOrDefaultAsync(x => x.UserName == loginModelView.Username)
//                ?? throw new ErrorException(
//                    StatusCodes.Status404NotFound,
//                    ResponseCodeConstants.NOT_FOUND,
//                    "User is not found."
//                );

//            if (user.DeletedTime.HasValue)
//            {
//                throw new ErrorException(
//                    StatusCodes.Status403Forbidden,
//                    ResponseCodeConstants.FORBIDDEN,
//                    "User account has been deactivated."
//                );
//            }

//            //User / Password / remember / lockoutOnFailure
//            SignInResult result = await _signInManager.PasswordSignInAsync(user, loginModelView.Password, loginModelView.RememberMe, false);

//            if (!result.Succeeded)
//            {
//                throw new ErrorException(
//                    StatusCodes.Status401Unauthorized,
//                    ResponseCodeConstants.UNAUTHORIZED,
//                    "UserName or Password is incorrect."
//                );
//            }


//            // ✅ Create expiration
//            int expireMinutes = loginModelView.RememberMe
//                ? int.Parse(_configuration["Jwtsettings:RememberMeExpirationMinutes"]!)
//                : int.Parse(_configuration["Jwtsettings:ExpirationMinutes"]!);


//            DateTime expires = DateTime.UtcNow.AddMinutes(expireMinutes);

//            // ✅ Create claim
//            List<Claim> claims = await GenerateClaims(user);

//            // ✅ Create JWT token
//            string token = GenerateJwtToken(claims, expires);

//            return new AuthResponseModelView
//            {
//                Token = token,
//                ExpiredAt = expires, // re-using
//                UserId = user.Id,
//                UserName = loginModelView.Username,
//                Picture = user.UserInfo?.Picture,
//                FamilyName = user.UserInfo?.FamilyName,
//                GivenName = user.UserInfo?.GivenName ?? string.Empty,
//            };
//        }

//        // Register
//        public async Task RegisterAsync(RegisterModelView model)
//        {
//            // Validate password
//            if (model.Password != model.ConfirmPassword)
//                throw new ErrorException(400, "PASSWORD_MISMATCH", "Passwords do not match");

//            //// Validate OTP
//            bool isValidOtp = await _otpService.ValidateAsync(model.Email, model.ConfirmationCode);
//            if (!isValidOtp)
//                throw new ErrorException(400, "INVALID_OTP", "Invalid or expired confirmation code");

//            var user = new ApplicationUser
//            {
//                UserName = model.Username,
//                Email = model.Email,
//                EmailConfirmed = true
//            };

//            // Create identity user
//            var createResult = await _userManager.CreateAsync(user, model.Password);
//            if (!createResult.Succeeded)
//            {
//                string error = createResult.Errors.FirstOrDefault()?.Description
//                               ?? "Unknown error occurred";
//                throw new ErrorException(400, "INVALID_INPUT", error);
//            }

//            // Add default role
//            await _userManager.AddToRoleAsync(user, "User");

//            // Create profile
//            var defaultUserInfo = new CreateUserInfoModelView
//            {
//                UserId = user.Id,
//                GivenName = model.Username,
//                BirthDate = DateTime.UtcNow,
//                Gender = GenderType.RatherNotSay
//            };

//            await _userInfoService.CreateAsync(defaultUserInfo);
//        }


//        public async Task SendEmailConfirmationAsync(string email)
//        {
//            var code = _otpService.GenerateConfirmationCode();
//            var expiration = DateTime.UtcNow.AddMinutes(10);
//            // Create OTP
//            await _otpService.StoreAsync(email, code, expiration);
//            // Send email confirmation
//            await _emailService.SendVerificationCodeAsync(email, code);
//        }


//        public async Task<AuthResponseModelView> LoginWithGoogle(string idToken)
//        {
//            // 1️⃣ Xác thực token từ Google
//            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
//            if (payload == null || string.IsNullOrEmpty(payload.Email))
//            {
//                throw new ErrorException(
//                    StatusCodes.Status401Unauthorized,
//                    ResponseCodeConstants.UNAUTHORIZED,
//                    "Invalid Google token."
//                );
//            }

//            // 2️⃣ Tìm user theo email
//            var user = await _userManager.FindByEmailAsync(payload.Email);

//            // 3️⃣ Nếu user đã bị deactivate (soft delete) -> chặn login
//            if (user?.DeletedTime != null)
//            {
//                throw new ErrorException(
//                    StatusCodes.Status403Forbidden,
//                    ResponseCodeConstants.FORBIDDEN,
//                    "User account has been deactivated."
//                );
//            }

//            // 4️⃣ Nếu chưa có user -> tạo mới
//            if (user == null)
//            {
//                user = new ApplicationUser
//                {
//                    Email = payload.Email,
//                    UserName = payload.Email,
//                    EmailConfirmed = true
//                };

//                var createResult = await _userManager.CreateAsync(user);
//                if (!createResult.Succeeded)
//                {
//                    string errorMsg = createResult.Errors.FirstOrDefault()?.Description ?? "Failed to create user.";
//                    throw new ErrorException(
//                        StatusCodes.Status400BadRequest,
//                        ResponseCodeConstants.INVALID_INPUT,
//                        errorMsg
//                    );
//                }
//            }
//            else if (!user.EmailConfirmed)
//            {
//                // 5️⃣ Nếu user đã có nhưng chưa xác nhận email -> xác nhận luôn
//                user.EmailConfirmed = true;
//                await _userManager.UpdateAsync(user);
//            }

//            // 6️⃣ Liên kết tài khoản Google (nếu chưa có)
//            var info = new UserLoginInfo("Google", payload.Subject, "Google");
//            var logins = await _userManager.GetLoginsAsync(user);
//            if (!logins.Any(l => l.LoginProvider == info.LoginProvider && l.ProviderKey == info.ProviderKey))
//            {
//                await _userManager.AddLoginAsync(user, info);
//            }

//            // 7️⃣ Kiểm tra và thêm/cập nhật UserInfo
//            var userInfoRepo = _unitOfWork.GetRepository<UserInfo>();
//            UserInfo? userInfo = await userInfoRepo.Entities
//                .Where(x => x.Id == user.Id && !x.DeletedTime.HasValue)
//                .FirstOrDefaultAsync();

//            if (userInfo == null)
//            {
//                userInfo = new UserInfo
//                {
//                    Id = user.Id,
//                    GivenName = payload.GivenName ?? "Unknown",
//                    FamilyName = payload.FamilyName,
//                    Picture = payload.Picture,
//                    Gender = GenderType.RatherNotSay
//                };
//                await userInfoRepo.InsertAsync(userInfo);
//            }
//            else
//            {
//                bool isChanged = false;
//                if (payload.GivenName != null && userInfo.GivenName != payload.GivenName)
//                {
//                    userInfo.GivenName = payload.GivenName;
//                    isChanged = true;
//                }
//                if (payload.FamilyName != null && userInfo.FamilyName != payload.FamilyName)
//                {
//                    userInfo.FamilyName = payload.FamilyName;
//                    isChanged = true;
//                }
//                if (payload.Picture != null && userInfo.Picture != payload.Picture)
//                {
//                    userInfo.Picture = payload.Picture;
//                    isChanged = true;
//                }

//                if (isChanged)
//                    userInfoRepo.Update(userInfo);
//            }

//            await _unitOfWork.SaveAsync();

//            // 8️⃣ Sinh token JWT
//            int expireMinutes = int.Parse(_configuration["Jwtsettings:ExpirationMinutes"]!);
//            DateTime expires = DateTime.UtcNow.AddMinutes(expireMinutes);

//            List<Claim> claims = await GenerateClaims(user);
//            string token = GenerateJwtToken(claims, expires);

//            // 9️⃣ Trả về AuthResponseModelView (đầy đủ thông tin)
//            return new AuthResponseModelView
//            {
//                Token = token,
//                ExpiredAt = expires,
//                UserId = user.Id,
//                UserName = user.Email,
//                GivenName = userInfo.GivenName,
//                FamilyName = userInfo.FamilyName,
//                Picture = userInfo.Picture,
//                BirthDate = userInfo.BirthDate,
//                Gender = userInfo.Gender
//            };
//        }

//        public async Task<bool> VerifyPassword(VerifyPasswordModelView model)
//        {
//            // find user by id
//            var user = await _userManager.FindByIdAsync(model.UserId.ToString())
//                ?? throw new ErrorException(
//                    StatusCodes.Status404NotFound,
//                    ResponseCodeConstants.NOT_FOUND,
//                    "User not found."
//                );

//            // check user is deleted
//            if (user.DeletedTime.HasValue)
//            {
//                throw new ErrorException(
//                    StatusCodes.Status403Forbidden,
//                    ResponseCodeConstants.FORBIDDEN,
//                    "User account has been deactivated."
//                );
//            }

//            bool isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);

//            if (!isPasswordValid)
//            {
//                throw new ErrorException(
//                    StatusCodes.Status401Unauthorized,
//                    ResponseCodeConstants.UNAUTHORIZED,
//                    "Password is incorrect."
//                );
//            }

//            return true;
//        }


//        // Send a forgot-password email to a user
//        public async Task SendForgotPasswordLinkAsync(string email)
//        {
//            var user = await _userManager.FindByEmailAsync(email)
//                ?? throw new ErrorException(
//                    StatusCodes.Status404NotFound,
//                    ResponseCodeConstants.NOT_FOUND,
//                    "User not found"
//                );

//            // Create password reset token
//            string token = await _userManager.GeneratePasswordResetTokenAsync(user);

//            // Send forgot password email
//            await _emailService.SendForgotPasswordLinkAsync(user, token);
//        }





//        #endregion

//        #region Private Service
//        private async Task<List<Claim>> GenerateClaims(ApplicationUser user)
//        {

//            var given = user.UserInfo?.GivenName ?? "";
//            var family = user.UserInfo?.FamilyName ?? "";
//            var fullName = $"{given} {family}".Trim();

//            List<Claim> claims = new List<Claim>
//            {
//                new (JwtRegisteredClaimNames.Sub, user.Id.ToString()),
//                new (JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
//                new (ClaimTypes.Name, user.UserName ?? string.Empty),
//                new (JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

//                new("fullName", fullName),
//                new("picture", user.UserInfo?.Picture ?? "")

//            };

//            // Get role
//            IList<string> roles = await _userManager.GetRolesAsync(user);
//            foreach (string role in roles)
//            {
//                claims.Add(new Claim(ClaimTypes.Role, role));
//            }

//            return claims;
//        }

//        private string GenerateJwtToken(IEnumerable<Claim> claims, DateTime expires)
//        {
//            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwtsettings:Key"]!));
//            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

//            var token = new JwtSecurityToken(
//                issuer: _configuration["Jwtsettings:Issuer"],
//                audience: _configuration["Jwtsettings:Audience"],
//                claims: claims,
//                expires: expires,
//                signingCredentials: creds
//            );

//            return new JwtSecurityTokenHandler().WriteToken(token);
//        }

//        #endregion
//    }
//}
