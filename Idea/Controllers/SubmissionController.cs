using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Idea.Data;
using Idea.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.IO.Compression;
using System.Net.Mime;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using MimeKit;
using MailKit.Net.Smtp;
using Org.BouncyCastle.Crypto;
using System.Xml.Linq;

namespace Idea.Controllers
{
    public class SubmissionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubmissionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminSubmission
        public async Task<IActionResult> Index()
        {
            return View(await _context.Submissions.ToListAsync());
        }

        // GET: AdminSubmission/Details/5
        public async Task<IActionResult> ViewIdeas(int? submissionid, int departmentId = -1)
        {
            if (submissionid == null)
            {
                return NotFound();
            }

            var submission = await _context.Submissions.FirstOrDefaultAsync(m => m.Id == submissionid);

            if (submission == null)
            {
                return NotFound();
            }

            List<ApplicationUser>? coordinatorList = new List<ApplicationUser>();

            if (departmentId != -1) {
                var coordinatorIds = await _context.UserRoles.Where(u => u.RoleId == "Coordinator").Select(u => u.UserId).ToListAsync();
                var coordinators = await _context.Users.Include(u => u.Department).Where(u => u.DepartmentId == departmentId && coordinatorIds.Contains(u.Id)).ToListAsync();

                foreach (var coordinator in coordinators)
                    coordinatorList.Add(coordinator);
            }

            ViewData["Ideas"] = await _context.Ideas.Include(i => i.Reactions).Where(i => i.SubmissionId == submissionid).ToListAsync();
            ViewData["CoordinatorList"] = coordinatorList;

            return View(submission);
        }

        // GET: Idea/Create
        public IActionResult AddIdea(int submissionid)
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            ViewData["SubmissionId"] = submissionid;
            ViewData["UserId"] = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIdea(NIdea nIdea, IFormFile file, bool isAcceptTerms)
        {
            //if (ModelState.IsValid)
            if (isAcceptTerms)
            {
                var submission = await _context.Submissions.FindAsync(nIdea.SubmissionId);

                if (submission.Deadline_1 >= DateTime.Now)
                {
                    _context.Add(nIdea);
                    await _context.SaveChangesAsync();

                    nIdea.FilePath = UploadFile(file, submission.Id, nIdea.Id);

                    _context.Update(nIdea);
                    await _context.SaveChangesAsync();

                    var user = await _context.Users.FindAsync(nIdea.UserId);
                    var departmentId = user.DepartmentId;
                    var coordinatorIds = await _context.UserRoles.Where(u => u.RoleId == "Coordinator").Select(u => u.UserId).ToListAsync();
                    var coordinators = await _context.Users.Include(u => u.Department).Where(u => u.DepartmentId == departmentId && coordinatorIds.Contains(u.Id)).ToListAsync();

                    string subject = $"New idea in the submission '{submission.Name}'";

                    List<ApplicationUser>? coordinatorList = new List<ApplicationUser>();

                    foreach (var coordinator in coordinators)
                    {
                        string content = $"Hello {coordinator.UserName},\n\n" +
                            $"Your department receives a new idea in the submission '{submission.Name}'.\n\n" +
                            $"Best regards,";

                        //SendEmail(coordinator.Id, content, subject);
                        coordinatorList.Add(coordinator);
                    }

                    return RedirectToAction(nameof(ViewIdeas), new { submissionid = nIdea.SubmissionId, departmentId = departmentId });
                }
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            ViewData["SubmissionId"] = nIdea.SubmissionId;
            ViewData["UserId"] = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return View(nIdea);
        }

        public string UploadFile(IFormFile file, int submissionId, int ideaId)
        {
            var path = "";

            if (file != null && file.Length > 0)
            {
                // file / submission_{id} / idea_{id}
                path = Path.Combine("file", $"submission_{submissionId}", $"idea_{ideaId}");

                if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }

                // {path} / {filename}
                path = Path.Combine(path, file.FileName);
                using var stream = new FileStream(path, FileMode.Create);
                file.CopyTo(stream);
            }

            return path;
        }

        public async Task<IActionResult> IdeaDetails(int? id, int isComment = -1)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nIdea = await _context.Ideas.Include(n => n.User).Include(n => n.Category).Include(n => n.Submission).FirstOrDefaultAsync(m => m.Id == id);

            if (nIdea == null)
            {
                return NotFound();
            }

            ViewData["Comments"] = await _context.Comments.Where(c => c.IdeaId == id).ToListAsync();
            ViewData["isComment"] = isComment;

            nIdea.View += 1;

            _context.Update(nIdea);
            await _context.SaveChangesAsync();

            return View(nIdea);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comment(string Content, int IdeaId)
        {
            var idea = await _context.Ideas.FindAsync(IdeaId);
            var submission = await _context.Submissions.FindAsync(idea.SubmissionId);

            int isComment = -1;

            if (submission.Deadline_2 >= DateTime.Now)
            {
                var comment = new Comment();
                comment.Content = Content;
                comment.IdeaId = IdeaId;
                comment.Created_Date = DateTime.Now;
                comment.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.Add(comment);
                await _context.SaveChangesAsync();

                var user = await _context.Users.FindAsync(comment.UserId);

                string subject = $"New comment on your idea '{idea.Title}'";
                string content = $"Hello {user.UserName},\n\n" +
                                 $"You have a new comment on the idea '{idea.Title}' in the submission '{submission.Name}'.\n\n" +
                                 $"Thank you for your interesting idea,\n\n" +
                                 $"Best regards,";

                //SendEmail(idea.UserId, content, subject);

                isComment = 1;
            }

            return RedirectToAction(nameof(IdeaDetails), new { id = IdeaId, isComment = isComment });
        }

