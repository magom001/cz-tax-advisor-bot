# Task 008 — Web Platform: SignalR Chat Hub & File Upload

## Objective

Build the ASP.NET Core web platform with a SignalR hub for real-time chat and a file upload endpoint.

## Work Items

1. Configure `Program.cs` with HostBuilder:
   - Register core services via `AddInfrastructureServices()`.
   - Add SignalR services.
   - Add CORS policy for local development.
   - Configure `user-secrets` for Azure AI keys.
2. Implement `ChatHub : Hub`:
   - `SendMessage(string message)` — receives user message, invokes `IConversationService`, streams responses back.
   - `SendProgress` — push progress updates to connected client.
   - `SendCompletion` — push final response with citations.
3. Implement `SignalRNotificationService : INotificationService` — bridges core notifications to SignalR clients.
4. Implement `DocumentController`:
   - `POST /api/documents/upload` — accepts file uploads, validates type/size, enqueues extraction job.
5. Create minimal `wwwroot/index.html` with:
   - Chat input box + message list.
   - SignalR client connection.
   - File drop zone.
6. Register the Web project in AppHost with Qdrant reference.
7. Write integration tests with `WebApplicationFactory`:
   - SignalR connection can be established.
   - File upload endpoint accepts and rejects correct file types.

## Expected Results

- User can open the web page, type a message, and receive a streamed AI response.
- Progress updates appear in real-time during long operations.
- Files can be uploaded via drag-and-drop.
- The UI never freezes — all communication is async via WebSocket.
- Integration tests pass.
