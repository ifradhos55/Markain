using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzarkLMS.Services;

namespace OzarkLMS.Controllers
{
    [AllowAnonymous] // Allow public access so we can verify easily without login, or can be restricted to Admin
    public class TestController : Controller
    {
        private readonly ISelfTestService _testService;

        public TestController(ISelfTestService testService)
        {
            _testService = testService;
        }

        public async Task<IActionResult> Index()
        {
            var results = await _testService.RunAllTestsAsync();
            return View(results);
        }
    }
}
