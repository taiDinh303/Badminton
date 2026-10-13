using Contract.Repositories.Entity;
using Contract.Repositories.IUnitOfWork;
using Contract.Services.Interface;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using ModelViews.CourtModelView;
using Services.Base;

namespace Services.Service.Court
{
    /// <summary>
    /// Service quản lý sân – kế thừa <see cref="BaseService{Court}"/>.
    ///
    /// ĐẶC ĐIỂM OOP:
    ///  - Kế thừa (Inheritance): CommonCrudBase được tái sử dụng cho mọi entity.
    ///  - Đa hình (Polymorphism): CreateAsync dispatch sang IndoorCourt / OutdoorCourt
    ///    – client chỉ thấy ICourt, không cần biết lớp con.
    ///  - Đóng gói (Encapsulation): logic giá/quyền nằm trong service, entity không lộ.
    /// </summary>
    public class CourtService : BaseService<Court>, ICourtService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CourtService(
            IUnitOfWork unitOfWork,
            Microsoft.AspNetCore.Http.IHttpContextAccessor accessor)
            : base(unitOfWork, accessor)
        {
            _unitOfWork = unitOfWork;
        }

        // ================= ICourtService =================

        public async Task<List<CourtResponseModelView>> GetAllAsync()
        {
            var courts = await Repository.Entities
                .Where(c => c.DeletedTime == null)
                .OrderByDescending(c => c.CreatedTime)
                .ToListAsync();

            return courts.Select(ToViewModel).ToList();
        }

        public async Task<CourtResponseModelView?> GetByIdAsync(Guid id)
        {
            var court = await FindByIdAsync(id);
            return ToViewModel(court);
        }

        public async Task<List<CourtResponseModelView>> GetByCategoryAsync(CourtCategory category)
        {
            var courts = await Repository.Entities
                .Where(c => c.Category == category && c.DeletedTime == null)
                .ToListAsync();

            return courts.Select(ToViewModel).ToList();
        }

        public async Task<List<CourtResponseModelView>> GetAvailableAsync(DateTime date, int startHour, int endHour)
        {
            // Sân trống = sân có TimeFrame cho khung giờ này, chưa gắn Booking
            var courtIds = await _unitOfWork
                .GetRepository<TimeFrame>()
                .Entities
                .Where(tf => tf.BookingId == null
                        && tf.DeletedTime == null
                        && tf.StartTime <= startHour
                        && tf.EndTime >= endHour)
                .Select(tf => tf.CourtId)
                .Distinct()
                .ToListAsync();

            var courts = await Repository.Entities
                .Where(c => courtIds.Contains(c.Id) && c.DeletedTime == null)
                .ToListAsync();

            return courts.Select(ToViewModel).ToList();
        }

        public async Task<Guid> CreateAsync(CourtCategory category, CreateCourtModelView model)
        {
            // ĐA HÌNH: chọn đúng lớp con theo category
            Court court = category switch
            {
                CourtCategory.Indoor => new IndoorCourt
                {
                    FloorMaterial = model.FloorMaterial,
                    HasAirConditioning = model.HasAirConditioning,
                    LightLevel = model.LightLevel,
                    Description = model.Description
                },
                CourtCategory.Outdoor => new OutdoorCourt
                {
                    FloorMaterial = model.FloorMaterial,
                    HasCanopy = model.HasCanopy,
                    WeatherNote = model.WeatherNote,
                    Description = model.Description
                },
                _ => throw new Core.Base.BaseException.BadRequestException("bad_request", $"Loại sân không hợp lệ: {category}")
            };

            court.CourtName = model.CourtName;
            court.Category = category;
            court.OpenTime = model.OpenTime;
            court.CloseTime = model.CloseTime;
            court.UnitPrice = model.UnitPrice;
            court.CreatedBy = CurrentUserId;

            await Repository.InsertAsync(court);
            await _unitOfWork.SaveAsync();
            return court.Id;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateCourtModelView model)
        {
            var court = await FindByIdAsync(id);
            court.CourtName = model.CourtName;
            court.OpenTime = model.OpenTime;
            court.CloseTime = model.CloseTime;
            court.UnitPrice = model.UnitPrice;
            court.Description = model.Description;
            UpdateAuditFields(court);

            await Repository.UpdateAsync(court);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<bool> SoftDeleteAsync(Guid id)
        {
            var court = await FindByIdAsync(id);
            MarkDeleted(court);
            await Repository.UpdateAsync(court);
            await _unitOfWork.SaveAsync();
            return true;
        }

        // ================= Mapping (đóng gói) =================

        /// <summary>
        /// Map entity đa hình (Indoor/Outdoor) sang DTO duy nhất – client không cần biết lớp con.
        /// </summary>
        private static CourtResponseModelView ToViewModel(Court c) =>
            new()
            {
                Id = c.Id,
                CourtName = c.CourtName,
                Category = c.Category,
                OpenTime = c.OpenTime,
                CloseTime = c.CloseTime,
                UnitPrice = c.UnitPrice,
                Description = c.Description,
                CreatedTime = c.CreatedTime,
                FloorMaterial = c switch { IndoorCourt i => i.FloorMaterial, _ => null },
                HasAirConditioning = c is IndoorCourt i && i.HasAirConditioning,
                LightLevel = c is IndoorCourt li ? li.LightLevel : 0,
                HasCanopy = c is OutdoorCourt o ? o.HasCanopy : false,
                WeatherNote = c is OutdoorCourt oc ? oc.WeatherNote : null
            };
    }
}
