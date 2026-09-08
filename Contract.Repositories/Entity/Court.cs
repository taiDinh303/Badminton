using Core.Base;
using System.ComponentModel.DataAnnotations;

namespace Contract.Repositories.Entity
{
    public class Court : BaseEntity
    {
        //Tên sân
        [StringLength(50)]
        public string CourtName { get; set; } = string.Empty;

        //Thời gian mở
        public int OpenTime { get; set; }

        //Thời gian đóng
        public int CloseTime { get; set; }

        //Đơn giá
        public int UnitPrice { get; set; }

        public virtual ICollection<TimeFrame> TimeFrames { get; set; } = new HashSet<TimeFrame>();

    }
}
