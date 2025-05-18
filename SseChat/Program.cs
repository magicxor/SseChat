using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;

namespace SseChat;

public class Program
{
    private static readonly ConcurrentDictionary<string, ChatRoomLog> ChatRoomMessages = new();

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var app = builder.Build();

        app.MapGet("/rooms/{room}", (
            [FromRoute] [Required] string room,
            [FromServices] IServer server) =>
        {
            var serverUrl = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? throw new InvalidOperationException("No server address configured");
            var htmlContent = $"""
                               <!doctype html>
                               <html lang=en>
                               <head>
                               <meta charset=utf-8>
                               <meta name="color-scheme" content="dark light">
                               <title>Chat</title>
                               </head>
                               <body>
                                   <iframe src="{serverUrl}/rooms/{room}/live-messages" style="width: 100%; height: 50vh; border: none;"></iframe>
                                   <iframe src="{serverUrl}/rooms/{room}/new-message-form" style="width: 100%; height: 50vh; border: none;"></iframe>
                               </body>
                               </html>
                               """;
            return TypedResults.Content(
                content: htmlContent,
                contentType: "text/html");
        }).WithName("Chat room page HTML");

        app.MapGet("/rooms/{room}/new-message-form", (
            [FromRoute] [Required] string room,
            [FromServices] IServer server) =>
        {
            var serverUrl = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? throw new InvalidOperationException("No server address configured");
            var htmlContent = $"""
                               <!doctype html>
                               <html lang=en>
                               <head>
                               <meta charset=utf-8>
                               <meta name="color-scheme" content="dark light">
                               <title>Chat form</title>
                               </head>
                               <body>
                                   <form action="{serverUrl}/rooms/{room}/messages" method="POST">
                                       <label for="text">Message text:</label>
                               		<br>
                                       <textarea type="text" id="text" name="text" rows="10" style="width: 100%;" required></textarea>
                               		<br>
                                       <button type="submit">Send</button>
                                   </form>
                               </body>
                               </html>
                               """;
            return TypedResults.Content(
                content: htmlContent,
                contentType: "text/html");
        }).WithName("Chat room new message form HTML");

        app.MapGet("/rooms/{room}/live-messages", (
            [FromRoute] [Required] string room,
            [FromQuery] DateTime? since,
            CancellationToken cancellationToken) =>
        {
            async IAsyncEnumerable<SseItem<string>> GetChatMessages(
                DateTime initAt,
                [EnumeratorCancellation] CancellationToken ct)
            {
                yield return new SseItem<string>($"you joined the room '{room}'");

                var timestamp = initAt;

                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(1000, ct);
                    var chatRoomLog = ChatRoomMessages.GetOrAdd(room, _ => new ChatRoomLog());
                    var currentBatch = chatRoomLog.GetMessagesSince(timestamp);
                    timestamp = DateTime.UtcNow;

                    foreach (var message in currentBatch)
                    {
                        yield return new SseItem<string>($"[{message.Timestamp}] {message.UserName ?? "Anonymous"}: {message.Text}");
                    }
                }
            }

            return TypedResults.ServerSentEvents(GetChatMessages(since ?? DateTime.UtcNow, cancellationToken));
        }).WithName("Chat room messages SSE");

        app.MapPost("/rooms/{room}/messages", (
            [FromRoute] [Required] string room,
            [FromForm] [Required] string text) =>
        {
            var chatRoomLog = ChatRoomMessages.GetOrAdd(room, _ => new ChatRoomLog());
            var message = new ChatMessage(null, text);
            chatRoomLog.AddMessage(message);
            return Results.LocalRedirect($"/rooms/{room}/new-message-form");
        }).WithName("Chat room new message POST").DisableAntiforgery();

        app.Run();
    }
}
