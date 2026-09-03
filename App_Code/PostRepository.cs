using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

public static class PostRepository
{
    public static List<Dictionary<string, object>> ListForUser(AppUser user, string sectionCode, bool includeDisabled)
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT p.PostId, p.SectionId, s.SectionCode, s.SectionName, p.Title, p.PointOfContact,
       p.Description, p.LatestUpdate, p.EstimatedCompletionDate, p.IsActive,
       p.CreatedByUserId, p.UpdatedByUserId, p.CreatedUtc, p.UpdatedUtc
FROM dbo.Posts p
INNER JOIN dbo.Sections s ON s.SectionId = p.SectionId
 WHERE (@IsAdmin = 1 AND (@SectionCode = N'' OR s.SectionCode = @SectionCode)
        AND (@IncludeDisabled = 1 OR s.SectionCode = N'TSU' OR s.IsEnabled = 1))
   OR (@IsAdmin = 0 AND p.SectionId = @AssignedSectionId)
ORDER BY s.SortOrder, p.IsActive DESC, p.UpdatedUtc DESC, p.Title;";
            command.Parameters.Add("@IsAdmin", SqlDbType.Bit).Value = user.IsAdmin;
            command.Parameters.Add("@AssignedSectionId", SqlDbType.Int).Value = user.AssignedSectionId.Value;
            command.Parameters.Add("@SectionCode", SqlDbType.NVarChar, 10).Value = sectionCode ?? "";
            command.Parameters.Add("@IncludeDisabled", SqlDbType.Bit).Value = includeDisabled && user.IsAdmin;
            return ReadPosts(command);
        }
    }

    public static List<Dictionary<string, object>> ListAllForAdmin()
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT p.PostId, p.SectionId, s.SectionCode, s.SectionName, p.Title, p.PointOfContact,
       p.Description, p.LatestUpdate, p.EstimatedCompletionDate, p.IsActive,
       p.CreatedByUserId, p.UpdatedByUserId, p.CreatedUtc, p.UpdatedUtc
FROM dbo.Posts p
INNER JOIN dbo.Sections s ON s.SectionId = p.SectionId
 WHERE s.SectionCode <> N'TSU'
   AND s.IsEnabled = 1
ORDER BY s.SortOrder, p.IsActive DESC, p.UpdatedUtc DESC, p.Title;";
            return ReadPosts(command);
        }
    }

    public static List<Dictionary<string, object>> ListPublic()
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT p.PostId, p.SectionId, s.SectionCode, s.SectionName, p.Title, p.PointOfContact,
       p.Description, p.LatestUpdate, p.EstimatedCompletionDate, p.IsActive,
       p.CreatedByUserId, p.UpdatedByUserId, p.CreatedUtc, p.UpdatedUtc
FROM dbo.Posts p
INNER JOIN dbo.Sections s ON s.SectionId = p.SectionId
WHERE p.IsActive = 1
  AND p.EstimatedCompletionDate >= CAST(GETDATE() AS DATE)
   AND (s.SectionCode = N'TSU' OR (s.IsEnabled = 1 AND s.IsPublicVisible = 1))
ORDER BY s.SortOrder, p.UpdatedUtc DESC, p.Title;";
            return ReadPosts(command);
        }
    }

    public static Dictionary<string, object> GetForUser(AppUser user, int postId)
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT p.PostId, p.SectionId, s.SectionCode, s.SectionName, p.Title, p.PointOfContact,
       p.Description, p.LatestUpdate, p.EstimatedCompletionDate, p.IsActive,
       p.CreatedByUserId, p.UpdatedByUserId, p.CreatedUtc, p.UpdatedUtc
