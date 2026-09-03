using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;

public class AppUser
{
    public int UserId;
    public string WindowsUserName;
    public string DisplayName;
    public int? AssignedSectionId;
    public string SectionCode;
    public string SectionName;
    public bool IsSectionEnabled;
    public bool IsActive;
    public bool IsAdmin;
    public bool CanAccessAssignmentsBoard;
    public bool IsTsuiAdmin;
    public bool IsGuest;
}




public static class Db
{
    private const string ConnectionStringName = "TSUDashboardDb";

    public static SqlConnection CreateConnection()
    {
        ConnectionStringSettings settings =
            ConfigurationManager.ConnectionStrings[ConnectionStringName];

        if (settings == null)
        {
            throw new InvalidOperationException(
                "Missing connection string: " + ConnectionStringName);
        }

        if (String.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException(
                "Connection string is empty: " + ConnectionStringName);
        }

        return new SqlConnection(settings.ConnectionString);
    }

    public static SqlConnection Open()
    {
        SqlConnection connection = CreateConnection();
        connection.Open();
        return connection;
    }
}






public static class Json
{
    private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

    public static Dictionary<string, object> ReadBody(HttpContext context)
    {
        context.Request.InputStream.Position = 0;
        string body = new StreamReader(context.Request.InputStream).ReadToEnd();
        if (String.IsNullOrWhiteSpace(body))
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        object parsed = Serializer.DeserializeObject(body);
        Dictionary<string, object> dict = parsed as Dictionary<string, object>;
        return dict ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public static void Ok(HttpContext context, object data)
    {
        Write(context, 200, true, data, new string[0]);
    }

    public static void Error(HttpContext context, int statusCode, string message)
    {
        Write(context, statusCode, false, null, new string[] { message });
    }

    public static void ValidationError(HttpContext context, IList<string> errors)
    {
        Write(context, 400, false, null, errors);
    }

    private static void Write(HttpContext context, int statusCode, bool ok, object data, object errors)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Write(Serializer.Serialize(new Dictionary<string, object>
        {
            { "ok", ok },
            { "data", data },
            { "errors", errors }
        }));
    }
}

public abstract class JsonHandler : IHttpHandler
{
    public bool IsReusable { get { return false; } }

    public void ProcessRequest(HttpContext context)
    {
        try
        {
            Handle(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            Json.Error(context, 403, ex.Message);
        }
        catch (ArgumentException ex)
        {
            Json.Error(context, 400, ex.Message);
        }
        catch (Exception ex)
        {
            Json.Error(context, 500, ex.Message);
        }
    }

    protected abstract void Handle(HttpContext context);
}

public static class CurrentUser
{
    public static bool IsGuestRequest(HttpContext context)
    {
        HttpCookie cookie = context.Request.Cookies["TSUGuest"];
        return cookie != null && String.Equals(cookie.Value, "1", StringComparison.Ordinal) &&
            (context.User == null || context.User.Identity == null || !context.User.Identity.IsAuthenticated);
    }

    public static AppUser Ensure(HttpContext context)
    {
        if (IsGuestRequest(context)) return GuestUser();

        string windowsUserName = GetWindowsUserName(context);

        using (SqlConnection connection = Db.Open())
        {
            AppUser user = FindByWindowsUserName(connection, windowsUserName);
            if (user == null)
            {
                using (SqlCommand insert = connection.CreateCommand())
                {
                    insert.CommandText = @"
INSERT dbo.Users (WindowsUserName, LastLoginUtc)
VALUES (@WindowsUserName, SYSUTCDATETIME());";
                    insert.Parameters.Add("@WindowsUserName", SqlDbType.NVarChar, 150).Value = windowsUserName;
                    insert.ExecuteNonQuery();
                }
            }
            else
            {
                using (SqlCommand update = connection.CreateCommand())
                {
                    update.CommandText = @"
UPDATE dbo.Users
SET LastLoginUtc = SYSUTCDATETIME(),
    UpdatedUtc = SYSUTCDATETIME()
WHERE WindowsUserName = @WindowsUserName;";
                    update.Parameters.Add("@WindowsUserName", SqlDbType.NVarChar, 150).Value = windowsUserName;
                    update.ExecuteNonQuery();
                }
            }

            return FindByWindowsUserName(connection, windowsUserName);
        }
    }

