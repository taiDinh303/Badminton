using Badminton.Core.Utils;

namespace Badminton.Core.Store
{
    public enum StatusCodeHelper
    {
        [CustomName("OK")]
        OK = 200,

        [CustomName("BAD_REQUEST")]
        BadRequest = 400,

        [CustomName("UNAUTHORIZED")]
        Unauthorized = 401,

        [CustomName("FORBIDDEN")]
        Forbidden = 403,

        [CustomName("NOT_FOUND")]
        NotFound = 404,

        [CustomName("CONFLICT")]
        Conflict = 409,

        [CustomName("SERVER_ERROR")]
        ServerError = 500
    }
}
