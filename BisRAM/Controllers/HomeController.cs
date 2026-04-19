// ============================================================
// HomeController.cs — Handles the HOME PAGE of BisRAM
// URL: / or /Home/Index
//
// This is the first page users see when they open the website.
// It loads featured products, categories, and recent listings to display.
// ============================================================

using Microsoft.AspNetCore.Mvc;
using BisRAM.Data; // Needed to use DbHelper for database access

namespace BisRAM.Controllers
{
    // HomeController inherits from Controller — this gives it access to
    // View(), ViewBag, RedirectToAction(), and other MVC tools.
    public class HomeController : Controller
    {
        // _db is the database helper — it does all the SQL queries for us.
        // The underscore prefix (_) is a naming convention for private fields.
        private readonly DbHelper _db;

        // ── CONSTRUCTOR ──
        // ASP.NET automatically calls this when the controller is created.
        // 'db' is injected (provided automatically) from the services registered in Program.cs.
        // This is called "Dependency Injection" — we don't create DbHelper ourselves,
        // ASP.NET hands it to us.
        public HomeController(DbHelper db) { _db = db; }

        // ── INDEX ACTION ── (handles GET /  or  GET /Home/Index)
        // This loads all the data needed for the home page and sends it to the view.
        public IActionResult Index()
        {
            // ViewBag is a dynamic container to pass data to the View (Index.cshtml).
            // It's like attaching sticky notes to the response before sending it.

            // Get all products from the DB, but only show the first 8 as "featured"
            ViewBag.FeaturedProducts = _db.GetAllProducts().Take(8).ToList();

            // Get the list of product categories to display in the navigation or filter
            ViewBag.Categories = _db.GetCategories();

            // Get the 4 newest marketplace listings to show in the "Recent Listings" section
            ViewBag.RecentListings = _db.GetListings(sort: "newest").Take(4).ToList();

            // Return View() tells ASP.NET to render Views/Home/Index.cshtml
            // with the data we attached to ViewBag above.
            return View();
        }

        // ── ERROR ACTION ── (handles GET /Home/Error)
        // This is called automatically when an unhandled error occurs in production.
        // It just shows the Error.cshtml view.
        // The => is a shorthand (lambda) for a method that just returns one thing.
        public IActionResult Error() => View();
    }
}