    public static AppUser RequireActive(HttpContext context)
    {
        AppUser user = Ensure(context);
        if (user == null || !user.IsActive || !user.AssignedSectionId.HasValue)
        {
            throw new UnauthorizedAccessException("Active application access and section assignment are required.");
        }

        if (!user.IsAdmin && !user.IsSectionEnabled)
        {
            throw new UnauthorizedAccessException("Your assigned section is currently unavailable.");
        }

        return user;
    }

    public static AppUser RequireReadAccess(HttpContext context)
    {
        AppUser user = Ensure(context);
        if (user != null && user.IsGuest) return user;
        return RequireActive(context);
    }

    public static AppUser RequireAssignmentsBoardRead(HttpContext context)
    {
        AppUser user = Ensure(context);
        if (user != null && user.IsGuest) return user;
        return RequireAssignmentsBoardAccess(context);
    }

    public static AppUser RequireAuthenticated(HttpContext context)
    {
        AppUser user = Ensure(context);
        if (user == null || user.IsGuest)
            throw new UnauthorizedAccessException("Authenticated application access is required.");
        return user;
    }

    public static AppUser RequireAssignmentsBoardAccess(HttpContext context)
    {
        AppUser user = Ensure(context);
        if (user == null || !user.IsActive || !user.AssignedSectionId.HasValue)
        {
            throw new UnauthorizedAccessException("Active application access and section assignment are required.");
        }

        if (!user.IsAdmin && !user.CanAccessAssignmentsBoard)
        {
            throw new UnauthorizedAccessException("Assignments board access is required.");
        }

        return user;
    }

    public static AppUser RequireAdmin(HttpContext context)
    {
        AppUser user = RequireActive(context);
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException("TSU admin access is required.");
        }

        return user;
    }

    public static Dictionary<string, object> ToJson(AppUser user)
    {
        List<string> allowedPages = GetAllowedPages(user);

        return new Dictionary<string, object>
    {
        { "userId", user.UserId },
        { "windowsUserName", user.WindowsUserName },
        { "displayName", user.DisplayName },
        { "assignedSectionId", user.AssignedSectionId.HasValue ? (object)user.AssignedSectionId.Value : null },
        { "sectionCode", user.SectionCode },
        { "sectionName", user.SectionName },
        { "sectionEnabled", user.IsSectionEnabled },
        { "isActive", user.IsActive },
        { "isAdmin", user.IsAdmin },
        { "canAccessAssignmentsBoard", user.CanAccessAssignmentsBoard },
        { "isTsuiAdmin", user.IsTsuiAdmin },
        { "isGuest", user.IsGuest },
        { "accessStatus", GetAccessStatus(user) },
        { "allowedPages", allowedPages },
        { "routeTarget", GetRouteTarget(allowedPages) }
    };
    }

    public static string GetAccessStatus(AppUser user)
    {
        if (user == null) return "unknown";
        if (user.IsGuest) return "guest";
        if (!user.IsActive) return "pending";
        if (!user.AssignedSectionId.HasValue) return "unassigned";
        if (!user.IsAdmin && !user.IsSectionEnabled) return "section-disabled";
        return "active";
    }

    // Page keys shared by the router and the per-page client guards.
    public const string PageSectionCommand = "section-command";
    public const string PageSectionDashboard = "section-dashboard";
    public const string PageUserAdmin = "user-admin";
    public const string PageAssignmentsBoard = "assignments-board";

    public const string AccessPendingUrl = "access-pending.html";

