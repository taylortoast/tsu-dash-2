using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

public static class UserRepository
{
    public static List<Dictionary<string, object>> List()
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT u.UserId, u.WindowsUserName, u.DisplayName, u.AssignedSectionId, u.IsActive, u.IsAdmin,
       u.CanAccessAssignmentsBoard, u.IsTsuiAdmin,
       u.FirstSeenUtc, u.LastLoginUtc, u.CreatedUtc, u.UpdatedUtc,
       s.SectionCode, s.SectionName
FROM dbo.Users u
LEFT JOIN dbo.Sections s ON s.SectionId = u.AssignedSectionId
ORDER BY u.IsActive ASC, u.LastLoginUtc DESC, u.DisplayName, u.WindowsUserName;";
            return ReadUsers(command);
        }
    }

    public static Dictionary<string, object> Get(int userId)
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT u.UserId, u.WindowsUserName, u.DisplayName, u.AssignedSectionId, u.IsActive, u.IsAdmin,
       u.CanAccessAssignmentsBoard, u.IsTsuiAdmin,
       u.FirstSeenUtc, u.LastLoginUtc, u.CreatedUtc, u.UpdatedUtc,
       s.SectionCode, s.SectionName
FROM dbo.Users u
LEFT JOIN dbo.Sections s ON s.SectionId = u.AssignedSectionId
WHERE u.UserId = @UserId;";
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            List<Dictionary<string, object>> rows = ReadUsers(command);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    public static void Update(Dictionary<string, object> body)
    {
        string displayName = Input.String(body, "displayName");
        if (String.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.");
        if (displayName.Length > 150)
            throw new ArgumentException("Display name must be 150 characters or fewer.");
        int userId = Input.Int(body, "userId");
        if (userId <= 0) throw new ArgumentException("UserId is required.");

        string sectionCode = Input.String(body, "sectionCode");
        object sectionId = DBNull.Value;

        using (SqlConnection connection = Db.Open())
        {
            if (!String.IsNullOrWhiteSpace(sectionCode))
            {
                using (SqlCommand findSection = connection.CreateCommand())
                {
                    findSection.CommandText = "SELECT SectionId FROM dbo.Sections WHERE SectionCode = @SectionCode;";
                    findSection.Parameters.Add("@SectionCode", SqlDbType.NVarChar, 10).Value = sectionCode;
                    object found = findSection.ExecuteScalar();
                    if (found == null) throw new ArgumentException("Assigned section is invalid.");
                    sectionId = found;
                }
            }

            using (SqlCommand update = connection.CreateCommand())
            {
                update.CommandText = @"
UPDATE dbo.Users
SET DisplayName = @DisplayName,
    AssignedSectionId = @AssignedSectionId,
    IsActive = @IsActive,
    IsAdmin = @IsAdmin,
    CanAccessAssignmentsBoard = @CanAccessAssignmentsBoard,
    IsTsuiAdmin = @IsTsuiAdmin,
    UpdatedUtc = SYSUTCDATETIME()
WHERE UserId = @UserId;";
                update.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 150).Value = displayName;
                update.Parameters.Add("@AssignedSectionId", SqlDbType.Int).Value = sectionId;
                update.Parameters.Add("@IsActive", SqlDbType.Bit).Value = Input.Bool(body, "isActive");
                update.Parameters.Add("@IsAdmin", SqlDbType.Bit).Value = Input.Bool(body, "isAdmin");
                update.Parameters.Add("@CanAccessAssignmentsBoard", SqlDbType.Bit).Value = Input.Bool(body, "canAccessAssignmentsBoard");
                update.Parameters.Add("@IsTsuiAdmin", SqlDbType.Bit).Value = Input.Bool(body, "isTsuiAdmin");
                update.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                if (update.ExecuteNonQuery() == 0) throw new ArgumentException("User was not found.");
            }
        }
    }

    public static void SetStatus(int userId, bool isActive)
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
UPDATE dbo.Users
SET IsActive = @IsActive,
    UpdatedUtc = SYSUTCDATETIME()
