// ============================================================
// ProductsController.cs — Handles the STORE PRODUCTS pages
//   - Browsing/searching the product catalog
//   - Viewing a single product's details
//
// URL prefix: /Products/...
// ============================================================

using Microsoft.AspNetCore.Mvc;
using BisRAM.Data;   // For DbHelper
using BisRAM.Models; // For Product, ProductSearchViewModel

namespace BisRAM.Controllers
{
    public class ProductsController : Controller
    {
        private readonly DbHelper _db; // Database helper

        // ── CONSTRUCTOR ──
        // DbHelper is injected automatically by ASP.NET.
        public ProductsController(DbHelper db) { _db = db; }

        // ── INDEX ACTION ── (GET /Products  or  GET /Products/Index)
        // Shows the product catalog with optional search/filter/sort.
        // All parameters come from the URL query string, e.g.:
        //   /Products?query=gpu&category=GPU&brand=NVIDIA&minPrice=5000&sortBy=price_asc
        public IActionResult Index(
            string? query,       // Text search (null = no text filter)
            string? category,    // Category filter, e.g., "CPU", "GPU" (null = all categories)
            string? brand,       // Brand filter, e.g., "Intel", "AMD" (null = all brands)
            decimal? minPrice,   // Minimum price filter (null = no minimum)
            decimal? maxPrice,   // Maximum price filter (null = no maximum)
            string sortBy = "name") // Sort order: "name", "price_asc", "price_desc", "newest" (default is "name")
        {
            // Build a ProductSearchViewModel to hold all the filters AND the results.
            // This single object is passed to the View so it has everything it needs.
            var vm = new ProductSearchViewModel
            {
                Query = query,
                Category = category,
                Brand = brand,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SortBy = sortBy,
                // Run the search in the database using all the given filters
                Products = _db.SearchProducts(query, category, brand, minPrice, maxPrice, sortBy),
                // Also load the full list of categories and brands for the filter dropdowns
                Categories = _db.GetCategories(),
                Brands = _db.GetBrands()
            };

            // Pass the view model to Products/Index.cshtml for rendering
            return View(vm);
        }

        // ── DETAILS ACTION ── (GET /Products/Details/5)
        // Shows the full details page for ONE specific product.
        // 'id' comes from the URL, e.g., /Products/Details/5 → id = 5
        public IActionResult Details(int id)
        {
            // Load the product from the database by its ID
            var product = _db.GetProductById(id);

            // If no product was found with that ID, return a 404 Not Found page
            if (product == null) return NotFound();

            // Load related products from the same category (up to 4), excluding the current product.
            // These are shown at the bottom of the details page as "You might also like..."
            ViewBag.RelatedProducts = _db.GetProductsByCategory(product.Category, 4)
                                         .Where(p => p.Id != id) // Exclude the product we're already viewing
                                         .ToList();

            // Pass the product to Products/Details.cshtml for rendering
            return View(product);
        }
    }
}
