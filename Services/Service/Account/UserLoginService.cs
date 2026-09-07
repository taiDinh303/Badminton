//using Contract.Repositories.AuthEntity;
//using Contract.Repositories.IUnitOfWork;
//using Contract.Services.Interface;
//using Core.Base;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using static Core.Base.BaseException;

//namespace Services.Service
//{
//    public class UserLoginService : IUserLoginService
//    {
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly UserManager<ApplicationUser> _userManager;
//        private readonly IHttpContextAccessor _httpContextAccessor;

//        public UserLoginService(
//            IUnitOfWork unitOfWork,
//            UserManager<ApplicationUser> userManager,
//            IHttpContextAccessor httpContextAccessor)
//        {
//            _unitOfWork = unitOfWork;
//            _userManager = userManager;
//            _httpContextAccessor = httpContextAccessor;
//        }

//        // Check whether the user is connected to a login provider (e.g., Google).
//        public async Task<bool> IsConnectedAsync(Guid userId, string provider)
//        {
//            IGenericRepository<ApplicationUserLogin> loginRepo = _unitOfWork.GetRepository<ApplicationUserLogin>();

//            return await loginRepo.Entities
//                .AnyAsync(l => l.UserId == userId && l.LoginProvider == provider);
//        }

//        // Retrieve all login providers linked to the user.
//        public async Task<IReadOnlyList<ApplicationUserLogin>> GetAllByUserAsync(Guid userId)
//        {
//            IGenericRepository<ApplicationUserLogin> loginRepo = _unitOfWork.GetRepository<ApplicationUserLogin>();

//            var logins = await loginRepo.Entities
//                .Where(l => l.UserId == userId)
//                .ToListAsync();

//            return logins.AsReadOnly();
//        }

//        // Link a Google account (or another login provider).
//        public async Task ConnectAsync(Guid userId, string provider, string providerKey, string? displayName = null)
//        {
//            var user = await _userManager.FindByIdAsync(userId.ToString())
//                ?? throw new ErrorException(
//                    StatusCodes.Status404NotFound,
//                    ResponseCodeConstants.NOT_FOUND,
//                    "User not found"
//                );

//            bool alreadyLinked = await IsConnectedAsync(userId, provider);
//            if (alreadyLinked)
//                throw new ErrorException(
//                    StatusCodes.Status400BadRequest,
//                    ResponseCodeConstants.DUPLICATE,
//                    $"{provider} is already connected."
//                );

//            IGenericRepository<ApplicationUserLogin> loginRepo = _unitOfWork.GetRepository<ApplicationUserLogin>();

//            ApplicationUserLogin newLogin = new ApplicationUserLogin
//            {
//                LoginProvider = provider,
//                ProviderKey = providerKey,
//                ProviderDisplayName = displayName ?? provider,
//                UserId = userId,
//            };

//            await loginRepo.InsertAsync(newLogin);
//            await _unitOfWork.SaveAsync();
//        }


//        // Need to update*****
//        // Unlink a connected account (e.g., Google).
//        public async Task DisconnectAsync(Guid userId, string provider)
//        {
//            IGenericRepository<ApplicationUserLogin> loginRepo = _unitOfWork.GetRepository<ApplicationUserLogin>();

//            ApplicationUserLogin login = await loginRepo.Entities
//                .FirstOrDefaultAsync(l => l.UserId == userId && l.LoginProvider == provider)
//                ?? throw new ErrorException(
//                    StatusCodes.Status404NotFound,
//                    ResponseCodeConstants.NOT_FOUND,
//                    $"{provider} connection was not found."
//                );

//            await loginRepo.DeleteAsync(login);
//            await _unitOfWork.SaveAsync();
//        }

//    }
//}
