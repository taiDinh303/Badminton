using Badminton.Core.Store;
using Badminton.Core.Utils;

namespace Badminton.Core.Base
{
    public class BaseResponse<T>
    {
        public T? Data { get; set; }
        public string? Message { get; set; }
        public StatusCodeHelper StatusCode { get; set; }
        public string? Code { get; set; }

        public BaseResponse() { }

        public BaseResponse(StatusCodeHelper statusCode, string code, T? data, string? message)
        {
            Data = data;
            Message = message;
            StatusCode = statusCode;
            Code = code;
        }

        public BaseResponse(StatusCodeHelper statusCode, string code, T? data)
        {
            Data = data;
            StatusCode = statusCode;
            Code = code;
        }

        public BaseResponse(StatusCodeHelper statusCode, string code, string? message)
        {
            Message = message;
            StatusCode = statusCode;
            Code = code;
        }

        // ---------- Success ----------
        public static BaseResponse<T> OkResponse(T? data, string? mess = null)
        {
            return new BaseResponse<T>(StatusCodeHelper.OK, ResponseCodeConstants.SUCCESS, data, mess);
        }

        public static BaseResponse<T> OkResponse(string? mess)
        {
            return new BaseResponse<T>(StatusCodeHelper.OK, ResponseCodeConstants.SUCCESS, mess);
        }

        // ---------- 4xx ----------
        public static BaseResponse<T> BadRequestResponse(string mess) =>
            new(StatusCodeHelper.BadRequest, ResponseCodeConstants.BAD_REQUEST, mess);

        public static BaseResponse<T> UnauthorizedResponse(string mess) =>
            new(StatusCodeHelper.Unauthorized, ResponseCodeConstants.UNAUTHORIZED, mess);

        public static BaseResponse<T> ForbiddenResponse(string mess) =>
            new(StatusCodeHelper.Forbidden, ResponseCodeConstants.FORBIDDEN, mess);

        public static BaseResponse<T> NotFoundResponse(string mess) =>
            new(StatusCodeHelper.NotFound, ResponseCodeConstants.NOT_FOUND, mess);

        public static BaseResponse<T> ConflictResponse(string mess) =>
            new(StatusCodeHelper.Conflict, ResponseCodeConstants.CONFLICT, mess);

        // ---------- 5xx ----------
        public static BaseResponse<T> InternalErrorResponse(string mess) =>
            new(StatusCodeHelper.ServerError, ResponseCodeConstants.INTERNAL_SERVER_ERROR, mess);
    }
}
