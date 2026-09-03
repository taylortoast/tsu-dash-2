using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

public static class ProjectBoardRepository
{
    public static List<Dictionary<string, object>> ListForBoard(AppUser user, string sectionCode)
    {
        List<Dictionary<string, object>> posts = new List<Dictionary<string, object>>();

        using (SqlConnection connection = Db.Open())
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    p.PostId,
    p.SectionId,
    s.SectionCode,
    s.SectionName,
    p.Title,
    p.PointOfContact,
    p.Description,
    p.LatestUpdate,
    p.EstimatedCompletionDate,
    p.IsActive,
    p.CreatedByUserId,
    p.UpdatedByUserId,
    p.CreatedUtc,
    p.UpdatedUtc,
    bs.Category
FROM dbo.Posts p
INNER JOIN dbo.Sections s
    ON s.SectionId = p.SectionId
LEFT JOIN dbo.ProjectBoardState bs
    ON bs.PostId = p.PostId
WHERE
    p.IsActive = 1
    AND (
        (@IsAdmin = 1 AND (@SectionCode = N'' OR s.SectionCode = @SectionCode)
            AND (s.SectionCode = N'TSU' OR s.IsEnabled = 1))
        OR
        (@IsAdmin = 0 AND p.SectionId = @AssignedSectionId AND s.IsEnabled = 1)
    )
ORDER BY s.SortOrder, p.UpdatedUtc DESC, p.Title;";

                command.Parameters.Add("@IsAdmin", SqlDbType.Bit).Value = user.IsAdmin;
                command.Parameters.Add("@AssignedSectionId", SqlDbType.Int).Value = user.AssignedSectionId.HasValue ? (object)user.AssignedSectionId.Value : DBNull.Value;
                command.Parameters.Add("@SectionCode", SqlDbType.NVarChar, 10).Value = sectionCode ?? "";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime estimated = Convert.ToDateTime(reader["EstimatedCompletionDate"]).Date;
                        DateTime created = Convert.ToDateTime(reader["CreatedUtc"]);
                        DateTime updated = Convert.ToDateTime(reader["UpdatedUtc"]);

                        string category = reader["Category"] == DBNull.Value
                            ? "Proposed"
                            : Convert.ToString(reader["Category"]);

                        Dictionary<string, object> post = new Dictionary<string, object>
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
                            { "updatedUtc", DbValue.DateTimeUtc(updated) },
                            { "category", category },
                            { "leadAssignments", new List<string>() },
                            { "helperAssignments", new List<string>() },
                            { "notes", new List<Dictionary<string, object>>() }
                        };

