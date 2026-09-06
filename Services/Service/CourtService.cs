using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.Court;

namespace Services.Service
{
    public class CourtService : ICourtService
    {
        private readonly IUOW _uow;

        public CourtService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<List<CourtResponse>> GetAllAsync()
        {
            var courts = await _uow.Courts.GetAllAsync();

            return courts.Select(MapToResponse).ToList();
        }

        public async Task<CourtResponse?> GetByIdAsync(int courtId)
        {
            var court = await _uow.Courts.GetByIdAsync(courtId);

            if (court == null)
                return null;

            return MapToResponse(court);
        }

        private static CourtResponse MapToResponse(
            Contract.Repositories.Entity.Court court)
        {
            return new CourtResponse
            {
                CourtId = court.CourtId,
                CourtTypeId = court.CourtTypeId,
                CourtCode = court.CourtCode,
                CourtName = court.CourtName,
                Description = court.Description,
                Location = court.Location,
                PricePerHour = court.PricePerHour,
                Status = court.Status,
                ImageUrl = court.ImageUrl
            };
        }
    }
}