WHERE UserId = @UserId;";
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            if (command.ExecuteNonQuery() == 0) throw new ArgumentException("User was not found.");
        }
    }

    public static void Delete(int userId)
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
DELETE FROM dbo.Users
WHERE UserId = @UserId
  AND NOT EXISTS (SELECT 1 FROM dbo.Posts WHERE CreatedByUserId = @UserId OR UpdatedByUserId = @UserId);";
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            if (command.ExecuteNonQuery() == 0)
            {
                throw new ArgumentException("User was not found or has post history. Deactivate the account instead.");
            }
        }
    }

    public static List<Dictionary<string, object>> Sections()
    {
        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT SectionId, SectionCode, SectionName, SortOrder, IsPublicVisible FROM dbo.Sections ORDER BY SortOrder;";
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new Dictionary<string, object>
                    {
                        { "sectionId", Convert.ToInt32(reader["SectionId"]) },
                        { "sectionCode", Convert.ToString(reader["SectionCode"]) },
                        { "sectionName", Convert.ToString(reader["SectionName"]) },
                        { "sortOrder", Convert.ToInt32(reader["SortOrder"]) },
                        { "isPublicVisible", Convert.ToBoolean(reader["IsPublicVisible"]) }
                    });
                }
            }

            return rows;
        }
    }

    public static void SetPublicDisplay(string sectionCode, bool isPublicVisible)
    {
        if (String.IsNullOrWhiteSpace(sectionCode)) throw new ArgumentException("SectionCode is required.");
        if (sectionCode.Equals("TSU", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("TSU Flight is not a public-board section column.");
        }

        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
UPDATE dbo.Sections
SET IsPublicVisible = @IsPublicVisible
WHERE SectionCode = @SectionCode;";
            command.Parameters.Add("@IsPublicVisible", SqlDbType.Bit).Value = isPublicVisible;
            command.Parameters.Add("@SectionCode", SqlDbType.NVarChar, 10).Value = sectionCode;
            if (command.ExecuteNonQuery() == 0) throw new ArgumentException("Section was not found.");
        }
    }

    private static List<Dictionary<string, object>> ReadUsers(SqlCommand command)
    {
        List<Dictionary<string, object>> users = new List<Dictionary<string, object>>();
        using (SqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                // Reuse CurrentUser.ReadUser/ToJson so the admin list reports the
                // same routeTarget the router will actually apply to this user.
                AppUser row = CurrentUser.ReadUser(reader);
                Dictionary<string, object> item = CurrentUser.ToJson(row);

                item["firstSeenUtc"] = DbValue.DateTimeUtc(Convert.ToDateTime(reader["FirstSeenUtc"]));
                item["lastLoginUtc"] = reader["LastLoginUtc"] == DBNull.Value ? null : DbValue.DateTimeUtc(Convert.ToDateTime(reader["LastLoginUtc"]));
                item["createdUtc"] = DbValue.DateTimeUtc(Convert.ToDateTime(reader["CreatedUtc"]));
                item["updatedUtc"] = DbValue.DateTimeUtc(Convert.ToDateTime(reader["UpdatedUtc"]));

                users.Add(item);
            }
        }

        return users;
    }

    public static void UpdateDisplayName(int userId, string displayName)
    {
        if (userId <= 0) throw new ArgumentException("UserId is required.");
        if (String.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.");
        if (displayName.Length > 150)
            throw new ArgumentException("Display name must be 150 characters or fewer.");

        using (SqlConnection connection = Db.Open())
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
UPDATE dbo.Users
SET DisplayName = @DisplayName,
    UpdatedUtc = SYSUTCDATETIME()
WHERE UserId = @UserId;";

            command.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 150).Value = displayName.Trim();
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

            if (command.ExecuteNonQuery() == 0)
                throw new ArgumentException("User was not found.");
        }
    }
}