                        posts.Add(post);
                    }
                }
            }

            MapAssignments(connection, posts);
            MapNotes(connection, posts);
        }

        return posts;
    }

    private static void MapAssignments(SqlConnection connection, List<Dictionary<string, object>> posts)
    {
        if (posts.Count == 0) return;

        Dictionary<int, Dictionary<string, object>> postMap = BuildPostMap(posts);

        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT
    PostId,
    WorkerName,
    AssignmentRole
FROM dbo.ProjectAssignments
ORDER BY PostId, AssignmentRole, WorkerName;";

            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int postId = Convert.ToInt32(reader["PostId"]);
                    string workerName = Convert.ToString(reader["WorkerName"]);
                    string role = Convert.ToString(reader["AssignmentRole"]);

                    if (!postMap.ContainsKey(postId)) continue;

                    Dictionary<string, object> post = postMap[postId];

                    if (String.Equals(role, "Lead", StringComparison.OrdinalIgnoreCase))
                    {
                        ((List<string>)post["leadAssignments"]).Add(workerName);
                    }
                    else if (String.Equals(role, "Helper", StringComparison.OrdinalIgnoreCase))
                    {
                        ((List<string>)post["helperAssignments"]).Add(workerName);
                    }
                }
            }
        }
    }

    private static void MapNotes(SqlConnection connection, List<Dictionary<string, object>> posts)
    {
        if (posts.Count == 0) return;

        Dictionary<int, Dictionary<string, object>> postMap = BuildPostMap(posts);

        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT
    n.ProjectNoteId,
    n.PostId,
    n.NoteText,
    n.CreatedByUserId,
    n.CreatedUtc,
    u.DisplayName,
    u.WindowsUserName
FROM dbo.ProjectNotes n
INNER JOIN dbo.Users u
    ON u.UserId = n.CreatedByUserId
ORDER BY n.PostId, n.CreatedUtc;";

            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int postId = Convert.ToInt32(reader["PostId"]);
                    if (!postMap.ContainsKey(postId)) continue;

                    string displayName = reader["DisplayName"] == DBNull.Value
                        ? ""
                        : Convert.ToString(reader["DisplayName"]);

                    string windowsUserName = reader["WindowsUserName"] == DBNull.Value
                        ? ""
                        : Convert.ToString(reader["WindowsUserName"]);

                    string createdByDisplay = !String.IsNullOrWhiteSpace(displayName)
                        ? displayName
                        : windowsUserName;

                    Dictionary<string, object> note = new Dictionary<string, object>
                    {
                        { "projectNoteId", Convert.ToInt32(reader["ProjectNoteId"]) },
                        { "postId", postId },
                        { "noteText", Convert.ToString(reader["NoteText"]) },
                        { "createdByUserId", Convert.ToInt32(reader["CreatedByUserId"]) },
                        { "createdByDisplayName", createdByDisplay },
                        { "createdUtc", DbValue.DateTimeUtc(Convert.ToDateTime(reader["CreatedUtc"])) }
                    };

                    ((List<Dictionary<string, object>>)postMap[postId]["notes"]).Add(note);
                }
            }
        }
    }

    public static List<Dictionary<string, object>> ListWorkersForBoard()
    {
        List<Dictionary<string, object>> workers = new List<Dictionary<string, object>>();

        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT
    w.ProjectWorkerId,
    w.DisplayName,
    w.IsActive,
    w.SortOrder,
    SUM(CASE WHEN a.AssignmentRole = N'Lead' THEN 1 ELSE 0 END) AS LeadCount,
    SUM(CASE WHEN a.AssignmentRole = N'Helper' THEN 1 ELSE 0 END) AS HelperCount
FROM dbo.ProjectWorkers w
LEFT JOIN dbo.ProjectAssignments a
    ON a.WorkerName = w.DisplayName
WHERE w.IsActive = 1
GROUP BY
    w.ProjectWorkerId,
    w.DisplayName,
    w.IsActive,
    w.SortOrder
ORDER BY
    w.SortOrder,
    w.DisplayName;";

            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    workers.Add(new Dictionary<string, object>
                {
                    { "projectWorkerId", Convert.ToInt32(reader["ProjectWorkerId"]) },
                    { "displayName", Convert.ToString(reader["DisplayName"]) },
                    { "isActive", Convert.ToBoolean(reader["IsActive"]) },
                    { "sortOrder", Convert.ToInt32(reader["SortOrder"]) },
                    { "leadCount", Convert.ToInt32(reader["LeadCount"]) },
                    { "helperCount", Convert.ToInt32(reader["HelperCount"]) }
                });
                }
            }
        }

        return workers;
    }


    public static void AddAssignment(AppUser user, int postId, string workerName, string assignmentRole)
    {
        if (postId <= 0) throw new ArgumentException("PostId is required.");
        if (String.IsNullOrWhiteSpace(workerName)) throw new ArgumentException("WorkerName is required.");
        if (assignmentRole != "Lead" && assignmentRole != "Helper")
        {
            throw new ArgumentException("AssignmentRole must be Lead or Helper.");
        }

        using (SqlConnection connection = Db.Open())
        {
            int sectionId;

            using (SqlCommand get = connection.CreateCommand())
            {
                get.CommandText = @"
SELECT SectionId
FROM dbo.Posts
WHERE PostId = @PostId
  AND IsActive = 1;";
                get.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;

                object found = get.ExecuteScalar();
                if (found == null) throw new ArgumentException("Post was not found.");

                sectionId = Convert.ToInt32(found);
            }

            if (!user.IsAdmin && (!user.AssignedSectionId.HasValue || sectionId != user.AssignedSectionId.Value))
            {
                throw new UnauthorizedAccessException("Post is outside your assigned section.");
            }

            using (SqlCommand removeOpposite = connection.CreateCommand())
            {
                removeOpposite.CommandText = @"
DELETE FROM dbo.ProjectAssignments
WHERE PostId = @PostId
  AND WorkerName = @WorkerName
  AND AssignmentRole <> @AssignmentRole;";
                removeOpposite.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                removeOpposite.Parameters.Add("@WorkerName", SqlDbType.NVarChar, 100).Value = workerName;
                removeOpposite.Parameters.Add("@AssignmentRole", SqlDbType.NVarChar, 20).Value = assignmentRole;
                removeOpposite.ExecuteNonQuery();
            }

            using (SqlCommand insert = connection.CreateCommand())
            {
                insert.CommandText = @"
IF NOT EXISTS (
    SELECT 1
    FROM dbo.ProjectAssignments
    WHERE PostId = @PostId
      AND WorkerName = @WorkerName
      AND AssignmentRole = @AssignmentRole
)
BEGIN
    INSERT dbo.ProjectAssignments
        (PostId, WorkerName, AssignmentRole, CreatedByUserId)
    VALUES
        (@PostId, @WorkerName, @AssignmentRole, @UserId);
END";
                insert.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                insert.Parameters.Add("@WorkerName", SqlDbType.NVarChar, 100).Value = workerName;
                insert.Parameters.Add("@AssignmentRole", SqlDbType.NVarChar, 20).Value = assignmentRole;
                insert.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
                insert.ExecuteNonQuery();
            }
        }
    }

    public static void RemoveAssignment(AppUser user, int postId, string workerName, string assignmentRole)
    {
        if (postId <= 0) throw new ArgumentException("PostId is required.");
        if (String.IsNullOrWhiteSpace(workerName)) throw new ArgumentException("WorkerName is required.");
        if (assignmentRole != "Lead" && assignmentRole != "Helper")
        {
            throw new ArgumentException("AssignmentRole must be Lead or Helper.");
        }

        using (SqlConnection connection = Db.Open())
        {
            int sectionId;

            using (SqlCommand get = connection.CreateCommand())
            {
                get.CommandText = @"
SELECT SectionId
FROM dbo.Posts
WHERE PostId = @PostId
  AND IsActive = 1;";
                get.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;

                object found = get.ExecuteScalar();
                if (found == null) throw new ArgumentException("Post was not found.");

                sectionId = Convert.ToInt32(found);
            }

            if (!user.IsAdmin && (!user.AssignedSectionId.HasValue || sectionId != user.AssignedSectionId.Value))
            {
                throw new UnauthorizedAccessException("Post is outside your assigned section.");
            }

            using (SqlCommand delete = connection.CreateCommand())
            {
                delete.CommandText = @"
DELETE FROM dbo.ProjectAssignments
WHERE PostId = @PostId
  AND WorkerName = @WorkerName
  AND AssignmentRole = @AssignmentRole;";
                delete.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                delete.Parameters.Add("@WorkerName", SqlDbType.NVarChar, 100).Value = workerName;
                delete.Parameters.Add("@AssignmentRole", SqlDbType.NVarChar, 20).Value = assignmentRole;
                delete.ExecuteNonQuery();
            }
        }
    }

    public static void SetCategory(AppUser user, int postId, string category)
    {
        if (postId <= 0) throw new ArgumentException("PostId is required.");
        if (String.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.");

        if (category != "Proposed" &&
            category != "Assigned / In Progress" &&
            category != "Customer Review")
        {
            throw new ArgumentException("Invalid category.");
        }

        using (SqlConnection connection = Db.Open())
        {
            int sectionId;

            using (SqlCommand get = connection.CreateCommand())
            {
                get.CommandText = "SELECT SectionId FROM dbo.Posts WHERE PostId = @PostId AND IsActive = 1;";
                get.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;

                object found = get.ExecuteScalar();
                if (found == null) throw new ArgumentException("Post was not found.");

                sectionId = Convert.ToInt32(found);
            }

            if (!user.IsAdmin && (!user.AssignedSectionId.HasValue || sectionId != user.AssignedSectionId.Value))
            {
                throw new UnauthorizedAccessException("Post is outside your assigned section.");
            }

            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
MERGE dbo.ProjectBoardState AS target
USING (SELECT @PostId AS PostId) AS source
ON target.PostId = source.PostId
WHEN MATCHED THEN
    UPDATE SET Category = @Category,
               UpdatedByUserId = @UserId,
               UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (PostId, Category, UpdatedByUserId, UpdatedUtc)
    VALUES (@PostId, @Category, @UserId, SYSUTCDATETIME());";

                command.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                command.Parameters.Add("@Category", SqlDbType.NVarChar, 50).Value = category;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
                command.ExecuteNonQuery();
            }
        }
    }

    public static void AddNote(AppUser user, int postId, string noteText)
    {
        if (postId <= 0) throw new ArgumentException("PostId is required.");
        if (String.IsNullOrWhiteSpace(noteText)) throw new ArgumentException("NoteText is required.");

        noteText = noteText.Trim();
        if (noteText.Length > 4000)
        {
            throw new ArgumentException("NoteText is too long.");
        }

        using (SqlConnection connection = Db.Open())
        {
            int sectionId;

            using (SqlCommand get = connection.CreateCommand())
            {
                get.CommandText = @"
SELECT SectionId
FROM dbo.Posts
WHERE PostId = @PostId
  AND IsActive = 1;";
                get.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;

                object found = get.ExecuteScalar();
                if (found == null) throw new ArgumentException("Post was not found.");

                sectionId = Convert.ToInt32(found);
            }

            if (!user.IsAdmin && (!user.AssignedSectionId.HasValue || sectionId != user.AssignedSectionId.Value))
            {
                throw new UnauthorizedAccessException("Post is outside your assigned section.");
            }

            using (SqlCommand insert = connection.CreateCommand())
            {
                insert.CommandText = @"
INSERT dbo.ProjectNotes
    (PostId, NoteText, CreatedByUserId)
VALUES
    (@PostId, @NoteText, @UserId);";
                insert.Parameters.Add("@PostId", SqlDbType.Int).Value = postId;
                insert.Parameters.Add("@NoteText", SqlDbType.NVarChar).Value = noteText;
                insert.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
                insert.ExecuteNonQuery();
            }
        }
    }

    private static Dictionary<int, Dictionary<string, object>> BuildPostMap(List<Dictionary<string, object>> posts)
    {
        Dictionary<int, Dictionary<string, object>> map = new Dictionary<int, Dictionary<string, object>>();

        foreach (Dictionary<string, object> post in posts)
        {
            int postId = Convert.ToInt32(post["postId"]);
            map[postId] = post;
        }

        return map;
    }
}
