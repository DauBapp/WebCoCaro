using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Web_chơi_cờ_Caro.Data;
using Web_chơi_cờ_Caro.Models;

namespace Web_chơi_cờ_Caro.Controllers
{
    public class DatabaseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DatabaseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                // Test database connection
                await _context.Database.CanConnectAsync();
                
                // Get user count
                var userCount = await _context.Users.CountAsync();
                
                return Json(new { 
                    success = true, 
                    message = $"✅ Database kết nối thành công! Số lượng user: {userCount}" 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"❌ Lỗi kết nối database: {ex.Message}" 
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDatabase()
        {
            try
            {
                // Apply migrations
                await _context.Database.MigrateAsync();
                
                return Json(new { 
                    success = true, 
                    message = "✅ Đã cập nhật database thành công!" 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"❌ Lỗi cập nhật database: {ex.Message}" 
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTestUser()
        {
            try
            {
                // Check if test user already exists
                var existingUser = await _userManager.FindByEmailAsync("test@example.com");
                if (existingUser != null)
                {
                    return Json(new { 
                        success = true, 
                        message = "ℹ️ Tài khoản test đã tồn tại: test@example.com / Test123!" 
                    });
                }

                // Create test user
                var user = new ApplicationUser
                {
                    UserName = "test@example.com",
                    Email = "test@example.com",
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, "Test123!");
                
                if (result.Succeeded)
                {
                    return Json(new { 
                        success = true, 
                        message = "✅ Đã tạo tài khoản test thành công: test@example.com / Test123!" 
                    });
                }
                else
                {
                    return Json(new { 
                        success = false, 
                        message = $"❌ Lỗi tạo user: {string.Join(", ", result.Errors.Select(e => e.Description))}" 
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"❌ Lỗi tạo test user: {ex.Message}" 
                });
            }
        }
    }
} 