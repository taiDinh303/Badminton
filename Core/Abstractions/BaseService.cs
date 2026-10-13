using Core.Base;
using Core.Enums;

namespace Core.Abstractions
{
    /// <summary>
    /// Base service trừu tượng – cung cấp các phương thức dùng chung
    /// (kiểm tra quyền, truy cập user hiện tại) cho mọi service con kế thừa.
    /// Đóng gói logic bảo mật chung, tránh lặp ở mỗi service.
    /// </summary>
    public abstract class BaseService
    {
        /// <summary>Truy cập user hiện tại (ID) từ JWT.</summary>
        protected abstract string? CurrentUserId { get; }

        /// <summary>User hiện tại có phải Admin không.</summary>
        protected abstract bool IsAdmin { get; }

        /// <summary>
        /// Kiểm tra quyền: cho phép admin hoặc chủ sở hữu.
        /// Ném <see cref="ErrorException"/> khi không đủ quyền.
        /// </summary>
        protected void RequirePermission(string resourceOwnerId)
        {
            if (IsAdmin) return;

            if (string.IsNullOrEmpty(CurrentUserId) || CurrentUserId != resourceOwnerId)
            {
                throw new BaseException.ErrorException(
                    403,
                    "Forbidden",
                    "Bạn không có quyền thao tác trên tài nguyên này.");
            }
        }

        /// <summary>
        /// Lấy userId hiện tại hoặc ném lỗi 401.
        /// </summary>
        protected string RequireUser()
        {
            if (string.IsNullOrEmpty(CurrentUserId))
                throw new BaseException.ErrorException(
                    401,
                    "Unauthorized",
                    "Bạn cần đăng nhập để thực hiện thao tác này.");

            return CurrentUserId;
        }

        /// <summary>Tạo ngoại lệ NotFound dùng chung.</summary>
        protected static BaseException.ErrorException NotFound(string entity, string id) =>
            new(404, "NotFound", $"{entity} [{id}] không tồn tại hoặc đã bị xóa.");

        /// <summary>Tạo ngoại lệ BadRequest dùng chung.</summary>
        protected static BaseException.ErrorException BadRequest(string message) =>
            new(400, "BadRequest", message);
    }
}
