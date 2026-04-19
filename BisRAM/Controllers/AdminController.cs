// ============================================================
// AdminController.cs — Handles the ADMIN DASHBOARD and management
//
// Only users with Role = "Admin" can access these pages.
// Every action checks IsAdmin() first — if not admin, redirect to login.
//
// Admin can manage:
//   - Products (create, edit, delete)
//   - Orders (view, update status)
//   - Users (view list)
//   - Marketplace Listings (view, remove)
//   - Dashboard statistics
//
// URL prefix: /Admin/...
// ============================================================

using Microsoft.AspNetCore.Mvc;
using BisRAM.Data;   // For DbHelper
using BisRAM.Models; // For Product, Order, etc.

namespace BisRAM.Controllers
{
    public class AdminController : Controller
    {
        private readonly DbHelper _db; // Database helper

        public AdminController(DbHelper db) { _db = db; }

        // ── SECURITY HELPERS ──

        // IsAdmin() checks if the currently logged-in user has the "Admin" role.
        // Reads from the session — if not set, returns false.
        private bool IsAdmin() => HttpContext.Session.GetString("UserRole") == "Admin";

        // RequireAdmin() is called at the start of every admin action.
        // If not an admin, it returns a redirect to the login page.
        // If IS an admin, it returns null (meaning "all clear, continue").
        // The 'null!' tells the compiler to trust us that null is intentional here.
        private IActionResult RequireAdmin() => !IsAdmin() ? RedirectToAction("Login", "Account") : null!;

        // ══════════════════════════════════════════════════════════════
        // ── DASHBOARD ──
        // ══════════════════════════════════════════════════════════════

        // GET /Admin/Dashboard — Shows the admin overview page with statistics.
        public IActionResult Dashboard()
        {
            var c = RequireAdmin(); if (c != null) return c; // Block non-admins
            // GetDashboardStats() returns an AdminDashboardViewModel with counts, revenue,
            // recent orders, and low-stock alerts
            return View(_db.GetDashboardStats());
        }

        // ══════════════════════════════════════════════════════════════
        // ── PRODUCTS MANAGEMENT ──
        // ══════════════════════════════════════════════════════════════

        // GET /Admin/Products — Lists all products (including inactive/deleted ones)
        public IActionResult Products()
        {
            var c = RequireAdmin(); if (c != null) return c;
            return View(_db.GetAllProducts(true)); // 'true' = include inactive products
        }

        // GET /Admin/CreateProduct — Shows the blank form to add a new product
        public IActionResult CreateProduct()
        {
            var c = RequireAdmin(); if (c != null) return c;
            return View(new Product()); // Pass an empty Product object so the form has a model
        }

        // POST /Admin/CreateProduct — Saves the new product to the database
        [HttpPost]
        public IActionResult CreateProduct(Product model)
        {
            var c = RequireAdmin(); if (c != null) return c;
            model.Id = 0; // Ensure Id is 0 so the DB auto-generates a new ID (INSERT instead of UPDATE)
            // Use default image if no image URL was provided
            if (string.IsNullOrEmpty(model.ImageUrl)) model.ImageUrl = "/images/products/default.jpg";
            _db.SaveProduct(model); // SaveProduct with Id=0 will INSERT a new row
            TempData["Success"] = "Product created!";
            return RedirectToAction("Products"); // Go back to the product list
        }

        // GET /Admin/EditProduct/5 — Shows the edit form pre-filled with the product's current data
        public IActionResult EditProduct(int id)
        {
            var c = RequireAdmin(); if (c != null) return c;
            var p = _db.GetProductById(id);
            if (p == null) return NotFound(); // 404 if the product ID doesn't exist
            return View(p);
        }

        // POST /Admin/EditProduct — Saves the changes to an existing product
        [HttpPost]
        public IActionResult EditProduct(Product model)
        {
            var c = RequireAdmin(); if (c != null) return c;
            _db.SaveProduct(model); // SaveProduct with Id > 0 will UPDATE the existing row
            TempData["Success"] = "Product updated!";
            return RedirectToAction("Products");
        }

        // POST /Admin/DeleteProduct — "Soft-deletes" a product (sets IsActive = false, doesn't erase data)
        [HttpPost]
        public IActionResult DeleteProduct(int id)
        {
            var c = RequireAdmin(); if (c != null) return c;
            _db.DeleteProduct(id); // Sets IsActive=0, so the product is hidden but not erased
            TempData["Success"] = "Product removed.";
            return RedirectToAction("Products");
        }

        // ══════════════════════════════════════════════════════════════
        // ── ORDERS MANAGEMENT ──
        // ══════════════════════════════════════════════════════════════

        // GET /Admin/Orders — Lists ALL orders placed by all users
        public IActionResult Orders()
        {
            var c = RequireAdmin(); if (c != null) return c;
            return View(_db.GetAllOrders()); // Returns all orders, newest first
        }

        // GET /Admin/OrderDetails/5 — Shows full details for one specific order
        public IActionResult OrderDetails(int id)
        {
            var c = RequireAdmin(); if (c != null) return c;
            var o = _db.GetOrderById(id);
            if (o == null) return NotFound();
            return View(o);
        }

        // POST /Admin/UpdateOrderStatus — Changes the status of an order
        // e.g., from "Pending" → "Processing" → "Shipped" → "Delivered"
        [HttpPost]
        public IActionResult UpdateOrderStatus(int orderId, string status)
        {
            var c = RequireAdmin(); if (c != null) return c;
            _db.UpdateOrderStatus(orderId, status); // Update the status column in the Orders table
            TempData["Success"] = "Order status updated.";
            return RedirectToAction("Orders");
        }

        // ══════════════════════════════════════════════════════════════
        // ── USERS MANAGEMENT ──
        // ══════════════════════════════════════════════════════════════

        // GET /Admin/Users — Lists ALL registered users (admins and customers)
        public IActionResult Users()
        {
            var c = RequireAdmin(); if (c != null) return c;
            return View(_db.GetAllUsers()); // Returns all users, newest first
        }

        // ══════════════════════════════════════════════════════════════
        // ── MARKETPLACE LISTINGS MANAGEMENT ──
        // ══════════════════════════════════════════════════════════════

        // GET /Admin/Listings — Lists ALL marketplace listings
        public IActionResult Listings()
        {
            var c = RequireAdmin(); if (c != null) return c;
            return View(_db.GetAllListings()); // All listings, newest first
        }

        // POST /Admin/RemoveListing — Marks a listing as "Removed" (soft-delete)
        [HttpPost]
        public IActionResult RemoveListing(int id)
        {
            var c = RequireAdmin(); if (c != null) return c;
            _db.DeleteListing(id); // Sets Status='Removed' so it's hidden from the marketplace
            TempData["Success"] = "Listing removed.";
            return RedirectToAction("Listings");
        }
    }
}
