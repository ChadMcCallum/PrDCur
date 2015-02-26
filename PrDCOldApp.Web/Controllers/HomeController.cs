using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
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

        public static string DataURLPrefix = "data:image/png;base64,";

        [HttpPost]
        public ActionResult Upload(UploadEntry entry)
        {
            if (entry.UploadFile != null && entry.UploadFile.ContentLength > 0)
            {
                var path = "/Content/Uploads/" + Path.GetRandomFileName();
                path = Path.ChangeExtension(path, Path.GetExtension(entry.UploadFile.FileName));
                entry.UploadFile.SaveAs(Server.MapPath(path));
                entry.UploadUrl = path;
            }
            else if (!string.IsNullOrEmpty(entry.CameraImage))
            {
                var path = "/Content/Uploads/" + Path.GetRandomFileName();
                path = Path.ChangeExtension(path, "png");
                System.IO.File.WriteAllBytes(Server.MapPath(path), Convert.FromBase64String(entry.CameraImage.Substring(DataURLPrefix.Length)));
                entry.UploadUrl = path;
            }

            SaveUploadEntryInDatabase(entry);

            return RedirectToAction("Index");
        }

        private static void SaveUploadEntryInDatabase(UploadEntry entry)
        {
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
            var c = new Comment() { Content = comment, User = username };
            ImageEntry image;
            using (var context = new ImageEntryContext())
            {
                image = context.Images.Find(id);
                image.Comments.Add(c);
                context.SaveChanges();
            }
            return View(image);
        }

        public HttpStatusCodeResult SendData()
        {
            HttpContext.AcceptWebSocketRequest(HandleWebSocket);
            return new HttpStatusCodeResult(HttpStatusCode.SwitchingProtocols);
        }

        private async Task HandleWebSocket(WebSocketContext context)
        {
            var closed = false;
            var maxMessageSize = 1024;
            var data = new byte[maxMessageSize];
            var socket = context.WebSocket;
            UploadEntry uploadModel = null;
            while (!closed)
            {
                try
                {
                    var receive = await socket.ReceiveAsync(new ArraySegment<byte>(data), CancellationToken.None);
                    if (receive.MessageType == WebSocketMessageType.Close)
                    {
                        SaveUploadEntryInDatabase(uploadModel);
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        closed = true;
                    }
                    else if (receive.MessageType == WebSocketMessageType.Binary && uploadModel != null)
                    {
                        using (var writer = new BinaryWriter(new FileStream(Server.MapPath(uploadModel.UploadUrl), FileMode.Append)))
                        {
                            writer.Write(data, 0, receive.Count);
                        }
                        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{ \"received\": true }")),
                            WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        var receivedString = Encoding.UTF8.GetString(data, 0, receive.Count);
                        uploadModel = JsonConvert.DeserializeObject<UploadEntry>(receivedString);
                        var path = "/Content/Uploads/" + Path.GetRandomFileName();
                        path = Path.ChangeExtension(path, "png");
                        uploadModel.UploadUrl = path;
                        var output = new ArraySegment<byte>(Encoding.UTF8.GetBytes("{ \"accepted\": true }"));
                        await socket.SendAsync(output, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    if (socket != null)
                        socket.CloseAsync(WebSocketCloseStatus.InternalServerError, ex.Message, CancellationToken.None);

                    closed = true;
                }
            }
        }

        public ActionResult Events(HttpRequestMessage message)
        {

        }
    }

    public class UploadEntry
    {
        public HttpPostedFileBase UploadFile { get; set; }
        public string UploadUrl { get; set; }
        public string EmailAddress { get; set; }
        public DateTime? DatePublished { get; set; }
        public string Location { get; set; }
        public string CameraImage { get; set; }
    }
}