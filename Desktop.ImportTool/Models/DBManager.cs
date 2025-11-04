using System;
using System.Collections.Generic;
using System.Data.SQLite;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Desktop.ImportTool.Models
{
    public class DBManager
    {
        private readonly string _connectionString;

        public DBManager(string dbFilePath)
        {
            try
            {
                _connectionString = "Data Source=" + dbFilePath + ";";
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    CreateTables();
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public List<TaskModel> GetTasks()
        {
            var tasks = new List<TaskModel>();
            try
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(
                        @"SELECT Id, TaskOrder, Source, Target, CreationTime, Status, CreatedBy, Metadata, Settings FROM Tasks ORDER BY TaskOrder", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tasks.Add(new TaskModel
                            {
                                Id = reader.GetString(0),
                                TaskOrder = reader.GetInt32(1),
                                Source = reader.GetString(2),
                                Target = reader.GetString(3),
                                CreationTime = reader.GetString(4),
                                Status = reader.IsDBNull(5) ? TaskStatus.Queued
                                    : Enum.TryParse<TaskStatus>(reader.GetString(5), out var status) ? status : TaskStatus.Queued,
                                CreatedBy = reader.GetString(6),
                                Metadata = reader.GetString(7),
                                Settings = reader.GetString(8)
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
            return tasks;
        }

        public void DeleteHistoryRow(string id)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string sql = "DELETE FROM History WHERE Id = @Id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public void DeleteTask(string id)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string sql = "DELETE FROM Tasks WHERE Id = @Id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public void UpdateTaskOrders(IEnumerable<TaskModel> tasks)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    foreach (var task in tasks)
                    {
                        string sql = "UPDATE Tasks SET TaskOrder = @TaskOrder WHERE Id = @Id";
                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@TaskOrder", task.TaskOrder);
                            cmd.Parameters.AddWithValue("@Id", task.Id.ToString());
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public void UpdateTaskStatus(string taskId, string status)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand("UPDATE Tasks SET Status = @Status WHERE Id = @Id", connection))
                {
                    command.Parameters.AddWithValue("@Status", status);
                    command.Parameters.AddWithValue("@Id", taskId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateTables()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string createTableSql = @"
                        CREATE TABLE IF NOT EXISTS Tasks (
                            Id TEXT PRIMARY KEY,
                            TaskOrder INTEGER,
                            Source TEXT NOT NULL,
                            Target TEXT NOT NULL,
                            CreationTime TEXT NOT NULL,
                            FinishingTime TEXT NOT NULL,
                            Status TEXT NOT NULL,
                            CreatedBy TEXT NOT NULL,
                            Metadata TEXT NOT NULL,
                            Settings TEXT NOT NULL
                        )";
                    using (var cmd = new SQLiteCommand(createTableSql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    createTableSql = @"
                        CREATE TABLE IF NOT EXISTS History (
                            Id TEXT PRIMARY KEY,
                            TaskOrder INTEGER,
                            Source TEXT NOT NULL,
                            Target TEXT NOT NULL,
                            CreationTime TEXT NOT NULL,
                            FinishingTime TEXT NOT NULL,
                            Status TEXT NOT NULL,
                            CreatedBy TEXT NOT NULL,
                            Metadata TEXT NOT NULL,
                            Settings TEXT NOT NULL
                        )";
                    using (var cmd = new SQLiteCommand(createTableSql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public void ClearTable(string tableName)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string sql = $"DELETE FROM {tableName}";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public void AddTask(string Source, string Target, string Metadata, string Settings)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string insertSql = @"
                        INSERT INTO Tasks (Id, TaskOrder, Source, Target, CreationTime, FinishingTime, Status, CreatedBy, Metadata, Settings)
                        VALUES (@Id, @TaskOrder, @Source, @Target, @CreationTime, @FinishingTime, @Status, @CreatedBy, @Metadata, @Settings)";
                    using (var cmd = new SQLiteCommand(insertSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                        cmd.Parameters.AddWithValue("@TaskOrder", GetNextTaskOrder(Environment.UserName));
                        cmd.Parameters.AddWithValue("@Source", Source);
                        cmd.Parameters.AddWithValue("@Target", Target);
                        cmd.Parameters.AddWithValue("@CreationTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@FinishingTime", DateTime.MinValue);
                        cmd.Parameters.AddWithValue("@Status", TaskStatus.Queued.ToString());
                        cmd.Parameters.AddWithValue("@CreatedBy", Environment.UserName);
                        cmd.Parameters.AddWithValue("@Metadata", Metadata);
                        cmd.Parameters.AddWithValue("@Settings", Settings);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        private int GetNextTaskOrder(string createdBy)
        {
            int nextOrder = 1;
            try
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT MAX(TaskOrder) FROM Tasks WHERE CreatedBy = @CreatedBy", conn))
                    {
                        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                        var result = cmd.ExecuteScalar();
                        if (result != DBNull.Value && result != null)
                            nextOrder = Convert.ToInt32(result) + 1;
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
            return nextOrder;
        }

        public void AddHistoryRow(TaskModel task)
        {
            try
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    string insertSql = @"
                        INSERT INTO History (Id, Source, Target, CreationTime, FinishingTime, Status, CreatedBy, Metadata, Settings)
                        VALUES (@Id, @Source, @Target, @CreationTime, @FinishingTime, @Status, @CreatedBy, @Metadata, @Settings)";
                    using (var cmd = new SQLiteCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", task.Id.ToString());
                        cmd.Parameters.AddWithValue("@Source", task.Source);
                        cmd.Parameters.AddWithValue("@Target", task.Target);
                        cmd.Parameters.AddWithValue("@CreationTime", task.CreationTime);
                        cmd.Parameters.AddWithValue("@FinishingTime", task.FinishingTime);
                        cmd.Parameters.AddWithValue("@Status", task.Status.ToString());
                        cmd.Parameters.AddWithValue("@CreatedBy", task.CreatedBy);
                        cmd.Parameters.AddWithValue("@Metadata", task.Metadata);
                        cmd.Parameters.AddWithValue("@Settings", task.Settings);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public List<TaskModel> GetHistory()
        {
            var history = new List<TaskModel>();
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string sql = "SELECT * FROM History";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int statusIndex = reader.GetOrdinal("Status");
                                var statusStr = reader.IsDBNull(statusIndex) ? null : reader.GetString(statusIndex)?.Trim();
                                history.Add(new TaskModel
                                {
                                    Id = reader["Id"].ToString(),
                                    Source = reader["Source"].ToString(),
                                    Target = reader["Target"].ToString(),
                                    CreationTime = reader["CreationTime"].ToString(),
                                    FinishingTime = reader["FinishingTime"].ToString(),
                                    Status = !string.IsNullOrWhiteSpace(statusStr)
                                        ? (Enum.TryParse<TaskStatus>(statusStr, true, out var status) ? status : TaskStatus.Finished)
                                        : TaskStatus.Finished,
                                    CreatedBy = reader["CreatedBy"].ToString(),
                                    Metadata = reader["Metadata"].ToString(),
                                    Settings = reader["Settings"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
            return history;
        }
    }
}