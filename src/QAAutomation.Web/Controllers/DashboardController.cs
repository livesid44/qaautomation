using Microsoft.AspNetCore.Mvc;

namespace QAAutomation.Web.Controllers;

public class DashboardController : ProjectAwareController
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Analytics");
    }
}
