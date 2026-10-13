using M.Core.Store;

namespace M.Core.Base
{
    /// <summary>
    /// ABSTRACTION (trừu tượng) + INHERITANCE (kế thừa): the root of every
    /// domain exception. It exposes <see cref="StatusCode"/> + <see cref="Code"/>
    /// so the API-layer ExceptionMiddleware can translate ANY subclass into a
    /// consistent <c>BaseResponse</c> without pattern-matching on concrete
    /// types. Subclasses only change the status; the translation stays in ONE
    /// place.
    /// </summary>
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

    /// <summary>400 – invalid input / business rule violation.</summary>
    public sealed class BadRequestException : BaseException
    {
        public BadRequestException(string message)
            : base(ResponseCodeConstants.BAD_REQUEST, message, (int)StatusCodeHelper.BadRequest) { }

        public BadRequestException(string code, string message)
            : base(code, message, (int)StatusCodeHelper.BadRequest) { }
    }

    /// <summary>404 – an entity was not found.</summary>
    public sealed class NotFoundException : BaseException
    {
        public NotFoundException(string entity, object key)
            : base(ResponseCodeConstants.NOT_FOUND,
                   $"{entity} '{key}' not found.",
                   (int)StatusCodeHelper.NotFound) { }
    }

    /// <summary>409 – a conflict, e.g. a booking slot is already taken.</summary>
    public sealed class ConflictException : BaseException
    {
        public ConflictException(string message)
            : base(ResponseCodeConstants.DUPLICATE, message, (int)StatusCodeHelper.Conflict) { }
    }
}
