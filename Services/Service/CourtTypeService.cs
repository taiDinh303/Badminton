using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.CourtType;

namespace Services.Service
{
    public class CourtTypeService : ICourtTypeService
    {
        private readonly IUOW _uow;

        public CourtTypeService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<List<CourtTypeResponse>> GetAllAsync()
        {
            var courtTypes = await _uow.CourtTypes.GetAllAsync();

            return courtTypes
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<CourtTypeResponse?> GetByIdAsync(int id)
        {
            var courtType = await _uow.CourtTypes.GetByIdAsync(id);

            if (courtType == null)
                return null;

            return MapToResponse(courtType);
        }

        public async Task<CourtTypeResponse> CreateAsync(
            CourtTypeRequest request)
        {
            var courtType = new CourtType
            {
                Name = request.Name,
                Description = request.Description,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _uow.CourtTypes.CreateAsync(courtType);

            return MapToResponse(result);
        }

        public async Task<CourtTypeResponse?> UpdateAsync(
            int id,
            CourtTypeRequest request)
        {
            var courtType = await _uow.CourtTypes.GetByIdAsync(id);

            if (courtType == null)
                return null;

            courtType.Name = request.Name;
            courtType.Description = request.Description;
            courtType.IsActive = request.IsActive;

            var result = await _uow.CourtTypes.UpdateAsync(courtType);

            if (result == null)
                return null;

            return MapToResponse(result);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _uow.CourtTypes.DeleteAsync(id);
        }

        private static CourtTypeResponse MapToResponse(
            CourtType courtType)
        {
            return new CourtTypeResponse
            {
                CourtTypeId = courtType.CourtTypeId,
                Name = courtType.Name,
                Description = courtType.Description,
                IsActive = courtType.IsActive,
                CreatedAt = courtType.CreatedAt
            };
        }
    }
}