    /// <summary>
    /// The single source of truth for application authorization. Every routing
    /// decision and every client-side page guard derives from this list; nothing
    /// re-implements these rules. The first entry is the user's landing page.
    /// URLs are relative to the application root (no leading "./"), so a caller
    /// in a subdirectory prefixes "../".
    /// </summary>
    public static List<string> GetAllowedPages(AppUser user)
    {
        List<string> pages = new List<string>();
        string accessStatus = GetAccessStatus(user);

        if (user != null && user.IsGuest)
        {
            pages.Add(PageSectionDashboard);
            pages.Add(PageSectionCommand);
            pages.Add(PageAssignmentsBoard);
            return pages;
        }

        // Covers both "not activated" and "activated but no section assigned".
        if (accessStatus != "active")
        {
            if (accessStatus == "section-disabled" && user.CanAccessAssignmentsBoard)
            {
                pages.Add(PageAssignmentsBoard);
            }

            return pages;
        }

        if (user.IsAdmin)
        {
            pages.Add(PageSectionCommand);
            pages.Add(PageSectionDashboard);
            pages.Add(PageUserAdmin);
            pages.Add(PageAssignmentsBoard);
            return pages;
        }

        if (user.CanAccessAssignmentsBoard)
        {
            bool isTsui = String.Equals(user.SectionCode, "TSUI", StringComparison.OrdinalIgnoreCase);

            // A TSUI board worker who is not a TSUI admin lives on the board only.
            if (isTsui && !user.IsTsuiAdmin)
            {
                pages.Add(PageAssignmentsBoard);
                return pages;
            }

            pages.Add(PageSectionDashboard);
            pages.Add(PageAssignmentsBoard);
            return pages;
        }

        // Any other active, section-assigned user gets their section CRUD dashboard.
        pages.Add(PageSectionDashboard);
        return pages;
    }

    public static string GetRouteTarget(AppUser user)
    {
        return GetRouteTarget(GetAllowedPages(user));
    }

    public static string GetRouteTarget(List<string> allowedPages)
    {
        if (allowedPages == null || allowedPages.Count == 0) return AccessPendingUrl;
        return PageUrl(allowedPages[0]);
    }

    public static string PageUrl(string pageKey)
    {
        switch (pageKey)
        {
            case PageSectionCommand: return "section-command.html";
            case PageSectionDashboard: return "section-dashboard.html";
            case PageUserAdmin: return "user-admin.html";
            case PageAssignmentsBoard: return "assignments-board/index.html";
            default: return AccessPendingUrl;
        }
    }

    private static string GetWindowsUserName(HttpContext context)
    {
        string name = null;
        if (context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated)
        {
            name = context.User.Identity.Name;
        }

        if (String.IsNullOrWhiteSpace(name)) name = context.Request.ServerVariables["LOGON_USER"];
        if (String.IsNullOrWhiteSpace(name)) name = context.Request.ServerVariables["REMOTE_USER"];
        if (String.IsNullOrWhiteSpace(name))
        {
            throw new UnauthorizedAccessException("IIS did not provide an authenticated Windows/CAC identity. Confirm Windows Authentication is enabled and Anonymous Authentication is disabled for this application.");
        }

        return name.Trim();
    }

