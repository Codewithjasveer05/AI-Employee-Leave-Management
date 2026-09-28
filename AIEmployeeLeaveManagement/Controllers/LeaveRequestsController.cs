using AIEmployeeLeaveManagement.Data;
using AIEmployeeLeaveManagement.Models;
using AIEmployeeLeaveManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIEmployeeLeaveManagement.Controllers
{
    public class LeaveRequestsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly AIService _ai;

        public LeaveRequestsController(AppDbContext context, AIService ai)
        {
            _context = context;
            _ai = ai;
        }

        // READ: list all leave records (with search by employee name)
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.LeaveRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(l => l.EmployeeName.Contains(search));
            }

            ViewBag.Search = search;
            var list = await query.OrderByDescending(l => l.AppliedOn).ToListAsync();
            return View(list);
        }

        // CREATE: show form
        public IActionResult Create()
        {
            return View(new LeaveRequest());
        }

        // CREATE: save form
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveRequest leave)
        {
            if (ModelState.IsValid)
            {
                leave.Status = "Pending";
                leave.AppliedOn = DateTime.Now;
                _context.LeaveRequests.Add(leave);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Leave request submitted successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(leave);
        }

        // UPDATE: show edit form
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave == null) return NotFound();

            return View(leave);
        }

        // UPDATE: save changes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LeaveRequest leave)
        {
            if (id != leave.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(leave);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Leave request updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(leave);
        }

        // DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave != null)
            {
                _context.LeaveRequests.Remove(leave);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Leave request deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        // Approve / Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, string status)
        {
            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave != null)
            {
                leave.Status = status;
                await _context.SaveChangesAsync();
                TempData["Message"] = "Leave " + status.ToLower() + ".";
            }
            return RedirectToAction(nameof(Index));
        }

        // AI FEATURE: analyze leave patterns
        public async Task<IActionResult> Analyze()
        {
            var leaves = await _context.LeaveRequests.ToListAsync();

            ViewBag.Total = leaves.Count;
            ViewBag.Approved = leaves.Count(l => l.Status == "Approved");
            ViewBag.Pending = leaves.Count(l => l.Status == "Pending");
            ViewBag.Rejected = leaves.Count(l => l.Status == "Rejected");

            if (!leaves.Any())
            {
                ViewBag.Summary = "No leave records found.";
                ViewBag.Suggestion = "Please add some leave requests first.";
                return View();
            }

            // C# calculation part
            var byEmployee = leaves.GroupBy(l => l.EmployeeName).Select(g =>
                "- " + g.Key + ": " + g.Count() + " requests, " +
                g.Sum(x => x.TotalDays) + " total days, types: " +
                string.Join(", ", g.Select(x => x.LeaveType).Distinct()));

            var byMonth = leaves.GroupBy(l => l.StartDate.ToString("MMMM")).Select(g =>
                "- " + g.Key + ": " + g.Count() + " requests");

            var byType = leaves.GroupBy(l => l.LeaveType).Select(g =>
                "- " + g.Key + ": " + g.Count() + " requests");

            string summary =
                "Leave per employee:\n" + string.Join("\n", byEmployee) +
                "\n\nLeave per month:\n" + string.Join("\n", byMonth) +
                "\n\nLeave per type:\n" + string.Join("\n", byType);

            // Prompt sent to AI
            string prompt =
                "You are an HR assistant. Here is a summary of employee leave records:\n\n" +
                summary +
                "\n\nAnalyze the leave patterns and give 4 short, practical suggestions for the HR manager. " +
                "Mention employees with frequent leave, busy months, and leave type trends. " +
                "Use simple English and bullet points.";

            ViewBag.Summary = summary;
            ViewBag.Suggestion = await _ai.GetSuggestionAsync(prompt);

            return View();
        }
    }
}