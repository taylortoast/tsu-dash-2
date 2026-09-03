using System.Collections.Generic;
using System.Web;
using System.Configuration;
using System.Data.SqlClient;
using System;

public class AuthWhoAmI : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        // Fetch the user from the database (now that permissions are fixed)
        AppUser user = CurrentUser.Ensure(context);

        // Return ONLY the clean user object
        Json.Ok(context, new Dictionary<string, object>
        {
            { "user", CurrentUser.ToJson(user) }
        });
    }
}

public class ProjectBoardGet : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireAssignmentsBoardRead(context);
        string sectionCode = context.Request.QueryString["section"] ?? "";

        Json.Ok(context, new Dictionary<string, object>
        {
            { "user", CurrentUser.ToJson(user) },
            { "projects", ProjectBoardRepository.ListForBoard(user, sectionCode) },
            { "sections", user.IsGuest ? UserRepository.PublicSections() : UserRepository.Sections() },
            { "workers", ProjectBoardRepository.ListWorkersForBoard() }
        });
    }
}

public class ProjectBoardAddNote : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireAssignmentsBoardAccess(context);
        Dictionary<string, object> body = Json.ReadBody(context);

        int postId = Input.Int(body, "postId");
        string noteText = Input.String(body, "noteText");

        ProjectBoardRepository.AddNote(user, postId, noteText);

        Json.Ok(context, new Dictionary<string, object>
        {
            { "updated", true }
        });
    }
}

public class ProjectBoardAssignWorker : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireAssignmentsBoardAccess(context);
        Dictionary<string, object> body = Json.ReadBody(context);

        int postId = Input.Int(body, "postId");
        string workerName = Input.String(body, "workerName");
        string assignmentRole = Input.String(body, "assignmentRole");

        ProjectBoardRepository.AddAssignment(user, postId, workerName, assignmentRole);

        Json.Ok(context, new Dictionary<string, object>
        {
            { "updated", true }
        });
    }
}

public class ProjectBoardUnassignWorker : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireAssignmentsBoardAccess(context);
        Dictionary<string, object> body = Json.ReadBody(context);

        int postId = Input.Int(body, "postId");
        string workerName = Input.String(body, "workerName");
        string assignmentRole = Input.String(body, "assignmentRole");

        ProjectBoardRepository.RemoveAssignment(user, postId, workerName, assignmentRole);

        Json.Ok(context, new Dictionary<string, object>
        {
            { "updated", true }
        });
    }
}

public class ProjectBoardSetCategory : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireAssignmentsBoardAccess(context);
        Dictionary<string, object> body = Json.ReadBody(context);

        int postId = Input.Int(body, "postId");
        string category = Input.String(body, "category");

        ProjectBoardRepository.SetCategory(user, postId, category);

        Json.Ok(context, new Dictionary<string, object>
        {
            { "updated", true }
        });
    }
}

public class AuthRoute : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.Ensure(context);
        string status = CurrentUser.GetAccessStatus(user);

        // Routing rules live in CurrentUser.GetAllowedPages so that the router,
        // the client page guards, and the admin route preview cannot drift apart.
        string target = CurrentUser.GetRouteTarget(user);

        Json.Ok(context, new Dictionary<string, object>
        {
            { "user", CurrentUser.ToJson(user) },
            { "target", target },
            { "status", status }
        });
    }
}

public class AuthUpdateDisplayName : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireAuthenticated(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        string displayName = Input.String(body, "displayName");

        UserRepository.UpdateDisplayName(user.UserId, displayName);

        Json.Ok(context, new Dictionary<string, object>
        {
            { "updated", true }
        });
    }
}

public class PostsList : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireReadAccess(context);
        string sectionCode = context.Request.QueryString["section"] ?? "";
        bool includeDisabled = user.IsAdmin &&
            (context.Request.QueryString["includeDisabled"] == "1" ||
             String.Equals(context.Request.QueryString["includeDisabled"], "true", StringComparison.OrdinalIgnoreCase));
        Json.Ok(context, new Dictionary<string, object>
        {
            { "user", CurrentUser.ToJson(user) },
            { "posts", user.IsGuest ? PostRepository.ListPublic() : PostRepository.ListForUser(user, sectionCode, includeDisabled) },
            { "sections", user.IsGuest ? UserRepository.PublicSections() : UserRepository.Sections() }
        });
    }
}

public class PostsGet : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireReadAccess(context);
        int postId = 0;
        int.TryParse(context.Request.QueryString["postId"], out postId);
        if (postId <= 0) throw new System.ArgumentException("PostId is required.");

        Dictionary<string, object> post = PostRepository.GetForUser(user, postId);
        if (post == null) throw new System.ArgumentException("Post was not found.");

        Json.Ok(context, new Dictionary<string, object> { { "post", post } });
    }
}

public class PostsCreate : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireActive(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        int postId = PostRepository.Create(user, body);
        PublicBoardRealtimeHub.BroadcastBoardChanged("post-created");
        Json.Ok(context, new Dictionary<string, object> { { "postId", postId } });
    }
}

public class PostsUpdate : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireActive(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        PostRepository.Update(user, body);
        PublicBoardRealtimeHub.BroadcastBoardChanged("post-updated");
        Json.Ok(context, new Dictionary<string, object> { { "updated", true } });
    }
}

public class PostsSetStatus : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireActive(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        int postId = Input.Int(body, "postId");
        bool isActive = Input.Bool(body, "isActive");
        if (postId <= 0) throw new System.ArgumentException("PostId is required.");

        PostRepository.SetStatus(user, postId, isActive);
        PublicBoardRealtimeHub.BroadcastBoardChanged(isActive ? "post-activated" : "post-deactivated");
        Json.Ok(context, new Dictionary<string, object> { { "updated", true } });
    }
}

