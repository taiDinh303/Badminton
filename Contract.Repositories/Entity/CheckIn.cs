using Contract.Repositories.Entity;
using Core.Base;
using System.Text.Json.Serialization;

namespace Contract.Repositories.Entity
{
    public class CheckIn : BaseEntity
    {
        //Thời gian khách vào Check in
        public DateTime CheckInDate { get; set; }
        //Khóa ngoại liên kết với TimeFrame 
        public string TimeFrameID { get; set; } = string.Empty;
        // TimeFrame liên kết với CheckIn, không được serialize trong JSON
        [JsonIgnore]
        public virtual TimeFrame? TimeFrame { get; set; }
    }
}
