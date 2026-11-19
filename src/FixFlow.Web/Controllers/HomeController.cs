using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FixFlow.Web.Models;

namespace FixFlow.Web.Controllers;

public class HomeController : Controller
{
    private readonly IConfiguration _configuration;

    public HomeController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        ViewData["ApiBaseUrl"] = (_configuration["ApiSettings:BaseUrl"] ?? string.Empty).TrimEnd('/');
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
