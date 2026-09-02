using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

public static class RumpRepository
{
    public static Dictionary<string, object> PullDraftPosts(AppUser user)
    {
        if (user.SectionCode != "TSUL" && user.SectionCode != "TSUS")
        {
            throw new UnauthorizedAccessException("RUMP pull is limited to TSUL and TSUS.");
        }

        ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["RumpDb"];
        if (settings == null || String.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException("Missing RumpDb connection string. Add it to Web.config before pulling RUMP records.");
        }

        string query = ConfigurationManager.AppSettings["RumpPullQuery"];
        if (String.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("Missing RumpPullQuery app setting. Configure a SELECT that returns Title and Description, with optional PointOfContact, LatestUpdate, and EstimatedCompletionDate columns.");
        }

        List<Dictionary<string, object>> rows = ReadRumpRows(settings.ConnectionString, query);
        List<int> postIds = InsertDraftPosts(user, rows);

        return new Dictionary<string, object>
        {
            { "importedCount", postIds.Count },
            { "postIds", postIds.ToArray() },
            { "message", postIds.Count == 0 ? "RUMP query returned no rows." : "RUMP draft posts were created for review." }
        };
    }

    private static List<Dictionary<string, object>> ReadRumpRows(string connectionString, string query)
    {
        List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
        using (SqlConnection connection = new SqlConnection(connectionString))
        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = query;
            command.CommandType = CommandType.Text;
            connection.Open();

            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string title = RequiredString(reader, "Title");
                    string description = RequiredString(reader, "Description");
                    rows.Add(new Dictionary<string, object>
                    {
                        { "title", title },
                        { "pointOfContact", OptionalString(reader, "PointOfContact", "RUMP Import") },
                        { "description", description },
                        { "latestUpdate", OptionalString(reader, "LatestUpdate", "Imported from RUMP for section review.") },
                        { "estimatedCompletionDate", OptionalDate(reader, "EstimatedCompletionDate", DateTime.Today) }
                    });
                }
            }
        }

        return rows;
    }

    private static List<int> InsertDraftPosts(AppUser user, List<Dictionary<string, object>> rows)
    {
        List<int> postIds = new List<int>();
        if (rows.Count == 0) return postIds;

        using (SqlConnection connection = Db.Open())
        {
            foreach (Dictionary<string, object> row in rows)
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT dbo.Posts
    (SectionId, Title, PointOfContact, Description, LatestUpdate, EstimatedCompletionDate, IsActive, CreatedByUserId, UpdatedByUserId)
OUTPUT INSERTED.PostId
VALUES
    (@SectionId, @Title, @PointOfContact, @Description, @LatestUpdate, @EstimatedCompletionDate, 0, @UserId, @UserId);";
                    command.Parameters.Add("@SectionId", SqlDbType.Int).Value = user.AssignedSectionId.Value;
                    command.Parameters.Add("@Title", SqlDbType.NVarChar, 150).Value = Truncate(Convert.ToString(row["title"]), 150);
                    command.Parameters.Add("@PointOfContact", SqlDbType.NVarChar, 150).Value = Truncate(Convert.ToString(row["pointOfContact"]), 150);
                    command.Parameters.Add("@Description", SqlDbType.NVarChar).Value = Convert.ToString(row["description"]);
                    command.Parameters.Add("@LatestUpdate", SqlDbType.NVarChar).Value = Convert.ToString(row["latestUpdate"]);
                    command.Parameters.Add("@EstimatedCompletionDate", SqlDbType.Date).Value = ((DateTime)row["estimatedCompletionDate"]).Date;
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
                    postIds.Add(Convert.ToInt32(command.ExecuteScalar()));
                }
            }
        }

        return postIds;
    }

    private static string RequiredString(SqlDataReader reader, string columnName)
    {
        int ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0) throw new InvalidOperationException("RUMP query must return a " + columnName + " column.");
        if (reader.IsDBNull(ordinal)) throw new InvalidOperationException("RUMP query returned a row with missing " + columnName + ".");
        string value = Convert.ToString(reader.GetValue(ordinal)).Trim();
        if (String.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("RUMP query returned a row with blank " + columnName + ".");
        return value;
    }

    private static string OptionalString(SqlDataReader reader, string columnName, string fallback)
    {
        int ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return fallback;
        string value = Convert.ToString(reader.GetValue(ordinal)).Trim();
        return String.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static DateTime OptionalDate(SqlDataReader reader, string columnName, DateTime fallback)
    {
        int ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return fallback;
        return Convert.ToDateTime(reader.GetValue(ordinal)).Date;
    }

    private static int TryGetOrdinal(SqlDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (String.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
