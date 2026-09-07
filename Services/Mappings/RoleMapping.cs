using Contract.Repositories.Entity;
using Core.Utils;
using ModelViews.RoleModelView;

namespace Services.Mappings
{
    public static class RoleMapping
    {
        // Map from ApplicationRole -> RoleReponseModelView
        public static RoleResponseModelView ToViewModel(this ApplicationRole role)
        {
            return new RoleResponseModelView
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                NormalizedName = role.NormalizedName ?? string.Empty,
                Description = role.Description ?? string.Empty,
                CreatedBy = role.CreatedBy,
                CreatedTime = role.CreatedTime,
                LastUpdatedTime = role.LastUpdatedTime
            };
        }

        // Map list ApplicationRole -> List<RoleReponseModelView>
        public static List<RoleResponseModelView> ToViewModelList(this IEnumerable<ApplicationRole> roles)
        {
            return roles.Select(r => r.ToViewModel()).ToList();
        }

        // Map from CreateRoleModelView -> ApplicationRole
        public static ApplicationRole ToEntity(this CreateRoleModelView model)
        {
            return new ApplicationRole
            {
                Name = model.Name,
                NormalizedName = model.Name?.ToUpper(),
                Description = model.Description,
                CreatedBy = "System",
                CreatedTime = CoreHelper.SystemTimeNow,
                LastUpdatedBy = "System",
                LastUpdatedTime = CoreHelper.SystemTimeNow
            };
        }

        // Map from UpdateRoleModelView -> AppRoles (apply changes)
        public static void UpdateEntity(this ApplicationRole role, UpdateRoleModelView model)
        {
            if (!string.IsNullOrEmpty(model.Name))
            {
                role.Name = model.Name;
                role.NormalizedName = model.Name.ToUpper();
            }

            if (!string.IsNullOrEmpty(model.Description))
            {
                role.Description = model.Description;
            }

            role.LastUpdatedBy = "System"; // hoặc lấy từ HttpContext
            role.LastUpdatedTime = CoreHelper.SystemTimeNow;
        }
    }
}
