using Core.Base;
using System.ComponentModel.DataAnnotations;

namespace Contract.Repositories.Entity
{
    public class CalendarType : BaseEntity
    {
        //Tên loại lịch
        [StringLength(50)]
        public string CalendarTypeName { get; set; } = string.Empty;

        //Chiết khấu
        public int Discount { get; set; }

    }
}
