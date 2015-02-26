using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Imaging;
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

        public static string DataUrlPrefix = "data:image/png;base64,";

        [HttpPost]
        public ActionResult Upload(UploadEntry entry)
        {
            if (entry.UploadFile != null && entry.UploadFile.ContentLength > 0)
            {
                var path = "/Content/Uploads/" + Path.GetRandomFileName();
                path = Path.ChangeExtension(path, Path.GetExtension(entry.UploadFile.FileName));
                entry.UploadFile.SaveAs(Server.MapPath(path));
                SaveThumbs(Server.MapPath(path));
                entry.UploadUrl = path;
            }
            else if (!string.IsNullOrEmpty(entry.CameraImage))
            {
                var path = "/Content/Uploads/" + Path.GetRandomFileName();
                path = Path.ChangeExtension(path, "png");
                System.IO.File.WriteAllBytes(Server.MapPath(path), Convert.FromBase64String(entry.CameraImage.Substring(DataUrlPrefix.Length)));
                SaveThumbs(Server.MapPath(path));
                entry.UploadUrl = path;
            }

            SaveUploadEntryInDatabase(entry);

            return RedirectToAction("Index");
        }

        private void SaveThumbs(string path)
        {
            var original = System.Drawing.Image.FromFile(path);
            var thumbnail = ScaleImage(original, 400, 400);
            var thumbnailPath = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + "_t.png";
            thumbnail.Save(thumbnailPath, ImageFormat.Png);
        }

        public static Image ScaleImage(Image image, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            var newImage = new Bitmap(newWidth, newHeight);
            var g = Graphics.FromImage(newImage);
            //.DrawImage(image, 0, 0, newWidth, newHeight);
            ColorMatrix colorMatrix = new ColorMatrix(
              new float[][] 
              {
                 new float[] {.3f, .3f, .3f, 0, 0},
                 new float[] {.59f, .59f, .59f, 0, 0},
                 new float[] {.11f, .11f, .11f, 0, 0},
                 new float[] {0, 0, 0, 1, 0},
                 new float[] {0, 0, 0, 0, 1}
              });

            //create some image attributes
            ImageAttributes attributes = new ImageAttributes();

            //set the color matrix attribute
            attributes.SetColorMatrix(colorMatrix);

            //draw the original image on the new image
            //using the grayscale color matrix
            g.DrawImage(image, new Rectangle(0, 0, newWidth, newHeight),
               0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

            //dispose the Graphics object
            g.Dispose();
            return newImage;
        }

        private static void SaveUploadEntryInDatabase(UploadEntry entry)
        {
            var imageEntry = new ImageEntry()
            {
                Path = entry.UploadUrl,
                Email = entry.EmailAddress,
                Published = entry.DatePublished,
                Location = entry.Location,
                Likes = 1
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
                image = context.Images.Include(i => i.Comments).First(i => i.Id == id);
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
                image = context.Images.Include(i => i.Comments).First(i => i.Id == id);
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
                        SaveThumbs(Server.MapPath(uploadModel.UploadUrl));
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