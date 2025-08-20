using System.ComponentModel.DataAnnotations;

namespace Web_chơi_cờ_Caro.Models
{
    public class AdminUserViewModel
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool EmailConfirmed { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsBanned { get; set; }
    }

    public class AdminStatisticsViewModel
    {
        public int TotalUsers { get; set; }
        public int PendingUsers { get; set; }
        public int BannedUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int NewUsersThisWeek { get; set; }
        public int NewUsersThisMonth { get; set; }
        public List<DailyUserStats> DailyStats { get; set; } = new();
    }

    public class DailyUserStats
    {
        public string Date { get; set; } = "";
        public int NewUsers { get; set; }
    }

    public class BanUserViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập lý do ban")]
        [StringLength(500, ErrorMessage = "Lý do ban không được quá 500 ký tự")]
        public string Reason { get; set; } = "";

        [Range(1, 365, ErrorMessage = "Số ngày ban phải từ 1 đến 365")]
        public int Days { get; set; } = 7;
    }
}
