using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Monitor.Web.Controllers;

[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [HttpGet("/error")]
    public IActionResult ServerError()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View();
    }

    [HttpGet("/access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpGet("/error/status/{statusCode:int}")]
    public IActionResult Status(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status403Forbidden => AccessDenied(),
            StatusCodes.Status404NotFound => NotFoundPage(),
            _ => ServerError()
        };
    }

    [NonAction]
    private IActionResult NotFoundPage()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }
}
