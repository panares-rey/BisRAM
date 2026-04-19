// ============================================================
// CartController.cs — Handles the SHOPPING CART and CHECKOUT
//
// This controller manages:
//   - Viewing the cart
//   - Adding items to the cart (store products & marketplace listings)
//   - Updating or removing cart items
//   - The checkout process and placing an order
//
// URL prefix: /Cart/...
// ============================================================

using Microsoft.AspNetCore.Mvc;
using BisRAM.Data;   // For DbHelper
using BisRAM.Models; // For CartItem, Order, OrderItem, CheckoutViewModel

namespace BisRAM.Controllers
{
    public class CartController : Controller
    {
        private readonly DbHelper _db; // Database helper

        public CartController(DbHelper db) { _db = db; }

        // Reads the logged-in user's ID from the session.
        // Returns null if no user is logged in.
        private int? UserId => HttpContext.Session.GetInt32("UserId");

        // ── INDEX ── (GET /Cart)
        // Shows the user's shopping cart with all their selected items.
        public IActionResult Index()
        {
            // Must be logged in to view the cart
            if (UserId == null) return RedirectToAction("Login", "Account");

            // Load the cart items for this user from the database and pass to the view
            return View(_db.GetCartItems(UserId.Value));
        }

        // ── ADD TO CART ── (POST /Cart/AddToCart)
        // Called via JavaScript (AJAX) when user clicks "Add to Cart" on a product.
        // Returns a JSON response (not a full page) because it's called without page reload.
        [HttpPost]
        public IActionResult AddToCart(int productId, int quantity = 1)
        {
            // If not logged in, return a JSON error (the frontend will show a "please login" message)
            if (UserId == null) return Json(new { success = false, message = "Please log in to add items to cart." });

            // Look up the product to verify it exists and has enough stock
            var product = _db.GetProductById(productId);

            // If product doesn't exist or doesn't have enough stock, return an error
            if (product == null || product.Stock < quantity)
                return Json(new { success = false, message = "Product not available." });

            // Add the item to the cart (or increase quantity if already in cart)
            _db.AddToCart(UserId.Value, productId, quantity);

            // Return success + the new total cart item count (used to update the cart badge in the navbar)
            return Json(new { success = true, cartCount = _db.GetCartCount(UserId.Value) });
        }

        // ── UPDATE QUANTITY ── (POST /Cart/UpdateQuantity)
        // Called when the user changes the quantity of an item in the cart.
        [HttpPost]
        public IActionResult UpdateQuantity(int cartItemId, int quantity)
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            // Update the quantity in the database (if quantity is 0 or less, the item is removed)
            _db.UpdateCartQuantity(cartItemId, quantity);

            return RedirectToAction("Index"); // Refresh the cart page
        }

        // ── REMOVE ITEM ── (POST /Cart/Remove)
        // Called when the user clicks "Remove" on a cart item.
        [HttpPost]
        public IActionResult Remove(int cartItemId)
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            // Delete this specific cart item from the database
            _db.RemoveFromCart(cartItemId);

