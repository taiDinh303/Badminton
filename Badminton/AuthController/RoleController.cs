using Contract.Services.Interface;
using Core.Base;
using Core.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.RoleModelView;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }



        /// <summary>
        /// Retrieve all roles with pagination
        /// </summary>
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAll(int pageNumber = 1, int pageSize = 10)
        {
            BasePaginatedList<RoleResponseModelView> result = await _roleService.GetAllAsync(pageNumber, pageSize);
            return Ok(new BaseResponse<BasePaginatedList<RoleResponseModelView>>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result
            ));
        }


        /// <summary>
        /// Retrieve a role by its ID
        /// </summary>
        [HttpGet("get-by-id/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            RoleResponseModelView result = await _roleService.GetByIdAsync(id);
            return Ok(new BaseResponse<RoleResponseModelView>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result
            ));
        }
        /// <summary>
        /// Creates a new role
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateRoleModelView model)
        {
            await _roleService.CreateAsync(model);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Role created successfully!"
            ));
        }

        /// <summary>
        /// Updates an existing role
        /// </summary>
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateRoleModelView model)
        {
            await _roleService.UpdateAsync(model);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Role updated successfully!"
            ));
        }



        /// <summary>
        /// Soft deletes a role
        /// </summary>
        [HttpDelete("soft-delete/{id}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            await _roleService.SoftDeleteAsync(id);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Role deleted successfully!"
            ));
        }

        /// <summary>
        /// Permanently deletes a role
        /// </summary>
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _roleService.DeleteAsync(id);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Role hard deleted successfully!"
            ));
        }
    }
}
