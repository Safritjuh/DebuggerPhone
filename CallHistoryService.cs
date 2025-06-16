using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using WindowsSipPhone.Pages;

namespace WindowsSipPhone.Database
{
    /// <summary>
    /// Professional call history service using embedded SQLite
    /// Zero-installation database for desktop applications
    /// Matches existing CallHistoryEntry model in Pages/DialerPage.xaml.cs
    /// </summary>
    public class CallHistoryService
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        public CallHistoryService()
        {
            // Store database in user's AppData folder (Windows standard)
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowsSipPhone"
            );
            
            Directory.CreateDirectory(appDataPath);
            _databasePath = Path.Combine(appDataPath, "CallHistory.db");
            _connectionString = $"Data Source={_databasePath}";
            
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS CallHistory (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Number TEXT NOT NULL,
                        CallType TEXT NOT NULL,
                        DateTime TEXT NOT NULL,
                        Duration INTEGER DEFAULT 0,
                        Status TEXT NOT NULL,
                        CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_call_datetime ON CallHistory(DateTime DESC);
                    CREATE INDEX IF NOT EXISTS idx_call_number ON CallHistory(Number);
                    CREATE INDEX IF NOT EXISTS idx_call_type ON CallHistory(CallType);";
                
                command.ExecuteNonQuery();
                Console.WriteLine($"[CallHistory] ✅ Database initialized: {_databasePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CallHistory] ❌ Database initialization failed: {ex.Message}");
                throw;
            }
        }        public void AddCall(CallHistoryEntry call)
        {
            var debugLog = Path.Combine(Path.GetDirectoryName(_databasePath) ?? "", "debug.log");
            
            try
            {
                File.AppendAllText(debugLog, $"[{DateTime.Now}] AddCall called for: {call.Number} ({call.CallType})\n");
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO CallHistory (Number, CallType, DateTime, Duration, Status)
                    VALUES (@number, @callType, @dateTime, @duration, @status)";
                
                command.Parameters.AddWithValue("@number", call.Number ?? "");
                command.Parameters.AddWithValue("@callType", call.CallType.ToString());
                command.Parameters.AddWithValue("@dateTime", call.DateTime.ToString("O"));
                command.Parameters.AddWithValue("@duration", (int)call.Duration.TotalSeconds);
                command.Parameters.AddWithValue("@status", call.Status.ToString());
                
                File.AppendAllText(debugLog, $"[{DateTime.Now}] Executing SQL INSERT for: {call.Number}\n");
                
                var rowsAffected = command.ExecuteNonQuery();
                
                File.AppendAllText(debugLog, $"[{DateTime.Now}] SQL INSERT completed. Rows affected: {rowsAffected}\n");
                Console.WriteLine($"[CallHistory] ✅ Added call: {call.Number} ({call.CallType}) - Rows affected: {rowsAffected}");
            }
            catch (Exception ex)
            {
                File.AppendAllText(debugLog, $"[{DateTime.Now}] AddCall ERROR: {ex.Message}\n");
                File.AppendAllText(debugLog, $"[{DateTime.Now}] Stack trace: {ex.StackTrace}\n");
                Console.WriteLine($"[CallHistory] ❌ Failed to add call: {ex.Message}");
            }
        }

        public void UpdateCall(CallHistoryEntry call)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE CallHistory 
                    SET Duration = @duration, Status = @status
                    WHERE Number = @number 
                      AND CallType = @callType 
                      AND DateTime = @dateTime";
                
                command.Parameters.AddWithValue("@number", call.Number ?? "");
                command.Parameters.AddWithValue("@callType", call.CallType.ToString());
                command.Parameters.AddWithValue("@dateTime", call.DateTime.ToString("O"));
                command.Parameters.AddWithValue("@duration", (int)call.Duration.TotalSeconds);
                command.Parameters.AddWithValue("@status", call.Status.ToString());
                
                var rowsAffected = command.ExecuteNonQuery();
                Console.WriteLine($"[CallHistory] ✅ Updated call: {call.Number} ({call.CallType}) - {rowsAffected} rows affected");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CallHistory] ❌ Failed to update call: {ex.Message}");
            }
        }

        public List<CallHistoryEntry> GetRecentCalls(int limit = 100)
        {
            var calls = new List<CallHistoryEntry>();
            
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Number, CallType, DateTime, Duration, Status
                    FROM CallHistory
                    ORDER BY DateTime DESC
                    LIMIT @limit";
                
                command.Parameters.AddWithValue("@limit", limit);
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var call = new CallHistoryEntry
                    {
                        Number = reader.GetString(reader.GetOrdinal("Number")),
                        CallType = Enum.Parse<CallType>(reader.GetString(reader.GetOrdinal("CallType"))),
                        DateTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("DateTime"))),
                        Status = Enum.Parse<CallStatus>(reader.GetString(reader.GetOrdinal("Status")))
                    };
                    
                    // Parse Duration
                    var durationSeconds = reader.GetInt32(reader.GetOrdinal("Duration"));
                    if (durationSeconds > 0)
                    {
                        call.Duration = TimeSpan.FromSeconds(durationSeconds);
                    }
                    
                    calls.Add(call);
                }
                
                Console.WriteLine($"[CallHistory] ✅ Retrieved {calls.Count} calls");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CallHistory] ❌ Failed to retrieve calls: {ex.Message}");
            }
            
            return calls;
        }

        public List<CallHistoryEntry> GetCallsByType(CallType callType, int limit = 50)
        {
            var calls = new List<CallHistoryEntry>();
            
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Number, CallType, DateTime, Duration, Status
                    FROM CallHistory
                    WHERE CallType = @callType
                    ORDER BY DateTime DESC
                    LIMIT @limit";
                
                command.Parameters.AddWithValue("@callType", callType.ToString());
                command.Parameters.AddWithValue("@limit", limit);
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var call = new CallHistoryEntry
                    {
                        Number = reader.GetString(reader.GetOrdinal("Number")),
                        CallType = Enum.Parse<CallType>(reader.GetString(reader.GetOrdinal("CallType"))),
                        DateTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("DateTime"))),
                        Status = Enum.Parse<CallStatus>(reader.GetString(reader.GetOrdinal("Status")))
                    };
                    
                    // Parse Duration
                    var durationSeconds = reader.GetInt32(reader.GetOrdinal("Duration"));
                    if (durationSeconds > 0)
                    {
                        call.Duration = TimeSpan.FromSeconds(durationSeconds);
                    }
                    
                    calls.Add(call);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CallHistory] ❌ Failed to retrieve calls by type: {ex.Message}");
            }
            
            return calls;
        }

        public void ExportToCsv(string filePath)
        {
            try
            {
                var calls = GetRecentCalls(int.MaxValue);
                
                using var writer = new StreamWriter(filePath);
                writer.WriteLine("Number,CallType,DateTime,Duration,Status");
                
                foreach (var call in calls)
                {
                    var duration = call.Duration.TotalMinutes.ToString("F1");
                    
                    writer.WriteLine($"{call.Number},{call.CallType},{call.DateTime:yyyy-MM-dd HH:mm:ss},{duration},{call.Status}");
                }
                
                Console.WriteLine($"[CallHistory] ✅ Exported {calls.Count} calls to CSV: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CallHistory] ❌ Failed to export CSV: {ex.Message}");
            }
        }

        public void ClearHistory()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM CallHistory";
                var deletedCount = command.ExecuteNonQuery();
                
                Console.WriteLine($"[CallHistory] ✅ Cleared {deletedCount} call records");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CallHistory] ❌ Failed to clear history: {ex.Message}");
            }
        }

        public int GetCallCount()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM CallHistory";
                
                return Convert.ToInt32(command.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CallHistory] ❌ Failed to get call count: {ex.Message}");
                return 0;
            }
        }

        public string GetDatabasePath() => _databasePath;
    }
}
