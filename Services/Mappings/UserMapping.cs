using Contract.Repositories.Entity;
using ModelViews.UserInfoModelView;
using ModelViews.UserModelView;

namespace Services.Mappings
{
    public static class UserMapping
    {
        // 🔹 Mapping ApplicationUser → UserResponseModelView
        public static UserResponseModelView ToViewModel(this ApplicationUser user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var model = new UserResponseModelView
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                EmailConfirmed = user.EmailConfirmed,
                //Check Password
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                // Audit info
                CreatedBy = user.CreatedBy,
                CreatedTime = user.CreatedTime,
                LastUpdatedBy = user.LastUpdatedBy,
                LastUpdatedTime = user.LastUpdatedTime,

            };

            return model;
        }

        // 🔹 Mapping List<ApplicationUser> → List<UserResponseModelView>
        public static List<UserResponseModelView> ToViewModelList(this IEnumerable<ApplicationUser> users)
        {
            if (users == null)
                return new List<UserResponseModelView>();

            // ✅ Bỏ qua user null hoặc user.UserInfo null
            return users
                .Where(u => u != null)
                .Select(u => u.ToViewModel())
                .ToList();
        }

        // 🔹 Mapping UserResponseModelView → ApplicationUser
        public static ApplicationUser ToEntity(this UserResponseModelView model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var entity = new ApplicationUser
            {
                Id = model.Id,
                UserName = model.UserName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = model.EmailConfirmed,
                // Audit info
                CreatedBy = model.CreatedBy,
                CreatedTime = model.CreatedTime,
                LastUpdatedBy = model.LastUpdatedBy,
                LastUpdatedTime = model.LastUpdatedTime,
            };

            return entity;
        }

        // Mapping CreateUserModelView -> ApplicationUser
        public static ApplicationUser ToEntity(this CreateUserModelView model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            return new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                // Audit info
                CreatedBy = model.Username,
            };
        }

        // Mapping UpdateUserModelView -> ApplicationUser
        public static void ToEntity(this UpdateUserModelView model, ApplicationUser user)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
        }
    }
}
