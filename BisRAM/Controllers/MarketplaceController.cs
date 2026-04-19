// ============================================================
// MarketplaceController.cs — Handles the PEER-TO-PEER MARKETPLACE
//
// The marketplace lets regular users sell their own items to other users
// (like OLX, Carousell, or Facebook Marketplace but for computer parts).
//
// Users can:
//   - Browse listings posted by other users
//   - View the details of a listing
//   - Post their own item for sale (Sell)
//   - Edit or delete their own listings
//   - Add marketplace listings to their cart
//
// URL prefix: /Marketplace/...
// ============================================================

using Microsoft.AspNetCore.Mvc;
using BisRAM.Data;   // For DbHelper
using BisRAM.Models; // For Listing, MarketplaceSearchViewModel

namespace BisRAM.Controllers
{
    public class MarketplaceController : Controller
    {
        private readonly DbHelper _db;             // Database helper
        private readonly IWebHostEnvironment _env; // For saving uploaded listing images

        public MarketplaceController(DbHelper db, IWebHostEnvironment env) { _db = db; _env = env; }

        // Reads the logged-in user's ID from the session. Null if not logged in.
        private int? UserId => HttpContext.Session.GetInt32("UserId");

        // ── INDEX ── (GET /Marketplace  or  /Marketplace?query=gpu&category=GPU)
        // Shows all marketplace listings with optional search/filter/sort.
        public IActionResult Index(string? query, string? category, string? condition, string sortBy = "newest")
        {
            // Build the search view model with filters and the matching listings
            var vm = new MarketplaceSearchViewModel
            {
                Query = query,
                Category = category,
                Condition = condition,
                SortBy = sortBy,
                // Fetch listings from the database using the given filters
                Listings = _db.GetListings(query, category, condition, sortBy)
            };
            return View(vm); // Show Marketplace/Index.cshtml
        }

        // ── DETAILS ── (GET /Marketplace/Details/5)
        // Shows the full page for one specific marketplace listing.
        public IActionResult Details(int id)
        {
            var listing = _db.GetListingById(id);
            if (listing == null) return NotFound(); // 404 if the listing ID doesn't exist
            return View(listing); // Show Marketplace/Details.cshtml
        }

        // ── SELL ── (GET /Marketplace/Sell)
        // Shows the empty form where a user can post a new item for sale.
        public IActionResult Sell()
        {
            if (UserId == null) return RedirectToAction("Login", "Account"); // Must be logged in
            return View(new Listing()); // Pass an empty Listing object to the form
        }

        // ── SELL ── (POST /Marketplace/Sell)
        // Processes the submitted listing form and saves the new listing.
        // 'async Task' because uploading a file is an asynchronous I/O operation.
        [HttpPost]
        public async Task<IActionResult> Sell(Listing model, IFormFile? imageFile)
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            // Load the seller's info to set SellerName
            var user = _db.GetUserById(UserId.Value);

            // Fill in who is selling this item
            model.SellerId = UserId.Value;
            model.SellerName = user?.FullName ?? "";
            model.Status = "Active"; // New listings are visible immediately

            // ── HANDLE IMAGE UPLOAD ──
            if (imageFile != null && imageFile.Length > 0)
            {
                // Get file extension in lowercase
                var ext = Path.GetExtension(imageFile.FileName).ToLower();

                // Build a unique filename: "listing_{userId}_{timestamp}.jpg"
                var fname = "listing_" + UserId.Value + "_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ext;

                // Path to the uploads/listings folder on the server
                var path = Path.Combine(_env.WebRootPath, "uploads", "listings");

                // Create the folder if it doesn't exist
                Directory.CreateDirectory(path);

                // Save the uploaded file to the server
                using var stream = new FileStream(Path.Combine(path, fname), FileMode.Create);
                await imageFile.CopyToAsync(stream); // 'await' waits for the file to finish saving

                // Set the image URL so it can be stored in the database and displayed
                model.ImageUrl = "/uploads/listings/" + fname;
            }
            else
            {
                // No image uploaded — use the default product image
                model.ImageUrl = "/images/products/default.jpg";
            }

            // Save the listing to the database
            _db.SaveListing(model);

            TempData["Success"] = "Your item is now listed for sale!";
            return RedirectToAction("Profile", "Account"); // Redirect to the user's profile page
        }

        // ── EDIT LISTING ── (GET /Marketplace/EditListing/5)
        // Shows the edit form for a listing, but ONLY to the seller who owns it.
        public IActionResult EditListing(int id)
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            var listing = _db.GetListingById(id);

            // Security check: only the listing's seller can edit it.
            // If the listing doesn't exist or belongs to someone else, return 403 Forbidden.
            if (listing == null || listing.SellerId != UserId.Value) return Forbid();

            return View(listing);
        }

        // ── EDIT LISTING ── (POST /Marketplace/EditListing)
        // Saves the changes made to the listing.
        [HttpPost]
        public IActionResult EditListing(Listing model)
        {
            if (UserId == null) return RedirectToAction("Login", "Account");
            _db.SaveListing(model); // SaveListing with Id > 0 will UPDATE the row
            TempData["Success"] = "Listing updated!";
            return RedirectToAction("Profile", "Account");
        }

        // ── DELETE LISTING ── (POST /Marketplace/DeleteListing)
        // "Soft-deletes" the user's own listing (sets Status = "Removed").
        [HttpPost]
        public IActionResult DeleteListing(int id)
        {
            if (UserId == null) return RedirectToAction("Login", "Account");
            _db.DeleteListing(id); // Sets Status='Removed', hides from marketplace but keeps data
            TempData["Success"] = "Listing removed.";
            return RedirectToAction("Profile", "Account");
        }

        // ── ADD LISTING TO CART ── (POST /Marketplace/AddToCart)
        // Lets a buyer add a marketplace listing to their cart.
        // Returns JSON because it's called via AJAX (no page reload).
        [HttpPost]
        public IActionResult AddToCart(int listingId, int quantity = 1)
        {
            if (UserId == null) return Json(new { success = false, message = "Please log in first." });

            var listing = _db.GetListingById(listingId);

            // Check the listing exists and has enough stock
            if (listing == null || listing.Stock < quantity)
                return Json(new { success = false, message = "Item not available." });

            // Prevent sellers from buying their own listings
            if (listing.SellerId == UserId.Value)
                return Json(new { success = false, message = "You cannot buy your own listing." });

            // Add the listing to the cart
            _db.AddListingToCart(UserId.Value, listingId, quantity);

            // Return the updated total cart count for the navbar badge
            var count = _db.GetCartCount(UserId.Value);
            return Json(new { success = true, cartCount = count });
        }
    }
}
