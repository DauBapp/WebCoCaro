using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_chơi_cờ_Caro.Data;
using Web_chơi_cờ_Caro.Models;
using System.Security.Claims;

namespace Web_chơi_cờ_Caro.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            ILogger<AdminController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        // Dashboard chính
        public async Task<IActionResult> Index()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var pendingUsers = await _userManager.Users.Where(u => !u.EmailConfirmed).CountAsync();
            var bannedUsers = await _userManager.Users.Where(u => u.LockoutEnd > DateTimeOffset.Now).CountAsync();
            var activeUsers = await _userManager.Users.Where(u => u.LastLoginTime > DateTime.Now.AddDays(-7)).CountAsync();

            ViewBag.Stats = new
            {
                TotalUsers = totalUsers,
                PendingUsers = pendingUsers,
                BannedUsers = bannedUsers,
                ActiveUsers = activeUsers
            };

            return View();
        }

        // Quản lý người dùng
        public async Task<IActionResult> Users(string searchTerm = "", string status = "all", int page = 1)
        {
            var query = _userManager.Users.AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(u => u.UserName.Contains(searchTerm) || u.Email.Contains(searchTerm));
            }

            // Lọc theo trạng thái
            switch (status.ToLower())
            {
                case "pending":
                    query = query.Where(u => !u.EmailConfirmed);
                    break;
                case "banned":
                    query = query.Where(u => u.LockoutEnd > DateTimeOffset.Now);
                    break;
                case "active":
                    query = query.Where(u => u.EmailConfirmed && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.Now));
                    break;
            }

            // Phân trang
            int pageSize = 10;
            var totalUsers = await query.CountAsync();
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Lấy thông tin roles cho mỗi user
            var userViewModels = new List<AdminUserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new AdminUserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnd = user.LockoutEnd,
                    CreatedAt = user.CreatedAt,
                    LastLoginTime = user.LastLoginTime,
                    Roles = roles.ToList(),
                    IsBanned = user.LockoutEnd > DateTimeOffset.Now
                });
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
            ViewBag.TotalUsers = totalUsers;

            return View(userViewModels);
        }

        // Duyệt người dùng đăng ký
        [HttpPost]
        public async Task<IActionResult> ApproveUser(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng!" });
                }

                user.EmailConfirmed = true;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"Admin {User.Identity.Name} approved user {user.Email}");
                    return Json(new { success = true, message = "Đã duyệt người dùng thành công!" });
                }
                else
                {
                    return Json(new { success = false, message = "Lỗi khi duyệt người dùng!" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving user {UserId}", userId);
                return Json(new { success = false, message = "Lỗi hệ thống!" });
            }
        }

        // Ban người dùng
        [HttpPost]
        public async Task<IActionResult> BanUser(string userId, string reason, int days = 7)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng!" });
                }

                var lockoutEnd = DateTimeOffset.Now.AddDays(days);
                var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);

                if (result.Succeeded)
                {
                    // Lưu lý do ban
                    await _context.BanRecords.AddAsync(new BanRecord
                    {
                        UserId = userId,
                        Reason = reason,
                        BannedBy = User.Identity.Name,
                        BannedAt = DateTime.Now,
                        BanEndDate = lockoutEnd.DateTime,
                        IsActive = true
                    });
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Admin {User.Identity.Name} banned user {user.Email} for {days} days. Reason: {reason}");
                    return Json(new { success = true, message = $"Đã ban người dùng {days} ngày!" });
                }
                else
                {
                    return Json(new { success = false, message = "Lỗi khi ban người dùng!" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error banning user {UserId}", userId);
                return Json(new { success = false, message = "Lỗi hệ thống!" });
            }
        }

        // Unban người dùng
        [HttpPost]
        public async Task<IActionResult> UnbanUser(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng!" });
                }

                var result = await _userManager.SetLockoutEndDateAsync(user, null);

                if (result.Succeeded)
                {
                    // Cập nhật ban record
                    var banRecord = await _context.BanRecords
                        .Where(b => b.UserId == userId && b.IsActive)
                        .FirstOrDefaultAsync();
                    
                    if (banRecord != null)
                    {
                        banRecord.IsActive = false;
                        banRecord.UnbannedBy = User.Identity.Name;
                        banRecord.UnbannedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }

                    _logger.LogInformation($"Admin {User.Identity.Name} unbanned user {user.Email}");
                    return Json(new { success = true, message = "Đã unban người dùng thành công!" });
                }
                else
                {
                    return Json(new { success = false, message = "Lỗi khi unban người dùng!" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unbanning user {UserId}", userId);
                return Json(new { success = false, message = "Lỗi hệ thống!" });
            }
        }

        // Xóa người dùng
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng!" });
                }

                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"Admin {User.Identity.Name} deleted user {user.Email}");
                    return Json(new { success = true, message = "Đã xóa người dùng thành công!" });
                }
                else
                {
                    return Json(new { success = false, message = "Lỗi khi xóa người dùng!" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return Json(new { success = false, message = "Lỗi hệ thống!" });
            }
        }

        // Thống kê hệ thống
        public async Task<IActionResult> Statistics()
        {
            var stats = new AdminStatisticsViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                PendingUsers = await _userManager.Users.Where(u => !u.EmailConfirmed).CountAsync(),
                BannedUsers = await _userManager.Users.Where(u => u.LockoutEnd > DateTimeOffset.Now).CountAsync(),
                ActiveUsers = await _userManager.Users.Where(u => u.LastLoginTime > DateTime.Now.AddDays(-7)).CountAsync(),
                NewUsersThisWeek = await _userManager.Users.Where(u => u.CreatedAt > DateTime.Now.AddDays(-7)).CountAsync(),
                NewUsersThisMonth = await _userManager.Users.Where(u => u.CreatedAt > DateTime.Now.AddMonths(-1)).CountAsync()
            };

            // Thống kê theo ngày trong tuần
            var dailyStats = new List<DailyUserStats>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Now.AddDays(-i);
                var newUsers = await _userManager.Users
                    .Where(u => u.CreatedAt.Date == date.Date)
                    .CountAsync();
                
                dailyStats.Add(new DailyUserStats
                {
                    Date = date.ToString("dd/MM"),
                    NewUsers = newUsers
                });
            }
            stats.DailyStats = dailyStats;

            return View(stats);
        }

        // Lịch sử ban
        public async Task<IActionResult> BanHistory(int page = 1)
        {
            var query = _context.BanRecords
                .Include(b => b.User)
                .OrderByDescending(b => b.BannedAt);

            int pageSize = 10;
            var totalRecords = await query.CountAsync();
            var records = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            return View(records);
        }
    }
}