FROM dbo.Posts p
INNER JOIN dbo.Sections s ON s.SectionId = p.SectionId
WHERE p.PostId = @PostId
  AND (@IsAdmin = 1 OR p.SectionId = @AssignedSectionId);";
            command.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
            command.Parameters.Add("@IsAdmin", SqlDbType.Bit).Value = user.IsAdmin;
            command.Parameters.Add("@AssignedSectionId", SqlDbType.Int).Value = user.AssignedSectionId.Value;

            List<Dictionary<string, object>> rows = ReadPosts(command);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    public static int Create(AppUser user, Dictionary<string, object> body)
    {
        List<string> errors = Validators.ValidatePost(body, user.SectionCode != "TSU");
        if (errors.Count > 0) throw new ArgumentException(String.Join(" ", errors.ToArray()));
        bool requestedActive = body.ContainsKey("isActive") ? Input.Bool(body, "isActive") : true;
        if (requestedActive && Input.Date(body, "estimatedCompletionDate") < DateTime.Today)
        {
            throw new ArgumentException("Active posts require an estimated completion date of today or later.");
        }

        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
INSERT dbo.Posts
    (SectionId, Title, PointOfContact, Description, LatestUpdate, EstimatedCompletionDate, IsActive, CreatedByUserId, UpdatedByUserId)
OUTPUT INSERTED.PostId
VALUES
    (@SectionId, @Title, @PointOfContact, @Description, @LatestUpdate, @EstimatedCompletionDate, @IsActive, @UserId, @UserId);";
            AddPostParameters(command, user, body, true);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public static void Update(AppUser user, Dictionary<string, object> body)
    {
        List<string> errors = Validators.ValidatePost(body, user.SectionCode != "TSU");
        if (errors.Count > 0) throw new ArgumentException(String.Join(" ", errors.ToArray()));

        int postId = Input.Int(body, "postId");
        if (postId <= 0) throw new ArgumentException("PostId is required.");
        bool requestedActive = Input.Bool(body, "isActive");
        DateTime requestedCompletionDate = Input.Date(body, "estimatedCompletionDate");

        using (SqlConnection connection = Db.Open())
        {
            bool wasActive;
            int sectionId;

            using (SqlCommand get = connection.CreateCommand())
            {
                get.CommandText = "SELECT SectionId, IsActive FROM dbo.Posts WHERE PostId = @PostId;";
                get.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                using (SqlDataReader reader = get.ExecuteReader())
                {
                    if (!reader.Read()) throw new ArgumentException("Post was not found.");
                    sectionId = Convert.ToInt32(reader["SectionId"]);
                    wasActive = Convert.ToBoolean(reader["IsActive"]);
                }
            }

            if (!user.IsAdmin && sectionId != user.AssignedSectionId.Value)
            {
                throw new UnauthorizedAccessException("Post is outside your assigned section.");
            }

            if (!wasActive && requestedActive && requestedCompletionDate < DateTime.Today)
            {
                throw new ArgumentException("Update the estimated completion date before reactivating this post.");
            }

            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE p
SET Title = @Title,
    PointOfContact = @PointOfContact,
    Description = @Description,
    LatestUpdate = @LatestUpdate,
    EstimatedCompletionDate = @EstimatedCompletionDate,
    IsActive = @IsActive,
    UpdatedByUserId = @UserId,
    UpdatedUtc = SYSUTCDATETIME()
FROM dbo.Posts p
WHERE p.PostId = @PostId
  AND (@IsAdmin = 1 OR p.SectionId = @SectionId);";
                command.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                command.Parameters.Add("@IsAdmin", SqlDbType.Bit).Value = user.IsAdmin;
                AddPostParameters(command, user, body, false);

                if (command.ExecuteNonQuery() == 0)
                {
                    throw new UnauthorizedAccessException("Post was not found or is outside your section.");
                }
            }
        }
    }

    public static void SetStatus(AppUser user, int postId, bool isActive)
    {
        using (SqlConnection connection = Db.Open())
        {
            DateTime estimatedCompletionDate;
            int sectionId;

            using (SqlCommand get = connection.CreateCommand())
            {
                get.CommandText = "SELECT SectionId, EstimatedCompletionDate FROM dbo.Posts WHERE PostId = @PostId;";
                get.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                using (SqlDataReader reader = get.ExecuteReader())
                {
                    if (!reader.Read()) throw new ArgumentException("Post was not found.");
                    sectionId = Convert.ToInt32(reader["SectionId"]);
                    estimatedCompletionDate = Convert.ToDateTime(reader["EstimatedCompletionDate"]).Date;
                }
            }

            if (!user.IsAdmin && sectionId != user.AssignedSectionId.Value)
            {
                throw new UnauthorizedAccessException("Post is outside your assigned section.");
            }

            if (isActive && estimatedCompletionDate < DateTime.Today)
            {
                throw new ArgumentException("Update the estimated completion date before reactivating this post.");
            }

            using (SqlCommand update = connection.CreateCommand())
            {
                update.CommandText = @"
UPDATE dbo.Posts
SET IsActive = @IsActive,
    UpdatedByUserId = @UserId,
    UpdatedUtc = SYSUTCDATETIME()
WHERE PostId = @PostId;";
                update.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
                update.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
                update.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                update.ExecuteNonQuery();
            }
        }
    }

    public static void Renew(AppUser user, int postId)
    {
        if (postId <= 0) throw new ArgumentException("PostId is required.");

        using (SqlConnection connection = Db.Open())
        {
            int sectionId;

            using (SqlCommand get = connection.CreateCommand())
            {
                get.CommandText = "SELECT SectionId FROM dbo.Posts WHERE PostId = @PostId;";
                get.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                object found = get.ExecuteScalar();
                if (found == null) throw new ArgumentException("Post was not found.");
                sectionId = Convert.ToInt32(found);
            }

            if (!user.IsAdmin && sectionId != user.AssignedSectionId.Value)
            {
                throw new UnauthorizedAccessException("Post is outside your assigned section.");
            }

            using (SqlCommand update = connection.CreateCommand())
            {
                update.CommandText = @"
UPDATE dbo.Posts
SET UpdatedByUserId = @UserId,
    UpdatedUtc = SYSUTCDATETIME()
WHERE PostId = @PostId;";
                update.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
                update.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                update.ExecuteNonQuery();
            }
        }
    }

    private static void AddPostParameters(SqlCommand command, AppUser user, Dictionary<string, object> body, bool creating)
    {
        command.Parameters.Add("@SectionId", SqlDbType.Int).Value = user.AssignedSectionId.Value;
        command.Parameters.Add("@Title", SqlDbType.NVarChar, 150).Value = Input.String(body, "title");
        command.Parameters.Add("@PointOfContact", SqlDbType.NVarChar, 150).Value = Input.String(body, "pointOfContact");
        command.Parameters.Add("@Description", SqlDbType.NVarChar).Value = Input.String(body, "description");
        command.Parameters.Add("@LatestUpdate", SqlDbType.NVarChar).Value = Input.String(body, "latestUpdate");
        command.Parameters.Add("@EstimatedCompletionDate", SqlDbType.Date).Value = Input.Date(body, "estimatedCompletionDate");
        bool isActive = creating && !body.ContainsKey("isActive") ? true : Input.Bool(body, "isActive");
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
    }

    private static List<Dictionary<string, object>> ReadPosts(SqlCommand command)
    {
        List<Dictionary<string, object>> posts = new List<Dictionary<string, object>>();
        using (SqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                DateTime estimated = Convert.ToDateTime(reader["EstimatedCompletionDate"]).Date;
                DateTime created = Convert.ToDateTime(reader["CreatedUtc"]);
                DateTime updated = Convert.ToDateTime(reader["UpdatedUtc"]);
                posts.Add(new Dictionary<string, object>
                {
                    { "postId", Convert.ToInt32(reader["PostId"]) },
                    { "sectionId", Convert.ToInt32(reader["SectionId"]) },
                    { "sectionCode", Convert.ToString(reader["SectionCode"]) },
                    { "sectionName", Convert.ToString(reader["SectionName"]) },
                    { "title", Convert.ToString(reader["Title"]) },
                    { "pointOfContact", Convert.ToString(reader["PointOfContact"]) },
                    { "description", Convert.ToString(reader["Description"]) },
                    { "latestUpdate", Convert.ToString(reader["LatestUpdate"]) },
                    { "estimatedCompletionDate", DbValue.Date(estimated) },
                    { "isActive", Convert.ToBoolean(reader["IsActive"]) },
                    { "createdByUserId", Convert.ToInt32(reader["CreatedByUserId"]) },
                    { "updatedByUserId", DbValue.NullableInt(reader["UpdatedByUserId"]) },
                    { "createdUtc", DbValue.DateTimeUtc(created) },
                    { "updatedUtc", DbValue.DateTimeUtc(updated) }
                });
            }
        }

        return posts;
    }
}
