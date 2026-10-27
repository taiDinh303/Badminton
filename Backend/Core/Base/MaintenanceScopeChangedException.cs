using Badminton.Core.Store;

namespace Badminton.Core.Base;

public sealed class MaintenanceScopeChangedException()
    : BaseException(ResponseCodeConstants.DUPLICATE, "Đơn không còn thuộc phạm vi bảo trì này.", (int)StatusCodeHelper.Conflict)
{
}
