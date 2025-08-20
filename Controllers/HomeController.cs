using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Web_chơi_cờ_Caro.Models;
using Microsoft.AspNetCore.Authorization;

namespace Web_chơi_cờ_Caro.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public IActionResult Multiplayer()
        {
            return View();
        }

        [Authorize]
        public IActionResult CreateRoom()
        {
            return View();
        }

        [Authorize]
        public IActionResult FindRoom()
        {
            return View();
        }

        [Authorize]
        public IActionResult GameRoom(string roomId)
        {
            ViewBag.RoomId = roomId;
            return View();
        }

        public IActionResult TestSignalR()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