            return RedirectToAction("Index"); // Refresh the cart page
        }

        // ── CHECKOUT ── (GET /Cart/Checkout)
        // Shows the checkout form pre-filled with the user's saved address.
        public IActionResult Checkout()
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            // Load the cart items to display the order summary
            var items = _db.GetCartItems(UserId.Value);

            // If the cart is empty, don't allow checkout — redirect back to cart
            if (!items.Any()) return RedirectToAction("Index");

            // Load the user's profile data to pre-fill the delivery address fields
            var user = _db.GetUserById(UserId.Value);

            // Build the CheckoutViewModel with pre-filled user data and cart totals
            var vm = new CheckoutViewModel
            {
                FullName = user?.FullName ?? "",
                Phone = user?.Phone ?? "",
                Region = user?.Region ?? "",
                Province = user?.Province ?? "",
                City = user?.City ?? "",
                Barangay = user?.Barangay ?? "",
                ZipCode = user?.ZipCode ?? "",
                StreetAddress = user?.StreetAddress ?? "",
                CartItems = items,
                // Calculate the total price: sum of (price × quantity) for all items
                Total = items.Sum(i => GetItemPrice(i) * i.Quantity)
            };

            return View(vm); // Show Cart/Checkout.cshtml with the pre-filled form
        }

        // ── PLACE ORDER ── (POST /Cart/PlaceOrder)
        // Processes the checkout form and creates the actual order in the database.
        [HttpPost]
        public IActionResult PlaceOrder(CheckoutViewModel model) // 'model' is filled from the submitted form
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            var items = _db.GetCartItems(UserId.Value);
            if (!items.Any()) return RedirectToAction("Index"); // Safety check: don't place empty orders

            // Estimate how many days delivery will take based on the user's region
            int deliveryDays = EstimateDeliveryDays(model.Region);

            // Combine all address parts into one full address string for the order record
            var fullAddress = $"{model.StreetAddress}, {model.Barangay}, {model.City}, {model.Province}, {model.Region} {model.ZipCode}";

            // Create the Order object with all the checkout details
            var order = new Order
            {
                UserId = UserId.Value,
                FullName = model.FullName,
                Address = fullAddress,
                Region = model.Region,
                Province = model.Province,
                City = model.City,
                Barangay = model.Barangay,
                ZipCode = model.ZipCode,
                Phone = model.Phone,
                PaymentMethod = model.PaymentMethod,
                PaymentReference = model.PaymentReference,
                Status = "Pending", // All new orders start as "Pending"
                // Calculate the total: sum of (price × quantity) for each cart item
                Total = items.Sum(i => GetItemPrice(i) * i.Quantity),
                // Estimated delivery date = today + calculated number of days
                EstimatedDelivery = DateTime.Now.AddDays(deliveryDays),
                // Convert each cart item into an OrderItem (a snapshot of what was ordered)
                Items = items.Select(i => new OrderItem
                {
                    // ProductId: if it's a store product use its ID, if a listing use the listing ID
                    ProductId = i.ProductId > 0 ? i.ProductId : (i.Listing?.Id ?? 0),
                    // ProductName: prefer store product name, fall back to listing title
                    ProductName = i.Product?.Name ?? i.Listing?.Title ?? "",
                    Price = GetItemPrice(i),
                    Quantity = i.Quantity
                }).ToList()
            };

            // Save the order to the database (also reduces product stock)
            var orderId = _db.CreateOrder(order);

            // Clear the user's cart now that the order has been placed
            _db.ClearCart(UserId.Value);

            // Redirect to the order confirmation page
            return RedirectToAction("OrderConfirmation", new { id = orderId });
        }

        // ── ORDER CONFIRMATION ── (GET /Cart/OrderConfirmation/5)
        // Shows the "thank you, your order was placed" page for a specific order.
        public IActionResult OrderConfirmation(int id)
        {
            var order = _db.GetOrderById(id);
            if (order == null) return NotFound(); // Return 404 if the order ID doesn't exist
            return View(order); // Show Cart/OrderConfirmation.cshtml with the order details
        }

        // ── GET CART COUNT ── (GET /Cart/GetCartCount)
        // Returns a JSON number used by the navbar to show the cart item count badge.
        // Called via JavaScript without reloading the page.
        public IActionResult GetCartCount()
        {
            if (UserId == null) return Json(new { count = 0 }); // Not logged in = 0 items
            return Json(new { count = _db.GetCartCount(UserId.Value) });
        }

        // ── PRIVATE HELPER: GetItemPrice() ──
        // Returns the price of a cart item, whether it's a store Product or a Marketplace Listing.
        // Cart items can be either type, so we check which one is set.
        private decimal GetItemPrice(CartItem item)
        {
            if (item.Product != null) return item.Product.Price;   // It's a store product
            if (item.Listing != null) return item.Listing.Price;   // It's a marketplace listing
            return 0; // Should never happen, but fallback to 0
        }

        // ── PRIVATE HELPER: EstimateDeliveryDays() ──
        // Returns an estimated number of delivery days based on the user's region in the Philippines.
        // Uses a switch expression (a modern C# way to match patterns).
        private int EstimateDeliveryDays(string region)
        {
            return region?.ToLower() switch
            {
                // Metro Manila and NCR are the fastest (2 days)
                var r when r != null && r.Contains("metro manila") => 2,
                var r when r != null && r.Contains("ncr") => 2,
                // Nearby Luzon provinces are 3 days
                var r when r != null && (r.Contains("luzon") || r.Contains("bulacan") ||
                    r.Contains("cavite") || r.Contains("laguna") || r.Contains("rizal")) => 3,
                // Visayas (including Western Visayas) is 5 days
                var r when r != null && r.Contains("visayas") => 5,
                // Mindanao is 7 days (farthest)
                var r when r != null && r.Contains("mindanao") => 7,
                // Default for anything else (e.g., other Luzon regions, Bicol, etc.)
                _ => 5
            };
        }
    }
}
