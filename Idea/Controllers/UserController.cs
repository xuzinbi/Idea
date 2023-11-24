using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Idea.Data;
using Idea.Models;
using Microsoft.AspNetCore.Identity;

namespace Idea.Areas.Admin
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cUser = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);

            if (cUser == null)
            {
                return NotFound();
            }

            return View(cUser);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cUser = await _context.Users.FindAsync(id);
            if (cUser == null)
            {
                return NotFound();
            }
            return View(cUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ApplicationUser cUser)
        {
            if (id != cUser.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cUser);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CUserExists(cUser.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(cUser);
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cUser = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (cUser == null)
            {
                return NotFound();
            }

            return View(cUser);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cUser = await _context.Users.FindAsync(id);
            _context.Users.Remove(cUser);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CUserExists(string id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        public async Task<IActionResult> AddRole(string id)
        {
            var user = await _context.Users.FindAsync(id);
            var roleIds = _context.UserRoles.ToList().Where(ur => ur.UserId == id).Select(ur => ur.RoleId);

            ViewData["currentRoles"] = _context.Roles.Where(r => roleIds.Contains(r.Id)).ToList();
            ViewData["remainingRoles"] = _context.Roles.Where(r => !roleIds.Contains(r.Id)).ToList();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRole(string userId, string roleId)
        {
            if (userId != null && roleId != null)
            {
                _context.Add(new IdentityUserRole<string>()
                {
                    UserId = userId,
                    RoleId = roleId
                });

                await _context.SaveChangesAsync();
            }

            var roleIds = _context.UserRoles.ToList().Where(ur => ur.UserId == userId).Select(ur => ur.RoleId);

            ViewData["currentRoles"] = _context.Roles.Where(r => roleIds.Contains(r.Id)).ToList();
            ViewData["remainingRoles"] = _context.Roles.Where(r => !roleIds.Contains(r.Id)).ToList();

            return RedirectToAction("AddRole", new { id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(string userId, string roleId)
        {
            if (userId != null && roleId != null)
            {
                _context.UserRoles.Remove(new IdentityUserRole<string>()
                {
                    UserId = userId,
                    RoleId = roleId
                });

                await _context.SaveChangesAsync();
            }

            var roleIds = _context.UserRoles.ToList().Where(ur => ur.UserId == userId).Select(ur => ur.RoleId);

            ViewData["currentRoles"] = _context.Roles.Where(r => roleIds.Contains(r.Id)).ToList();
            ViewData["remainingRoles"] = _context.Roles.Where(r => !roleIds.Contains(r.Id)).ToList();

            return RedirectToAction("AddRole", new { id = userId });
        }
    }
}