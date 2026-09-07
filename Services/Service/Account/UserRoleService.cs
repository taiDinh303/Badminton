using Contract.Repositories.Entity;
using Contract.Services.Interface;
using Core.Base;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Core.Base.BaseException;

namespace Services.Service
{
    public class UserRoleService : IUserRoleService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public UserRoleService(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task AddRoleToUserAsync(Guid userId, Guid roleId)
        {
            // find user
            ApplicationUser user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == userId && !u.DeletedTime.HasValue)
                ?? throw new ErrorException(StatusCodes.Status404NotFound,
                                            ResponseCodeConstants.NOT_FOUND,
                                            "User not found");

            // find role
            ApplicationRole role = await _roleManager.Roles
                .FirstOrDefaultAsync(r => r.Id == roleId && !r.DeletedTime.HasValue)
                ?? throw new ErrorException(StatusCodes.Status404NotFound,
                                            ResponseCodeConstants.NOT_FOUND,
                                            "Role not found");

            // check duplicate
            bool alreadyInRole = await _userManager.IsInRoleAsync(user, role.Name!);
            if (alreadyInRole)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest,
                                         ResponseCodeConstants.DUPLICATE,
                                         "User already has this role");
            }

            // add
            IdentityResult result = await _userManager.AddToRoleAsync(user, role.Name!);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ErrorException(StatusCodes.Status400BadRequest,
                                         ResponseCodeConstants.FAILED,
                                         $"Failed to assign role: {errors}");
            }
        }


        public async Task RemoveRoleFromUserAsync(Guid userId, Guid roleId)
        {
            ApplicationUser user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == userId && !u.DeletedTime.HasValue)
                ?? throw new ErrorException(StatusCodes.Status404NotFound,
                                            ResponseCodeConstants.NOT_FOUND,
                                            "User not found");

            ApplicationRole role = await _roleManager.Roles
                .FirstOrDefaultAsync(r => r.Id == roleId && !r.DeletedTime.HasValue)
                ?? throw new ErrorException(StatusCodes.Status404NotFound,
                                            ResponseCodeConstants.NOT_FOUND,
                                            "Role not found");

            bool inRole = await _userManager.IsInRoleAsync(user, role.Name!);
            if (!inRole)
            {
                throw new ErrorException(StatusCodes.Status404NotFound,
                                         ResponseCodeConstants.NOT_FOUND,
                                         "User does not have this role");
            }

            IdentityResult result = await _userManager.RemoveFromRoleAsync(user, role.Name!);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ErrorException(StatusCodes.Status400BadRequest,
                                         ResponseCodeConstants.FAILED,
                                         $"Failed to remove role: {errors}");
            }
        }
    }
}
