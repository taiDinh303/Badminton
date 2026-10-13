using M.Core.Utils;

namespace M.Core.Store
{
    /// <summary>
    /// Standard HTTP-like status codes surfaced through <c>BaseResponse&lt;T&gt;</c>.
    /// Abstraction: every layer returns this instead of raw ints, so the API
    /// layer (SERVER) is the only place that maps it to real HTTP responses.
    /// </summary>
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
