// ============================================================
// MessagesController.cs — Handles the IN-APP MESSAGING (Chat) system
//
// Users can message each other, usually to ask about marketplace listings.
// Features:
//   - View all conversations (inbox)
//   - Open a chat with a specific user
//   - Send a message (both regular form POST and AJAX)
//   - Get unread message count (for the navbar notification badge)
//
// URL prefix: /Messages/...
// ============================================================

using Microsoft.AspNetCore.Mvc;
using BisRAM.Data;   // For DbHelper
using BisRAM.Models; // For Message, Conversation

namespace BisRAM.Controllers
{
    public class MessagesController : Controller
    {
        private readonly DbHelper _db; // Database helper

        public MessagesController(DbHelper db) { _db = db; }

        // Reads the logged-in user's ID from the session. Null if not logged in.
        private int? UserId => HttpContext.Session.GetInt32("UserId");

        // ── INDEX ── (GET /Messages)
        // Shows the user's inbox — a list of all their conversations.
        public IActionResult Index()
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            // GetConversations() groups messages by the other user and returns a summary
            // of each conversation (last message, unread count, etc.)
            var convos = _db.GetConversations(UserId.Value);
            return View(convos); // Show Messages/Index.cshtml
        }

        // ── CHAT ── (GET /Messages/Chat?userId=3  or  /Messages/Chat?userId=3&listingId=7)
        // Opens the full chat thread with a specific user.
        // 'listingId' is optional — it shows which listing the conversation is about.
        public IActionResult Chat(int userId, int? listingId)
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            // Load the other user's info to display their name/avatar in the chat
            var other = _db.GetUserById(userId);
            if (other == null) return NotFound(); // The other user doesn't exist

            // Load the full conversation history between the two users, ordered by time
            var messages = _db.GetConversation(UserId.Value, userId);

            // Mark all messages FROM the other user TO the current user as "read"
            // This clears the unread count badge
            _db.MarkMessagesRead(userId, UserId.Value);

            // Pass the other user's info and optional listing to the view
            ViewBag.OtherUser = other;
            ViewBag.ListingId = listingId;

            // If a listing is specified, load it so we can show a reference to it in the chat
            if (listingId.HasValue) ViewBag.Listing = _db.GetListingById(listingId.Value);

            return View(messages); // Show Messages/Chat.cshtml with all messages
        }

        // ── SEND ── (POST /Messages/Send)
        // Sends a message from the current user to another user.
        // Called when the user submits the message form (traditional form POST).
        [HttpPost]
        public IActionResult Send(int receiverId, string content, int? listingId)
        {
            if (UserId == null) return RedirectToAction("Login", "Account");

            // Don't send empty messages — just redirect back to the chat
            if (string.IsNullOrWhiteSpace(content))
                return RedirectToAction("Chat", new { userId = receiverId, listingId });

            // Load the sender's info to save their name with the message
            var sender = _db.GetUserById(UserId.Value);

            // Save the message to the Messages table in the database
            _db.SendMessage(new Message
            {
                SenderId = UserId.Value,
                ReceiverId = receiverId,
                SenderName = sender?.FullName ?? "",
                ListingId = listingId,          // Which listing this message is about (optional)
                Content = content.Trim()        // Remove leading/trailing whitespace from the message
            });

            // Redirect back to the chat after sending
            return RedirectToAction("Chat", new { userId = receiverId, listingId });
        }

        // ── GET UNREAD COUNT ── (GET /Messages/GetUnreadCount)
        // Returns the number of unread messages as JSON.
        // Called by JavaScript periodically to update the notification badge in the navbar.
        public IActionResult GetUnreadCount()
        {
            if (UserId == null) return Json(new { count = 0 }); // Not logged in = 0 unread
            return Json(new { count = _db.GetUnreadMessageCount(UserId.Value) });
        }

        // ── SEND AJAX ── (POST /Messages/SendAjax)
        // An alternative send endpoint used by JavaScript for a smoother chat experience.
        // Receives JSON data from the browser and returns a JSON result (no page reload).
        // [FromBody] means the data comes from the request body (JSON), not the URL.
        [HttpPost]
        public IActionResult SendAjax([FromBody] AjaxMessageModel model)
        {
            if (UserId == null) return Json(new { success = false, message = "Not logged in." });
            if (string.IsNullOrWhiteSpace(model.Content)) return Json(new { success = false });

            var sender = _db.GetUserById(UserId.Value);

            // Save the message (same as regular Send, just via JSON)
            _db.SendMessage(new Message
            {
                SenderId = UserId.Value,
                ReceiverId = model.ReceiverId,
                SenderName = sender?.FullName ?? "",
                ListingId = model.ListingId,
                Content = model.Content.Trim()
            });

            return Json(new { success = true }); // Tell the browser the message was sent successfully
        }
    }

    // ── AJAX MESSAGE MODEL ──
    // A simple data class used to receive the JSON body from the SendAjax endpoint.
    // When the browser sends JSON to SendAjax, ASP.NET maps it to this class automatically.
    public class AjaxMessageModel
    {
        public int ReceiverId { get; set; }       // ID of the user receiving the message
        public string Content { get; set; } = ""; // Text content of the message
        public int? ListingId { get; set; }        // Optional: the listing the message is about
    }
}
