namespace Badminton.Core.Store
{
    public class MembershipSettings
    {
        public decimal MultiplierSilver { get; set; } = 1.5m;
        public decimal MultiplierGold { get; set; } = 2.0m;
        public int SilverDays { get; set; } = 30;
        public int GoldDays { get; set; } = 90;
    }
}