public class PostsRenew : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireActive(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        int postId = Input.Int(body, "postId");

        PostRepository.Renew(user, postId);
        PublicBoardRealtimeHub.BroadcastBoardChanged("post-renewed");
        Json.Ok(context, new Dictionary<string, object> { { "updated", true } });
    }
}

public class PublicBoardActivePosts : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        Json.Ok(context, new Dictionary<string, object>
        {
            { "posts", PostRepository.ListPublic() },
            { "sections", UserRepository.PublicSections() }
        });
    }
}

public class UsersList : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireAdmin(context);
        Json.Ok(context, new Dictionary<string, object>
        {
            { "user", CurrentUser.ToJson(user) },
            { "users", UserRepository.List() },
            { "sections", UserRepository.Sections() }
        });
    }
}

public class SectionsSetPublicDisplay : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        CurrentUser.RequireAdmin(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        string sectionCode = Input.String(body, "sectionCode");
        bool isPublicVisible = Input.Bool(body, "isPublicVisible");

        UserRepository.SetPublicDisplay(sectionCode, isPublicVisible);
        PublicBoardRealtimeHub.BroadcastBoardChanged(isPublicVisible ? "section-public-display-enabled" : "section-public-display-disabled");
        Json.Ok(context, new Dictionary<string, object> { { "updated", true } });
    }
}

public class SectionsSetEnabled : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        CurrentUser.RequireAdmin(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        string sectionCode = Input.String(body, "sectionCode");
        bool isEnabled = Input.Bool(body, "isEnabled");

        UserRepository.SetEnabled(sectionCode, isEnabled);
        PublicBoardRealtimeHub.BroadcastBoardChanged(isEnabled ? "section-enabled" : "section-disabled");
        Json.Ok(context, new Dictionary<string, object> { { "updated", true } });
    }
}

public class UsersGet : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        CurrentUser.RequireAdmin(context);
        int userId = 0;
        int.TryParse(context.Request.QueryString["userId"], out userId);
        if (userId <= 0) throw new System.ArgumentException("UserId is required.");

        Dictionary<string, object> user = UserRepository.Get(userId);
        if (user == null) throw new System.ArgumentException("User was not found.");
        Json.Ok(context, new Dictionary<string, object> { { "selectedUser", user } });
    }
}

public class UsersUpdate : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        CurrentUser.RequireAdmin(context);
        UserRepository.Update(Json.ReadBody(context));
        Json.Ok(context, new Dictionary<string, object> { { "updated", true } });
    }
}

public class UsersSetStatus : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        CurrentUser.RequireAdmin(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        int userId = Input.Int(body, "userId");
        bool isActive = Input.Bool(body, "isActive");
        if (userId <= 0) throw new System.ArgumentException("UserId is required.");
        UserRepository.SetStatus(userId, isActive);
        Json.Ok(context, new Dictionary<string, object> { { "updated", true } });
    }
}

public class UsersDelete : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        CurrentUser.RequireAdmin(context);
        Dictionary<string, object> body = Json.ReadBody(context);
        int userId = Input.Int(body, "userId");
        if (userId <= 0) throw new System.ArgumentException("UserId is required.");
        UserRepository.Delete(userId);
        Json.Ok(context, new Dictionary<string, object> { { "deleted", true } });
    }
}

public class RumpSearch : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        CurrentUser.RequireActive(context);

        Json.Ok(context, new Dictionary<string, object>
        {
            { "items", new object[0] },
            { "message", "RUMP tables and fields are not configured yet. This endpoint is a prefill-only placeholder." }
        });
    }
}

public class RumpPrefill : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        // 1. Validate the user is active
        CurrentUser.RequireActive(context);

        var jobsList = new List<Dictionary<string, string>>();

        // Ensure this connection string has access to the [RUMP] database.
        string connectionString = ConfigurationManager.ConnectionStrings["RumpDb"].ConnectionString;

        // 2. Exact SQL Query based on your schema
        string query = @"
            SELECT 
                WorkOrder,
                ProjectName
            FROM [RUMP].[dbo].[Projects]
            WHERE IsArchived = 0"; // Excludes archived projects

        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // 3. Map the SQL columns to the JavaScript keys (Older C# compatible)
                            var job = new Dictionary<string, string>
                            {
                                { "jobNumber", reader["WorkOrder"] != System.DBNull.Value ? reader["WorkOrder"].ToString() : "" },
                                { "title", reader["ProjectName"] != System.DBNull.Value ? reader["ProjectName"].ToString() : "" }
                            };
                            jobsList.Add(job);
                        }

                    }
                }
            }

            // 4. Return the payload to the frontend
            Json.Ok(context, new Dictionary<string, object>
            {
                { "jobs", jobsList }
            });
        }
        catch (System.Exception ex)
        {
            // Set status to 500 so we know it's a server error
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            // Get the exact Windows identity that the web application is running as
            string appPoolUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            // Format a clean JSON object containing the real error and the user identity
            // NOTE: Replaced interpolated string ($"...") with standard concatenation (+) for compatibility with older C# versions.
            string safeErrorMessage = "SQL Error: " + ex.Message + " --- Web App User: " + appPoolUser;
            safeErrorMessage = safeErrorMessage.Replace("\"", "\\\""); // Escape quotes for JSON

            context.Response.Write("{\"error\": \"" + safeErrorMessage + "\"}");
        }



    }
}


public class RumpPull : JsonHandler
{
    protected override void Handle(HttpContext context)
    {
        AppUser user = CurrentUser.RequireActive(context);
        Dictionary<string, object> result = RumpRepository.PullDraftPosts(user);
        PublicBoardRealtimeHub.BroadcastBoardChanged("rump-posts-imported");
        Json.Ok(context, result);
    }
}