    private static AppUser FindByWindowsUserName(SqlConnection connection, string windowsUserName)
    {
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT u.UserId, u.WindowsUserName, u.DisplayName, u.AssignedSectionId, u.IsActive, u.IsAdmin,
       u.CanAccessAssignmentsBoard, u.IsTsuiAdmin,
       s.SectionCode, s.SectionName, s.IsEnabled
FROM dbo.Users u
LEFT JOIN dbo.Sections s ON s.SectionId = u.AssignedSectionId
WHERE u.WindowsUserName = @WindowsUserName;";
            command.Parameters.Add("@WindowsUserName", SqlDbType.NVarChar, 150).Value = windowsUserName;

            using (SqlDataReader reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return ReadUser(reader);
            }
        }
    }

    public static AppUser ReadUser(SqlDataReader reader)
    {
        AppUser user = new AppUser();
        user.UserId = Convert.ToInt32(reader["UserId"]);
        user.WindowsUserName = Convert.ToString(reader["WindowsUserName"]);
        user.DisplayName = Convert.ToString(reader["DisplayName"]);
        user.AssignedSectionId = reader["AssignedSectionId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AssignedSectionId"]);
        user.SectionCode = reader["SectionCode"] == DBNull.Value ? "" : Convert.ToString(reader["SectionCode"]);
        user.SectionName = reader["SectionName"] == DBNull.Value ? "" : Convert.ToString(reader["SectionName"]);
        user.IsSectionEnabled = reader["IsEnabled"] != DBNull.Value && Convert.ToBoolean(reader["IsEnabled"]);
        if (String.Equals(user.SectionCode, "TSU", StringComparison.OrdinalIgnoreCase))
        {
            user.IsSectionEnabled = true;
        }
        user.IsActive = Convert.ToBoolean(reader["IsActive"]);
        user.IsAdmin = Convert.ToBoolean(reader["IsAdmin"]);
        user.CanAccessAssignmentsBoard = Convert.ToBoolean(reader["CanAccessAssignmentsBoard"]);
        user.IsTsuiAdmin = Convert.ToBoolean(reader["IsTsuiAdmin"]);
        return user;
    }

    private static AppUser GuestUser()
    {
        return new AppUser
        {
            UserId = 0,
            WindowsUserName = "Guest",
            DisplayName = "Guest",
            IsActive = true,
            IsSectionEnabled = true,
            IsGuest = true
        };
    }
}

public static class DbValue
{
    public static string String(object value)
    {
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    public static int? NullableInt(object value)
    {
        return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
    }

    public static string Date(DateTime value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string DateTimeUtc(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
    }
}

public static class Input
{
    public static string String(Dictionary<string, object> body, string key)
    {
        if (!body.ContainsKey(key) || body[key] == null) return "";
        return Convert.ToString(body[key]).Trim();
    }

    public static bool Bool(Dictionary<string, object> body, string key)
    {
        if (!body.ContainsKey(key) || body[key] == null) return false;
        object value = body[key];
        if (value is bool) return (bool)value;
        string text = Convert.ToString(value);
        return text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("active", StringComparison.OrdinalIgnoreCase);
    }

    public static int Int(Dictionary<string, object> body, string key)
    {
        if (!body.ContainsKey(key) || body[key] == null) return 0;
        return Convert.ToInt32(body[key]);
    }

    public static DateTime Date(Dictionary<string, object> body, string key)
    {
        string text = String(body, key);
        DateTime value;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value))
        {
            throw new ArgumentException(key + " must be a valid date.");
        }

        return value.Date;
    }
}

public static class Validators
{
    public static List<string> ValidatePost(Dictionary<string, object> body)
    {
        return ValidatePost(body, true);
    }

    public static List<string> ValidatePost(Dictionary<string, object> body, bool requireLatestUpdate)
    {
        List<string> errors = new List<string>();
        Required(body, "title", "Title", 150, errors);
        Required(body, "pointOfContact", "Point of contact", 150, errors);
        Required(body, "description", "Description", 4000, errors);
        if (requireLatestUpdate)
        {
            Required(body, "latestUpdate", "Latest update", 4000, errors);
        }

        string dateText = Input.String(body, "estimatedCompletionDate");
        DateTime ignored;
        if (String.IsNullOrWhiteSpace(dateText))
        {
            errors.Add("Estimated completion date is required.");
        }
        else if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out ignored))
        {
            errors.Add("Estimated completion date must be valid.");
        }

        return errors;
    }

    private static void Required(Dictionary<string, object> body, string key, string label, int maxLength, List<string> errors)
    {
        string value = Input.String(body, key);
        if (String.IsNullOrWhiteSpace(value))
        {
            errors.Add(label + " is required.");
        }
        else if (value.Length > maxLength)
        {
            errors.Add(label + " must be " + maxLength + " characters or fewer.");
        }
    }
}
