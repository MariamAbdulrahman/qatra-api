using Microsoft.AspNetCore.Mvc;

namespace Qatra.Api.Controllers
{
    public class ReportsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
