using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PrDCOldApp.Web.Models;

namespace PrDCOldApp.Web.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/
        public ActionResult Index()
        {
            List<ImageEntry> model = null;
            using (var context = new ImageEntryContext())
            {
                model = context.Images.Take(16).ToList();
            }
            return View(model);
        }

        public ActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Upload(UploadEntry entry)
        {
            if (entry.UploadFile.ContentLength > 0)
            {
                var path = "/Content/Uploads/" + Path.GetRandomFileName();
                path = Path.ChangeExtension(path, Path.GetExtension(entry.UploadFile.FileName));
                entry.UploadFile.SaveAs(Server.MapPath(path));
                entry.UploadUrl = path;
            }

            var imageEntry = new ImageEntry()
            {
                Path = entry.UploadUrl,
                Email = entry.EmailAddress,
                Published = entry.DatePublished,
                Location = entry.Location
            };

            using (var context = new ImageEntryContext())
            {
                context.Images.Add(imageEntry);
                context.SaveChanges();
            }

            return RedirectToAction("Index");
        }

        public ActionResult Image(int id)
        {
            ImageEntry image;
            using (var context = new ImageEntryContext())
            {
                image = context.Images.Find(id);
            }
            return View(image);
        }

        [HttpPost]
        public ActionResult Image(int id, string username, string comment)
        {
            var c = new Comment() {Content = comment, User = username};
            ImageEntry image;
            using (var context = new ImageEntryContext())
            {
                image = context.Images.Find(id);
                image.Comments.Add(c);
                context.SaveChanges();
            }
            return View(image);
        }
	}

    public class UploadEntry
    {
        public HttpPostedFileBase UploadFile { get; set; }
        public string UploadUrl { get; set; }
        public string EmailAddress { get; set; }
        public DateTime? DatePublished { get; set; }
        public string Location { get; set; }
    }
}