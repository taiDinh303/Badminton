using BadmintonBooking.Core.Utils;

namespace BadmintonBooking.Core.Constants
{
    public enum StatusCodeHelper
    {
        [CustomName("Success")]
        OK = 200,

        [CustomName("Bad Request")]
        BadRequest = 400,

        [CustomName("Not Found")]
        NotFound = 404,

        [CustomName("Unauthorized")]
        Unauthorized = 401,

        [CustomName("Internal Server Error")]
        ServerError = 500
    }
}