        public async Task<IActionResult> Like(int ideaid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var reaction = await _context.Reactions.Where(r => r.IdeaId == ideaid && r.UserId == userId).FirstOrDefaultAsync();

            if (reaction == null)
            {
                reaction = new Reaction();
                reaction.Type = 1;
                reaction.UserId = userId;
                reaction.IdeaId = ideaid;

                _context.Add(reaction);
                await _context.SaveChangesAsync();
            }

            else
            {
                if (reaction.Type == 1)
                {
                    reaction.Type = 0;
                }
                else
                {
                    reaction.Type = 1;
                }

                _context.Update(reaction);
                await _context.SaveChangesAsync();
            }

            var idea = await _context.Ideas.FindAsync(ideaid);

            return RedirectToAction(nameof(ViewIdeas), new { submissionid = idea.SubmissionId });
        }

        public async Task<IActionResult> Dislike(int ideaid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var reaction = await _context.Reactions.Where(r => r.IdeaId == ideaid && r.UserId == userId).FirstOrDefaultAsync();

            if (reaction == null)
            {
                reaction = new Reaction();
                reaction.Type = 2;
                reaction.UserId = userId;
                reaction.IdeaId = ideaid;

                _context.Add(reaction);
                await _context.SaveChangesAsync();
            }

            else
            {
                if (reaction.Type == 2)
                {
                    reaction.Type = 0;
                }
                else
                {
                    reaction.Type = 2;
                }

                _context.Update(reaction);
                await _context.SaveChangesAsync();
            }

            var idea = await _context.Ideas.FindAsync(ideaid);

            return RedirectToAction(nameof(ViewIdeas), new { submissionid = idea.SubmissionId });
        }

        public IActionResult ExportZIP(int submissionId)
        {
            var path = Path.Combine("file", "submission_" + submissionId);

            if (Directory.Exists(path))
            {
                var zipPath = Path.Combine("file", $"submission_{submissionId}.zip");

                ZipFile.CreateFromDirectory(path, zipPath);

                byte[] fileBytes = System.IO.File.ReadAllBytes(zipPath);

                System.IO.File.Delete(zipPath);

                return File(fileBytes, MediaTypeNames.Application.Zip, Path.GetFileName(zipPath));
            }

            return NoContent();
        }

        public async Task<IActionResult> ExportExcel(int submissionId)
        {
            // Prepare Excel file.
            var path = Path.Combine("file", $"submission_{submissionId}.xlsx");
            FileInfo file = new FileInfo(path);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var ideas = await _context.Ideas.Include(i => i.Reactions).Where(i => i.SubmissionId == submissionId).ToListAsync();

                int rowNum = 0;
                IWorkbook workbook;
                workbook = new XSSFWorkbook();
                ISheet excelSheet = workbook.CreateSheet("Idea List");

                IRow row = excelSheet.CreateRow(rowNum++);
                row.CreateCell(0).SetCellValue("No.");
                row.CreateCell(1).SetCellValue("Title");
                row.CreateCell(2).SetCellValue("Brief");
                row.CreateCell(3).SetCellValue("Content");
                row.CreateCell(4).SetCellValue("File Path");
                row.CreateCell(5).SetCellValue("Total View");
                row.CreateCell(6).SetCellValue("Total Like");
                row.CreateCell(7).SetCellValue("Total Dislike");

                foreach (var idea in ideas)
                {
                    row = excelSheet.CreateRow(rowNum);

                    row.CreateCell(0).SetCellValue(rowNum);
                    row.CreateCell(1).SetCellValue(idea.Title);
                    row.CreateCell(2).SetCellValue(idea.Brief);
                    row.CreateCell(3).SetCellValue(idea.Content);
                    row.CreateCell(4).SetCellValue(idea.FilePath);
                    row.CreateCell(5).SetCellValue(idea.View);
                    row.CreateCell(6).SetCellValue(idea.Reactions.Where(i => i.Type == 1).Count().ToString());
                    row.CreateCell(7).SetCellValue(idea.Reactions.Where(i => i.Type == 2).Count().ToString());

                    ++rowNum;
                }

                workbook.Write(fs);
            }

            // Download file.
            byte[] fileBytes = System.IO.File.ReadAllBytes(path);

            System.IO.File.Delete(path);

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Path.GetFileName(path));
        }

        public async void SendEmail(string userId, string content, string subject)
        {
            var user = await _context.Users.FindAsync(userId);

            MailboxAddress from = new MailboxAddress("FGW Idea Management System", "honguyenphubao@gmail.com");
            MailboxAddress to = new MailboxAddress(user.UserName, user.Email);

            BodyBuilder bodyBuilder = new BodyBuilder();
            bodyBuilder.TextBody = content;

            MimeMessage message = new MimeMessage();
            message.From.Add(from);
            message.To.Add(to);
            message.Subject = subject;
            message.Body = bodyBuilder.ToMessageBody();

            SmtpClient client = new SmtpClient();
            client.Connect("smtp.gmail.com", 465, true);
            client.Authenticate("honguyenphubao", "lzsopinuhlmamavg");

            client.Send(message);
            client.Disconnect(true);
            client.Dispose();
        }

        public async Task<IActionResult> Statistics()
        {
            ViewData["Departments"] = await _context.Departments.ToListAsync();
            ViewData["Ideas"] = await _context.Ideas.Include(i => i.User).ThenInclude(u => u.Department).ToListAsync();

            return View();
        }
    }
}