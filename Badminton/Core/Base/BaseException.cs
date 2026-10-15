using M.Core.Store;

namespace M.Core.Base
{
    public abstract class BaseException : Exception
    {
        protected BaseException(string code, string message, int statusCode)
            : base(message)
        {
            Code = code;
            StatusCode = statusCode;
        }

        public string Code { get; }

        public int StatusCode { get; }
    }

    public sealed class BadRequestException : BaseException
    {
        public BadRequestException(string message)
            : base(ResponseCodeConstants.BAD_REQUEST, message, (int)StatusCodeHelper.BadRequest) { }

        public BadRequestException(string code, string message)
            : base(code, message, (int)StatusCodeHelper.BadRequest) { }
    }

    public sealed class NotFoundException : BaseException
    {
        public NotFoundException(string entity, object key)
            : base(ResponseCodeConstants.NOT_FOUND,
                   $"{entity} '{key}' not found.",
                   (int)StatusCodeHelper.NotFound) { }
    }

    public sealed class ConflictException : BaseException
    {
        public ConflictException(string message)
            : base(ResponseCodeConstants.DUPLICATE, message, (int)StatusCodeHelper.Conflict) { }
    }
}
