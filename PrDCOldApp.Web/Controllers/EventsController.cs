using System;
using System.Collections.Concurrent;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web.Http;
using Newtonsoft.Json;
using PrDCOldApp.Web.Models;

namespace PrDCOldApp.Web.Controllers
{
    public class EventsController : ApiController
    {
        private static Timer _timer = default(Timer);
        private static ConcurrentQueue<Client> _clients = new ConcurrentQueue<Client>();

        public EventsController()
        {
            _timer = _timer ?? new Timer((s) =>
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                using (var context = new ImageEntryContext())
                {
                    foreach (var c in _clients)
                    {
                        try
                        {
                            var image = context.Images.Include(i => i.Comments).First(i => i.Id == c.ImageId);
                            var lastComments = image.Comments.Where(comment => comment.Id > c.LastMessage);
                            if (lastComments.Any())
                            {
                                c.StreamWriter.WriteLine("data:" +
                                                         JsonConvert.SerializeObject(lastComments.ToArray()));
                                c.StreamWriter.WriteLine();
                                c.StreamWriter.Flush();
                                c.LastMessage = lastComments.Max(lc => lc.Id);
                            }
                            c.StreamWriter.WriteLine("data:" +
                                                     JsonConvert.SerializeObject(new {Likes = image.Likes}));
                            c.StreamWriter.WriteLine();
                            c.StreamWriter.Flush();
                        }
                        catch
                        {
                            _clients = new ConcurrentQueue<Client>(_clients.Where(client => client != c));
                        }
                    }
                }
                _timer.Change(1000, 1000);
            }, null, 0, 1000);
        }

        public HttpResponseMessage Get(HttpRequestMessage request, int id)
        {
            int lastCommentId = 0;
            using (var context = new ImageEntryContext())
            {
                lastCommentId = context.Images.Include(i => i.Comments).First(i => i.Id == id).Comments.Max(c => c.Id);
            }
            var client = new Client() {ImageId = id, LastMessage = lastCommentId };
            var response = request.CreateResponse();
            response.Content = new PushStreamContent((s, c, t) =>
            {
                var writer = new StreamWriter(s);
                client.StreamWriter = writer;
                _clients.Enqueue(client);
            }, "text/event-stream");
            return response;
        }
    }

    public class Client
    {
        public int ImageId { get; set; }
        public int LastMessage { get; set; }
        public StreamWriter StreamWriter { get; set; }
    }
}
