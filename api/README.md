# API Handler Layout

Each `.ashx` file in this folder is intentionally a one-line ASP.NET WebHandler directive, for example:

```aspx
<%@ WebHandler Language="C#" Class="PublicBoardActivePosts" %>
```

The handler implementation classes live in the application-root `App_Code` folder:

- `App_Code/Handlers.cs` contains endpoint classes such as `PublicBoardActivePosts`, `AuthWhoAmI`, `PostsList`, `PostsRenew`, `RumpPull`, `UsersList`, and `SectionsSetPublicDisplay`.
- `App_Code/DashboardCore.cs` contains shared request, JSON, database, current-user, and validation helpers.
- `App_Code/PostRepository.cs` contains post data access and post lifecycle rules.
- `App_Code/RumpRepository.cs` contains the configurable RUMP pull path for TSUL and TSUS draft post creation.
- `App_Code/UserRepository.cs` contains user and section data access.
- `App_Code/PublicBoardRealtime.cs` contains the public-board WebSocket handler and in-memory broadcast hub.

This is valid ASP.NET Web Site style. IIS/ASP.NET compiles `App_Code` at runtime, then each `.ashx` directive resolves its `Class` value to the matching public handler class.

If IIS reports that a handler class cannot be found, check these first:

1. The IIS application root must be `C:\Users\Admin\Documents\Web\TSU_Dashboard`, not the `api` subfolder.
2. The `App_Code` folder must remain at the application root.
3. The app pool must use .NET Framework 4.x with Integrated pipeline.
4. The `.ashx` directive `Class` value must match a public class name in `App_Code/Handlers.cs`.

`api/public-board/socket.ashx` is the realtime WebSocket endpoint. It resolves to `PublicBoardSocket` in `App_Code/PublicBoardRealtime.cs`.
