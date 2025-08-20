using Microsoft.AspNetCore.Mvc;

namespace Web_chơi_cờ_Caro.Controllers
{
    public class TestController : Controller
    {
        public IActionResult Index()
        {
            return Content("Hello World! Application is running.");
        }
    }
} 